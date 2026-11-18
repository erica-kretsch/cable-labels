using System.Collections;
using Assets.Scripts;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
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

        // The pipe label is scaled by its pipe's LabelSizeOffset (Label.OnGridPlaced); cables
        // have no such field, so this measures the cable instead, from its meshes' bounds in
        // this label's space. The panels sit at 0.035 m, clear of normal and heavy cable
        // (0.025 m), but super-heavy is 0.038 m: then they widen across the cable, not along it,
        // so the label stays within its cell. Its colliders stay as they are, already wider than
        // any vanilla cable's (r 0.1). A burnt cable is a CableRuptured, not a Cable, so the
        // cell has no cable and the label keeps the size it had (Cable.Break leaves mounted
        // devices in place).
        private void FitToCable()
        {
            if (IsCursor || MeshRenderer == null)
                return;
            var visual = MeshRenderer.transform;
            var filterOwn = visual.GetComponent<MeshFilter>();
            var cable = SmallCell?.Cable;
            if (filterOwn == null || filterOwn.sharedMesh == null || cable == null)
                return;
            var mesh = filterOwn.sharedMesh;
            var cableRadius = 0f;
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
                    cableRadius = Mathf.Max(cableRadius, Mathf.Abs(p.x), Mathf.Abs(p.y));
                }
            }
            var ownRadius = mesh.bounds.extents.x;
            var scale = Mathf.Max(1f, (cableRadius + Clearance) / ownRadius);
            visual.localScale = new Vector3(scale, scale, 1f);
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
