using System;
using UnityEditor;
using UnityEngine;

namespace Editor
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
                try
                {
                    visualizer.Visualize();
                }
                catch (Exception)
                {
                    Debug.LogWarning("Pause the game first when async rasterize is on!");
                }
            }
        }
    }
#endif
}