using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CableLabels.Editor
{
    /// <summary>
    /// Renders the label's and the kit's inventory icons from the mesh with a camera, into
    /// Assets/Art/ThumbCableLabel.png and ThumbCableLabelKit.png.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Run it after changing the mesh, from the menu or headless without <c>-nographics</c>,
    /// which can't render:
    /// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod CableLabels.Editor.RenderIcons.Run</c>.
    /// </para>
    /// <para>
    /// The pipe label's icons run top-left to bottom-right; these run the other way, and the
    /// placed label's shows a stub of cable, so the two are easy to tell apart. They must be
    /// this mod's own: the creative spawn menu keys its buttons on the icon's texture.
    /// </para>
    /// </remarks>
    public static class RenderIcons
    {
        private const string Art = "Assets/Art";
        private const string MeshPath = "Assets/Prefabs/StructureCableLabel.mesh.asset";
        private const int Size = 256;

        [MenuItem("Cable Labels/Render icons")]
        public static void Run()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            var kitTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/CableLabelKit.png");
            if (mesh == null || kitTexture == null)
                throw new InvalidOperationException("Build the prefab and run tools/make_art.py first");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.43f, 0.45f);
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.LookRotation(new Vector3(-0.7f, -0.8f, 0.45f));

            Render(Art + "/ThumbCableLabelKit.png", root =>
                AddPart(root, "Label", mesh, Material(Color.white, kitTexture),
                    Quaternion.identity, Vector3.one));

            Render(Art + "/ThumbCableLabel.png", root =>
            {
                AddPart(root, "Label", mesh, Material(Color.white, null),
                    Quaternion.identity, Vector3.one);
                // 0.5 m of normal cable, 0.05 m across; the built-in cylinder lies along y.
                var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
                var size = cylinder.bounds.size;
                AddPart(root, "Cable", cylinder, Material(new Color(0.72f, 0.13f, 0.1f), null),
                    Quaternion.Euler(90f, 0f, 0f),
                    new Vector3(0.05f / size.x, 0.5f / size.y, 0.05f / size.z));
            });

            Object.DestroyImmediate(light.gameObject);
            AssetDatabase.Refresh();
            Debug.Log("Cable Labels: icons rendered");
        }

        private static void Render(string path, Action<GameObject> build)
        {
            // The label rises to the right on screen, its +x panel facing the camera upright.
            var root = new GameObject("Icon");
            root.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);
            build(root);

            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;
            camera.transform.position = new Vector3(1f, 0.45f, 0f).normalized * 2f;
            camera.transform.LookAt(Vector3.zero);
            Frame(camera, root);

            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8,
            };
            camera.targetTexture = target;
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(root);
        }

        /// <summary>Centres and fits the camera on everything under <paramref name="root"/>.</summary>
        private static void Frame(Camera camera, GameObject root)
        {
            var view = camera.transform;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue,
                maxY = float.MinValue;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var b = renderer.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3(
                        (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = view.InverseTransformPoint(corner);
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }
            }
            view.position += view.right * ((minX + maxX) / 2f) + view.up * ((minY + maxY) / 2f);
            camera.orthographicSize = Mathf.Max(maxX - minX, maxY - minY) / 2f * 1.06f;
        }

        private static void AddPart(GameObject root, string name, Mesh mesh, Material material,
            Quaternion rotation, Vector3 scale)
        {
            var part = new GameObject(name);
            part.transform.SetParent(root.transform, false);
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material Material(Color color, Texture texture)
        {
            var material = new Material(Shader.Find("Standard")) { color = color };
            material.SetFloat("_Glossiness", 0.3f);
            if (texture != null)
                material.mainTexture = texture;
            return material;
        }
    }
}
