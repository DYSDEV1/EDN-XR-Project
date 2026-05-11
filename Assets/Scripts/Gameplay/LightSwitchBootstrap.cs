using UnityEngine;

namespace EDNXR.Gameplay
{
    public static class LightSwitchBootstrap
    {
        private const string PreferredLightSwitchName = "LightSwitch 1 (1)";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureLightSwitch()
        {
            GameObject lightSwitch = FindLightSwitch();

            if (lightSwitch == null)
            {
                Debug.LogWarning("[LightSwitchBootstrap] Aucun interrupteur trouve. Nom attendu proche de LightSwitch 1 (1).");
                return;
            }

            EnsureCollider(lightSwitch);

            if (lightSwitch.GetComponent<LightSwitchController>() == null)
                lightSwitch.AddComponent<LightSwitchController>();

            Debug.Log($"[LightSwitchBootstrap] Interrupteur prepare: {lightSwitch.name}");
        }

        private static GameObject FindLightSwitch()
        {
            GameObject lightSwitch = GameObject.Find(PreferredLightSwitchName);

            if (lightSwitch != null)
                return lightSwitch;

            lightSwitch = GameObject.Find("Light switch 1 (1)");

            if (lightSwitch != null)
                return lightSwitch;

            Transform[] transforms = Object.FindObjectsOfType<Transform>(true);

            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null)
                    continue;

                string normalizedName = transforms[i].name.Replace(" ", "").ToLowerInvariant();

                if (normalizedName == "lightswitch1(1)")
                    return transforms[i].gameObject;
            }

            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] == null)
                    continue;

                string normalizedName = transforms[i].name.Replace(" ", "").ToLowerInvariant();

                if (normalizedName.Contains("lightswitch"))
                    return transforms[i].gameObject;
            }

            return null;
        }

        private static void EnsureCollider(GameObject lightSwitch)
        {
            BoxCollider collider = lightSwitch.GetComponent<BoxCollider>();
            
            if (collider == null)
                collider = lightSwitch.AddComponent<BoxCollider>();
            
            collider.isTrigger = false;
            collider.enabled = true;
            Renderer[] renderers = lightSwitch.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                collider.size = new Vector3(0.25f, 0.35f, 0.08f);
                return;
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 localMin = lightSwitch.transform.InverseTransformPoint(bounds.min);
            Vector3 localMax = lightSwitch.transform.InverseTransformPoint(bounds.max);
            collider.center = (localMin + localMax) * 0.5f;
            collider.size = Abs(localMax - localMin);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
