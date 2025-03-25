using UnityEngine;

public class UnlockFrameLimit : MonoBehaviour
{
    [SerializeField] private int targetFrameRate = 360;

    private void OnEnable()
    {
        Application.targetFrameRate = targetFrameRate;
    }
}