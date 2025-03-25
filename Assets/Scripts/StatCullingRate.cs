using System.Linq;
using UnityEngine;

[RequireComponent(typeof(CullingSystem))]
public class StatCullingRate : MonoBehaviour
{
    private CullingSystem _cullingSystem;

    private void Start()
    {
        _cullingSystem = GetComponent<CullingSystem>();
    }

    public void Stat()
    {
        var cullingResults = _cullingSystem.GetCullingResults();
        var numCulled = cullingResults.Sum(it => it ? 1 : 0);
        var numTotal = cullingResults.Length;
        Debug.Log($"Culling Rate: {numCulled} / {numTotal}");
    }
}