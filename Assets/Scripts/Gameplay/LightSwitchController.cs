using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class LightSwitchController : MonoBehaviour
    {
        [SerializeField] private string switchClipName = "Lightswitch";
        [SerializeField] private Color sleepingAmbientColor = new Color(0.015f, 0.018f, 0.025f);
        [SerializeField] private float sleepingAmbientIntensity = 0.08f;
        [SerializeField] private Color highlightColor = new Color(1f, 0.88f, 0.28f);
        [SerializeField] private float highlightPulseSpeed = 4f;
        [SerializeField] private float haloScale = 1.75f;

        private readonly List<LightState> lightStates = new List<LightState>();
        private readonly List<RendererState> rendererStates = new List<RendererState>();

        private AmbientMode originalAmbientMode;
        private Color originalAmbientColor;
        private float originalAmbientIntensity;
        private Material originalSkybox;
        private bool originalFog;
        private bool isTurnedOn;
        private bool hasSetDarkObjective;
        private bool hasSetRestoredObjective;
        private GameObject highlightObject;
        private Renderer highlightRenderer;
        private Material highlightMaterial;
        private Camera mainCamera;
        private AudioClip switchClip;

        private void Awake()
        {
            SaveLightingState();
            SaveRendererState();
            EnsureXRInteraction();
            LoadAudio();
            TurnSceneLightsOff();
            EnableHighlight();
            TrySetDarkObjective();
        }

        private void Update()
        {
            if (!isTurnedOn)
                TrySetDarkObjective();
            else
                TrySetRestoredObjective();

            if (isTurnedOn || highlightRenderer == null)
                return;

            float pulse = 0.65f + Mathf.Sin(Time.time * highlightPulseSpeed) * 0.35f;
            Color markerColor = Color.Lerp(highlightColor * 0.75f, highlightColor * 1.7f, pulse);
            markerColor.a = Mathf.Lerp(0.35f, 0.78f, pulse);
            highlightRenderer.material.color = markerColor;
            ApplyHighlightEmission(pulse);
        }

        private void LateUpdate()
        {
            if (isTurnedOn || highlightObject == null)
                return;

            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera != null)
                highlightObject.transform.rotation = Quaternion.LookRotation(highlightObject.transform.position - mainCamera.transform.position, Vector3.up);
        }

        public void TryTurnOn()
        {
            if (isTurnedOn)
                return;

            isTurnedOn = true;
            RestoreLightingState();
            DisableHighlight();
            PlaySwitchSound();
            TrySetRestoredObjective();
            Debug.Log("[LightSwitchController] Lumieres rallumees via interrupteur.");
        }

        private void OnMouseDown()
        {
            TryTurnOn();
        }

        private void EnsureXRInteraction()
        {
            XRSimpleInteractable interactable = GetComponent<XRSimpleInteractable>();

            if (interactable == null)
                interactable = gameObject.AddComponent<XRSimpleInteractable>();

            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.selectEntered.AddListener(OnSelected);
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            TryTurnOn();
        }

        private void SaveLightingState()
        {
            originalAmbientMode = RenderSettings.ambientMode;
            originalAmbientColor = RenderSettings.ambientLight;
            originalAmbientIntensity = RenderSettings.ambientIntensity;
            originalSkybox = RenderSettings.skybox;
            originalFog = RenderSettings.fog;

            Light[] lights = Object.FindObjectsOfType<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                    lightStates.Add(new LightState(lights[i]));
            }
        }

        private void SaveRendererState()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Material[] materials = renderers[i].materials;

                for (int j = 0; j < materials.Length; j++)
                {
                    if (materials[j] != null)
                        rendererStates.Add(new RendererState(materials[j]));
                }
            }
        }

        private void TurnSceneLightsOff()
        {
            for (int i = 0; i < lightStates.Count; i++)
            {
                if (lightStates[i].Light != null)
                    lightStates[i].Light.enabled = false;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = sleepingAmbientColor;
            RenderSettings.ambientIntensity = sleepingAmbientIntensity;
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            DynamicGI.UpdateEnvironment();
            Debug.Log($"[LightSwitchController] Debut sombre: {lightStates.Count} light(s) eteinte(s).");
        }

        private void RestoreLightingState()
        {
            for (int i = 0; i < lightStates.Count; i++)
                lightStates[i].Restore();

            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientLight = originalAmbientColor;
            RenderSettings.ambientIntensity = originalAmbientIntensity;
            RenderSettings.skybox = originalSkybox;
            RenderSettings.fog = originalFog;
            DynamicGI.UpdateEnvironment();
        }

        private void EnableHighlight()
        {
            GetHighlightPlacement(out Vector3 localCenter, out float diameter);

            highlightObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            highlightObject.name = "Light Switch Highlight";
            highlightObject.transform.SetParent(transform, false);
            highlightObject.transform.localPosition = localCenter;
            highlightObject.transform.localRotation = Quaternion.identity;
            highlightObject.transform.localScale = Vector3.one * diameter;

            Collider markerCollider = highlightObject.GetComponent<Collider>();
            if (markerCollider != null)
                Destroy(markerCollider);

            highlightRenderer = highlightObject.GetComponent<Renderer>();
            Shader shader = Shader.Find("Unlit/Transparent");
            highlightMaterial = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
            highlightMaterial.mainTexture = BuildHaloTexture();
            highlightMaterial.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.65f);
            highlightMaterial.renderQueue = 3000;

            if (highlightRenderer != null)
                highlightRenderer.material = highlightMaterial;

            ApplyHighlightEmission(1f);
        }

        private void DisableHighlight()
        {
            for (int i = 0; i < rendererStates.Count; i++)
                rendererStates[i].Restore();

            if (highlightObject != null)
                Destroy(highlightObject);

            if (highlightMaterial != null)
                Destroy(highlightMaterial);
        }

        private void ApplyHighlightEmission(float strength)
        {
            Color emission = highlightColor * Mathf.Lerp(0.7f, 1.8f, strength);

            for (int i = 0; i < rendererStates.Count; i++)
                rendererStates[i].ApplyEmission(emission);
        }

        private void GetHighlightPlacement(out Vector3 localCenter, out float diameter)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                localCenter = Vector3.zero;
                diameter = 0.45f;
                return;
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            localCenter = transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = transform.InverseTransformVector(bounds.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            diameter = Mathf.Max(localSize.x, localSize.y, localSize.z, 0.28f) * haloScale;
        }

        private Texture2D BuildHaloTexture()
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.62f) / 0.16f);
                    float glow = Mathf.Clamp01(1f - distance);
                    float alpha = Mathf.Max(ring * 0.85f, glow * 0.22f);
                    alpha *= Mathf.SmoothStep(1f, 0f, distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
        }

        private void LoadAudio()
        {
            switchClip = Resources.Load<AudioClip>(switchClipName);

            if (switchClip == null)
                switchClip = Resources.Load<AudioClip>("lightswitch");
        }

        private void PlaySwitchSound()
        {
            if (switchClip != null)
                AudioSource.PlayClipAtPoint(switchClip, transform.position, 0.9f);
        }

        private void TrySetDarkObjective()
        {
            if (hasSetDarkObjective || ObjectiveHud.Instance == null)
                return;

            ObjectiveHud.Instance.SetMessage("Rallumer les lumieres");
            hasSetDarkObjective = true;
        }

        private void TrySetRestoredObjective()
        {
            if (hasSetRestoredObjective || ObjectiveHud.Instance == null)
                return;

            if (!PhoneObjectiveController.TryActivatePhoneObjective())
                ObjectiveHud.Instance.SetMessage("Repondre au telephone");

            hasSetRestoredObjective = true;
        }

        private struct LightState
        {
            public readonly Light Light;
            private readonly bool enabled;
            private readonly float intensity;

            public LightState(Light light)
            {
                Light = light;
                enabled = light.enabled;
                intensity = light.intensity;
            }

            public void Restore()
            {
                if (Light == null)
                    return;

                Light.enabled = enabled;
                Light.intensity = intensity;
            }
        }

        private struct RendererState
        {
            private readonly Material material;
            private readonly Color emissionColor;
            private readonly bool hadEmissionKeyword;

            public RendererState(Material material)
            {
                this.material = material;
                emissionColor = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                hadEmissionKeyword = material.IsKeywordEnabled("_EMISSION");
            }

            public void ApplyEmission(Color color)
            {
                if (material == null || !material.HasProperty("_EmissionColor"))
                    return;

                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color);
            }

            public void Restore()
            {
                if (material == null || !material.HasProperty("_EmissionColor"))
                    return;

                material.SetColor("_EmissionColor", emissionColor);

                if (!hadEmissionKeyword)
                    material.DisableKeyword("_EMISSION");
            }
        }
    }
}
