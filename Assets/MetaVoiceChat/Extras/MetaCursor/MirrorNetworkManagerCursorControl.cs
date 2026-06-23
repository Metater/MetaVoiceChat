using UnityEngine;
using UnityEngine.InputSystem;

#if MIRROR
using Mirror;

[RequireComponent(typeof(NetworkManager))]
#endif

namespace Metater
{
    public class MirrorNetworkManagerCursorControl : MonoBehaviour
    {
        public bool escapeMenuOpen = false;

#if MIRROR
    private NetworkManager netManager;

    private void Awake()
    {
        netManager = GetComponent<NetworkManager>();
    }
#endif

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                escapeMenuOpen = !escapeMenuOpen;
            }

#if MIRROR
        if (netManager != null)
#endif
            {
#if MIRROR
            bool notConnected = !NetworkServer.active && !NetworkClient.active;
#else
                bool notConnected = false;
#endif
                if (notConnected)
                {
                    escapeMenuOpen = false;
                }

                if (notConnected || escapeMenuOpen)
                {
                    if (MetaCursor.Instance != null)
                    {
                        MetaCursor.Instance.CursorUsers.Add(this);
                    }

                    if (MetaPlayerController.Instance != null)
                    {
                        MetaPlayerController.Instance.blocker.InputBlockers.Add(this);
                    }
                }
                else
                {
                    if (MetaCursor.Instance != null)
                    {
                        MetaCursor.Instance.CursorUsers.Remove(this);
                    }

                    if (MetaPlayerController.Instance != null)
                    {
                        MetaPlayerController.Instance.blocker.InputBlockers.Remove(this);
                    }
                }
            }
        }
    }
}