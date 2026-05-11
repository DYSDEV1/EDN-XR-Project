using UnityEngine;

namespace EDNXR.Gameplay
{
    public class PcGrabbableObject : MonoBehaviour
    {
        public void NotifyPcGrabbed()
        {
            SendMessage("OnPcGrabbed", SendMessageOptions.DontRequireReceiver);
        }

        public void NotifyPcReleased()
        {
            SendMessage("OnPcReleased", SendMessageOptions.DontRequireReceiver);
        }
    }
}
