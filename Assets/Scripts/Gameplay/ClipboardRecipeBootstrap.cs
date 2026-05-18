using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    public static class ClipboardRecipeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureClipboard()
        {
            GameObject clipboard = FindClipboard();

            if (clipboard == null)
                return;

            EnsureGrabbable(clipboard);
            EnsureRecipeText(clipboard);
        }

        private static GameObject FindClipboard()
        {
            GameObject clipboard = GameObject.Find("Clipboard");

            if (clipboard != null)
                return clipboard;

            clipboard = GameObject.Find("clipboard");

            if (clipboard != null)
                return clipboard;

            GameObject[] objects = Object.FindObjectsOfType<GameObject>();

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i].name.IndexOf("clipboard", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return objects[i];
            }

            return null;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureClipboard();
        }

        private static void EnsureGrabbable(GameObject clipboard)
        {
            if (clipboard.GetComponentInChildren<Collider>() == null)
            {
                BoxCollider collider = clipboard.AddComponent<BoxCollider>();
                Renderer renderer = clipboard.GetComponentInChildren<Renderer>();

                if (renderer != null)
                {
                    Bounds localBounds = ToLocalBounds(clipboard.transform, renderer.bounds);
                    collider.center = localBounds.center;
                    collider.size = localBounds.size;
                }
                else
                {
                    collider.size = new Vector3(0.25f, 0.35f, 0.03f);
                }
            }

            Rigidbody rb = clipboard.GetComponent<Rigidbody>();

            if (rb == null)
                rb = clipboard.AddComponent<Rigidbody>();

            rb.mass = 0.35f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            XRGrabInteractable grabInteractable = clipboard.GetComponent<XRGrabInteractable>();

            if (grabInteractable == null)
                grabInteractable = clipboard.AddComponent<XRGrabInteractable>();

            grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;

            if (clipboard.GetComponent<PcGrabbableObject>() == null)
                clipboard.AddComponent<PcGrabbableObject>();

            if (clipboard.GetComponent<ClipboardObjectiveTrigger>() == null)
                clipboard.AddComponent<ClipboardObjectiveTrigger>();
        }

        private static Bounds ToLocalBounds(Transform root, Bounds worldBounds)
        {
            Vector3 min = root.InverseTransformPoint(worldBounds.min);
            Vector3 max = root.InverseTransformPoint(worldBounds.max);
            Bounds bounds = new Bounds((min + max) * 0.5f, Abs(max - min));
            return bounds;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static void EnsureRecipeText(GameObject clipboard)
        {
            if (clipboard.transform.Find("RecipeText") != null)
                return;

            GameObject textObject = new GameObject("RecipeText");
            textObject.transform.SetParent(clipboard.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.02f, 0.01f);
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            textObject.transform.localScale = Vector3.one;

            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.text =
                "RECETTES\n" +
                "Proton = 2 Up + 1 Down\n" +
                "Neutron = 1 Up + 2 Down\n" +
                "Helium = 2 Protons + 2 Neutrons + 2 Electrons\n" +
                "Uranium = 92 Protons + 146 Neutrons + 92 Electrons";
            text.fontSize = 0.043f;
            text.color = Color.black;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(0.5f, 0.34f);
        }
    }
}
