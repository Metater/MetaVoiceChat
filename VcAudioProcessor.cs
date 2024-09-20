using System.Linq;
using Assets.Metater.MetaRefs;
using Assets.Metater.MetaUtils;
using UnityEngine;
using UnityEngine.Audio;

namespace Assets.Metater.MetaVoiceChat
{
    public class VcAudioProcessor : MetaNbl<VcAudioProcessor>
    {
        private readonly static string InputVolumeKey = $"{nameof(MetaVoiceChat)}.{nameof(inputVolume)}";
        private readonly static string OutputVolumeKey = $"{nameof(MetaVoiceChat)}.{nameof(outputVolume)}";
        private readonly static string InputSensitivityKey = $"{nameof(MetaVoiceChat)}.{nameof(inputSensitivity)}";

        [Header("Audio Mixer")]
        public AudioMixer audioMixer;
        public string outputVolumeParameter;

        [Header("Values")]
        public MetaValue<float> inputVolume;
        public MetaValue<float> outputVolume;
        public MetaValue<float> currentInputVolume;
        public MetaValue<float> inputSensitivity;

        public override void OnStartLocalPlayer()
        {
            inputVolume.Value = PlayerPrefs.GetFloat(InputVolumeKey, 1.0f);
            outputVolume.Value = PlayerPrefs.GetFloat(OutputVolumeKey, 1.0f);
            currentInputVolume.Value = 0.0f;
            inputSensitivity.Value = PlayerPrefs.GetFloat(InputSensitivityKey, 0.05f);

            inputVolume.AddListener(this, SaveInputVolume);
            outputVolume.AddListener(this, SetOutputVolume);
            inputSensitivity.AddListener(this, SaveInputSensitivity);
        }

        public bool Process(float[] segment)
        {
            currentInputVolume.Value = segment.Max(v => Mathf.Abs(v));

            return true;
        }

        private void SetOutputVolume(float volume)
        {
            SaveOutputVolume(volume);

            float targetValue = Mathf.Clamp(volume, 0.0001f, 2.0f);
            audioMixer.SetFloat(outputVolumeParameter, Mathf.Log10(targetValue) * 20.0f);
        }

        private void SaveInputVolume(float value) => SetAndSaveFloat(InputVolumeKey, value);
        private void SaveOutputVolume(float value) => SetAndSaveFloat(OutputVolumeKey, value);
        private void SaveInputSensitivity(float value) => SetAndSaveFloat(InputSensitivityKey, value);

        private static void SetAndSaveFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
        }
    }
}