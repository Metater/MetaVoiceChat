using UnityEngine;

namespace Metater
{
    [CreateAssetMenu(fileName = "Shortcuts", menuName = "Metater/Shortcuts")]
    public class Shortcuts : ScriptableObject
    {
        public Object[] shortcuts;
    }
}
