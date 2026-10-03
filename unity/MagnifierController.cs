using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagXR
{
    /// <summary>
    /// Supported stereoscopic magnification configurations in MagXR (Phase 1 & Step 5).
    /// </summary>
    public enum MagnificationMode
    {
        SelectiveZoom, // Virtual loupe rendered via secondary camera + RenderTexture
        ImageZoom,     // Magnification applied directly to the 2D map plane
        FullViewZoom   // Magnification altering global camera view frustum (FOV)
    }

    /// <summary>
    /// Step 5 Action Item 3: Receives JSON payloads from the WebSocket AI Agent bridge
    /// and applies smooth view transitions via Mathf.SmoothDamp and Vector3.Lerp.
    /// Also manages Spatial Checkpoints (Step 1.2).
    /// </summary>
    public class MagnifierController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private MagnificationMode activeMode = MagnificationMode.SelectiveZoom;

        [Header("References")]
        [Tooltip("Main VR Headset Camera")]
        [SerializeField] private Camera mainCamera;

        [Tooltip("Secondary Camera for Selective Zoom Virtual Loupe")]
        [SerializeField] private Camera loupeSecondaryCamera;

        [Tooltip("Transform of the physical/virtual Loupe object")]
        [SerializeField] private Transform loupeMeshTransform;

        [Tooltip("Target 2D Map Plane Transform (for Image Zoom mode)")]
        [SerializeField] private Transform mapPlaneTransform;

        [Header("Zoom Parameters")]
        [Range(1.0f, 10.0f)]
        [SerializeField] private float currentZoom = 1.0f;
        [SerializeField] private float targetZoom = 1.0f;
        [SerializeField] private float defaultFov = 90.0f;
        [SerializeField] private Vector3 defaultMapScale = Vector3.one;

        [Header("Smooth Transition Settings")]
        [SerializeField] private float defaultTransitionDuration = 0.5f;

        // Damping velocities
        private float zoomVelocity = 0.0f;
        private Vector3 moveVelocity = Vector3.zero;

        // Target position for Move commands
        private bool isMovingToTarget = false;
        private Vector3 targetMovePosition;
        private float moveTransitionDuration = 0.5f;

        // Spatial Checkpoint Storage (Step 1.2)
        [System.Serializable]
        public struct SpatialCheckpoint
        {
            public string name;
            public Vector3 cameraPosition;
            public Quaternion cameraRotation;
            public float zoomFactor;
            public MagnificationMode mode;
            public double timestamp;
        }

        private readonly Dictionary<string, SpatialCheckpoint> checkpoints = new Dictionary<string, SpatialCheckpoint>();

        private void Awake()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    defaultFov = mainCamera.fieldOfView;
                }
            }

            if (mapPlaneTransform != null)
            {
                defaultMapScale = mapPlaneTransform.localScale;
            }
        }

        private void Update()
        {
            // Apply smooth zoom transition every frame via Mathf.SmoothDamp
            ApplySmoothZoom();

            // Apply smooth position transition if moving to a target object
            if (isMovingToTarget)
            {
                ApplySmoothMove();
            }
        }

        /// <summary>
        /// Entry point: Dispatches parsed action payloads received over WebSockets.
        /// </summary>
        public void ProcessActionPayload(string jsonPayload)
        {
            try
            {
                var actionData = JsonUtility.FromJson<ActionPayloadWrapper>(jsonPayload);
                if (actionData == null || string.IsNullOrEmpty(actionData.action))
                {
                    Debug.LogWarning("[MagXR] Received invalid or empty action payload: " + jsonPayload);
                    return;
                }

                Debug.Log($"[MagXR] Processing Action: {actionData.action}");

                switch (actionData.action.ToUpper())
                {
                    case "SET_ZOOM":
                        HandleSetZoom(actionData.factor, actionData.duration > 0 ? actionData.duration : defaultTransitionDuration);
                        break;

                    case "MOVE_TO_TARGET":
                        HandleMoveToTarget(actionData.target, actionData.duration > 0 ? actionData.duration : defaultTransitionDuration);
                        break;

                    case "RESET_ZOOM":
                        HandleResetZoom(actionData.duration > 0 ? actionData.duration : defaultTransitionDuration);
                        break;

                    case "CREATE_CHECKPOINT":
                        HandleCreateCheckpoint(actionData.name, actionData.timestamp);
                        break;

                    case "RESTORE_CHECKPOINT":
                        HandleRestoreCheckpoint(actionData.name);
                        break;

                    case "CONFIRMATION_CANCELLED":
                        Debug.Log("[MagXR] Action was cancelled by user in VR.");
                        break;

                    default:
                        Debug.LogWarning($"[MagXR] Unrecognized action: {actionData.action}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MagXR] Error executing payload '{jsonPayload}': {ex.Message}");
            }
        }

        #region Action Handlers

        private void HandleSetZoom(float factor, float duration)
        {
            targetZoom = Mathf.Clamp(factor, 1.0f, 10.0f);
            defaultTransitionDuration = Mathf.Max(0.05f, duration);
            Debug.Log($"[MagXR] Target Zoom set to {targetZoom:F2}x over {defaultTransitionDuration:F2}s");
        }

        private void HandleMoveToTarget(string targetObjectName, float duration)
        {
            if (string.IsNullOrEmpty(targetObjectName)) return;

            // Search for target object in the scene
            GameObject targetObj = GameObject.Find(targetObjectName);
            if (targetObj == null)
            {
                // Fuzzy search by substring
                foreach (var go in FindObjectsOfType<GameObject>())
                {
                    if (go.name.IndexOf(targetObjectName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetObj = go;
                        break;
                    }
                }
            }

            if (targetObj != null)
            {
                targetMovePosition = targetObj.transform.position;
                moveTransitionDuration = Mathf.Max(0.05f, duration);
                isMovingToTarget = true;
                Debug.Log($"[MagXR] Moving magnifier to '{targetObj.name}' at {targetMovePosition} over {moveTransitionDuration:F2}s");
            }
            else
            {
                Debug.LogWarning($"[MagXR] Target object '{targetObjectName}' not found in scene.");
            }
        }

        private void HandleResetZoom(float duration)
        {
            targetZoom = 1.0f;
            defaultTransitionDuration = Mathf.Max(0.05f, duration);
            isMovingToTarget = false;
            Debug.Log($"[MagXR] Resetting magnification to default 1.0x view.");
        }

        private void HandleCreateCheckpoint(string checkpointName, double timestamp)
        {
            string cpName = string.IsNullOrEmpty(checkpointName) ? $"Checkpoint_{checkpoints.Count + 1}" : checkpointName;

            Vector3 camPos = mainCamera != null ? mainCamera.transform.position : transform.position;
            Quaternion camRot = mainCamera != null ? mainCamera.transform.rotation : transform.rotation;

            var cp = new SpatialCheckpoint
            {
                name = cpName,
                cameraPosition = camPos,
                cameraRotation = camRot,
                zoomFactor = currentZoom,
                mode = activeMode,
                timestamp = timestamp
            };

            checkpoints[cpName] = cp;
            Debug.Log($"[MagXR] Created spatial checkpoint '{cpName}' (Zoom: {currentZoom:F2}x, Position: {camPos})");
        }

        public void HandleRestoreCheckpoint(string checkpointName)
        {
            if (checkpoints.TryGetValue(checkpointName, out var cp))
            {
                activeMode = cp.mode;
                targetZoom = cp.zoomFactor;
                if (mainCamera != null)
                {
                    mainCamera.transform.position = cp.cameraPosition;
                    mainCamera.transform.rotation = cp.cameraRotation;
                }
                Debug.Log($"[MagXR] Restored checkpoint '{checkpointName}' (Zoom: {targetZoom:F2}x)");
            }
            else
            {
                Debug.LogWarning($"[MagXR] Checkpoint '{checkpointName}' does not exist.");
            }
        }

        #endregion

        #region Smooth View Transitions (Mathf.SmoothDamp & Vector3.Lerp)

        private void ApplySmoothZoom()
        {
            // Smoothly damp currentZoom towards targetZoom
            currentZoom = Mathf.SmoothDamp(currentZoom, targetZoom, ref zoomVelocity, defaultTransitionDuration);

            // Apply currentZoom to active magnification mode
            switch (activeMode)
            {
                case MagnificationMode.SelectiveZoom:
                    ApplySelectiveZoom(currentZoom);
                    break;

                case MagnificationMode.ImageZoom:
                    ApplyImageZoom(currentZoom);
                    break;

                case MagnificationMode.FullViewZoom:
                    ApplyFullViewZoom(currentZoom);
                    break;
            }
        }

        private void ApplySelectiveZoom(float zoom)
        {
            // Adjust secondary camera FOV to simulate magnification through RenderTexture
            if (loupeSecondaryCamera != null)
            {
                loupeSecondaryCamera.fieldOfView = defaultFov / zoom;
            }
        }

        private void ApplyImageZoom(float zoom)
        {
            // Scale the 2D map plane
            if (mapPlaneTransform != null)
            {
                mapPlaneTransform.localScale = defaultMapScale * zoom;
            }
        }

        private void ApplyFullViewZoom(float zoom)
        {
            // Adjust main camera view frustum FOV
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = defaultFov / zoom;
            }
        }

        private void ApplySmoothMove()
        {
            Transform activeTransform = (activeMode == MagnificationMode.SelectiveZoom && loupeMeshTransform != null)
                ? loupeMeshTransform
                : (mainCamera != null ? mainCamera.transform : transform);

            // Smoothly move position using Vector3.SmoothDamp
            activeTransform.position = Vector3.SmoothDamp(
                activeTransform.position,
                targetMovePosition,
                ref moveVelocity,
                moveTransitionDuration
            );

            // Check if reached destination
            if (Vector3.Distance(activeTransform.position, targetMovePosition) < 0.01f)
            {
                isMovingToTarget = false;
            }
        }

        #endregion

        #region Public Setters

        public void SetMagnificationMode(MagnificationMode mode)
        {
            activeMode = mode;
            Debug.Log($"[MagXR] Magnification Mode changed to: {activeMode}");
        }

        public float GetCurrentZoom() => currentZoom;
        public MagnificationMode GetActiveMode() => activeMode;

        #endregion

        [System.Serializable]
        private class ActionPayloadWrapper
        {
            public string action;
            public float factor = 1.0f;
            public string speed = "normal";
            public float duration = 0.5f;
            public string target;
            public string name;
            public double timestamp;
        }
    }
}
