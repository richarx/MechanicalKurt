using PurrNet;
using UnityEngine;

/// <summary>Une couleur par joueur, déduite de son identifiant réseau : la même sur chaque machine, sans rien synchroniser.</summary>
public static class PlayerColors
{
    static readonly Color[] Palette =
    {
        new Color(1.00f, 0.62f, 0.20f), // orange
        new Color(0.30f, 0.78f, 1.00f), // bleu ciel
        new Color(1.00f, 0.45f, 0.72f), // rose
        new Color(0.55f, 0.90f, 0.35f), // vert pomme
        new Color(1.00f, 0.88f, 0.25f), // jaune
        new Color(0.72f, 0.55f, 1.00f), // violet
        new Color(1.00f, 0.38f, 0.35f), // rouge
        new Color(0.25f, 0.90f, 0.78f), // turquoise
    };

    public static Color For(PlayerID? player)
    {
        if (!player.HasValue)
            return Color.white;
        return Palette[(int)(player.Value.id.value % (ulong)Palette.Length)];
    }
}
