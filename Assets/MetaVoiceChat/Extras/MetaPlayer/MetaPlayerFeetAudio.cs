using UnityEngine;

namespace Metater
{
    public class MetaPlayerFeetAudio : MonoBehaviour
    {
        [Header("Audio")]
        public MetaRangeFloat pitchRange = new(0.9f, 1.1f);

        [Header("Walk")]
        public AudioClipPlayer walkPlayer;
        public MetaRangeFloat walkVolumeRange = new(0.75f, 1f);

        [Header("Run")]
        public AudioClipPlayer runPlayer;
        public MetaRangeFloat runVolumeRange = new(0.8f, 1f);

        [Header("Jump")]
        public AudioClipPlayer jumpPlayer;
        public MetaRangeFloat jumpVolumeRange = new(0.75f, 1f);

        [Header("Land")]
        public AudioClipPlayer landPlayer;
        public float landSpeedCeiling = 30f;
        public MetaRangeFloat landVolumeRange = new(0.1f, 1f);

        public void WalkSfx()
        {
            walkPlayer.audioSource.pitch = pitchRange.Random;
            walkPlayer.PlayRandom(walkVolumeRange.Random);
        }

        public void RunSfx()
        {
            runPlayer.audioSource.pitch = pitchRange.Random;
            runPlayer.PlayCyclical(runVolumeRange.Random);
        }

        public void JumpSfx()
        {
            jumpPlayer.audioSource.pitch = pitchRange.Random;
            jumpPlayer.PlayRandom(jumpVolumeRange.Random);
        }

        public void LandSfx(float impactSpeed)
        {
            float t = Mathf.InverseLerp(0, landSpeedCeiling, impactSpeed);
            landPlayer.audioSource.pitch = pitchRange.Random;
            landPlayer.PlayRandom(landVolumeRange.Lerp(t));
        }
    }
}