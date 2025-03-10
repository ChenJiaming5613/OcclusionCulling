using UnityEditor;
using UnityEngine;

namespace MOC.Editor
{
#if UNITY_EDITOR
    [CustomEditor(typeof(DepthBufferVisualizer))]
    public class DepthBufferVisualizerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var visualizer = (DepthBufferVisualizer)target;
            if (GUILayout.Button("Visualize"))
            {
                visualizer.Visualize();
            }
        }
    }
#endif
}