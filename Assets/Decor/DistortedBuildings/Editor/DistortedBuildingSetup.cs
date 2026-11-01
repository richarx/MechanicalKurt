using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DistortedBuildingSetup
{
    private const string Folder = "Assets/Decor/DistortedBuildings";
    private const string ModelPath = Folder + "/flat_Lshape_vertical_stripes_distorted.fbx";
    private const string PrefabPath = Folder + "/flat_Lshape_vertical_stripes_distorted.prefab";

    [MenuItem("Tools/Decor/Prepare distorted building")]
    public static void CreatePrefab()
    {
        AssetDatabase.Refresh();
        var reference = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Decor/URBAN/Buildings Prebuild/Houses 3.mat");
        if (reference == null)
            throw new InvalidOperationException("Le matériau Houses 3 est introuvable.");

        if (!AssetDatabase.IsValidFolder(Folder + "/Materials"))
            AssetDatabase.CreateFolder(Folder, "Materials");

        var materials = new Dictionary<string, Material>
        {
            ["Houses"] = SaveMaterial(reference, "Distorted_Houses", Color.white, true),
            ["Distorted_Interior"] = SaveMaterial(reference, "Distorted_Interior", new Color(.018f, .013f, .012f)),
            ["Distorted_WindowFrames"] = SaveMaterial(reference, "Distorted_WindowFrames", new Color(.075f, .048f, .029f)),
            ["Distorted_WarmGlass"] = SaveMaterial(reference, "Distorted_WarmGlass", new Color(.65f, .43f, .18f)),
            ["Distorted_OldStone"] = SaveMaterial(reference, "Distorted_OldStone", new Color(.42f, .36f, .25f))
        };
        var glass = materials["Distorted_WarmGlass"];
        glass.EnableKeyword("_EMISSION");
        glass.SetColor("_EmissionColor", new Color(.65f, .43f, .18f) * .3f);
        EditorUtility.SetDirty(glass);

        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("Le FBX distordu est introuvable.");
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.importNormals = ModelImporterNormals.Import;
        foreach (var pair in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
        importer.SaveAndReimport();

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
            throw new InvalidOperationException("Unity n'a pas pu charger le FBX.");

        var root = new GameObject("flat_Lshape_vertical_stripes_distorted");
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Visual_and_collision";
            instance.transform.SetParent(root.transform, false);
            var filters = instance.GetComponentsInChildren<MeshFilter>();
            var collision = filters.SingleOrDefault(filter => filter.name.StartsWith("Collision_LowPoly"));
            if (collision == null || collision.sharedMesh == null)
                throw new InvalidOperationException("Le maillage de collision est absent.");

            var collider = collision.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = collision.sharedMesh;
            collider.convex = false;
            var collisionRenderer = collision.GetComponent<MeshRenderer>();
            if (collisionRenderer != null)
                collisionRenderer.enabled = false;

            var renderers = instance.GetComponentsInChildren<MeshRenderer>()
                .Where(renderer => renderer.enabled).ToArray();
            if (renderers.Length == 0)
                throw new InvalidOperationException("Le bâtiment ne contient aucun rendu visible.");

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                if (renderer.sharedMaterials.Any(mat => mat == null || mat.shader == null || mat.shader.name.Contains("InternalError")))
                    throw new InvalidOperationException("Un matériau est absent ou invalide sur " + renderer.name);
            }
            if (bounds.size.y < 28f || bounds.size.y > 31f)
                throw new InvalidOperationException("La hauteur importée est inattendue : " + bounds.size.y);

            // A new ground-centred pivot, independent of the old model's offsets.
            instance.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();

            var report = "Import Unity réussi\n" +
                "Dimensions avant recentrage : " + bounds.size + "\n" +
                "Rendus visibles : " + renderers.Length + "\n" +
                "Triangles de collision : " + collision.sharedMesh.triangles.Length / 3 + "\n" +
                "Collision statique non convexe ; aucun Rigidbody.\n" +
                "Prefab : " + PrefabPath + "\n";
            File.WriteAllText(Path.Combine(Application.dataPath, "../ArtSource/DistortedBuildings/unity-verification.txt"), report);
            Debug.Log(report);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Material SaveMaterial(Material reference, string name, Color color, bool keepTexture = false)
    {
        var path = Folder + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        material = new Material(reference) { name = name };
        if (!keepTexture)
        {
            material.SetTexture("_BaseMap", null);
            material.SetTexture("_MainTex", null);
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Cull", 2);
        }
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
