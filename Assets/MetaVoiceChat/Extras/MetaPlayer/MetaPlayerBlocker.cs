using System.Collections.Generic;
using UnityEngine;

namespace Metater
{
    public class MetaPlayerBlocker
    {
        public HashSet<MonoBehaviour> InputBlockers { get; private set; } = new();
        public HashSet<MonoBehaviour> SprintBlockers { get; private set; } = new();
        public HashSet<MonoBehaviour> JumpBlockers { get; private set; } = new();
        public HashSet<MonoBehaviour> SimulationBlockers { get; private set; } = new();
        public HashSet<MonoBehaviour> MoveBlockers { get; private set; } = new();

        public bool CanTakeInput => InputBlockers.Count == 0;
        public bool CanSprint => SprintBlockers.Count == 0;
        public bool CanJump => JumpBlockers.Count == 0;
        public bool CanSimulate => SimulationBlockers.Count == 0;
        public bool CanMove => MoveBlockers.Count == 0;

        public void ClearAllBlockers()
        {
            InputBlockers.Clear();
            SprintBlockers.Clear();
            JumpBlockers.Clear();
            SimulationBlockers.Clear();
            MoveBlockers.Clear();
        }
    }
}