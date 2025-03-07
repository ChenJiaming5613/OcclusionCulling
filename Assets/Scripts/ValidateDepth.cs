using UnityEngine;
using UnityEngine.Assertions;

public class ValidateDepth : MonoBehaviour
{
    [SerializeField] private Texture2D msocDepth;
    
    [SerializeField] private Texture2D gtDepth;

    public void Validate()
    {
        Assert.IsTrue(msocDepth != null && gtDepth != null);
        Assert.IsTrue(msocDepth.width == gtDepth.width && msocDepth.height == gtDepth.height);
        for (var y = 0; y < gtDepth.height; y++)
        {
            for (var x = 0; x < gtDepth.width; x++)
            {
                var gtZ = gtDepth.GetPixel(x, y);
                var msocZ = msocDepth.GetPixel(x, y);
                if (msocZ.r < gtZ.r)
                {
                    Debug.Log($"{x},{y} => msoc: {msocZ.r}, gt: {gtZ.r}");
                }
            }
        }
    }
}