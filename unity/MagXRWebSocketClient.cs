using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MagXR
{
    /// <summary>
    /// Step 5: Unity C# WebSocket Client connecting to the Python AI Agent server (ws://127.0.0.1:8080).
    /// Thread-safe: Queues incoming messages and dispatches them on Unity's main thread in Update().
    /// Compatible with Meta Quest 3 over Quest Link or Wi-Fi LAN.
    /// </summary>
    public class MagXRWebSocketClient : MonoBehaviour
    {
        [Header("Connection Settings")]
        [Tooltip("WebSocket Server URI (e.g., ws://127.0.0.1:8080 or ws://192.168.1.X:8080 for Quest over Wi-Fi)")]
        [SerializeField] private string serverUri = "ws://127.0.0.1:8080";
        [SerializeField] private bool autoConnectOnStart = true;
        [SerializeField] private float reconnectInterval = 3.0f;

        [Header("Component References")]
        [SerializeField] private MagnifierController magnifierController;
        [SerializeField] private WorldSpaceConfirmationCanvas confirmationCanvas;

        // WebSocket state
        private ClientWebSocket webSocket;
        private CancellationTokenSource cts;
        private bool isConnecting = false;

        // Thread-safe dispatch queue for Unity main thread
        private readonly ConcurrentQueue<string> mainThreadQueue = new ConcurrentQueue<string>();

        // Events
        public event Action<bool> OnConnectionStatusChanged;
        public event Action<string> OnRawMessageReceived;

        public bool IsConnected => webSocket != null && webSocket.State == WebSocketState.Open;

        private void Start()
        {
            if (magnifierController == null)
            {
                magnifierController = FindObjectOfType<MagnifierController>();
            }

            if (confirmationCanvas == null)
            {
                confirmationCanvas = FindObjectOfType<WorldSpaceConfirmationCanvas>();
            }

            if (autoConnectOnStart)
            {
                Connect();
            }
        }

        private void Update()
        {
            // Drain queue on Unity's main thread
            while (mainThreadQueue.TryDequeue(out string message))
            {
                DispatchMessageOnMainThread(message);
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        #region Connection Management

        public void Connect()
        {
            if (isConnecting || IsConnected) return;

            cts = new CancellationTokenSource();
            Task.Run(() => ConnectAsync(serverUri, cts.Token));
        }

        public void Disconnect()
        {
            cts?.Cancel();
            if (webSocket != null)
            {
                try
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None).Wait(1000);
                    }
                    webSocket.Dispose();
                }
                catch { }
                webSocket = null;
            }
        }

        private async Task ConnectAsync(string uri, CancellationToken token)
        {
            isConnecting = true;
            Debug.Log($"[MagXR WebSocket] Connecting to {uri}...");

            while (!token.IsCancellationRequested && !IsConnected)
            {
                try
                {
                    webSocket = new ClientWebSocket();
                    await webSocket.ConnectAsync(new Uri(uri), token);

                    Debug.Log($"[MagXR WebSocket] Connected successfully to {uri}");
                    mainThreadQueue.Enqueue("{\"__internal_type__\": \"CONNECTED\"}");

                    // Begin receive loop
                    await ReceiveLoopAsync(webSocket, token);
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Debug.LogWarning($"[MagXR WebSocket] Connection failed: {ex.Message}. Retrying in {reconnectInterval}s...");
                        mainThreadQueue.Enqueue("{\"__internal_type__\": \"DISCONNECTED\"}");
                        await Task.Delay((int)(reconnectInterval * 1000), token);
                    }
                }
            }

            isConnecting = false;
        }

        private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token)
        {
            var buffer = new byte[8192];

            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var sb = new StringBuilder();
                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server requested closure", token);
                        mainThreadQueue.Enqueue("{\"__internal_type__\": \"DISCONNECTED\"}");
                        return;
                    }

                    string chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    sb.Append(chunk);

                } while (!result.EndOfMessage);

                string fullMessage = sb.ToString();
                mainThreadQueue.Enqueue(fullMessage);
            }
        }

        #endregion

        #region Dispatcher (Main Thread)

        private void DispatchMessageOnMainThread(string message)
        {
            OnRawMessageReceived?.Invoke(message);

            // Handle internal connection events
            if (message.Contains("\"__internal_type__\": \"CONNECTED\""))
            {
                OnConnectionStatusChanged?.Invoke(true);
                return;
            }
            if (message.Contains("\"__internal_type__\": \"DISCONNECTED\""))
            {
                OnConnectionStatusChanged?.Invoke(false);
                return;
            }

            try
            {
                var envelope = JsonUtility.FromJson<MessageEnvelope>(message);
                if (envelope == null) return;

                string type = (envelope.type ?? "").ToUpper();

                // 1. Step 4 Human Confirmation Breakpoint emitted from LangGraph interrupt()
                if (type == "CONFIRMATION_REQUEST")
                {
                    var confReq = JsonUtility.FromJson<ConfirmationRequestPayload>(message);
                    Debug.Log($"[MagXR] Received Confirmation Request: {confReq.summary}");
                    if (confirmationCanvas != null)
                    {
                        confirmationCanvas.ShowConfirmation(confReq);
                    }
                }
                // 2. Step 5 Action execution payloads dispatched to MagnifierController
                else if (!string.IsNullOrEmpty(envelope.action))
                {
                    if (magnifierController != null)
                    {
                        magnifierController.ProcessActionPayload(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MagXR] Error dispatching message: {ex.Message}\nRaw message: {message}");
            }
        }

        #endregion

        #region Public Outbound Sending

        /// <summary>
        /// Sends user confirmation response (from VR controller trigger/button) back to Python LangGraph agent.
        /// </summary>
        public void SendConfirmationResponse(string threadId, bool confirmed)
        {
            var resp = new ConfirmationResponseData
            {
                type = "CONFIRMATION_RESPONSE",
                thread_id = threadId,
                confirmed = confirmed
            };

            string json = JsonUtility.ToJson(resp);
            SendMessageAsync(json);
            Debug.Log($"[MagXR] Sent Confirmation Response: thread={threadId}, confirmed={confirmed}");
        }

        /// <summary>
        /// Sends a text command to be processed by the LangGraph agent.
        /// </summary>
        public void SendCommandText(string commandText, string threadId = null)
        {
            var cmd = new CommandTextData
            {
                type = "COMMAND_TEXT",
                command = commandText,
                thread_id = string.IsNullOrEmpty(threadId) ? Guid.NewGuid().ToString("N").Substring(0, 8) : threadId
            };

            string json = JsonUtility.ToJson(cmd);
            SendMessageAsync(json);
            Debug.Log($"[MagXR] Sent Command: \"{commandText}\"");
        }

        public async void SendMessageAsync(string jsonMessage)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[MagXR] Cannot send message: WebSocket is not connected.");
                return;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(jsonMessage);
                await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MagXR] Send failed: {ex.Message}");
            }
        }

        #endregion

        #region Data Models

        [System.Serializable]
        private class MessageEnvelope
        {
            public string type;
            public string action;
        }

        [System.Serializable]
        public class ConfirmationRequestPayload
        {
            public string type;
            public string thread_id;
            public string summary;
            public string target_object;
            public string direction;
            public float zoom_factor;
            public string speed;
            public string status;
        }

        [System.Serializable]
        private class ConfirmationResponseData
        {
            public string type;
            public string thread_id;
            public bool confirmed;
        }

        [System.Serializable]
        private class CommandTextData
        {
            public string type;
            public string command;
            public string thread_id;
        }

        #endregion
    }
}
