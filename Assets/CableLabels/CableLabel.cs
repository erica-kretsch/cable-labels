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
        private static readonly AccessTools.FieldRef<bool> HasDrawn =
            AccessTools.StaticFieldRefAccess<bool>(AccessTools.Field(typeof(Label), "HasDrawn"));

        private static readonly WaitForEndOfFrame WaitFrame = new WaitForEndOfFrame();

        [Header("CableLabel")]
        public MeshRenderer MeshRenderer;
        public int materialLabelIndex;
        public int textTextureWidth = 512;
        public int textTextureHeight = 128;

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
            while (HasDrawn());
            HasDrawn() = true;
            var cursor = CursorManager.Instance;
            cursor.TextCamera.gameObject.SetActive(true);
            cursor.TextCamera.targetTexture = _textTexture;
            cursor.LabelTextField.text = labelText;
            yield return WaitFrame;
            cursor.TextCamera.Render();
            cursor.TextCamera.gameObject.SetActive(false);
            HasDrawn() = false;
        }
    }
}
