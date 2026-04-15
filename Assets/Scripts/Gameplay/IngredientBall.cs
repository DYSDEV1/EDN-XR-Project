using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        public IngredientType Type => ingredientType;
        public string DisplayName => displayName;
        public bool IsConsumed => isConsumed;

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