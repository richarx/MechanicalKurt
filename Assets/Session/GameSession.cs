using UnityEngine;

/// <summary>Partie en cours, lancée depuis le menu principal. Sans menu (scène de la ville lancée seule), Code reste null.</summary>
public static class GameSession
{
    /// <summary>Code de la partie, à partager pour la rejoindre.</summary>
    public static string Code { get; private set; }
    public static bool IsHost { get; private set; }

    /// <summary>Le menu principal est affiché : pause et commandes du pigeon inactives.</summary>
    public static bool InMenu { get; set; }

    /// <summary>Message à afficher au retour dans le menu (partie terminée, connexion perdue…).</summary>
    public static string PendingMessage { get; set; }

    public static string InviteLink => SessionCode.InviteLink(PageUrl, Code);

    /// <summary>Adresse de la page du jeu (export web), null ailleurs.</summary>
    public static string PageUrl => Application.platform == RuntimePlatform.WebGLPlayer ? Application.absoluteURL : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Code = null;
        IsHost = false;
        InMenu = false;
        PendingMessage = null;
    }

    public static void Begin(string code, bool host)
    {
        Code = code;
        IsHost = host;
    }

    public static void End()
    {
        Code = null;
        IsHost = false;
    }
}
