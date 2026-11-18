using System.Collections.Generic;
using System.IO;
using Assets.Scripts.Objects;
using UnityEditor;
using UnityEngine;

namespace CableLabels.Editor
{
    // Builds the cable label prefab from code, so it is reproducible and reviewable. Run it from
    // the menu; Export.Run runs it before every export. It overwrites the prefab and its mesh,
    // so edit this file, not them.
    //
    // The mesh is ours, made here after the vanilla pipe label's (read from the game's assets
    // with UnityPy): two curved panels either side of the pipe, open at top and bottom, each
    // showing the whole text texture, v up the arc and u along the length, so the text reads
    // left to right from either side. Ours is scaled down to a cable. The material stays a
    // stand-in: CableLabelPrefabs gives it the pipe label's at load, which the Unity project
    // doesn't have. The icons and the kit's texture come from tools/make_art.py.
    public static class BuildPrefabs
    {
        private const string Folder = "Assets/Prefabs";
        private const string Art = "Assets/Art";

        private const string Name = "StructureCableLabel";

        // The vanilla pipe label is 0.204 m across on a pipe, 0.5 m long, each panel spanning
        // 120°. A cable is 0.05 m across (StructureCableStraight's mesh), so the panels sit at
        // 0.035 m. They wrap further, 140°, and are shorter, so each panel keeps about the 4:1
        // shape of the 512 × 128 text texture; vanilla's are 2.3:1, its text drawn taller.
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

                // What the Labeller and the cursor hit: vanilla's two trigger capsules, either
                // side of the axis, which stand out past the cable's own (r 0.1 about its axis)
                // so the label is hit before the cable it sits on.
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

        // Two panels, centred on +x and −x, z along the cable. Front faces wind clockwise seen
        // from outside, as Unity draws them.
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
                    // Seen from outside, the viewer's right is +z on the +x panel and −z on
                    // the −x one; u runs left to right.
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
            // Replaced at load by the pipe label's material (CableLabelPrefabs).
            material = new Material(Shader.Find("Standard")) { color = new Color(0.9f, 0.9f, 0.8f) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
