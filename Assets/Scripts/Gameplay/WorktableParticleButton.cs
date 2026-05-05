using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class WorktableParticleButton : MonoBehaviour
    {
        private WorktableParticleSpawner spawner;
        private IngredientType particleType;
        private ButtonAction action;

        private enum ButtonAction
        {
            SelectParticle,
            DecreaseQuantity,
            IncreaseQuantity,
            SpawnPacket
        }

        public void ConfigureParticle(WorktableParticleSpawner owner, IngredientType type)
        {
            spawner = owner;
            particleType = type;
            action = ButtonAction.SelectParticle;
            HookXRSelect();
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

        private void Press()
        {
            if (spawner == null)
                return;

            switch (action)
            {
                case ButtonAction.SelectParticle:
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
