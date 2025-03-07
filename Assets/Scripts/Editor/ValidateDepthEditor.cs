using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(ValidateDepth))]
    public class ValidateDepthEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var validateDepth = (ValidateDepth)target;
            if (GUILayout.Button("Validate"))
            {
                validateDepth.Validate();
            }
        }
    }
}