#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Lien de test entre l'hôte (ScenarioRunner) et un vrai second joueur : l'éditeur clone de Multiplayer Play Mode
//  (« Player 2 »), où tourne ScenarioClientAgent.
//
//  TCP en boucle locale, à côté du réseau du jeu : seules les commandes de test et ce que voit le second joueur
//  passent ici. Ses actions (validation F, battements d'ailes, fin de cinématique…) passent, elles, par le vrai
//  chemin du jeu : l'input de son pigeon, envoyé au serveur par PurrNet.
//
//  Une ligne UTF-8 par message, champs séparés par des tabulations :
//    client → hôte : HELLO <version>              à la connexion
//                    SNAP  <clé␟valeur␞clé␟valeur…>  ce que voit le client, environ 10 fois par seconde
//                    ACK   <id> ok|err <détail>    réponse à une commande
//    hôte → client : CMD   <id> <verbe> <argument>
// ─────────────────────────────────────────────────────────────────────────────
public static class ScenarioLinkProtocol
{
    public const int Port = 47811;
    public const int Version = 1;
    public const char EntrySeparator = '\u001E';
    public const char KeyValueSeparator = '\u001F';
    public const string ListSeparator = "|";

    /// <summary>Valeur transportable sur une ligne (sauts de ligne échappés, séparateurs retirés).</summary>
    public static string Clean(string value) => value == null ? "" : value
        .Replace("\r", "").Replace("\n", "\\n").Replace('\t', ' ')
        .Replace(EntrySeparator, ' ').Replace(KeyValueSeparator, ' ');

    public static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string Format(Vector3 value) => $"{Format(value.x)};{Format(value.y)};{Format(value.z)}";

    public static bool TryParse(string text, out float value)
        => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public static bool TryParse(string text, out Vector3 value)
    {
        value = default;
        if (string.IsNullOrEmpty(text))
            return false;
        string[] parts = text.Split(';');
        if (parts.Length != 3 || !TryParse(parts[0], out float x) || !TryParse(parts[1], out float y) || !TryParse(parts[2], out float z))
            return false;
        value = new Vector3(x, y, z);
        return true;
    }
}

/// <summary>Lignes échangées sur un socket, sans bloquer : à appeler depuis Update (thread principal).</summary>
public sealed class ScenarioLinkConnection : IDisposable
{
    readonly TcpClient _client;
    readonly NetworkStream _stream;
    readonly List<byte> _pending = new List<byte>();
    readonly byte[] _buffer = new byte[16 * 1024];

    public bool IsOpen { get; private set; } = true;

    public ScenarioLinkConnection(TcpClient client)
    {
        _client = client;
        _client.NoDelay = true;
        _stream = client.GetStream();
    }

    public void Send(string line)
    {
        if (!IsOpen)
            return;
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
            _stream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception)
        {
            Close();
        }
    }

    /// <summary>Ajoute à <paramref name="into"/> les lignes complètes reçues depuis le dernier appel.</summary>
    public void Receive(List<string> into)
    {
        if (!IsOpen)
            return;
        try
        {
            // Lisible sans rien à lire : l'autre côté a fermé la connexion.
            if (_client.Client.Poll(0, SelectMode.SelectRead) && _client.Available == 0)
            {
                Close();
                return;
            }
            while (_client.Available > 0)
            {
                int read = _stream.Read(_buffer, 0, Math.Min(_buffer.Length, _client.Available));
                if (read <= 0)
                {
                    Close();
                    return;
                }
                // '\n' n'apparaît jamais au milieu d'un caractère UTF-8 : on peut couper les octets dessus.
                for (int i = 0; i < read; i++)
                {
                    if (_buffer[i] != (byte)'\n')
                    {
                        _pending.Add(_buffer[i]);
                        continue;
                    }
                    into.Add(Encoding.UTF8.GetString(_pending.ToArray()));
                    _pending.Clear();
                }
            }
        }
        catch (Exception)
        {
            Close();
        }
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        try { _client.Close(); }
        catch (Exception) { }
    }

    public void Dispose() => Close();
}

/// <summary>Côté hôte : écoute le second joueur, garde son dernier instantané et les réponses à ses commandes.</summary>
public sealed class ScenarioRemoteLink : IDisposable
{
    TcpListener _listener;
    ScenarioLinkConnection _connection;
    readonly List<string> _lines = new List<string>();
    readonly Dictionary<string, string> _snapshot = new Dictionary<string, string>();
    readonly Dictionary<int, (bool ok, string detail)> _replies = new Dictionary<int, (bool, string)>();
    int _nextId = 1;

    public string ListenError { get; private set; }
    public bool IsListening => _listener != null;
    /// <summary>Le second joueur est relié et s'est présenté.</summary>
    public bool Connected => _connection != null && _connection.IsOpen && HelloReceived;
    public bool HelloReceived { get; private set; }
    /// <summary>Instant (realtimeSinceStartup) de réception du dernier instantané, -1 si aucun.</summary>
    public float LastSnapshotTime { get; private set; } = -1f;
    public int SnapshotCount { get; private set; }
    public IReadOnlyDictionary<string, string> Snapshot => _snapshot;

    public bool Listen()
    {
        if (_listener != null)
            return true;
        // Sous Windows, ReuseAddress laisserait un second écouteur prendre le port en silence (autre éditeur en
        // pleins scénarios) : vérifier d'abord que personne n'y répond. Un port en TIME_WAIT ne répond pas.
        if (SomeoneListens())
        {
            ListenError = "port déjà utilisé (un autre éditeur fait-il tourner des scénarios ?)";
            return false;
        }
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, ScenarioLinkProtocol.Port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();
            ListenError = null;
            return true;
        }
        catch (Exception e)
        {
            ListenError = e.Message;
            _listener = null;
            return false;
        }
    }

    static bool SomeoneListens()
    {
        try
        {
            using (var probe = new TcpClient(AddressFamily.InterNetwork))
                return probe.ConnectAsync(IPAddress.Loopback, ScenarioLinkProtocol.Port).Wait(300) && probe.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Poll()
    {
        if (_listener != null)
        {
            try
            {
                while (_listener.Pending())
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    // Un seul second joueur : le premier relié garde la main (Player 3 / 4 éventuels refusés).
                    if (Connected)
                    {
                        client.Close();
                        continue;
                    }
                    _connection?.Close();
                    _connection = new ScenarioLinkConnection(client);
                    ResetRemoteState();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        if (_connection == null)
            return;
        _lines.Clear();
        _connection.Receive(_lines);
        foreach (string line in _lines)
            Handle(line);
        // Lien coupé : ne plus rien croire de l'ancien instantané.
        if (!_connection.IsOpen)
        {
            _connection = null;
            ResetRemoteState();
        }
    }

    void ResetRemoteState()
    {
        HelloReceived = false;
        _snapshot.Clear();
        SnapshotCount = 0;
        LastSnapshotTime = -1f;
    }

    void Handle(string line)
    {
        string[] parts = line.Split('\t');
        switch (parts[0])
        {
            case "HELLO":
                HelloReceived = true;
                break;

            case "SNAP":
                _snapshot.Clear();
                if (parts.Length > 1)
                    foreach (string entry in parts[1].Split(ScenarioLinkProtocol.EntrySeparator))
                    {
                        int separator = entry.IndexOf(ScenarioLinkProtocol.KeyValueSeparator);
                        if (separator > 0)
                            _snapshot[entry.Substring(0, separator)] = entry.Substring(separator + 1);
                    }
                LastSnapshotTime = Time.realtimeSinceStartup;
                SnapshotCount++;
                break;

            case "ACK":
                if (parts.Length >= 3 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                    _replies[id] = (parts[2] == "ok", parts.Length > 3 ? parts[3] : "");
                break;
        }
    }

    /// <summary>Envoie une commande au second joueur ; renvoie son identifiant (réponse : TryGetReply).</summary>
    public int Send(string verb, string argument = "")
    {
        int id = _nextId++;
        _connection?.Send($"CMD\t{id}\t{verb}\t{ScenarioLinkProtocol.Clean(argument)}");
        return id;
    }

    public bool TryGetReply(int id, out bool ok, out string detail)
    {
        if (_replies.TryGetValue(id, out (bool ok, string detail) reply))
        {
            _replies.Remove(id);
            ok = reply.ok;
            detail = reply.detail;
            return true;
        }
        ok = false;
        detail = null;
        return false;
    }

    /// <summary>Valeur du dernier instantané du second joueur (null si absente).</summary>
    public string Get(string key) => _snapshot.TryGetValue(key, out string value) ? value : null;

    public void Dispose()
    {
        _connection?.Close();
        _connection = null;
        HelloReceived = false;
        try { _listener?.Stop(); }
        catch (Exception) { }
        _listener = null;
    }
}
#endif
