using Mirror;
using UnityEngine;

namespace Assets.Metater.MetaVoiceChat.VcImpls.Mirror
{
    [RequireComponent(typeof(MetaVc))]
    public class MirrorVcImpl : NetworkBehaviour
    {
        private MetaVc vc;

        private void Awake()
        {
            vc = GetComponent<MetaVc>();
        }


    }
}
