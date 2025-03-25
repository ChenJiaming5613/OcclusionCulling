using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(StatCullingRate))]
    public class StatCullingRateEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var statCullingRate = (StatCullingRate)target;
            if (Application.isPlaying && GUILayout.Button("Stat"))
            {
                statCullingRate.Stat();
            }
        }
    }
}