//using Assets.Metater.MetaUtils;
//using UnityEngine;
//using UnityEngine.Audio;

//// Future inspiration for how to do detection: https://github.com/alphacep/vosk-unity-asr/blob/master/Assets/Scripts/VoiceProcessor.cs

////[Header("Detection")]
////[SerializeField] private float detectionValue = 0.002f;
////[SerializeField] private float detectionPercentage = 0.05f;
////[SerializeField] private float detectionLatchSeconds = 0.1f;

////public float DetectionValue => detectionValue;
////public float DetectionPercentage => detectionPercentage;
////public float DetectionLatchSeconds => detectionLatchSeconds;

//namespace Assets.Metater.MetaVoiceChat.Input.Mic
//{
//    public class VcMicInputFilter : VcInputFilter
//    {
//        private readonly static string InputVolumeKey = $"{nameof(MetaVoiceChat)}.{nameof(inputVolume)}";
//        private readonly static string OutputVolumeKey = $"{nameof(MetaVoiceChat)}.{nameof(outputVolume)}";
//        private readonly static string InputSensitivityKey = $"{nameof(MetaVoiceChat)}.{nameof(inputSensitivity)}";

//        [Header("Audio Mixer")]
//        public AudioMixer audioMixer;
//        public string outputVolumeParameter;

//        [Header("Values")]
//        public MetaValue<float> inputVolume;
//        public MetaValue<float> outputVolume;
//        public MetaValue<float> currentInputVolume;
//        public MetaValue<float> inputSensitivity;

//        //private double lastDetectionSeconds = double.MinValue;

//        private void Start()
//        {
//            inputVolume.Value = PlayerPrefs.GetFloat(InputVolumeKey, 1.0f);
//            outputVolume.Value = PlayerPrefs.GetFloat(OutputVolumeKey, 1.0f);
//            currentInputVolume.Value = 0.0f;
//            inputSensitivity.Value = PlayerPrefs.GetFloat(InputSensitivityKey, 0.05f);

//            inputVolume.AddListener(this, SaveInputVolume);
//            outputVolume.AddListener(this, SetOutputVolume);
//            inputSensitivity.AddListener(this, SaveInputSensitivity);
//        }

//        //private void Mic_OnFrameReady(int frameIndex, float[] frame)
//        //{
//        //    //bool isVoiceDetected = DetectVoice(segment);
//        //    //if (isVoiceDetected)
//        //    //{
//        //    //    lastDetectionSeconds = Meta.Realtime;
//        //    //}

//        //    //bool shouldSpeak = Meta.Realtime - config.DetectionLatchSeconds < lastDetectionSeconds;
//        //    //if (shouldSpeak)
//        //    //{
//        //    OnFrameReady?.Invoke(frameIndex, frame);
//        //    //}
//        //    //else
//        //    //{
//        //    //    OnSegmentReady?.Invoke(segmentIndex, null);
//        //    //}
//        //}

//        //private bool DetectVoice(float[] segment)
//        //{
//        //    float percentage = GetPercentageAboveThreshold(segment);
//        //    return percentage > config.DetectionPercentage;
//        //}

//        //private float GetPercentageAboveThreshold(float[] segment)
//        //{
//        //    float percentageIncrement = 1f / segment.Length;
//        //    float percentage = 0;
//        //    float detectionValue = config.DetectionValue;
//        //    foreach (float value in segment)
//        //    {
//        //        if (Math.Abs(value) > detectionValue)
//        //        {
//        //            percentage += percentageIncrement;
//        //        }
//        //    }

//        //    return percentage;
//        //}

//        //public bool Process(float[] samples)
//        //{
//        //    currentInputVolume.Value = samples.Max(v => Mathf.Abs(v));

//        //    return true;
//        //}

//        private void SetOutputVolume(float volume)
//        {
//            SaveOutputVolume(volume);

//            float targetValue = Mathf.Clamp(volume, 0.0001f, 2.0f);
//            audioMixer.SetFloat(outputVolumeParameter, Mathf.Log10(targetValue) * 20.0f);
//        }

//        private void SaveInputVolume(float value) => SetAndSaveFloat(InputVolumeKey, value);
//        private void SaveOutputVolume(float value) => SetAndSaveFloat(OutputVolumeKey, value);
//        private void SaveInputSensitivity(float value) => SetAndSaveFloat(InputSensitivityKey, value);

//        private static void SetAndSaveFloat(string key, float value)
//        {
//            PlayerPrefs.SetFloat(key, value);
//            PlayerPrefs.Save();
//        }

//        protected override void Filter(int index, ref float[] samples)
//        {

//        }
//    }
//}