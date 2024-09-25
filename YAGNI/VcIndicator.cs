//using UnityEngine;
//using UnityEngine.UI;

//namespace Assets.Metater.MetaVoiceChat
//{
//    public class VcIndicator : MonoBehaviour
//    {
//        [Header("Sprites")]
//        public Sprite speakerSprite;
//        public Sprite speakingSprite;

//        [Header("Colors")]
//        public Color isSpeakingColor;

//        private Image image;

//        private void Awake()
//        {
//            image = GetComponent<Image>();
//        }
//    }
//}

//// public class SpeakingIndicator : BBMonoBehaviourSingleton
//// {
////     public Image speakerIconImage;
////     public Color isSpeakingColor;

////     private bool isInit = false;

////     private void Update()
////     {
////         if (!isInit)
////         {
////             var vc = Single<MetaVc>(true);

////             if (vc != null)
////             {
////                 isInit = true;
////                 vc.isSpeaking.Add(this, isSpeaking =>
////                 {
////                     if (isSpeaking)
////                     {
////                         speakerIconImage.color = isSpeakingColor;
////                     }
////                     else
////                     {
////                         speakerIconImage.color = new(0, 0, 0, 0);
////                     }
////                 });

////                 // IDEA Turn icon red for muted or deafened
////             }
////         }
////     }
//// }