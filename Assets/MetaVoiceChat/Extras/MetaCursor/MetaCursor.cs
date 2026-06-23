using System.Collections.Generic;
using UnityEngine;

namespace Metater
{
    public class MetaCursor : MonoBehaviour
    {
        private static MetaCursor instance;

        public static MetaCursor Instance => instance;

        public HashSet<MonoBehaviour> CursorUsers { get; private set; } = new();

        public bool IsCursorInUse => CursorUsers.Count > 0;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            bool isCursorInUse = IsCursorInUse;
            Cursor.visible = isCursorInUse;
            Cursor.lockState = isCursorInUse ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }
}