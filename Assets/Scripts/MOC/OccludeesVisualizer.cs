using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MOC
{
    [RequireComponent(typeof(MaskedOcclusionCulling))]
    public class OccludeesVisualizer : MonoBehaviour
    {
        private MaskedOcclusionCulling _occlusionCulling;
        private readonly Dictionary<Renderer, Color> _rendererToColor = new();

        private void Start()
        {
            _occlusionCulling = GetComponent<MaskedOcclusionCulling>();
        }

        private void Update()
        {
            var occludedMeshFilters = _occlusionCulling.GetOccludedMeshFilters();
            if (occludedMeshFilters == null) return;
            var meshFiltersSet = new HashSet<Renderer>();
            foreach (var meshFilter in occludedMeshFilters)
            {
                var currRenderer = meshFilter.GetComponent<Renderer>();
                if (!currRenderer) continue;
                if (!_rendererToColor.ContainsKey(currRenderer))
                {
                    _rendererToColor.Add(currRenderer, currRenderer.material.color);
                }
                currRenderer.material.color = Color.red;
                meshFiltersSet.Add(currRenderer);
            }
            foreach (var pair in _rendererToColor.Where(pair => !meshFiltersSet.Contains(pair.Key)))
            {
                pair.Key.material.color = pair.Value;
            }
        }
    }
}