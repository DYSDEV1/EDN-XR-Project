using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class WorktableParticleButton : MonoBehaviour
    {
        private WorktableParticleSpawner spawner;
        private IngredientType particleType;
        public IngredientType ParticleType => particleType;
        private ButtonAction action;
        private Color originalColor;
        private Renderer buttonRenderer;
        
        public bool isUnlocked { get; private set; } = true;

        private enum ButtonAction
        {
            SelectParticle,
            DecreaseQuantity,
            IncreaseQuantity,
            SpawnPacket
        }

        public void ConfigureParticle(WorktableParticleSpawner owner, IngredientType type, Color color, bool initialUnlockState)
        {
            spawner = owner;
            particleType = type;
            originalColor = color;
            buttonRenderer = GetComponent<Renderer>();
            action = ButtonAction.SelectParticle;
            HookXRSelect();
            SetUnlocked(initialUnlockState);
        }

        public void SetUnlocked(bool unlocked)
        {
            isUnlocked = unlocked;
            if (buttonRenderer != null)
            {
                buttonRenderer.material.color = unlocked ? originalColor : new Color(0.2f, 0.2f, 0.2f);
            }
        }

        public void ConfigureDecrease(WorktableParticleSpawner owner)
        {
            spawner = owner;
            action = ButtonAction.DecreaseQuantity;
            HookXRSelect();
        }

        public void ConfigureIncrease(WorktableParticleSpawner owner)
        {
            spawner = owner;
            action = ButtonAction.IncreaseQuantity;
            HookXRSelect();
        }

        public void ConfigureSpawn(WorktableParticleSpawner owner)
        {
            spawner = owner;
            action = ButtonAction.SpawnPacket;
            HookXRSelect();
        }

        private void HookXRSelect()
        {
            XRSimpleInteractable interactable = GetComponent<XRSimpleInteractable>();

            if (interactable == null)
                interactable = gameObject.AddComponent<XRSimpleInteractable>();

            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.selectEntered.AddListener(OnSelected);
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            Press();
        }

        private void OnMouseDown()
        {
            Press();
        }

        public void Press()
        {
            if (spawner == null)
                return;

            switch (action)
            {
                case ButtonAction.SelectParticle:
                    if (isUnlocked)
                        spawner.SelectParticle(particleType);
                    break;
                case ButtonAction.DecreaseQuantity:
                    spawner.ChangeQuantity(-1);
                    break;
                case ButtonAction.IncreaseQuantity:
                    spawner.ChangeQuantity(1);
                    break;
                case ButtonAction.SpawnPacket:
                    spawner.SpawnSelectedPacket();
                    break;
            }
        }
    }
}
