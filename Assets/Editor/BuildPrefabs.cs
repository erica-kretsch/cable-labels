using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Objects;
using UnityEditor;
using UnityEngine;

namespace CableLabels.Editor
{
    // Builds the cable label prefab from code, so it is reproducible and reviewable. Run it from
    // the menu; Export.Run runs it before every export. It overwrites the prefab, so edit this
    // file, not the prefab.
    //
    // The prefab is a placeholder: a small box in a stand-in material. At load,
    // CableLabelPrefabs gives it the vanilla pipe label's mesh, materials and text settings and
    // fits its collider, so this only has to get the structure right: the component, the
    // Sleeve that makes it thin, and the Visual inside it.
    public static class BuildPrefabs
    {
        private const string Folder = "Assets/Prefabs";

        private const string Name = "StructureCableLabel";

        // The sleeve's thinness across the cable. Keep in step with CableLabelPrefabs.Thinness,
        // which sets it again at load.
        private const float Thinness = 0.5f;

        [MenuItem("Cable Labels/Build prefabs")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            var root = new GameObject(Name);
            try
            {
                // Root → Sleeve (thin across the cable, z along it) → Visual (the mesh, placed as
                // on the pipe label once CableLabelPrefabs has run).
                var sleeve = new GameObject("Sleeve");
                sleeve.transform.SetParent(root.transform, false);
                sleeve.transform.localScale = new Vector3(Thinness, Thinness, 1f);
                var visual = new GameObject("Visual");
                visual.transform.SetParent(sleeve.transform, false);
                visual.transform.localScale = new Vector3(0.2f, 0.2f, 0.3f);
                visual.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = PlaceholderMaterial();

                var box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(0.2f * Thinness, 0.2f * Thinness, 0.3f);

                var label = root.AddComponent<CableLabel>();
                label.PrefabName = Name;
                label.PrefabHash = Animator.StringToHash(Name);
                label.ThingTransform = root.transform;
                label.Bounds = new Bounds(Vector3.zero, Vector3.one * 0.5f);
                label.MeshRenderer = renderer;
                // A small-grid device, one 0.5 m cell, as a cable's. CableLabelPrefabs takes a
                // cable fuse's at load, but these must be valid without it: GridSize 0 divides
                // by zero in Structure.GetLocalGridBounds.
                label.GridSize = 0.5f;
                label.GridOffset = 0.25f;
                label.BuildStates = new List<BuildState>
                {
                    new BuildState
                    {
                        Visualizer = renderer,
                        Tool = new ToolUse { EntryQuantity = 1, EntryTime = 0.5f, ExitTime = 0.5f },
                    },
                };

                PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{Name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Cable Labels: prefab built");
        }

        private static Material PlaceholderMaterial()
        {
            var path = Folder + "/CableLabelPlaceholder.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            // Replaced at load by the pipe label's materials (CableLabelPrefabs).
            material = new Material(Shader.Find("Standard")) { color = new Color(0.9f, 0.9f, 0.8f) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
