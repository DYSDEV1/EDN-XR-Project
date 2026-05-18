using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    public class ReturnToSleepObjectiveInteractable : MonoBehaviour
    {
        private UraniumDeliveryObjectiveController objectiveController;

        private void Awake()
        {
            MakeDynamic();
            EnsureCollider();
            EnsureXRInteraction();
        }

        public void Configure(UraniumDeliveryObjectiveController controller)
        {
            objectiveController = controller;
        }

        public void TrySleep()
        {
            UraniumDeliveryObjectiveController controller = objectiveController != null
                ? objectiveController
                : UraniumDeliveryObjectiveController.Instance;

            if (controller != null)
                controller.TryReturnToSleep();
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            TrySleep();
        }

        private void OnMouseDown()
        {
            TrySleep();
        }

        private void EnsureXRInteraction()
        {
            XRSimpleInteractable interactable = GetComponent<XRSimpleInteractable>();

            if (interactable == null)
                interactable = gameObject.AddComponent<XRSimpleInteractable>();

            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.selectEntered.AddListener(OnSelected);
        }

        private void EnsureCollider()
        {
            Collider rootCollider = GetComponent<Collider>();

            if (rootCollider != null)
                return;

            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            Bounds localBounds = GetLocalBounds();
            collider.center = localBounds.center;
            collider.size = localBounds.size;
            collider.isTrigger = true;
        }

        private void MakeDynamic()
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null)
                    children[i].gameObject.isStatic = false;
            }
        }

        private Bounds GetLocalBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one * 0.8f);

            Bounds bounds = ToLocalBounds(renderers[0].bounds);

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(ToLocalBounds(renderers[i].bounds));

            Vector3 size = bounds.size;
            size.x = Mathf.Max(size.x, 0.8f);
            size.y = Mathf.Max(size.y, 0.45f);
            size.z = Mathf.Max(size.z, 0.8f);
            bounds.size = size;
            return bounds;
        }

        private Bounds ToLocalBounds(Bounds worldBounds)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Bounds bounds = new Bounds(transform.InverseTransformPoint(min), Vector3.zero);
            bounds.Encapsulate(transform.InverseTransformPoint(new Vector3(min.x, min.y, max.z)));
            bounds.Encapsulate(transform.InverseTransformPoint(new Vector3(min.x, max.y, min.z)));
            bounds.Encapsulate(transform.InverseTransformPoint(new Vector3(min.x, max.y, max.z)));
            bounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, min.y, min.z)));
            bounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, min.y, max.z)));
            bounds.Encapsulate(transform.InverseTransformPoint(new Vector3(max.x, max.y, min.z)));
            bounds.Encapsulate(transform.InverseTransformPoint(max));
            return bounds;
        }
    }
}
