using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Metater
{
#if MIRROR
    public class MetaPlayerPushable : NetworkBehaviour
#else
    public class MetaPlayerPushable : MonoBehaviour
#endif  
    {
        public EphemeralRigidbody ephemeralRigidbody;

        public Rigidbody Rigidbody => ephemeralRigidbody.Rigidbody;

#if MIRROR
        public override void OnStartServer()
        {
            ephemeralRigidbody.IsEnabled = true;
        }

        public override void OnStopServer()
        {
            ephemeralRigidbody.IsEnabled = false;
        }
#else
        public void OnEnable()
        {
            ephemeralRigidbody.IsEnabled = true;
        }

        public void OnDisable()
        {
            ephemeralRigidbody.IsEnabled = false;
        }
#endif
    }
}