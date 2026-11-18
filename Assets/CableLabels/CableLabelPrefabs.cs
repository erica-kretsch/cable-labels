using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
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
    // The Unity project has none of the game's assets, so its prefab is a placeholder
    // (Assets/Editor/BuildPrefabs.cs). Here, once the game's prefabs exist, it takes the vanilla
    // pipe label's mesh, materials and text settings, shrunk across its axis to fit a cable, and
    // its placement settings from a vanilla cable fuse, the closest cable-mounted device. Its kit
    // is a copy of the pipe label's.
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

        // How much thinner than the pipe label the sleeve is, across the cable. The length along
        // it is unchanged. A first guess, to tune in game against a normal cable.
        private const float Thinness = 0.5f;

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
            // The kit builds this label.
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

            // The look: the pipe label's mesh and materials, placed as they are on the pipe label,
            // inside the sleeve, which BuildPrefabs scales across the axis.
            var visual = label.MeshRenderer.transform;
            var theirs = vanilla.MeshRenderer.transform;
            visual.GetComponent<MeshFilter>().sharedMesh = theirs.GetComponent<MeshFilter>().sharedMesh;
            label.MeshRenderer.sharedMaterials = vanilla.MeshRenderer.sharedMaterials;
            var local = vanilla.transform.worldToLocalMatrix * theirs.localToWorldMatrix;
            visual.localPosition = local.GetColumn(3);
            visual.localRotation = local.rotation;
            visual.localScale = local.lossyScale;
            visual.parent.localScale = new Vector3(Thinness, Thinness, 1f);
            label.materialLabelIndex = vanilla.materialLabelIndex;
            label.textTextureWidth = vanilla.textTextureWidth;
            label.textTextureHeight = vanilla.textTextureHeight;
            FitCollider(label, visual);

            // The placement preview and inventory icon. The preview is the pipe label's, so it
            // shows pipe-thick until the label has its own.
            label.Blueprint = vanilla.Blueprint;
            label.Thumbnail = vanilla.Thumbnail;

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
            // The kit's inventory icon is the pipe label kit's, from the copy.
            Log.LogInfo($"Cable label set up from {VanillaLabel}"
                + (fuse != null ? $" and {fuse.PrefabName}" : "")
                + (_kit != null ? $"; built from {KitName}" : "; no kit"));
        }

        // The box BuildPrefabs made is a guess; fit it to the mesh as placed.
        private static void FitCollider(CableLabel label, Transform visual)
        {
            var box = label.GetComponent<BoxCollider>();
            var mesh = visual.GetComponent<MeshFilter>().sharedMesh;
            if (box == null || mesh == null)
                return;
            var b = mesh.bounds;
            var root = label.transform;
            var fitted = new Bounds(root.InverseTransformPoint(visual.TransformPoint(b.center)), Vector3.zero);
            for (var i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                fitted.Encapsulate(root.InverseTransformPoint(visual.TransformPoint(corner)));
            }
            box.center = fitted.center;
            box.size = fitted.size;
        }
    }
}
