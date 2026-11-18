using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Objects;
using UnityEditor;
using UnityEngine;

namespace CableLabels.Editor
{
    /// <summary>
    /// Builds the cable label prefab and its mesh into Assets/Prefabs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Export.Run"/> runs this before every export, and it overwrites what it built,
    /// so edit this file, not the prefab.
    /// </para>
    /// <para>
    /// The mesh follows the vanilla pipe label's, scaled down to a cable: two curved panels either
    /// side of the axis, open at top and bottom, each showing the whole text texture. The
    /// material is a stand-in; <c>CableLabelPrefabs</c> gives the label the pipe label's at load.
    /// </para>
    /// </remarks>
    public static class BuildPrefabs
    {
        private const string Folder = "Assets/Prefabs";
        private const string Art = "Assets/Art";

        private const string Name = "StructureCableLabel";

        // The pipe label is 0.204 m across and 0.5 m long, its panels 120° each. A normal cable is
        // 0.05 m across. These panels wrap further and are shorter, keeping about the 4:1 shape
        // of the 512 × 128 text texture.
        private const float Radius = 0.035f;
        private const float Length = 0.32f;
        private const float PanelDegrees = 140f;
        private const int PanelSegments = 7;

        [MenuItem("Cable Labels/Build prefabs")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Folder);
            var mesh = LabelMesh();
            var root = new GameObject(Name);
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = PlaceholderMaterial();

                // What the cursor and the Labeller hit: two trigger capsules, as the pipe label's,
                // standing out past the cable's own (r 0.1) so the label is hit first.
                foreach (var side in new[] { -1f, 1f })
                {
                    var capsule = root.AddComponent<CapsuleCollider>();
                    capsule.isTrigger = true;
                    capsule.direction = 2;
                    capsule.radius = 0.1f;
                    capsule.height = Length + 0.05f;
                    capsule.center = new Vector3(0.1f * side, 0f, 0f);
                }

                var label = root.AddComponent<CableLabel>();
                label.PrefabName = Name;
                label.PrefabHash = Animator.StringToHash(Name);
                label.ThingTransform = root.transform;
                label.Bounds = new Bounds(Vector3.zero, Vector3.one * 0.5f);
                label.MeshRenderer = renderer;
                label.materialLabelIndex = 0;
                label.Thumbnail = LoadSprite(Art + "/ThumbCableLabel.png");
                label.KitThumbnail = LoadSprite(Art + "/ThumbCableLabelKit.png");
                label.KitTexture = LoadTexture(Art + "/CableLabelKit.png");
                // One 0.5 m small-grid cell. CableLabelPrefabs replaces these with a cable fuse's,
                // but they must be valid without it: GridSize 0 breaks GetLocalGridBounds.
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

        /// <summary>
        /// Two panels centred on ±x, z along the cable, front faces wound clockwise from outside.
        /// </summary>
        private static Mesh LabelMesh()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var side in new[] { 1f, -1f })
            {
                var start = vertices.Count;
                for (var i = 0; i <= PanelSegments; i++)
                {
                    var v = (float)i / PanelSegments;
                    var angle = Mathf.Deg2Rad * PanelDegrees * (v - 0.5f);
                    var normal = new Vector3(side * Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    // u runs left to right as seen from outside: +z on the +x panel, −z on −x.
                    foreach (var u in new[] { 0f, 1f })
                    {
                        var z = side * (u - 0.5f) * Length;
                        vertices.Add(normal * Radius + new Vector3(0f, 0f, z));
                        normals.Add(normal);
                        uvs.Add(new Vector2(u, v));
                    }
                }
                for (var i = 0; i < PanelSegments; i++)
                {
                    var bl = start + 2 * i;
                    int br = bl + 1, tl = bl + 2, tr = bl + 3;
                    triangles.AddRange(new[] { bl, tl, tr, bl, tr, br });
                }
            }

            var path = $"{Folder}/{Name}.mesh.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = Name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Sprite LoadSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Texture2D LoadTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material PlaceholderMaterial()
        {
            var path = Folder + "/CableLabelPlaceholder.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            // Replaced at load by the pipe label's material.
            material = new Material(Shader.Find("Standard")) { color = new Color(0.9f, 0.9f, 0.8f) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
