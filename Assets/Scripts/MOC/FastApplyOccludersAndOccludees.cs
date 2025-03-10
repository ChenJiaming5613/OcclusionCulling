using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

namespace MOC
{
    [RequireComponent(typeof(MaskedOcclusionCulling), typeof(Camera))]
    public class FastApplyOccludersAndOccludees : MonoBehaviour
    {
        [SerializeField] private GameObject targetGameObject;
        private MaskedOcclusionCulling _msoc;
        private Camera _camera;
        
        private void OnEnable()
        {
            _msoc = GetComponent<MaskedOcclusionCulling>();
            _camera = GetComponent<Camera>();
            Assert.IsTrue(targetGameObject != null);
            var occluders = targetGameObject.transform.Find("Occluders");
            if (occluders != null)
            {
                var occludersMeshFilters = occluders.GetComponentsInChildren<MeshFilter>()
                    .Where(it => it.gameObject.activeSelf).ToArray();
                _msoc.SetOccluders(occludersMeshFilters);
                Debug.Log($"Num Occluders: {occludersMeshFilters.Length}");
            }
            var occludees = targetGameObject.transform.Find("Occludees");
            if (occludees != null)
            {
                var occludeesMeshFilters = occludees.GetComponentsInChildren<MeshFilter>()
                    .Where(it => it.gameObject.activeSelf).ToArray();
                _msoc.SetOccludees(occludeesMeshFilters);
                Debug.Log($"Num Occludees: {occludeesMeshFilters.Length}");
            }
            var eye = targetGameObject.transform.Find("Eye");
            if (eye != null)
            {
                _camera.transform.position = eye.position;
                _camera.transform.rotation = eye.rotation;
                Debug.Log("Applied Camera Posture!");
            }
        }
    }
}