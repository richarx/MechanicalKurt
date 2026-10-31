using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Presse-papiers : celui du système dans l'éditeur et sur PC, celui du navigateur dans l'export web
/// (Plugins/WebGL/Clipboard.jslib), où Unity n'y a pas accès.
/// </summary>
public static class ClipboardBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void ClipboardCopy(string text);
    [DllImport("__Internal")] static extern void ClipboardRequestPaste(string objectName, string method);
    [DllImport("__Internal")] static extern void ClipboardListenPaste(string objectName, string method);
#endif

    public static void Copy(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
#if UNITY_WEBGL && !UNITY_EDITOR
        ClipboardCopy(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }

    /// <summary>Lit le presse-papiers et l'envoie à receiver.method(string) (tout de suite, ou plus tard sur le web).</summary>
    public static void RequestPaste(GameObject receiver, string method)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ClipboardRequestPaste(receiver.name, method);
#else
        receiver.SendMessage(method, GUIUtility.systemCopyBuffer ?? "", SendMessageOptions.DontRequireReceiver);
#endif
    }

    /// <summary>Web : chaque Ctrl+V (ou collage du navigateur) envoie le texte à receiver.method(string).</summary>
    public static void ListenBrowserPaste(GameObject receiver, string method)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ClipboardListenPaste(receiver.name, method);
#endif
    }
}
