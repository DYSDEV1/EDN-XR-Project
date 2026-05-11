using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    public class ClipboardObjectiveTrigger : MonoBehaviour
    {
        [SerializeField] private string grabbedMessage = "Ouvrir la porte";

        private XRGrabInteractable grabInteractable;
        private bool hasTriggered;

        private void OnEnable()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();

            if (grabInteractable != null)
                grabInteractable.selectEntered.AddListener(OnVrGrabbed);
        }

        private void OnDisable()
        {
            if (grabInteractable != null)
                grabInteractable.selectEntered.RemoveListener(OnVrGrabbed);
        }

        private void OnVrGrabbed(SelectEnterEventArgs args)
        {
            TriggerObjective();
        }

        public void OnPcGrabbed()
        {
            TriggerObjective();
        }

        private void TriggerObjective()
        {
            if (hasTriggered)
                return;

            hasTriggered = true;

            if (!DoorObjectiveController.TryActivateDoorObjective() && ObjectiveHud.Instance != null)
                ObjectiveHud.Instance.SetMessage(grabbedMessage);
        }
    }
}
