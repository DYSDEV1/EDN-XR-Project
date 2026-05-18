using System.Collections.Generic;
using UnityEngine;

namespace EDNXR.Gameplay
{
    public static class PlayerMovementLock
    {
        private static readonly Dictionary<Behaviour, bool> savedStates = new Dictionary<Behaviour, bool>();
        private static int lockCount;

        public static void Lock(string reason)
        {
            lockCount++;

            if (lockCount > 1)
                return;

            SetMovementEnabled(false);
            Debug.Log($"[PlayerMovementLock] Locked player movement. reason={reason}");
        }

        public static void Unlock(string reason)
        {
            if (lockCount <= 0)
                return;

            lockCount--;

            if (lockCount > 0)
                return;

            foreach (var pair in savedStates)
            {
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            }

            savedStates.Clear();
            Debug.Log($"[PlayerMovementLock] Unlocked player movement. reason={reason}");
        }

        public static void ForceUnlockAll(string reason)
        {
            foreach (var pair in savedStates)
            {
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            }

            savedStates.Clear();
            lockCount = 0;
            Debug.Log($"[PlayerMovementLock] Force unlocked player movement. reason={reason}");
        }

        private static void SetMovementEnabled(bool enabled)
        {
            Behaviour[] behaviours = Object.FindObjectsOfType<Behaviour>(true);

            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];

                if (behaviour == null || !IsMovementBehaviour(behaviour))
                    continue;

                if (!savedStates.ContainsKey(behaviour))
                    savedStates.Add(behaviour, behaviour.enabled);

                behaviour.enabled = enabled;
            }
        }

        private static bool IsMovementBehaviour(Behaviour behaviour)
        {
            string typeName = behaviour.GetType().Name;

            return typeName == nameof(PcPlayerController)
                || typeName.IndexOf("ContinuousMoveProvider", System.StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("ContinuousTurnProvider", System.StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("SnapTurnProvider", System.StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("TeleportationProvider", System.StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("TeleportInteractor", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
