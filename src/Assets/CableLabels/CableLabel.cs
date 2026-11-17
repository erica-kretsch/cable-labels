using Assets.Scripts.Objects.Electrical;
using UnityEngine;
#if CL_GAME
using System.Collections;
using Assets.Scripts;
using Assets.Scripts.Objects;
#endif

namespace CableLabels
{
    // The vanilla pipe label (Assets.Scripts.Objects.Label) on a cable mount. Label derives from
    // DevicePipeMounted, so it can't be reused: its text drawing is copied here, and placement
    // comes from DeviceCableMounted, which the cable fuse and cable analyser already use (a
    // straight, unbroken cable in the same orientation).
    //
    // The fields are serialized, so they stay outside #if: Unity's editor compile must see the
    // same class the shipped DLL does. Only the Game build (CL_GAME) has the behaviour.
    public class CableLabel : DeviceCableMounted
    {
        [Header("CableLabel")]
        public MeshRenderer MeshRenderer;
        public int materialLabelIndex;
        public int textTextureWidth = 512;
        public int textTextureHeight = 128;

#if CL_GAME
        private Material _material;
        private RenderTexture _textTexture;

        public const string DefaultString = "Cable Label";

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

        // As Label.Draw. Every label in the world renders through the one TextCamera, so they
        // take turns on Label's own HasDrawn flag (publicized), not a copy: with separate flags
        // a pipe label and a cable label could draw in the same frame and swap their text.
        private IEnumerator Draw(string labelText)
        {
            do
            {
                yield return Label.WaitFrame;
            }
            while (Label.HasDrawn);
            Label.HasDrawn = true;
            var cursor = CursorManager.Instance;
            cursor.TextCamera.gameObject.SetActive(true);
            cursor.TextCamera.targetTexture = _textTexture;
            cursor.LabelTextField.text = labelText;
            yield return Label.WaitFrame;
            cursor.TextCamera.Render();
            cursor.TextCamera.gameObject.SetActive(false);
            Label.HasDrawn = false;
        }
#endif
    }
}
