using System;
using UnityEngine;
using UnityEngine.UI;

namespace MagXR
{
    /// <summary>
    /// Step 1.3 & Step 4: Floating World-Space Confirmation Canvas.
    /// Parented to the VR controller or loupe.
    /// Displays parsed voice commands received from the LangGraph agent interrupt() breakpoint.
    /// Dispatches user confirmation or cancellation back to Python over WebSockets.
    /// </summary>
    public class WorldSpaceConfirmationCanvas : MonoBehaviour
    {
        [Header("UI Element References")]
        [SerializeField] private GameObject canvasRoot;
        [SerializeField] private Text summaryText;
        [SerializeField] private Text targetText;
        [SerializeField] private Text zoomLevelText;
        [SerializeField] private Text speedText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        [Header("Networking")]
        [SerializeField] private MagXRWebSocketClient webSocketClient;

        [Header("VR Follow Options")]
        [Tooltip("Transform to follow (e.g., Right Controller or Loupe Mesh)")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 followOffset = new Vector3(0.0f, 0.15f, 0.25f);
        [SerializeField] private bool faceCamera = true;

        private string currentThreadId;
        private Camera vrCamera;

        private void Awake()
        {
            if (canvasRoot == null) canvasRoot = gameObject;
            vrCamera = Camera.main;

            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelClicked);

            // Hide initially until a confirmation request arrives
            HideConfirmation();
        }

        private void LateUpdate()
        {
            // Follow target transform smoothly
            if (followTarget != null && canvasRoot.activeSelf)
            {
                transform.position = followTarget.position + followTarget.TransformDirection(followOffset);
                if (faceCamera && vrCamera != null)
                {
                    transform.LookAt(transform.position + vrCamera.transform.rotation * Vector3.forward,
                                     vrCamera.transform.rotation * Vector3.up);
                }
            }

            // Keyboard / Controller Shortcuts for testing in Editor or VR
            if (canvasRoot.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.Return))
                {
                    OnConfirmClicked();
                }
                else if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.Escape))
                {
                    OnCancelClicked();
                }
            }
        }

        /// <summary>
        /// Displays the confirmation dialog with parameters extracted by Gemini.
        /// </summary>
        public void ShowConfirmation(MagXRWebSocketClient.ConfirmationRequestPayload payload)
        {
            currentThreadId = payload.thread_id;

            if (summaryText != null)
                summaryText.text = string.IsNullOrEmpty(payload.summary) ? "Confirm Magnification Action?" : payload.summary;

            if (targetText != null)
                targetText.text = $"Target: {(string.IsNullOrEmpty(payload.target_object) ? "None" : payload.target_object)}";

            if (zoomLevelText != null)
                zoomLevelText.text = $"Zoom: {payload.zoom_factor:F1}x ({payload.direction?.ToUpper()})";

            if (speedText != null)
                speedText.text = $"Speed: {payload.speed}";

            canvasRoot.SetActive(true);
            Debug.Log($"[MagXR UI] Showing World-Space Confirmation for thread [{currentThreadId}]");
        }

        public void HideConfirmation()
        {
            if (canvasRoot != null)
            {
                canvasRoot.SetActive(false);
            }
            currentThreadId = null;
        }

        public void OnConfirmClicked()
        {
            if (string.IsNullOrEmpty(currentThreadId)) return;

            Debug.Log($"[MagXR UI] User CONFIRMED action for thread [{currentThreadId}]");
            if (webSocketClient != null)
            {
                webSocketClient.SendConfirmationResponse(currentThreadId, true);
            }

            HideConfirmation();
        }

        public void OnCancelClicked()
        {
            if (string.IsNullOrEmpty(currentThreadId)) return;

            Debug.Log($"[MagXR UI] User CANCELLED action for thread [{currentThreadId}]");
            if (webSocketClient != null)
            {
                webSocketClient.SendConfirmationResponse(currentThreadId, false);
            }

            HideConfirmation();
        }
    }
}
