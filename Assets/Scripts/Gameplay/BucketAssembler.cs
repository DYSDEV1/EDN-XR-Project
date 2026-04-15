using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace EDNXR.Gameplay
{
    public class BucketAssembler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ParticleRecipe targetRecipe;
        [SerializeField] private TMP_Text feedbackText;

        [Header("Success Visual")]
        [SerializeField] private GameObject protonVisual;
        [SerializeField] private Transform protonSpawnPoint;

        [Header("Settings")]
        [SerializeField] private bool consumeIngredientOnEnter = true;
        [SerializeField] private float successDelay = 0.2f;

        [Header("Events")]
        public UnityEvent onRecipeCompleted;
        public UnityEvent onWrongRecipe;

        private readonly Dictionary<IngredientType, int> currentCounts = new();
        private bool recipeCompleted = false;

        private void Start()
        {
            UpdateFeedback();

            if (protonVisual != null)
                protonVisual.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (recipeCompleted) return;

            IngredientBall ingredient = other.GetComponent<IngredientBall>();
            if (ingredient == null) return;
            if (ingredient.IsConsumed) return;

            AddIngredient(ingredient);
        }

        private void AddIngredient(IngredientBall ingredient)
        {
            IngredientType type = ingredient.Type;

            if (!currentCounts.ContainsKey(type))
                currentCounts[type] = 0;

            currentCounts[type]++;

            if (consumeIngredientOnEnter)
                ingredient.Consume();

            UpdateFeedback();

            if (targetRecipe == null)
                return;

            if (targetRecipe.Matches(currentCounts))
            {
                recipeCompleted = true;

                SetText("Bravo ! Proton créé !");
                ShowProton();

                Invoke(nameof(CompleteRecipe), successDelay);
                return;
            }

            if (targetRecipe.IsOverfilled(currentCounts))
            {
                SetText("Mauvaise combinaison. Appuie sur Reset.");
                onWrongRecipe?.Invoke();
            }
        }

        private void ShowProton()
        {
            if (protonVisual == null)
                return;

            protonVisual.SetActive(true);

            if (protonSpawnPoint != null)
            {
                protonVisual.transform.position = protonSpawnPoint.position;
                protonVisual.transform.rotation = protonSpawnPoint.rotation;
            }
        }

        private void CompleteRecipe()
        {
            onRecipeCompleted?.Invoke();
        }

        public void ResetBucket()
        {
            currentCounts.Clear();
            recipeCompleted = false;
            UpdateFeedback();

            if (protonVisual != null)
                protonVisual.SetActive(false);
        }

        private void UpdateFeedback()
        {
            if (targetRecipe == null)
            {
                SetText("Aucune recette assignée.");
                return;
            }

            int up = GetCount(IngredientType.QuarkUp);
            int down = GetCount(IngredientType.QuarkDown);

            SetText(
                $"Objectif : Proton = 2 Up + 1 Down\n" +
                $"Dans le seau : Up={up}, Down={down}"
            );
        }

        private int GetCount(IngredientType type)
        {
            currentCounts.TryGetValue(type, out int count);
            return count;
        }

        private void SetText(string message)
        {
            if (feedbackText != null)
                feedbackText.text = message;
        }
    }
}