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
            if (IsCursor || IsCursorCreating)
            {
                DropCursorVisual();
                return;
            }
            _textTexture = new RenderTexture(textTextureWidth, textTextureHeight, 0);
            if (MeshRenderer == null)
                return;
            _material = MeshRenderer.materials[materialLabelIndex];
            _material.mainTexture = _textTexture;
            ChangeText(DefaultString);
        }

        // The construction cursor is a copy of this prefab showing the blueprint instead. Its
        // code (InventoryManager) destroys the copy's Renderers before adding the blueprint, but
        // the copy is made inactive, so this Awake, which fills Renderers, hasn't run yet: the
        // label's own mesh survived and drew inside the blueprint, and its text texture redrew
        // through the shared TextCamera, so it came and went. A cursor drops its own mesh and
        // never draws text.
        private void DropCursorVisual()
        {
            if (MeshRenderer == null)
                return;
            Renderers.RemoveAll(r => r.IsCurrentRenderer(MeshRenderer));
            Destroy(MeshRenderer.gameObject);
            MeshRenderer = null;
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
            // No texture on a cursor (DropCursorVisual) or in batch mode: nothing to draw into.
            if (GameManager.IsBatchMode || _textTexture == null)
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
