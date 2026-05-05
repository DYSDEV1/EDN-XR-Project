using TMPro;
using UnityEngine;

namespace EDNXR.Gameplay
{
    public class ProtonStageController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BucketAssembler bucketAssembler;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private GameObject protonVisual;
        [SerializeField] private Transform protonSpawnPoint;

        [Header("Optional respawn")]
        [SerializeField] private IngredientBall[] ingredientsToRespawn;
        [SerializeField] private Transform[] respawnPoints;

        private void Start()
        {
            SetRecipeInstructions();

            if (protonVisual != null)
                protonVisual.SetActive(false);
        }

        public void OnProtonRecipeCompleted()
        {
            if (instructionText != null)
            {
                instructionText.text =
                    "Recette reussie !\n" +
                    "La nouvelle particule est apparue sur la workbench.";
            }
        }

        public void ResetStage()
        {
            if (bucketAssembler != null)
                bucketAssembler.ResetBucket();

            for (int i = 0; i < ingredientsToRespawn.Length; i++)
            {
                if (ingredientsToRespawn[i] == null) continue;

                ingredientsToRespawn[i].ResetConsumed();

                if (i < respawnPoints.Length && respawnPoints[i] != null)
                {
                    Transform t = ingredientsToRespawn[i].transform;
                    Rigidbody rb = ingredientsToRespawn[i].GetComponent<Rigidbody>();

                    t.position = respawnPoints[i].position;
                    t.rotation = respawnPoints[i].rotation;

                    if (rb != null)
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }

            if (protonVisual != null)
                protonVisual.SetActive(false);

            SetRecipeInstructions();
        }

        private void SetRecipeInstructions()
        {
            if (instructionText == null)
                return;

            instructionText.text =
                "Recettes disponibles :\n" +
                "Proton = 2 Up + 1 Down (uud)\n" +
                "Neutron = 1 Up + 2 Down (udd)\n" +
                "Helium = 2 Protons + 2 Neutrons + 2 Electrons.";
        }
    }
}
