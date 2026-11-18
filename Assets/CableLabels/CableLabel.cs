using System.Collections;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Util;
using HarmonyLib;
using UnityEngine;

namespace CableLabels
{
    // The vanilla pipe label (Assets.Scripts.Objects.Label) on a cable mount. Label derives from
    // DevicePipeMounted, so it can't be reused: its text drawing is copied here, and placement
    // comes from DeviceCableMounted, which the cable fuse and cable analyser already use (a
    // straight, unbroken cable in the same orientation).
    public class CableLabel : DeviceCableMounted
    {
        public const string DefaultString = "Cable Label";

        // Every label in the world renders through the one TextCamera, so they take turns on
        // Label's own private HasDrawn flag, not a copy: with separate flags a pipe label and a
        // cable label could draw in the same frame and swap their text.
        //
        // Looked up on first use, never in a static initializer: AccessTools' own type
        // initializer throws in the Unity editor (its Mono.Cecil isn't Harmony's), and a type
        // whose initializer throws gets built by AddComponent without any field initializers,
        // so the prefab saved every default as 0.
        private static AccessTools.FieldRef<bool> _hasDrawn;

        private static ref bool HasDrawn => ref (_hasDrawn ??=
            AccessTools.StaticFieldRefAccess<bool>(AccessTools.Field(typeof(Label), "HasDrawn")))();

        private static readonly WaitForEndOfFrame WaitFrame = new WaitForEndOfFrame();

        [Header("CableLabel")]
        public MeshRenderer MeshRenderer;
        public int materialLabelIndex;
        public int textTextureWidth = 512;
        public int textTextureHeight = 128;

        // The kit's art, carried here because LaunchPad hands OnLoaded only the bundle's
        // GameObjects, and the kit itself is a copy made at load (CableLabelPrefabs.MakeKit).
        [Header("Kit art")]
        public Sprite KitThumbnail;
        public Texture2D KitTexture;

        private Material _material;
        private RenderTexture _textTexture;

        public override void Awake()
        {
            base.Awake();
            _textTexture = new RenderTexture(textTextureWidth, textTextureHeight, 0);
            if (MeshRenderer == null)
                return;
            _material = MeshRenderer.materials[materialLabelIndex];
            _material.mainTexture = _textTexture;
            ChangeText(DefaultString);
        }

        // How far the panels stand off the cable's surface when they have to grow to clear it.
        private const float Clearance = 0.01f;

        // DeviceCableMounted calls this whenever the cell changes: placed, loaded, registered,
        // or the cable under it replaced.
        public override void CheckForCable()
        {
            base.CheckForCable();
            FitToCable();
        }

        // The label's own mesh, kept to go back to if a custom one is replaced (the cable under
        // it changes).
        private Mesh _standardMesh;

        // The cable prefab this label is fitted on, for CableLabelMeshes to refit it when that
        // prefab's mesh goes stale. 0 when on none.
        private int _fittedPrefab;

        // Fits again: CableLabelMeshes calls this when this label's cable prefab changed its
        // mount's answer.
        internal void Refit() => FitToCable();

        public override void OnDestroy()
        {
            CableLabelMeshes.Track(this, _fittedPrefab, 0);
            _fittedPrefab = 0;
            base.OnDestroy();
        }

        // Fits the label to the cable under it, in order:
        //
        // 1. The cable's ICableLabelMount (CableLabelMounts), if it has one: its own mesh and
        //    pose, for a cable that isn't round (one per cable prefab, CableLabelMeshes), given in
        //    the cable's frame in case the cable isn't rotationally symmetric of order 4 (a label
        //    matches the cable's run axis, IsSameOrientation, but not its roll), or else
        // 2. its CableLabelRadius, if it gives one, or else
        // 3. the cable measured: the pipe label is scaled by its pipe's LabelSizeOffset
        //    (Label.OnGridPlaced), and cables have no such field, so this reads the cable's
        //    meshes' bounds in this label's space. The panels sit at 0.035 m, clear of normal
        //    and heavy cable (0.025 m), but super-heavy is 0.038 m: then they widen to stand
        //    Clearance off it.
        //
        // The label only ever widens across the cable, never along it, so it stays within its
        // cell. Then the hover outline and the colliders follow the mesh as fitted (FitSelection).
        // A burnt cable is a CableRuptured, not a Cable, so the cell has no cable and the label
        // keeps the fit it had (Cable.Break leaves mounted devices in place).
        private void FitToCable()
        {
            // Grid events come on the main thread in practice, but nothing promises it, and
            // meshes and transforms may only be touched there.
            if (ThreadedManager.IsThread)
            {
                if (UnityMainThreadDispatcher.Exists())
                    UnityMainThreadDispatcher.Instance().Enqueue(FitToCable);
                return;
            }
            if (IsCursor || MeshRenderer == null)
                return;
            var visual = MeshRenderer.transform;
            var filter = visual.GetComponent<MeshFilter>();
            var cable = SmallCell?.Cable;
            if (filter == null || cable == null)
                return;
            if (_standardMesh == null)
                _standardMesh = filter.sharedMesh;
            if (_standardMesh == null)
                return;

            var mesh = _standardMesh;
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            var scale = 1f;
            CableLabelMeshes.Track(this, _fittedPrefab, cable.PrefabHash);
            _fittedPrefab = cable.PrefabHash;
            CableLabelMounts.TryGet(cable, out var mount);
            var custom = mount != null ? CableLabelMeshes.Get(cable.PrefabHash, mount) : null;
            if (custom != null && custom.HasMesh)
            {
                // From the cable's frame into this label's. Along the run, the offset is
                // dropped: the label stays in its own cell of a long cable.
                var toLabel = Quaternion.Inverse(transform.rotation) * cable.transform.rotation;
                var offset = toLabel * custom.Offset;
                mesh = custom.Mesh;
                position = new Vector3(offset.x, offset.y, 0f);
                rotation = toLabel * custom.Rotation;
            }
            else
            {
                var standardRadius = _standardMesh.bounds.extents.x;
                if (mount?.CableLabelRadius is float radius && radius > 0f)
                    scale = radius / standardRadius;
                else
                    scale = Mathf.Max(1f, (MeasureCableRadius(cable) + Clearance) / standardRadius);
            }
            filter.sharedMesh = mesh;
            visual.localPosition = position;
            visual.localRotation = rotation;
            visual.localScale = new Vector3(scale, scale, 1f);
            FitSelection(visual, mesh);
        }

        // The fitted mesh's bounds in this label's frame: the hover outline (GetSelection).
        // Null until the label has been fitted, when the prefab's Bounds serve.
        private Bounds? _selectionBounds;

        // The colliders' prefab lengths and centres, kept to go back to.
        private CapsuleCollider[] _capsules;
        private float[] _capsuleHeights;
        private Vector3[] _capsuleCentres;

        // The outline is Thing.Bounds unless GetSelection says otherwise. Bounds itself is left
        // alone, since the grid reads it to register the label's cells
        // (Structure.GetLocalGridBounds); only what's drawn on hover follows the mesh.
        public override SelectionInstance GetSelection()
        {
            var selection = base.GetSelection();
            if (_selectionBounds is Bounds bounds)
                selection.Bounds = bounds;
            return selection;
        }

        // The outline and the colliders follow the mesh as fitted. The two trigger capsules keep
        // their radius and their distance either side of the mesh, reaching 0.2 m across the
        // cable, past any cable's own colliders (vanilla's r 0.1, SBR fencing wire's 0.1 m box),
        // so the label is hit before the cable it sits on. Along the run they shrink to the
        // mesh's extent, so a custom mesh in part of the cell leaves the rest (a fencing spacer)
        // to the cable.
        //
        // The standard label's panels face its ±x, so its capsules sit there. A custom mesh can
        // face any way in this label's frame (it's posed in the cable's, and the label's roll is
        // the player's), so its capsules go either side of its thinnest axis across the run,
        // the way its text faces: above and below SBR's fencing plate, not over its wires. The
        // axis is found in the mesh's own space and turned into this label's, so it holds at
        // any roll.
        private void FitSelection(Transform visual, Mesh mesh)
        {
            var b = mesh.bounds;
            var fitted = new Bounds(transform.InverseTransformPoint(visual.TransformPoint(b.center)),
                Vector3.zero);
            for (var i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3(
                    (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                fitted.Encapsulate(transform.InverseTransformPoint(visual.TransformPoint(corner)));
            }
            _selectionBounds = fitted;

            if (_capsules == null)
            {
                _capsules = GetComponents<CapsuleCollider>();
                _capsuleHeights = _capsules.Select(c => c.height).ToArray();
                _capsuleCentres = _capsules.Select(c => c.center).ToArray();
            }
            var standard = mesh == _standardMesh;
            var facing = standard ? Vector3.right : FacingAcrossRun(visual, mesh);
            for (var i = 0; i < _capsules.Length; i++)
            {
                var capsule = _capsules[i];
                var centre = _capsuleCentres[i];
                if (standard)
                {
                    capsule.center = centre;
                    capsule.height = _capsuleHeights[i];
                    continue;
                }
                var across = facing * centre.x;
                capsule.center = new Vector3(fitted.center.x + across.x, fitted.center.y + across.y,
                    fitted.center.z);
                capsule.height = Mathf.Max(fitted.size.z + 0.05f, 2f * capsule.radius);
            }
        }

        // The direction a custom mesh's text faces, in this label's frame: of the mesh's own
        // axes that lie across the run (z), the one along which the mesh is thinnest.
        private static Vector3 FacingAcrossRun(Transform visual, Mesh mesh)
        {
            var size = mesh.bounds.size;
            var facing = Vector3.right;
            var thinnest = float.MaxValue;
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            for (var i = 0; i < 3; i++)
            {
                var direction = visual.localRotation * axes[i];
                if (Mathf.Abs(direction.z) > 0.7f || size[i] >= thinnest)
                    continue;
                thinnest = size[i];
                facing = direction;
            }
            facing.z = 0f;
            return facing.sqrMagnitude > 1e-6f ? facing.normalized : Vector3.right;
        }

        // How far the cable's meshes reach from its axis, in this label's space.
        private float MeasureCableRadius(Cable cable)
        {
            var radius = 0f;
            foreach (var filter in cable.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.GetComponent<Renderer>() == null)
                    continue;
                var b = filter.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3(
                        (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    radius = Mathf.Max(radius, Mathf.Abs(p.x), Mathf.Abs(p.y));
                }
            }
            return radius;
        }

        public override void DeserializeSave(ThingSaveData savedData)
        {
            base.DeserializeSave(savedData);
            ChangeText(DisplayName);
        }

        public override void OnFinishedThingSync()
        {
            base.OnFinishedThingSync();
            ChangeText(DisplayName);
        }

        public override void OnRenamed()
        {
            base.OnRenamed();
            ChangeText(DisplayName);
        }

        public void ChangeText(string labelText)
        {
            if (GameManager.IsBatchMode)
                return;
            StartCoroutine(Draw(labelText));
        }

        // As Label.Draw.
        private IEnumerator Draw(string labelText)
        {
            do
            {
                yield return WaitFrame;
            }
            while (HasDrawn);
            HasDrawn = true;
            var cursor = CursorManager.Instance;
            cursor.TextCamera.gameObject.SetActive(true);
            cursor.TextCamera.targetTexture = _textTexture;
            cursor.LabelTextField.text = labelText;
            yield return WaitFrame;
            cursor.TextCamera.Render();
            cursor.TextCamera.gameObject.SetActive(false);
            HasDrawn = false;
        }
    }
}
