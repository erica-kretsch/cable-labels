using System.Collections;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Localization2;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Util;
using CableLabels.Contracts;
using HarmonyLib;
using UnityEngine;

namespace CableLabels
{
    /// <summary>
    /// A label mounted on a straight cable, showing its name on both sides; the cable counterpart
    /// of the vanilla pipe label.
    /// </summary>
    /// <remarks>
    /// Placement comes from <see cref="DeviceCableMounted"/>, as for the cable fuse. The text
    /// drawing is copied from <see cref="Label"/>, which can't be reused because it derives from
    /// <c>DevicePipeMounted</c>.
    /// </remarks>
    public class CableLabel : DeviceCableMounted
    {
        /// <summary>The text a label shows until it's named.</summary>
        public const string DefaultString = "Cable Label";

        private static AccessTools.FieldRef<bool> _hasDrawn;

        /// <summary>
        /// <see cref="Label"/>'s private flag for the one shared text camera, which pipe and cable
        /// labels take turns on.
        /// </summary>
        /// <remarks>
        /// Looked up on first use: AccessTools' type initializer throws in the Unity editor, and
        /// calling it from a static initializer here made <c>AddComponent</c> skip every field
        /// initializer, so the prefab was saved with all defaults 0.
        /// </remarks>
        private static ref bool HasDrawn => ref (_hasDrawn ??=
            AccessTools.StaticFieldRefAccess<bool>(AccessTools.Field(typeof(Label), "HasDrawn")))();

        private static readonly WaitForEndOfFrame WaitFrame = new WaitForEndOfFrame();

        [Header("CableLabel")]
        public MeshRenderer MeshRenderer;
        public int materialLabelIndex;
        public int textTextureWidth = 512;
        public int textTextureHeight = 128;

        // Art for the kit, which is made at load (CableLabelPrefabs) and so has no prefab of its
        // own to carry it.
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

        /// <summary>Gap between a widened label and the cable's surface, in metres.</summary>
        private const float Clearance = 0.01f;

        /// <inheritdoc />
        /// <remarks>
        /// Also refuses a cell that already holds a device, as the pipe label does. A cell has one
        /// device slot, and <see cref="DeviceCableMounted"/> doesn't check it.
        /// </remarks>
        public override CanConstructInfo CanConstruct()
        {
            var info = base.CanConstruct();
            if (!info.CanConstruct)
                return info;
            var occupant = GridController.GetSmallCell(ThingTransformPosition)?.Device;
            return occupant != null && occupant != this
                ? CanConstructInfo.InvalidPlacement(
                    GameStrings.PlacementBlockedBySmallGrid.AsString(occupant.DisplayName))
                : info;
        }

        /// <inheritdoc />
        /// <remarks>Refits the label whenever its cell changes.</remarks>
        public override void CheckForCable()
        {
            base.CheckForCable();
            FitToCable();
        }

        /// <summary>The prefab's own mesh, restored when a custom mesh no longer applies.</summary>
        private Mesh _standardMesh;

        /// <summary>Prefab hash of the cable this label is fitted to; 0 when none.</summary>
        private int _fittedPrefab;

        /// <summary>Fits the label to its cable again.</summary>
        internal void Refit() => FitToCable();

        public override void OnDestroy()
        {
            CableLabelMeshes.Track(this, _fittedPrefab, 0);
            _fittedPrefab = 0;
            base.OnDestroy();
        }

        /// <summary>
        /// Fits the label to the cable in its cell: the cable's custom mesh if it gives one, else
        /// its <see cref="ICableLabelMount.CableLabelRadius"/>, else the cable measured.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A custom mesh is posed in the cable's frame, since a label follows the cable's run axis
        /// but not its roll. The standard mesh only ever widens across the cable, to stand
        /// <see cref="Clearance"/> off it, so it stays within its cell: the panels sit at
        /// 0.035 m, clear of normal and heavy cable (0.025 m) but not super-heavy (0.038 m).
        /// </para>
        /// <para>
        /// A burnt cable is a <c>CableRuptured</c>, not a <see cref="Cable"/>, so the label keeps
        /// its fit until the cable is rebuilt.
        /// </para>
        /// </remarks>
        private void FitToCable()
        {
            // Meshes and transforms may only be touched on the main thread.
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
                // From the cable's frame into the label's; the label stays in its own cell, so
                // any offset along the run is dropped.
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

        /// <summary>The fitted mesh's bounds in the label's frame; null until fitted.</summary>
        private Bounds? _selectionBounds;

        // The trigger capsules and their prefab placement, restored for the standard mesh.
        private CapsuleCollider[] _capsules;
        private float[] _capsuleHeights;
        private Vector3[] _capsuleCentres;

        /// <inheritdoc />
        /// <remarks>
        /// The hover outline follows the fitted mesh. <see cref="Thing.Bounds"/> is left as the
        /// prefab's, since the grid registers the label's cells from it.
        /// </remarks>
        public override SelectionInstance GetSelection()
        {
            var selection = base.GetSelection();
            if (_selectionBounds is Bounds bounds)
                selection.Bounds = bounds;
            return selection;
        }

        /// <summary>
        /// Fits the hover outline and the two trigger capsules to the mesh as placed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The capsules keep their radius and their distance either side of the mesh, reaching
        /// 0.2 m across the cable, past a vanilla cable's own colliders (r 0.1), so the label is
        /// hit before the cable. Along the run they cover only the mesh's length, leaving the
        /// rest of the cell to the cable.
        /// </para>
        /// <para>
        /// The standard mesh faces ±x. A custom mesh's capsules go either side of
        /// <see cref="FacingAcrossRun"/>: on SBR Electrical's fencing wire, above and below the
        /// plate between its wires rather than over the wires.
        /// </para>
        /// </remarks>
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

        /// <summary>
        /// The direction a custom mesh faces, in the label's frame: the mesh's thinnest own axis
        /// across the run.
        /// </summary>
        /// <remarks>Taken from the mesh's own axes, so it holds at any roll.</remarks>
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

        /// <summary>How far the cable's meshes reach from its axis, in the label's frame.</summary>
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

        /// <summary>Redraws the label's text.</summary>
        public void ChangeText(string labelText)
        {
            if (GameManager.IsBatchMode)
                return;
            StartCoroutine(Draw(labelText));
        }

        /// <summary>Renders the text into the label's texture, as <c>Label.Draw</c> does.</summary>
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
