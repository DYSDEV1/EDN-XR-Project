using UnityEngine;

namespace EDNXR.Gameplay
{
    /// <summary>
    /// Diagnostic bootstrap: runs automatically after scene load and logs
    /// all information about BucketAssembler instances in the scene.
    /// Remove this script once the issue is resolved.
    /// </summary>
    public static class BucketAssemblerDiagnostic
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void DiagnoseBucketAssemblers()
        {
            Debug.Log("=== [BucketAssemblerDiagnostic] Running scene diagnostic ===");

            BucketAssembler[] assemblers = Object.FindObjectsOfType<BucketAssembler>(true);
            Debug.Log($"[Diagnostic] Found {assemblers.Length} BucketAssembler(s) in scene");

            for (int i = 0; i < assemblers.Length; i++)
            {
                BucketAssembler ba = assemblers[i];
                GameObject go = ba.gameObject;
                Transform parent = go.transform.parent;

                Debug.Log($"[Diagnostic] BucketAssembler #{i}: " +
                    $"name='{go.name}', " +
                    $"active={go.activeInHierarchy}, " +
                    $"enabled={ba.enabled}, " +
                    $"parent='{(parent != null ? parent.name : "none")}', " +
                    $"position={go.transform.position}, " +
                    $"lossyScale={go.transform.lossyScale}");

                Collider[] colliders = go.GetComponents<Collider>();
                Debug.Log($"[Diagnostic]   Colliders: {colliders.Length}");
                for (int j = 0; j < colliders.Length; j++)
                {
                    Debug.Log($"[Diagnostic]   Collider[{j}]: type={colliders[j].GetType().Name}, " +
                        $"isTrigger={colliders[j].isTrigger}, enabled={colliders[j].enabled}, " +
                        $"bounds={colliders[j].bounds}");
                }

                Rigidbody rb = go.GetComponent<Rigidbody>();
                Debug.Log($"[Diagnostic]   Rigidbody: {(rb != null ? $"exists, isKinematic={rb.isKinematic}" : "MISSING")}");

                // Check parent
                if (parent != null)
                {
                    Rigidbody parentRb = parent.GetComponent<Rigidbody>();
                    Collider[] parentColliders = parent.GetComponents<Collider>();
                    Debug.Log($"[Diagnostic]   Parent '{parent.name}': colliders={parentColliders.Length}, " +
                        $"rb={parentRb != null}, parentActive={parent.gameObject.activeInHierarchy}");
                }
            }

            // Also check for IngredientBall instances
            IngredientBall[] balls = Object.FindObjectsOfType<IngredientBall>(true);
            Debug.Log($"[Diagnostic] Found {balls.Length} IngredientBall(s) in scene at startup");

            // Check paintcans in scene
            Transform[] sceneObjects = Object.FindObjectsOfType<Transform>(true);
            int paintCanCount = 0;

            for (int p = 0; p < sceneObjects.Length; p++)
            {
                Transform paintCan = sceneObjects[p];

                if (paintCan == null || !paintCan.name.Replace(" ", "").StartsWith("PaintCan", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                paintCanCount++;
                Debug.Log($"[Diagnostic] PaintCan found: '{paintCan.name}' at {paintCan.position}, " +
                    $"children={paintCan.childCount}, grabbable={paintCan.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable>() != null}");

                for (int i = 0; i < paintCan.childCount; i++)
                {
                    Transform child = paintCan.GetChild(i);
                    Debug.Log($"[Diagnostic]   Child[{i}]: '{child.name}', " +
                        $"hasBA={child.GetComponent<BucketAssembler>() != null}, " +
                        $"hasCollider={child.GetComponent<Collider>() != null}");
                }
            }

            if (paintCanCount == 0)
                Debug.LogWarning("[Diagnostic] No PaintCan found in scene!");

            Debug.Log("=== [BucketAssemblerDiagnostic] Diagnostic complete ===");
        }
    }
}
