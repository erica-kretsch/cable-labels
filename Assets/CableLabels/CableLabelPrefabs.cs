using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.UI;
using BepInEx.Logging;
using LaunchPadBooster;
using LaunchPadBooster.Utils;
using UnityEngine;

namespace CableLabels
{
    // Registers the asset bundle's cable label and makes its kit. LaunchPad finds any
    // MonoBehaviour with an OnLoaded(List<GameObject>) method and calls it with the bundles'
    // prefabs.
    //
    // The prefab has our own mesh and icons (Assets/Editor/BuildPrefabs.cs), but the Unity
    // project has none of the game's assets. Here, once the game's prefabs exist, the label takes
    // the vanilla pipe label's material, text settings and placement preview (with our mesh), and
    // its placement settings from a vanilla cable fuse, the closest cable-mounted device. Its kit
    // is a copy of the pipe label's, given our mesh, texture and icon.
    //
    // This runs in LaunchPadBooster's prefix on Prefab.LoadAll: the game's source prefabs exist
    // (WorldManager.SourcePrefabs, which PrefabUtils.FindPrefab searches), but Prefab.AllPrefabs
    // is still empty, so never look anything up there.
    public class CableLabelPrefabs : MonoBehaviour
    {
        public static readonly Mod MOD = new Mod(ThisModInfo.AssemblyName, ThisModInfo.Version);

        internal static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("Cable Labels");

        private const string VanillaLabel = "StructurePipeLabel";
        private const string VanillaKit = "ItemPipeLabel";

        // The cable label's own kit, a copy of the pipe label's (MakeKit). Its recipe is in
        // Assets/GameData/Recipes/cable_label.xml.
        private const string KitName = "ItemCableLabel";

        private static Constructor _kit;

        public void OnLoaded(List<GameObject> prefabs)
        {
            try
            {
                MOD.AddPrefabs(prefabs);
                MOD.SetupPrefabs<CableLabel>()
                    .RunFunc(_ => MakeKit())
                    .SetEntryTool(KitName)
                    .RunFunc(BorrowFromVanilla);
                Log.LogInfo($"Cable Labels {ThisModInfo.Version}: registered {prefabs.Count} prefab(s)");
            }
            catch (Exception ex)
            {
                Log.LogError($"Registering the cable label failed: {ex}");
            }
        }

        // The kit, once: a copy of the pipe label kit's source prefab, so it is a real item with
        // that kit's model, physics and stacking, renamed. Added to the game's source prefabs,
        // which Prefab.LoadAll registers next, as it does the bundle's. It lives under an
        // inactive holder, so the copy never wakes as a thing in the world. SBR Electrical makes
        // its coils the same way (CablePrefabSet.MakeKit).
        private static void MakeKit()
        {
            if (_kit != null)
                return;
            var vanilla = WorldManager.Instance.SourcePrefabs
                .FirstOrDefault(t => t != null && t.PrefabName == VanillaKit);
            if (!(vanilla is Constructor constructor))
            {
                Log.LogError(vanilla == null
                    ? $"No {VanillaKit} to copy; the cable label can't be built"
                    : $"{VanillaKit} is a {vanilla.GetType().Name}, not a Constructor; the cable label can't be built");
                return;
            }
            _kit = UnityEngine.Object.Instantiate(constructor, Holder.transform);
            _kit.name = KitName;
            _kit.PrefabName = KitName;
            _kit.PrefabHash = Animator.StringToHash(KitName);
            WorldManager.Instance.SourcePrefabs.Add(_kit);
        }

        private static GameObject _holder;

        private static GameObject Holder
        {
            get
            {
                if (_holder != null)
                    return _holder;
                _holder = new GameObject("~CableLabelsPrefabs");
                _holder.SetActive(false);
                DontDestroyOnLoad(_holder);
                return _holder;
            }
        }

        private static void BorrowFromVanilla(CableLabel label)
        {
            // The kit builds this label, whatever else is found.
            if (_kit != null)
                _kit.BuildStructure = label;

            var vanilla = PrefabUtils.FindPrefab<Label>(VanillaLabel);
            var fuse = WorldManager.Instance.SourcePrefabs.OfType<CableFuse>().FirstOrDefault();
            if (vanilla == null || vanilla.MeshRenderer == null)
            {
                Log.LogError($"No vanilla {VanillaLabel} (or it has no MeshRenderer) to borrow from; "
                    + "the cable label stays a placeholder");
                return;
            }
            if (fuse == null)
                Log.LogError("No cable fuse to borrow placement settings from; keeping the builder's");

            // The look: our mesh, in the pipe label's material, whose label slot the text
            // texture replaces (CableLabel.Awake).
            var mesh = label.MeshRenderer.GetComponent<MeshFilter>().sharedMesh;
            label.MeshRenderer.sharedMaterials = vanilla.MeshRenderer.sharedMaterials;
            label.materialLabelIndex = vanilla.materialLabelIndex;
            label.textTextureWidth = vanilla.textTextureWidth;
            label.textTextureHeight = vanilla.textTextureHeight;

            // The placement preview: a copy of the pipe label's, in its hologram material, with
            // our mesh. Its Wireframe draws the outline from edges stored with the prefab,
            // generated from the pipe label's mesh, so they are generated again from ours with
            // the game's own generator; otherwise a pipe-sized outline is drawn round our fill.
            if (vanilla.Blueprint != null)
            {
                var blueprint = Instantiate(vanilla.Blueprint, Holder.transform);
                blueprint.name = label.PrefabName + "Blueprint";
                foreach (var filter in blueprint.GetComponentsInChildren<MeshFilter>(true))
                    filter.sharedMesh = mesh;
                var wireframe = blueprint.GetComponent<Wireframe>();
                if (wireframe != null)
                {
                    wireframe.WireframeEdges = new WireframeGenerator(blueprint.transform).Edges;
                    wireframe.BlueprintBounds = mesh.bounds;
                }
                else
                    Log.LogWarning($"{VanillaLabel}'s blueprint has no Wireframe; its outline isn't ours");
                label.Blueprint = blueprint;
            }

            if (_kit != null)
                DressKit(label, mesh);

            // Comes apart with what the pipe label uses; SetEntryTool made our kit the entry tool.
            var exit = vanilla.BuildStates[0].Tool;
            label.BuildStates[0].Tool.ToolExit = exit.ToolExit;
            label.BuildStates[0].Tool.ExitTime = exit.ExitTime;

            // Where it sits and what it collides with, as any device mounted on a cable.
            if (fuse != null)
            {
                label.GridSize = fuse.GridSize;
                label.GridOffset = fuse.GridOffset;
                label.SmallCollisionType = fuse.SmallCollisionType;
            }
            Log.LogInfo($"Cable label set up from {VanillaLabel}"
                + (fuse != null ? $" and {fuse.PrefabName}" : "")
                + (_kit != null ? $"; built from {KitName}" : "; no kit"));
        }

        // The kit is the pipe label kit's model, a loose label: give it our mesh, our texture
        // reading "Cable Label" in place of "Pipe Label", our icon, and a collider to match.
        private static void DressKit(CableLabel label, Mesh mesh)
        {
            if (label.KitThumbnail != null)
                _kit.Thumbnail = label.KitThumbnail;
            foreach (var filter in _kit.GetComponentsInChildren<MeshFilter>(true))
                filter.sharedMesh = mesh;
            foreach (var renderer in _kit.GetComponentsInChildren<MeshRenderer>(true))
            {
                var material = new Material(renderer.sharedMaterial) { name = KitName };
                if (label.KitTexture != null)
                    material.mainTexture = label.KitTexture;
                renderer.sharedMaterial = material;
            }
            var box = _kit.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = mesh.bounds.center;
                box.size = mesh.bounds.size + Vector3.one * 0.01f;
            }
        }
    }
}
