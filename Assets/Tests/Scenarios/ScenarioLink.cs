#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// Lien de test (TCP local) entre le ScenarioRunner de l'hôte et ScenarioClientAgent dans le second joueur.
/// Une ligne UTF-8 par message, champs séparés par des tabulations :
/// client → hôte : HELLO version ; SNAP clé␟valeur␞… (ce qu'il voit, ~10 fois par seconde) ; ACK id ok|err détail.
/// hôte → client : CMD id verbe argument.
/// </summary>
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

/// <summary>Clés de l'instantané du second joueur : écrites par ScenarioClientAgent, lues par les scénarios.</summary>
public static class RemoteKeys
{
    public const string Network = "net";
    public const string NetworkNone = "none";
    public const string NetworkOffline = "offline";
    public const string NetworkConnecting = "connecting";
    public const string NetworkConnected = "connected";
    public const string PlayerId = "net/player";
    public const string AgentDriving = "driven";
    public const string AutoDialogs = "dialogs/auto";

    public const string HasPigeon = "local";
    public const string Position = "local/pos";
    public const string Frozen = "local/frozen";
    public const string Grounded = "local/grounded";
    public const string Speed = "local/speed";
    public const string Ready = "local/ready";
    public const string Equipment = "local/equipment";
    public const string ControlsBlocked = "local/blocked";
    public const string PigeonDriven = "local/driven";
    public const string PigeonCount = "pigeons";

    public const string PlayingDialogs = "dialogs";
    public const string DialogOpen = "dialogs/open";
    public const string Loots = "loots";
    public const string Achievements = "ach";
    public const string OwnSounds = "audio/local";
    public const string OtherSounds = "audio/others";
    public const string RingSounds = "audio/rings";
    public const string ScreenSpeedLines = "fx/speedlines";
    public const string DayNightClock = "daynight/clock";
    public const string Paused = "local/paused";

    public const string ZonePrefix = "zone/";
    public const string RacePrefix = "race/";
    public const string DialogsPrefix = "dialogs/";

    public static string ZonePhase(MissionStartZone zone) => ZonePrefix + zone.name + "/phase";
    public static string ZoneHud(MissionStartZone zone) => ZonePrefix + zone.name + "/hud";
    public static string ZonePrompt(MissionStartZone zone) => ZonePrefix + zone.name + "/prompt";

    public static string RacePhase(PigeonCharacterRace race) => RacePrefix + race.name + "/phase";
    public static string RaceDeparture(PigeonCharacterRace race) => RacePrefix + race.name + "/departure";
    public static string RaceArrival(PigeonCharacterRace race) => RacePrefix + race.name + "/arrival";
    public static string RaceCharacter(PigeonCharacterRace race) => RacePrefix + race.name + "/character";
    public static string RaceFailure(PigeonCharacterRace race) => RacePrefix + race.name + "/failure";
    public static string RaceMarker(PigeonCharacterRace race) => RacePrefix + race.name + "/marker";

    public static string TutoConsumed(PigeonStopDialogCinematic tuto) => "tuto/" + tuto.name + "/consumed";
    public static string TutoPlaying(PigeonStopDialogCinematic tuto) => "tuto/" + tuto.name + "/playing";

    public static string DialogStarts(string cinematic) => DialogsPrefix + "starts/" + cinematic;
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
