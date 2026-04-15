using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
            if (instructionText != null)
            {
                instructionText.text =
                    "Étape 1 : Construis un proton.\n" +
                    "Dépose 2 quarks Up (rouges) et 1 quark Down (bleu) dans le seau.";
            }

            if (protonVisual != null)
                protonVisual.SetActive(false);
        }

        public void OnProtonRecipeCompleted()
        {
            if (instructionText != null)
            {
                instructionText.text =
                    "Bravo ! Tu as créé un proton.\n" +
                    "Un proton contient 2 quarks Up et 1 quark Down.";
            }

            if (protonVisual != null)
            {
                protonVisual.SetActive(true);

                if (protonSpawnPoint != null)
                {
                    protonVisual.transform.position = protonSpawnPoint.position;
                    protonVisual.transform.rotation = protonSpawnPoint.rotation;
                }
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

            if (instructionText != null)
            {
                instructionText.text =
                    "Étape 1 : Construis un proton.\n" +
                    "Dépose 2 quarks Up (rouges) et 1 quark Down (bleu) dans le seau.";
            }
        }
    }
}