using UnityEngine;

namespace Metater
{
    public class FpsController : MonoBehaviour
    {
        [Range(-1, 1000)]
        public int targetFrameRate = -1;

#if UNITY_EDITOR
        private int appliedFrameRate = -1_000_000;

        private void Update()
        {
            if (appliedFrameRate != targetFrameRate)
            {
                Application.targetFrameRate = targetFrameRate;
                appliedFrameRate = targetFrameRate;
            }
        }
#endif
    }
}