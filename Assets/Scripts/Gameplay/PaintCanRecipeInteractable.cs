using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    public class PaintCanRecipeInteractable : MonoBehaviour
    {
        private BucketAssembler bucketAssembler;
        private XRGrabInteractable grabInteractable;

        public void Configure(BucketAssembler assembler, XRGrabInteractable interactable)
        {
            bucketAssembler = assembler != null ? assembler : GetComponentInChildren<BucketAssembler>(true);
            HookActivate(interactable != null ? interactable : GetComponent<XRGrabInteractable>());
        }

        private void OnEnable()
        {
            if (bucketAssembler == null)
                bucketAssembler = GetComponentInChildren<BucketAssembler>(true);

            HookActivate(GetComponent<XRGrabInteractable>());
        }

        private void OnDisable()
        {
            UnhookActivate();
        }

        public bool TryMixFromInteraction()
        {
            if (bucketAssembler == null)
                bucketAssembler = GetComponentInChildren<BucketAssembler>(true);

            if (bucketAssembler == null)
                return false;

            bucketAssembler.TryMixCurrentContents();
            return true;
        }

        private void HookActivate(XRGrabInteractable interactable)
        {
            if (grabInteractable == interactable && grabInteractable != null)
                return;

            UnhookActivate();
            grabInteractable = interactable;

            if (grabInteractable == null)
                return;

            grabInteractable.activated.RemoveListener(OnActivated);
            grabInteractable.activated.AddListener(OnActivated);
        }

        private void UnhookActivate()
        {
            if (grabInteractable == null)
                return;

            grabInteractable.activated.RemoveListener(OnActivated);
            grabInteractable = null;
        }

        private void OnActivated(ActivateEventArgs args)
        {
            TryMixFromInteraction();
        }
    }
}
