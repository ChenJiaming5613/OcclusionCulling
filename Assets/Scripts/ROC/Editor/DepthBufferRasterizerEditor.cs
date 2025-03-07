using UnityEditor;
using UnityEngine;

namespace ROC.Editor
{
    [CustomEditor(typeof(DepthBufferRasterizer))]
    public class DepthBufferRasterizerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var depthBufferValidator = (DepthBufferRasterizer)target;
            if (GUILayout.Button("Convert To Texture"))
            {
                depthBufferValidator.ConvertToTexture();
            }
        }
    }
}