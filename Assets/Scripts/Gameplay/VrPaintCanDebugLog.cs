using System;
using System.Collections.Generic;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

namespace EDNXR.Gameplay
{
    public static class VrPaintCanDebugLog
    {
        private const string FileName = "vr_paintcan_debug.log";
        private static readonly object SyncRoot = new object();
        private static bool initialized;
        private static string logPath;

        public static string LogPath
        {
            get
            {
                EnsureInitialized();
                return logPath;
            }
        }

        public static void Write(string message)
        {
            EnsureInitialized();

            string line = $"{DateTime.UtcNow:O} frame={Time.frameCount} t={Time.realtimeSinceStartup:F3} {message}";
            Debug.Log($"[VRPaintCanDebug] {message}");

            try
            {
                lock (SyncRoot)
                {
                    File.AppendAllText(logPath, line + Environment.NewLine);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[VRPaintCanDebug] Failed to write log file: {exception.Message}");
            }
        }

        public static string FormatVector(Vector3 value)
        {
            return $"({value.x:F3},{value.y:F3},{value.z:F3})";
        }

        private static void EnsureInitialized()
        {
            if (initialized)
                return;

            initialized = true;
            logPath = Path.Combine(Application.persistentDataPath, FileName);

            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(
                    logPath,
                    $"=== VR PaintCan debug session {DateTime.UtcNow:O} ==={Environment.NewLine}" +
                    $"unityPersistentDataPath={Application.persistentDataPath}{Environment.NewLine}" +
                    $"deviceModel={SystemInfo.deviceModel} deviceName={SystemInfo.deviceName} os={SystemInfo.operatingSystem}{Environment.NewLine}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[VRPaintCanDebug] Failed to initialize log file: {exception.Message}");
            }
        }
    }

    public class PaintCanPlayerContactDebug : MonoBehaviour
    {
        private const float ActiveSnapshotInterval = 0.2f;
        private const float QuietSnapshotInterval = 2f;
        private const float NearPlayerDistance = 0.75f;
        private const float PlayerMoveWarningDistance = 0.25f;

        private XRGrabInteractable grabInteractable;
        private Rigidbody rb;
        private Collider[] paintCanColliders = Array.Empty<Collider>();
        private readonly List<Collider> playerColliders = new List<Collider>();
        private Transform playerRigRoot;
        private CharacterController playerController;
        private Camera mainCamera;
        private bool wasHeld;
        private Vector3 lastPlayerPosition;
        private float nextSnapshotTime;
        private float nextPlayerRefreshTime;
        private float nextCollisionStayLogTime;
        private float nextTriggerStayLogTime;

        public void Configure(XRGrabInteractable interactable)
        {
            HookGrabInteractable(interactable != null ? interactable : GetComponent<XRGrabInteractable>());
            RefreshCachedReferences(true);
            LogPaintCanSetup();
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            paintCanColliders = GetComponentsInChildren<Collider>(true);
        }

        private void OnEnable()
        {
            HookGrabInteractable(grabInteractable != null ? grabInteractable : GetComponent<XRGrabInteractable>());
            RefreshCachedReferences(true);
            VrPaintCanDebugLog.Write($"enabled paintcan='{name}' logPath='{VrPaintCanDebugLog.LogPath}'");
        }

        private void OnDisable()
        {
            UnhookGrabInteractable();
            VrPaintCanDebugLog.Write($"disabled paintcan='{name}'");
        }

        private void FixedUpdate()
        {
            if (Time.unscaledTime >= nextPlayerRefreshTime)
                RefreshCachedReferences(false);

            bool held = IsHeld();

            if (held != wasHeld)
            {
                wasHeld = held;
                VrPaintCanDebugLog.Write($"heldChanged paintcan='{name}' held={held} {BuildStateSummary()}");
            }

            if (Time.unscaledTime < nextSnapshotTime)
                return;

            ContactProbe probe = ProbePlayerContact();
            bool playerMovedFast = PlayerMovedFast();
            bool playerUnsafe = playerRigRoot != null && (!IsFinite(playerRigRoot.position) || playerRigRoot.position.y < -0.1f);
            bool interesting = held || probe.overlapPairs > 0 || probe.minDistance <= NearPlayerDistance || probe.nonIgnoredPairs > 0 || playerMovedFast || playerUnsafe;

            nextSnapshotTime = Time.unscaledTime + (interesting ? ActiveSnapshotInterval : QuietSnapshotInterval);

            if (interesting)
            {
                VrPaintCanDebugLog.Write(
                    $"snapshot paintcan='{name}' {BuildStateSummary()} " +
                    $"contact={probe} playerMovedFast={playerMovedFast} playerUnsafe={playerUnsafe}");
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            VrPaintCanDebugLog.Write($"collisionEnter paintcan='{name}' other={DescribeCollider(collision != null ? collision.collider : null)} impulse={FormatVector(collision != null ? collision.impulse : Vector3.zero)} {BuildStateSummary()}");
        }

        private void OnCollisionStay(Collision collision)
        {
            if (Time.unscaledTime < nextCollisionStayLogTime)
                return;

            nextCollisionStayLogTime = Time.unscaledTime + 0.5f;
            VrPaintCanDebugLog.Write($"collisionStay paintcan='{name}' other={DescribeCollider(collision != null ? collision.collider : null)} impulse={FormatVector(collision != null ? collision.impulse : Vector3.zero)} {BuildStateSummary()}");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayerCollider(other))
                VrPaintCanDebugLog.Write($"triggerEnterPlayer paintcan='{name}' other={DescribeCollider(other)} ignoredPairs={CountIgnoredPairsWith(other)} {BuildStateSummary()}");
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsPlayerCollider(other) || Time.unscaledTime < nextTriggerStayLogTime)
                return;

            nextTriggerStayLogTime = Time.unscaledTime + 0.5f;
            VrPaintCanDebugLog.Write($"triggerStayPlayer paintcan='{name}' other={DescribeCollider(other)} ignoredPairs={CountIgnoredPairsWith(other)} {BuildStateSummary()}");
        }

        private void HookGrabInteractable(XRGrabInteractable interactable)
        {
            if (grabInteractable == interactable && grabInteractable != null)
                return;

            UnhookGrabInteractable();
            grabInteractable = interactable;

            if (grabInteractable == null)
                return;

            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
            grabInteractable.activated.RemoveListener(OnActivated);
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
            grabInteractable.activated.AddListener(OnActivated);
        }

        private void UnhookGrabInteractable()
        {
            if (grabInteractable == null)
                return;

            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
            grabInteractable.activated.RemoveListener(OnActivated);
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            VrPaintCanDebugLog.Write($"selectEntered paintcan='{name}' interactor={DescribeTransform(args != null && args.interactorObject != null ? args.interactorObject.transform : null)} {BuildStateSummary()}");
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            VrPaintCanDebugLog.Write($"selectExited paintcan='{name}' interactor={DescribeTransform(args != null && args.interactorObject != null ? args.interactorObject.transform : null)} {BuildStateSummary()}");
        }

        private void OnActivated(ActivateEventArgs args)
        {
            VrPaintCanDebugLog.Write($"activated paintcan='{name}' interactor={DescribeTransform(args != null && args.interactorObject != null ? args.interactorObject.transform : null)} {BuildStateSummary()}");
        }

        private void RefreshCachedReferences(bool force)
        {
            if (!force && Time.unscaledTime < nextPlayerRefreshTime)
                return;

            nextPlayerRefreshTime = Time.unscaledTime + 1f;

            if (rb == null)
                rb = GetComponent<Rigidbody>();

            paintCanColliders = GetComponentsInChildren<Collider>(true);
            mainCamera = Camera.main;
            playerRigRoot = FindPlayerRigRoot();
            playerController = playerRigRoot != null ? playerRigRoot.GetComponent<CharacterController>() : null;
            playerColliders.Clear();

            if (playerRigRoot != null)
                playerColliders.AddRange(playerRigRoot.GetComponentsInChildren<Collider>(true));

            CharacterController[] controllers = FindObjectsOfType<CharacterController>(true);

            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null && !playerColliders.Contains(controllers[i]))
                    playerColliders.Add(controllers[i]);
            }

            if (playerRigRoot != null && lastPlayerPosition == Vector3.zero)
                lastPlayerPosition = playerRigRoot.position;
        }

        private Transform FindPlayerRigRoot()
        {
            if (mainCamera != null)
            {
                XROrigin origin = mainCamera.GetComponentInParent<XROrigin>();

                if (origin != null)
                    return origin.transform;

                Transform namedRoot = FindNamedRigParent(mainCamera.transform);

                if (namedRoot != null)
                    return namedRoot;
            }

            XROrigin sceneOrigin = FindObjectOfType<XROrigin>(true);

            if (sceneOrigin != null)
                return sceneOrigin.transform;

            CharacterController[] controllers = FindObjectsOfType<CharacterController>(true);

            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] == null)
                    continue;

                Transform namedRoot = FindNamedRigParent(controllers[i].transform);

                if (namedRoot != null)
                    return namedRoot;
            }

            return null;
        }

        private Transform FindNamedRigParent(Transform source)
        {
            Transform current = source;

            while (current != null)
            {
                if (current.name.IndexOf("XR Origin", StringComparison.OrdinalIgnoreCase) >= 0
                    || current.name.IndexOf("XR Rig", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private ContactProbe ProbePlayerContact()
        {
            ContactProbe probe = new ContactProbe
            {
                minDistance = float.PositiveInfinity,
                nearestPaintCanCollider = "none",
                nearestPlayerCollider = "none"
            };

            for (int i = 0; i < paintCanColliders.Length; i++)
            {
                Collider paintCanCollider = paintCanColliders[i];

                if (!IsUsableCollider(paintCanCollider))
                    continue;

                for (int p = 0; p < playerColliders.Count; p++)
                {
                    Collider playerCollider = playerColliders[p];

                    if (!IsUsableCollider(playerCollider) || playerCollider.transform.IsChildOf(transform))
                        continue;

                    bool ignored = Physics.GetIgnoreCollision(paintCanCollider, playerCollider);
                    if (ignored)
                        probe.ignoredPairs++;
                    else
                        probe.nonIgnoredPairs++;

                    float distance = EstimateColliderDistance(paintCanCollider, playerCollider);
                    if (distance < probe.minDistance)
                    {
                        probe.minDistance = distance;
                        probe.nearestPaintCanCollider = paintCanCollider.name;
                        probe.nearestPlayerCollider = playerCollider.name;
                        probe.nearestPairIgnored = ignored;
                    }

                    if (TryComputePenetration(paintCanCollider, playerCollider, out float penetration))
                    {
                        probe.overlapPairs++;

                        if (penetration > probe.maxPenetration)
                            probe.maxPenetration = penetration;
                    }
                }
            }

            if (float.IsPositiveInfinity(probe.minDistance))
                probe.minDistance = -1f;

            return probe;
        }

        private bool PlayerMovedFast()
        {
            if (playerRigRoot == null)
                return false;

            Vector3 current = playerRigRoot.position;
            float movement = Vector3.Distance(current, lastPlayerPosition);
            lastPlayerPosition = current;
            return movement > PlayerMoveWarningDistance;
        }

        private float EstimateColliderDistance(Collider a, Collider b)
        {
            Vector3 pointOnA = a.ClosestPoint(b.bounds.center);
            Vector3 pointOnB = b.ClosestPoint(pointOnA);
            return Vector3.Distance(pointOnA, pointOnB);
        }

        private bool TryComputePenetration(Collider a, Collider b, out float distance)
        {
            distance = 0f;

            try
            {
                Vector3 direction;
                return Physics.ComputePenetration(
                    a,
                    a.transform.position,
                    a.transform.rotation,
                    b,
                    b.transform.position,
                    b.transform.rotation,
                    out direction,
                    out distance);
            }
            catch (Exception exception)
            {
                VrPaintCanDebugLog.Write($"penetrationProbeFailed paintcan='{name}' a='{a.name}' b='{b.name}' error='{exception.Message}'");
                return false;
            }
        }

        private bool IsPlayerCollider(Collider other)
        {
            if (other == null)
                return false;

            if (playerColliders.Contains(other))
                return true;

            Transform root = playerRigRoot != null ? playerRigRoot : FindPlayerRigRoot();
            return root != null && other.transform.IsChildOf(root);
        }

        private int CountIgnoredPairsWith(Collider other)
        {
            if (other == null)
                return 0;

            int ignored = 0;

            for (int i = 0; i < paintCanColliders.Length; i++)
            {
                if (paintCanColliders[i] != null && Physics.GetIgnoreCollision(paintCanColliders[i], other))
                    ignored++;
            }

            return ignored;
        }

        private bool IsHeld()
        {
            return grabInteractable != null && grabInteractable.isSelected;
        }

        private string BuildStateSummary()
        {
            Vector3 paintCanVelocity = rb != null ? rb.velocity : Vector3.zero;
            Vector3 paintCanAngularVelocity = rb != null ? rb.angularVelocity : Vector3.zero;
            Vector3 rigPosition = playerRigRoot != null ? playerRigRoot.position : Vector3.zero;
            Vector3 cameraPosition = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
            string controllerState = playerController != null
                ? $"ccEnabled={playerController.enabled} ccGrounded={playerController.isGrounded} ccVelocity={FormatVector(playerController.velocity)} ccCenter={FormatVector(playerController.center)} ccHeight={playerController.height:F3} ccRadius={playerController.radius:F3}"
                : "cc=null";
            string bodyState = rb != null
                ? $"rbKinematic={rb.isKinematic} rbGravity={rb.useGravity} rbDetect={rb.detectCollisions} rbCollisionMode={rb.collisionDetectionMode} rbMaxDepen={rb.maxDepenetrationVelocity:F3}"
                : "rb=null";
            int selectingCount = grabInteractable != null ? grabInteractable.interactorsSelecting.Count : 0;

            return $"held={IsHeld()} selecting={selectingCount} paintPos={FormatVector(transform.position)} paintVel={FormatVector(paintCanVelocity)} paintAngVel={FormatVector(paintCanAngularVelocity)} {bodyState} rig='{DescribeTransform(playerRigRoot)}' rigPos={FormatVector(rigPosition)} camPos={FormatVector(cameraPosition)} {controllerState} playerColliders={playerColliders.Count} paintColliders={paintCanColliders.Length}";
        }

        private void LogPaintCanSetup()
        {
            ContactProbe probe = ProbePlayerContact();
            VrPaintCanDebugLog.Write($"setup paintcan='{name}' logPath='{VrPaintCanDebugLog.LogPath}' {BuildStateSummary()} contact={probe}");
        }

        private static bool IsUsableCollider(Collider collider)
        {
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string DescribeCollider(Collider collider)
        {
            if (collider == null)
                return "null";

            return $"'{collider.name}' root='{collider.transform.root.name}' trigger={collider.isTrigger} type={collider.GetType().Name}";
        }

        private static string DescribeTransform(Transform target)
        {
            if (target == null)
                return "null";

            return $"{target.name}/root={target.root.name}";
        }

        private static string FormatVector(Vector3 value)
        {
            return VrPaintCanDebugLog.FormatVector(value);
        }

        private struct ContactProbe
        {
            public int ignoredPairs;
            public int nonIgnoredPairs;
            public int overlapPairs;
            public float maxPenetration;
            public float minDistance;
            public bool nearestPairIgnored;
            public string nearestPaintCanCollider;
            public string nearestPlayerCollider;

            public override string ToString()
            {
                return $"ignoredPairs={ignoredPairs} nonIgnoredPairs={nonIgnoredPairs} overlapPairs={overlapPairs} maxPen={maxPenetration:F4} minDist={minDistance:F4} nearest='{nearestPaintCanCollider}'-'{nearestPlayerCollider}' nearestIgnored={nearestPairIgnored}";
            }
        }
    }

    public sealed class VrCharacterControllerSafetyGuard : MonoBehaviour
    {
        private const float RefreshInterval = 0.5f;
        private const float LowestExpectedRigY = -0.05f;
        private const float UnexpectedDisabledMoveDistance = 0.35f;
        private const float DisabledLogInterval = 0.25f;

        private static VrCharacterControllerSafetyGuard instance;
        private static float temporaryDisableAllowedUntil;
        private static string temporaryDisableReason = "none";

        private Transform xrOrigin;
        private CharacterController characterController;
        private Camera mainCamera;
        private bool hasLastSafePose;
        private Vector3 lastSafePosition;
        private Quaternion lastSafeRotation;
        private float nextRefreshTime;
        private float nextDisabledLogTime;

        public static void EnsureInScene()
        {
            if (instance != null)
                return;

            GameObject host = new GameObject("VR CharacterController Safety Guard");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<VrCharacterControllerSafetyGuard>();
            VrPaintCanDebugLog.Write("ccSafetyGuard created");
        }

        public static void AllowTemporaryDisable(string reason, float seconds)
        {
            EnsureInScene();

            float until = Time.unscaledTime + Mathf.Max(0.02f, seconds);
            if (until >= temporaryDisableAllowedUntil)
            {
                temporaryDisableAllowedUntil = until;
                temporaryDisableReason = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason;
            }

            VrPaintCanDebugLog.Write($"ccTemporaryDisableAllowed reason='{temporaryDisableReason}' until={temporaryDisableAllowedUntil:F3}");
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            RefreshReferences(true);
        }

        private void LateUpdate()
        {
            if (!XRSettings.isDeviceActive)
                return;

            if (Time.unscaledTime >= nextRefreshTime || xrOrigin == null || characterController == null)
                RefreshReferences(false);

            if (xrOrigin == null || characterController == null || !characterController.gameObject.activeInHierarchy)
                return;

            if (characterController.enabled)
            {
                if (ShouldResetToSafePose())
                {
                    xrOrigin.SetPositionAndRotation(lastSafePosition, lastSafeRotation);
                    Physics.SyncTransforms();
                    VrPaintCanDebugLog.Write(
                        $"ccUnsafePoseRecovered origin='{xrOrigin.name}' originPos={VrPaintCanDebugLog.FormatVector(xrOrigin.position)} " +
                        $"lastSafe={VrPaintCanDebugLog.FormatVector(lastSafePosition)} camPos={VrPaintCanDebugLog.FormatVector(mainCamera != null ? mainCamera.transform.position : Vector3.zero)}");
                }

                RememberSafePose();
                return;
            }

            if (Time.unscaledTime <= temporaryDisableAllowedUntil)
            {
                LogAllowedDisabledState();
                return;
            }

            bool resetPose = ShouldResetToSafePose();

            if (resetPose)
                xrOrigin.SetPositionAndRotation(lastSafePosition, lastSafeRotation);

            characterController.enabled = true;
            Physics.SyncTransforms();

            VrPaintCanDebugLog.Write(
                $"ccUnexpectedDisabledRecovered resetPose={resetPose} allowedReason='{temporaryDisableReason}' " +
                $"origin='{xrOrigin.name}' originPos={VrPaintCanDebugLog.FormatVector(xrOrigin.position)} " +
                $"lastSafe={VrPaintCanDebugLog.FormatVector(lastSafePosition)} camPos={VrPaintCanDebugLog.FormatVector(mainCamera != null ? mainCamera.transform.position : Vector3.zero)}");

            RememberSafePose();
        }

        private void RefreshReferences(bool force)
        {
            if (!force && Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime + RefreshInterval;
            mainCamera = Camera.main;

            XROrigin origin = mainCamera != null ? mainCamera.GetComponentInParent<XROrigin>() : null;

            if (origin == null)
                origin = FindObjectOfType<XROrigin>(true);

            if (origin == null)
            {
                xrOrigin = null;
                characterController = null;
                return;
            }

            xrOrigin = origin.transform;
            characterController = xrOrigin.GetComponent<CharacterController>();
            RememberSafePose();
        }

        private void RememberSafePose()
        {
            if (xrOrigin == null || characterController == null || !characterController.enabled)
                return;

            if (!IsFinite(xrOrigin.position) || xrOrigin.position.y < LowestExpectedRigY)
                return;

            hasLastSafePose = true;
            lastSafePosition = xrOrigin.position;
            lastSafeRotation = xrOrigin.rotation;
        }

        private bool ShouldResetToSafePose()
        {
            if (!hasLastSafePose || xrOrigin == null || !IsFinite(xrOrigin.position))
                return false;

            return xrOrigin.position.y < LowestExpectedRigY
                || Vector3.Distance(xrOrigin.position, lastSafePosition) > UnexpectedDisabledMoveDistance;
        }

        private void LogAllowedDisabledState()
        {
            if (Time.unscaledTime < nextDisabledLogTime)
                return;

            nextDisabledLogTime = Time.unscaledTime + DisabledLogInterval;
            VrPaintCanDebugLog.Write(
                $"ccDisabledAllowed reason='{temporaryDisableReason}' origin='{(xrOrigin != null ? xrOrigin.name : "null")}' " +
                $"originPos={VrPaintCanDebugLog.FormatVector(xrOrigin != null ? xrOrigin.position : Vector3.zero)}");
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
