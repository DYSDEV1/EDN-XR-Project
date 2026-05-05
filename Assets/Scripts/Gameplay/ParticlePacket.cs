using UnityEngine;

namespace EDNXR.Gameplay
{
    public class ParticlePacket : MonoBehaviour
    {
        [SerializeField] private IngredientType ingredientType = IngredientType.None;
        [SerializeField] private int particleCount = 1;

        public IngredientType Type => ingredientType;
        public int Count => Mathf.Max(1, particleCount);

        public void Configure(IngredientType type, int count)
        {
            ingredientType = type;
            particleCount = Mathf.Max(1, count);
            gameObject.name = $"{type} Packet x{particleCount}";
        }
    }
}
