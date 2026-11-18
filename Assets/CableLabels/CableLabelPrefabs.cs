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
    /// <summary>
    /// Registers the cable label from the asset bundle and makes its kit, finishing both from the
    /// vanilla pipe label and cable fuse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LaunchPad calls <see cref="OnLoaded"/> in LaunchPadBooster's prefix on
    /// <c>Prefab.LoadAll</c>. The game's source prefabs exist by then
    /// (<c>WorldManager.SourcePrefabs</c>, which <c>PrefabUtils.FindPrefab</c> searches), but
    /// <c>Prefab.AllPrefabs</c> is still empty.
    /// </para>
    /// <para>
    /// The Unity project has none of the game's assets, so the label takes the pipe label's
    /// material, text settings, deconstruct tool and placement preview here, and a cable fuse's
    /// grid settings.
    /// </para>
    /// </remarks>
    public class CableLabelPrefabs : MonoBehaviour
    {
        public static readonly Mod MOD = new Mod(ThisModInfo.AssemblyName, ThisModInfo.Version);

        internal static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("Cable Labels");

        private const string VanillaLabel = "StructurePipeLabel";
        private const string VanillaKit = "ItemPipeLabel";

        /// <summary>The kit's prefab name; its recipe is in GameData/Recipes/cable_label.xml.</summary>
        private const string KitName = "ItemCableLabel";

        private static Constructor _kit;

        /// <summary>Registers the bundle's prefabs; LaunchPad calls this once.</summary>
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

        /// <summary>
        /// Makes the kit once, as a renamed copy of the pipe label kit, and adds it to the game's
        /// source prefabs.
        /// </summary>
        /// <remarks>
        /// The copy keeps the vanilla kit's physics and stacking. It lives under an inactive
        /// holder, so it never wakes as a thing in the world. SBR Electrical makes its cable
        /// coils the same way.
        /// </remarks>
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

        /// <summary>An inactive parent for prefabs copied at load.</summary>
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

        /// <summary>
        /// Finishes the label from the vanilla pipe label and a cable fuse, and dresses the kit.
        /// </summary>
        private static void BorrowFromVanilla(CableLabel label)
        {
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

            // Our mesh in the pipe label's material; CableLabel.Awake puts the text texture in it.
            var mesh = label.MeshRenderer.GetComponent<MeshFilter>().sharedMesh;
            label.MeshRenderer.sharedMaterials = vanilla.MeshRenderer.sharedMaterials;
            label.materialLabelIndex = vanilla.materialLabelIndex;
            label.textTextureWidth = vanilla.textTextureWidth;
            label.textTextureHeight = vanilla.textTextureHeight;

            // The placement preview: the pipe label's, with our mesh. Its Wireframe stores edges
            // generated from the pipe label's mesh, so they're generated again from ours.
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

            // Deconstructed with the pipe label's tool.
            var exit = vanilla.BuildStates[0].Tool;
            label.BuildStates[0].Tool.ToolExit = exit.ToolExit;
            label.BuildStates[0].Tool.ExitTime = exit.ExitTime;

            // Grid placement and collision as for any cable-mounted device.
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

        /// <summary>
        /// Gives the kit the label's mesh, a texture reading "Cable Label", its own icon and a
        /// collider to match.
        /// </summary>
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
