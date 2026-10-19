using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

/// <summary>Packs the flight glyphs and links the Start glyph into a persistent TMP sprite asset.</summary>
public static class TutorialGlyphAsset
{
    public const string AssetPath = "Assets/Achievements/Glyphs/TutorialXboxGlyphs.asset";
    private const string StartAssetPath = "Assets/Achievements/Glyphs/TutorialXboxStartGlyph.asset";
    private const string StartTexturePath = "Assets/Achievements/Glyphs/xbox_button_start.png";

    [MenuItem("Tools/Tutorial/Repair Xbox Glyphs")]
    public static void Repair()
    {
        TMP_SpriteAsset asset = GetOrCreate();
        TMPro_EventManager.ON_SPRITE_ASSET_PROPERTY_CHANGED(true, asset);
        Selection.activeObject = asset;
        Debug.Log("Xbox glyph textures repaired.", asset);
    }

    public static TMP_SpriteAsset GetOrCreate()
    {
        TMP_SpriteAsset existing = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
        if (existing != null)
        {
            LinkStartGlyph(existing);
            return existing;
        }

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
            LinkStartGlyph(asset);
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

    private static void LinkStartGlyph(TMP_SpriteAsset asset)
    {
        AssetDatabase.ImportAsset(StartTexturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(StartTexturePath);
        if (texture == null) throw new InvalidOperationException("Tutorial: the Xbox Start image did not import as a Texture2D.");
        TMP_SpriteAsset start = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(StartAssetPath);
        if (start == null) throw new InvalidOperationException("Tutorial: the Xbox Start glyph asset is missing.");
        start.spriteSheet = texture;
        if (start.material == null)
        {
            Shader shader = Shader.Find("TextMeshPro/Sprite");
            if (shader == null) throw new InvalidOperationException("Tutorial: the TextMeshPro/Sprite shader is missing.");
            start.material = new Material(shader) { name = "Tutorial Xbox Start Glyph Material" };
            AssetDatabase.AddObjectToAsset(start.material, start);
        }
        start.material.mainTexture = texture;
        EditorUtility.SetDirty(start.material);
        EditorUtility.SetDirty(start);
        AssetDatabase.SaveAssetIfDirty(start);
        TMPro_EventManager.ON_SPRITE_ASSET_PROPERTY_CHANGED(true, start);
        if (asset.fallbackSpriteAssets == null) asset.fallbackSpriteAssets = new List<TMP_SpriteAsset>();
        if (asset.fallbackSpriteAssets.Contains(start)) return;
        asset.fallbackSpriteAssets.Add(start);
        EditorUtility.SetDirty(asset);
        if (EditorUtility.IsPersistent(asset)) AssetDatabase.SaveAssetIfDirty(asset);
    }
}
