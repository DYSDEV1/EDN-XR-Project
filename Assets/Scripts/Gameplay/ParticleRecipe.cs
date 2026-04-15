using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Collections.Generic;
using UnityEngine;

namespace EDNXR.Gameplay
{
    public class ParticleRecipe : MonoBehaviour
    {
        [System.Serializable]
        public struct RecipeEntry
        {
            public IngredientType ingredientType;
            public int requiredCount;
        }

        [Header("Recipe")]
        [SerializeField] private string particleName = "Proton";
        [SerializeField] private RecipeEntry[] recipeEntries;

        public string ParticleName => particleName;
        public RecipeEntry[] Entries => recipeEntries;

        public bool Matches(Dictionary<IngredientType, int> currentCounts)
        {
            for (int i = 0; i < recipeEntries.Length; i++)
            {
                RecipeEntry entry = recipeEntries[i];

                currentCounts.TryGetValue(entry.ingredientType, out int currentValue);

                if (currentValue != entry.requiredCount)
                    return false;
            }

            foreach (var pair in currentCounts)
            {
                bool found = false;
                for (int i = 0; i < recipeEntries.Length; i++)
                {
                    if (recipeEntries[i].ingredientType == pair.Key)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found && pair.Value > 0)
                    return false;
            }

            return true;
        }

        public bool IsOverfilled(Dictionary<IngredientType, int> currentCounts)
        {
            for (int i = 0; i < recipeEntries.Length; i++)
            {
                RecipeEntry entry = recipeEntries[i];
                currentCounts.TryGetValue(entry.ingredientType, out int currentValue);

                if (currentValue > entry.requiredCount)
                    return true;
            }

            foreach (var pair in currentCounts)
            {
                bool found = false;
                for (int i = 0; i < recipeEntries.Length; i++)
                {
                    if (recipeEntries[i].ingredientType == pair.Key)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found && pair.Value > 0)
                    return true;
            }

            return false;
        }

        public string GetRecipeDescription()
        {
            List<string> parts = new List<string>();

            for (int i = 0; i < recipeEntries.Length; i++)
            {
                parts.Add($"{recipeEntries[i].requiredCount} x {recipeEntries[i].ingredientType}");
            }

            return $"{particleName} = " + string.Join(" + ", parts);
        }
    }
}