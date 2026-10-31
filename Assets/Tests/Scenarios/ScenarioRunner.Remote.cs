#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public partial class ScenarioRunner
{
    const float RemoteWaitInteractive = 45f;
    const float RemoteWaitCommandLine = 300f;
    ScenarioRemoteLink _link;
    bool _lastRemoteOk;
    string _lastRemoteDetail;

    /// <summary>Second joueur relié, en jeu, et son pigeon présent chez l'hôte.</summary>
    protected bool RemoteReady => _link != null && _link.Connected && _link.Get(RemoteKeys.Network) == RemoteKeys.NetworkConnected
        && _link.Get(RemoteKeys.HasPigeon) == "1" && RemotePigeon != null;

    /// <summary>Le pigeon du second joueur, tel que l'hôte le simule (null s'il n'est pas en jeu).</summary>
    protected PigeonController RemotePigeon
    {
        get
        {
            if (_link == null || !_link.Connected)
                return null;
            string id = _link.Get(RemoteKeys.PlayerId);
            if (id == null)
                return null;
            foreach (PigeonController pigeon in PigeonController.All)
                if (pigeon != Pigeon && pigeon.owner.HasValue && !pigeon.owner.Value.isBot
                    && pigeon.owner.Value.id.value.ToString(CultureInfo.InvariantCulture) == id)
                    return pigeon;
            return null;
        }
    }

    /// <summary>Un pigeon de joueur autre que celui de l'hôte (les bots n'ont pas de propriétaire).</summary>
    static bool HasRemotePlayerPigeon()
    {
        foreach (PigeonController pigeon in PigeonController.All)
            if (pigeon != PigeonController.Local && pigeon.owner.HasValue && !pigeon.owner.Value.isBot)
                return true;
        return false;
    }

    static bool MppmVirtualPlayerActive()
    {
        try
        {
            string path = Path.Combine("Library", "VP", "SystemData.json");
            if (!File.Exists(path))
                return false;
            string json = File.ReadAllText(path);
            return Regex.IsMatch(json, "\"IsMppmActive\"\\s*:\\s*true")
                && Regex.IsMatch(json, "\"Active\"\\s*:\\s*true\\s*,\\s*\"Index\"\\s*:\\s*\\d+\\s*,\\s*\"Type\"\\s*:\\s*1\\b");
        }
        catch (Exception)
        {
            return false;
        }
    }

    IEnumerator PrepareRemote(bool required)
    {
        bool expected = required || HasRemotePlayerPigeon() || (!_commandLine && MppmVirtualPlayerActive());
        if (!expected)
            yield break;

        _currentScenario = "Second joueur";
        _scenarioStart = Time.realtimeSinceStartup;
        _link = new ScenarioRemoteLink();
        if (!_link.Listen())
        {
            Record($"Lien de test avec le second joueur impossible (port {ScenarioLinkProtocol.Port})", false, () => _link.ListenError);
            _link = null;
            yield break;
        }

        float timeout = required ? (_commandLine ? RemoteWaitCommandLine : RemoteWaitInteractive) : RemoteWaitInteractive;
        Debug.Log($"[Scénario] En attente du second joueur (au plus {timeout:0} s)…");
        float deadline = Time.realtimeSinceStartup + timeout;
        float nextConnect = 0f;
        while (Time.realtimeSinceStartup < deadline && !(required ? RemoteReady : _link.Connected))
        {
            _lastProgress = Time.realtimeSinceStartup;
            if (required && _link.Connected && _link.Get(RemoteKeys.Network) == RemoteKeys.NetworkOffline && Time.realtimeSinceStartup >= nextConnect)
            {
                _link.Send("connect");
                nextConnect = Time.realtimeSinceStartup + 5f;
            }
            yield return null;
        }

        if (_link.Connected)
            yield return ResetRemoteAchievementsOnce();
        if (!(required ? RemoteReady : _link.Connected))
        {
            Record(required ? "Aucun second joueur : les scénarios multi vont échouer" : "Second joueur annoncé mais jamais relié",
                false, RemoteMissingReason);
            yield break;
        }
        Record("Second joueur relié (éditeur clone, client PurrNet)", true, RemoteState);
    }

    bool _remoteAchievementsReset;

    IEnumerator ResetRemoteAchievementsOnce()
    {
        if (_remoteAchievementsReset || _link == null || !_link.Connected)
            yield break;
        yield return RemoteCommand("reset-achievements");
        _remoteAchievementsReset = _lastRemoteOk;
        if (!_lastRemoteOk)
            Debug.LogWarning($"[Scénario] Achievements du second joueur non remis à zéro : {_lastRemoteDetail}");
    }

    string RemoteMissingReason()
    {
        if (_link == null)
            return "lien de test non ouvert";
        if (!_link.Connected)
            return "aucun éditeur clone relié : activer Player 2 (Window > Multiplayer > Multiplayer Play Mode), "
                + "ou lancer ScenarioMenu.JoinAsClientFromCommandLine dans le clone";
        return $"relié mais pas en jeu : réseau {_link.Get(RemoteKeys.Network) ?? "?"}, pigeon local {_link.Get(RemoteKeys.HasPigeon) ?? "?"}, "
            + $"pigeon vu par l'hôte {(RemotePigeon != null ? "oui" : "non")}";
    }

    IEnumerator MissingRemote()
    {
        yield return Check("Second joueur en jeu", false, RemoteMissingReason());
    }

    IEnumerator EnsureRemoteOffline()
    {
        if (_link == null || !_link.Connected)
        {
            if (HasRemotePlayerPigeon())
                Debug.LogWarning("[Scénario] Un autre joueur est en jeu sans agent de test : les scénarios solo peuvent échouer.");
            yield break;
        }
        if (_link.Get(RemoteKeys.Network) == RemoteKeys.NetworkOffline && !HasRemotePlayerPigeon())
            yield break;

        yield return RemoteCommand("override", "off");
        yield return RemoteCommand("disconnect");
        float deadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < deadline && (HasRemotePlayerPigeon() || _link.Get(RemoteKeys.Network) != RemoteKeys.NetworkOffline))
        {
            _lastProgress = Time.realtimeSinceStartup;
            yield return null;
        }
        if (HasRemotePlayerPigeon())
        {
            string previous = _currentScenario;
            _currentScenario = "Second joueur";
            Record("Le second joueur n'a pas quitté la partie : les scénarios solo peuvent échouer", false, RemoteState);
            _currentScenario = previous;
        }
        yield return new WaitForSecondsRealtime(0.5f);
    }

    IEnumerator EnsureRemoteOnline()
    {
        if (_link == null || !_link.Connected)
            yield break;
        yield return ResetRemoteAchievementsOnce();
        if (RemoteReady)
            yield break;

        yield return RemoteCommand("connect");
        float deadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < deadline && !RemoteReady)
        {
            _lastProgress = Time.realtimeSinceStartup;
            yield return null;
        }
        if (RemoteReady)
            yield return new WaitForSecondsRealtime(1f);
    }

    IEnumerator ReleaseRemote()
    {
        if (_link == null || !_link.Connected)
            yield break;
        yield return EnsureRemoteOnline();
        yield return RemoteCommand("override", "off");
        yield return RemoteCommand(_commandLine ? "quit" : "bye");
    }

    /// <summary>Commande au second joueur ; résultat dans _lastRemoteOk / _lastRemoteDetail (sans faire échouer).</summary>
    IEnumerator RemoteCommand(string verb, string argument = "", float timeout = 5f)
    {
        _lastRemoteOk = false;
        _lastRemoteDetail = "pas de second joueur relié";
        if (_link == null || !_link.Connected)
            yield break;

        int id = _link.Send(verb, argument);
        float deadline = Time.realtimeSinceStartup + timeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (_link.TryGetReply(id, out bool ok, out string detail))
            {
                _lastRemoteOk = ok;
                _lastRemoteDetail = detail;
                yield break;
            }
            if (!_link.Connected)
            {
                _lastRemoteDetail = "lien perdu";
                yield break;
            }
            yield return null;
        }
        _lastRemoteDetail = $"pas de réponse en {timeout:0} s";
    }

    /// <summary>Résumé de ce que voit le second joueur, pour le détail des étapes.</summary>
    protected string RemoteState()
    {
        if (_link == null || !_link.Connected)
            return "second joueur non relié";
        var sb = new StringBuilder();
        sb.Append($"chez le second joueur : réseau {_link.Get(RemoteKeys.Network)}, pigeon {_link.Get(RemoteKeys.Position) ?? "absent"}");
        sb.Append($", gelé {_link.Get(RemoteKeys.Frozen)}, prêt {_link.Get(RemoteKeys.Ready)}, piloté {_link.Get(RemoteKeys.PigeonDriven)}");
        string dialogs = _link.Get(RemoteKeys.PlayingDialogs);
        if (!string.IsNullOrEmpty(dialogs))
            sb.Append($", dialogues [{dialogs}]");
        foreach (KeyValuePair<string, string> entry in _link.Snapshot)
            if (entry.Key.StartsWith(RemoteKeys.ZonePrefix, StringComparison.Ordinal) || entry.Key.StartsWith(RemoteKeys.DialogsPrefix, StringComparison.Ordinal)
                || (entry.Key.StartsWith(RemoteKeys.RacePrefix, StringComparison.Ordinal)
                && (entry.Key.EndsWith("/phase", StringComparison.Ordinal) || entry.Key.EndsWith("/departure", StringComparison.Ordinal)
                    || entry.Key.EndsWith("/arrival", StringComparison.Ordinal))))
                sb.Append($", {entry.Key} « {entry.Value} »");
        sb.Append($" (instantané il y a {Time.realtimeSinceStartup - _link.LastSnapshotTime:0.00} s)");
        return sb.ToString();
    }
}
#endif
