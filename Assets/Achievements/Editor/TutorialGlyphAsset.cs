using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

/// <summary>Packs the three supplied Kenney glyphs into a persistent TMP sprite asset.</summary>
public static class TutorialGlyphAsset
{
    public const string AssetPath = "Assets/Achievements/Glyphs/TutorialXboxGlyphs.asset";

    public static TMP_SpriteAsset GetOrCreate()
    {
        TMP_SpriteAsset existing = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
        if (existing != null) return existing;

        string[] files = { "xbox_button_color_a", "xbox_button_color_b", "xbox_stick_l" };
        string[] names = { "xbox_a", "xbox_b", "xbox_left_stick" };
        Shader shader = Shader.Find("TextMeshPro/Sprite");
        if (shader == null) throw new InvalidOperationException("Tutorial: the TextMeshPro/Sprite shader is missing.");

        List<Texture2D> sources = new List<Texture2D>();
        Texture2D atlas = null;
        TMP_SpriteAsset asset = null;
        Material material = null;
        bool saved = false;
        try
        {
            foreach (string file in files)
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                sources.Add(texture);
                string path = "Assets/Achievements/Glyphs/" + file + ".png";
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                    throw new InvalidOperationException("Tutorial: cannot load " + path);
            }

            atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "Tutorial Xbox Glyph Atlas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Rect[] rects = atlas.PackTextures(sources.ToArray(), 4, 1024, false);
            asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "TutorialXboxGlyphs";
            SerializedObject settings = new SerializedObject(asset);
            settings.FindProperty("m_Version").stringValue = "1.1.0";
            settings.ApplyModifiedPropertiesWithoutUndo();
            asset.spriteSheet = atlas;
            asset.faceInfo = new FaceInfo { pointSize = 32, scale = 1, ascentLine = 28, descentLine = -4, lineHeight = 36 };
            material = new Material(shader) { name = "Tutorial Xbox Glyph Material", mainTexture = atlas };
            asset.material = material;

            for (int i = 0; i < rects.Length; i++)
            {
                Rect rect = rects[i];
                TMP_SpriteGlyph glyph = new TMP_SpriteGlyph
                {
                    index = (uint)i,
                    glyphRect = new GlyphRect(Mathf.RoundToInt(rect.x * atlas.width), Mathf.RoundToInt(rect.y * atlas.height),
                        Mathf.RoundToInt(rect.width * atlas.width), Mathf.RoundToInt(rect.height * atlas.height)),
                    metrics = new GlyphMetrics(32, 32, 0, 28, 36),
                    scale = 1
                };
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, asset, glyph) { name = names[i] });
            }
            asset.UpdateLookupTables();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.AddObjectToAsset(atlas, asset);
            AssetDatabase.AddObjectToAsset(material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            saved = true;
            return asset;
        }
        finally
        {
            foreach (Texture2D texture in sources) UnityEngine.Object.DestroyImmediate(texture);
            if (!saved)
            {
                if (material != null && !EditorUtility.IsPersistent(material)) UnityEngine.Object.DestroyImmediate(material);
                if (atlas != null && !EditorUtility.IsPersistent(atlas)) UnityEngine.Object.DestroyImmediate(atlas);
                if (asset != null && !EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
