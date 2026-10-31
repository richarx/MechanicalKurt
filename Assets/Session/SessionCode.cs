using System.Text;

/// <summary>Codes de session : 6 caractères sans ambiguïté à la lecture (ni 0/O, ni 1/I/L).</summary>
public static class SessionCode
{
    public const int Length = 6;
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    const string InviteParameter = "code=";

    public static string Generate(System.Random random = null)
    {
        random ??= new System.Random();
        var code = new StringBuilder(Length);
        for (int i = 0; i < Length; i++)
            code.Append(Alphabet[random.Next(Alphabet.Length)]);
        return code.ToString();
    }

    /// <summary>
    /// Saisie ou texte collé ramené à un code : lien d'invitation accepté, majuscules, caractères hors alphabet
    /// retirés, coupé à <see cref="Length"/>.
    /// </summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        int invite = text.IndexOf(InviteParameter, System.StringComparison.OrdinalIgnoreCase);
        if (invite >= 0)
            text = text.Substring(invite + InviteParameter.Length);

        var code = new StringBuilder(Length);
        foreach (char c in text.ToUpperInvariant())
        {
            if (Alphabet.IndexOf(c) >= 0)
                code.Append(c);
            else if (invite >= 0 && (c == '&' || c == '#'))
                break;
            if (code.Length == Length)
                break;
        }
        return code.ToString();
    }

    public static bool IsValid(string code) => code != null && code.Length == Length && Normalize(code) == code;

    /// <summary>Lien qui ouvre le jeu sur « Rejoindre » avec ce code (export web), sinon null.</summary>
    public static string InviteLink(string pageUrl, string code)
    {
        if (string.IsNullOrEmpty(pageUrl) || !IsValid(code))
            return null;
        int cut = pageUrl.IndexOfAny(new[] { '?', '#' });
        string page = cut >= 0 ? pageUrl.Substring(0, cut) : pageUrl;
        return $"{page}?{InviteParameter}{code}";
    }

    /// <summary>Code d'un lien d'invitation (null s'il n'y en a pas).</summary>
    public static string FromInviteLink(string pageUrl)
    {
        if (string.IsNullOrEmpty(pageUrl) || pageUrl.IndexOf(InviteParameter, System.StringComparison.OrdinalIgnoreCase) < 0)
            return null;
        string code = Normalize(pageUrl);
        return IsValid(code) ? code : null;
    }
}
