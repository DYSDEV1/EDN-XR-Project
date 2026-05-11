using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class IngredientBall : MonoBehaviour
    {
        [Header("Ingredient")]
        [SerializeField] private IngredientType ingredientType = IngredientType.None;

        [Header("Debug")]
        [SerializeField] private string displayName = "Ingredient";

        private bool isConsumed = false;

        public IngredientType Type => ResolveType();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Type.ToString() : displayName;
        public bool IsConsumed => isConsumed;

        public void Configure(IngredientType type, string newDisplayName)
        {
            ingredientType = type;
            displayName = newDisplayName;
            isConsumed = false;
        }

        private IngredientType ResolveType()
        {
            string objectName = gameObject.name;

            if (objectName.IndexOf("QuarkDown", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return IngredientType.QuarkDown;

            if (objectName.IndexOf("QuarkUp", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return IngredientType.QuarkUp;

            if (objectName.IndexOf("Electron", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return IngredientType.Electron;

            if (objectName.IndexOf("Proton", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return IngredientType.Proton;

            if (objectName.IndexOf("Neutron", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return IngredientType.Neutron;

            if (objectName.IndexOf("Helium", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Atom", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return IngredientType.Atom;

            if (objectName.IndexOf("Uranium", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return IngredientType.Uranium;

            return ingredientType;
        }

        public void Consume()
        {
            if (isConsumed) return;
            isConsumed = true;

            GameObject objectToHide = ResolveObjectToHide();
            HideObject(objectToHide);
        }

        private GameObject ResolveObjectToHide()
        {
            XRGrabInteractable grabInteractable = GetComponentInParent<XRGrabInteractable>();

            if (grabInteractable != null)
                return grabInteractable.gameObject;

            ParticlePacket packet = GetComponentInParent<ParticlePacket>();

            if (packet != null)
                return packet.gameObject;

            Rigidbody rb = GetComponentInParent<Rigidbody>();

            if (rb != null)
                return rb.gameObject;

            return gameObject;
        }

        private void HideObject(GameObject objectToHide)
        {
            Renderer[] renderers = objectToHide.GetComponentsInChildren<Renderer>(true);
            Collider[] colliders = objectToHide.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;

            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            objectToHide.SetActive(false);
        }

        public void ResetConsumed()
        {
            isConsumed = false;
            gameObject.SetActive(true);
        }
    }
}
