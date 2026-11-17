// Game flavour only: the shipped DLL, built by dotnet. LaunchPad calls OnLoaded; Unity's editor
// compile doesn't need it.
#if CL_GAME
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using BepInEx.Logging;
using LaunchPadBooster;
using LaunchPadBooster.Utils;
using UnityEngine;

namespace CableLabels
{
    // Registers the asset bundle's cable label. LaunchPad finds any MonoBehaviour with an
    // OnLoaded(List<GameObject>) method and calls it with the bundles' prefabs.
    //
    // The Unity project has none of the game's assets, so its prefab is a placeholder
    // (Editor/BuildPrefabs.cs). Here, once the game's prefabs exist, it takes the vanilla pipe
    // label's mesh, materials and text settings, shrunk across its axis to fit a cable, and its
    // placement settings from a vanilla cable fuse, the closest cable-mounted device.
    public class CableLabelPrefabs : MonoBehaviour
    {
        public static readonly Mod MOD = new Mod(ThisModInfo.AssemblyName, ThisModInfo.Version);

        internal static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("Cable Labels");

        private const string VanillaLabel = "StructurePipeLabel";

        // The pipe label's kit also places the cable label: one more option to cycle to.
        private const string Kit = "ItemPipeLabel";

        // How much thinner than the pipe label the sleeve is, across the cable. The length along
        // it is unchanged. A first guess, to tune in game against a normal cable.
        private const float Thinness = 0.5f;

        public void OnLoaded(List<GameObject> prefabs)
        {
            try
            {
                MOD.AddPrefabs(prefabs);
                MOD.SetupPrefabs<CableLabel>()
                    .RunFunc(_ => RemoveEmptyOptions(Kit))
                    .AddToMultiConstructorKit(Kit)
                    .RunFunc(BorrowFromVanilla);
                Log.LogInfo($"Cable Labels {ThisModInfo.Version}: registered {prefabs.Count} prefab(s)");
            }
            catch (Exception ex)
            {
                Log.LogError($"Registering the cable label failed: {ex}");
            }
        }

        // The game copies every prefab at load, and a kit's copy drops its empty options
        // (MultiConstructor.OnPrefabLoad), so an option appended after an empty one would have
        // one index in the original and another in the copy. Electrical DC hit this with the
        // Super Heavy coil; drop them from the original too before adding ours.
        private static void RemoveEmptyOptions(string kitName)
        {
            var kit = PrefabUtils.FindPrefab<MultiConstructor>(kitName);
            if (kit == null)
            {
                Log.LogError($"{kitName} isn't a multi-constructor kit; the cable label can't be built");
                return;
            }
            kit.Constructables.RemoveAll(s => s == null);
        }

        private static void BorrowFromVanilla(CableLabel label)
        {
            var vanilla = PrefabUtils.FindPrefab<Label>(VanillaLabel);
            var fuse = Prefab.AllPrefabs.OfType<CableFuse>().FirstOrDefault();
            if (vanilla == null || vanilla.MeshRenderer == null || fuse == null)
            {
                Log.LogError($"No vanilla {VanillaLabel} or cable fuse to borrow from; the cable label stays a placeholder");
                return;
            }

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

            // Comes apart with what the pipe label uses; AddToMultiConstructorKit made the kit
            // the entry tool.
            var exit = vanilla.BuildStates[0].Tool;
            label.BuildStates[0].Tool.ToolExit = exit.ToolExit;
            label.BuildStates[0].Tool.ExitTime = exit.ExitTime;

            // Where it sits and what it collides with, as any device mounted on a cable.
            label.GridSize = fuse.GridSize;
            label.GridOffset = fuse.GridOffset;
            label.SmallCollisionType = fuse.SmallCollisionType;
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
#endif
