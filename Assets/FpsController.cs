using UnityEngine;

public class FpsController : MonoBehaviour
{
    [Range(-1, 1000)]
    public int targetFrameRate = -1;

    private void Update()
    {
        Application.targetFrameRate = targetFrameRate;
    }
}
