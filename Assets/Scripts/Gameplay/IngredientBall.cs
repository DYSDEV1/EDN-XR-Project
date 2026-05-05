using UnityEngine;

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
                || objectName.IndexOf("Helium", System.StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Atom", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return IngredientType.Atom;

            return ingredientType;
        }

        public void Consume()
        {
            if (isConsumed) return;
            isConsumed = true;
            gameObject.SetActive(false);
        }

        public void ResetConsumed()
        {
            isConsumed = false;
            gameObject.SetActive(true);
        }
    }
}
