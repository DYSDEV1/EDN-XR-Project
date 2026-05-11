using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    public class MemoryMiniGameCell : MonoBehaviour
    {
        private BucketAssembler owner;
        private int cellIndex;

        public int CellIndex => cellIndex;

        public void Configure(BucketAssembler newOwner, int newCellIndex)
        {
            owner = newOwner;
            cellIndex = newCellIndex;

            XRSimpleInteractable interactable = GetComponent<XRSimpleInteractable>();
            if (interactable == null)
                interactable = gameObject.AddComponent<XRSimpleInteractable>();

            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.selectEntered.AddListener(OnSelected);
        }

        public void Select()
        {
            if (owner != null)
                owner.SelectMemoryMiniGameCell(cellIndex);
        }

        private void OnSelected(SelectEnterEventArgs args)
        {
            Select();
        }

        private void OnMouseDown()
        {
            Select();
        }
    }
}
