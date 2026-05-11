using UnityEngine;

namespace EDNXR.Gameplay
{
    public class PacketQuantityLabel : MonoBehaviour
    {
        private Transform target;
        private Camera cachedCamera;
        private float verticalOffset = 0.17f;

        public void Configure(Transform followTarget, float offset)
        {
            target = followTarget;
            verticalOffset = offset;
        }

        private void LateUpdate()
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = target.position + Vector3.up * verticalOffset;

            Camera camera = GetCamera();
            if (camera == null)
                return;

            Vector3 directionToCamera = transform.position - camera.transform.position;

            if (directionToCamera.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
        }

        private Camera GetCamera()
        {
            if (cachedCamera == null)
                cachedCamera = Camera.main;

            return cachedCamera;
        }
    }
}
