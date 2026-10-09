import asyncio
import json
import logging
import uuid
import sys
from typing import Set, Dict, Any, Optional

import websockets
from websockets.server import WebSocketServerProtocol

from src.magxr.agent import MagXROrchestrator
from src.magxr.prompt_parser import MagnificationPrompt

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s"
)
logger = logging.getLogger("MagXR.Server")


class MagXRWebSocketServer:
    """
    Step 5: Network Communication Bridge (WebSockets)
    Dispatches parsed instructions to Unity and handles real-time scene updates.
    """

    def __init__(self, host: str = "127.0.0.1", port: int = 8080):
        self.host = host
        self.port = port
        self.orchestrator = MagXROrchestrator()
        self.connected_clients: Set[WebSocketServerProtocol] = set()
        self.server = None
        self._running = False
        # Map pending threads waiting for human confirmation
        self.pending_confirmations: Dict[str, Dict[str, Any]] = {}

    async def register_client(self, websocket: WebSocketServerProtocol):
        """Registers a newly connected Unity client."""
        self.connected_clients.add(websocket)
        client_ip = websocket.remote_address
        logger.info(f"Unity client connected from {client_ip}. Total clients: {len(self.connected_clients)}")

        # Send welcome handshake
        handshake = {
            "type": "SERVER_STATUS",
            "status": "CONNECTED",
            "version": "1.0",
            "message": "Connected to MagXR AI Agent WebSocket Server"
        }
        await websocket.send(json.dumps(handshake))

    async def unregister_client(self, websocket: WebSocketServerProtocol):
        """Removes a disconnected Unity client."""
        self.connected_clients.discard(websocket)
        logger.info(f"Unity client disconnected. Total clients: {len(self.connected_clients)}")

    async def broadcast(self, message_dict: Dict[str, Any]):
        """Broadcasts a JSON message to all connected Unity clients."""
        if not self.connected_clients:
            logger.warning("No Unity clients connected to receive broadcast.")
            return

        payload = json.dumps(message_dict)
        logger.info(f"Broadcasting to {len(self.connected_clients)} client(s): {payload}")
        
        # Broadcast concurrently to all clients
        await asyncio.gather(
            *[client.send(payload) for client in self.connected_clients],
            return_exceptions=True
        )

    async def send_to_client(self, websocket: WebSocketServerProtocol, message_dict: Dict[str, Any]):
        """Sends a JSON message to a specific client."""
        try:
            payload = json.dumps(message_dict)
            await websocket.send(payload)
        except Exception as e:
            logger.error(f"Failed to send to client: {e}")

    async def handle_command_text(self, command_text: str, thread_id: Optional[str] = None):
        """
        Processes a voice/text command through Step 4 LangGraph agent.
        Emits confirmation request to Unity via interrupt().
        """
        thread_id = thread_id or f"session-{uuid.uuid4().hex[:8]}"
        logger.info(f"Processing command for thread [{thread_id}]: \"{command_text}\"")

        # Run Step 4 up to interrupt() asynchronously in thread pool
        start_res = await asyncio.to_thread(
            self.orchestrator.start_command,
            command_text,
            thread_id=thread_id
        )

        conf_payload = start_res.get("confirmation_payload", {})
        self.pending_confirmations[thread_id] = conf_payload

        # Emit Confirmation Request to Unity World-Space Canvas
        canvas_message = {
            "type": "CONFIRMATION_REQUEST",
            "thread_id": thread_id,
            "summary": conf_payload.get("summary", command_text),
            "target_object": conf_payload.get("target_object"),
            "direction": conf_payload.get("direction", "in"),
            "zoom_factor": conf_payload.get("zoom_factor", 1.0),
            "speed": conf_payload.get("speed", "normal"),
            "planned_tools": conf_payload.get("planned_tools", []),
            "status": "awaiting_confirmation"
        }
        await self.broadcast(canvas_message)
        return canvas_message

    async def handle_confirmation_response(self, thread_id: str, confirmed: bool):
        """
        Resumes LangGraph execution with Command(resume={'confirmed': confirmed}).
        Dispatches standardized JSON tool payloads to Unity.
        """
        logger.info(f"Received confirmation response for [{thread_id}]: confirmed={confirmed}")

        # Resume LangGraph state graph in thread pool
        final_res = await asyncio.to_thread(
            self.orchestrator.confirm_command,
            thread_id,
            confirmed=confirmed
        )

        # Remove from pending
        self.pending_confirmations.pop(thread_id, None)

        unity_payloads = final_res.get("unity_payloads", [])
        status = final_res.get("status", "unknown")

        logger.info(f"LangGraph execution finished for [{thread_id}] (Status: {status}). Payloads: {len(unity_payloads)}")

        # Dispatch each standardized tool payload to Unity
        for payload in unity_payloads:
            # Wrap in standard message envelope for Unity dispatcher
            envelope = {
                "type": "ACTION_EXECUTE",
                "thread_id": thread_id,
                "status": status,
                "payload": payload
            }
            # Also send raw payload matching Step 5 specification (e.g., {"action": "SET_ZOOM", "factor": 3.0, "duration": 0.4})
            await self.broadcast(payload)

        return final_res

    async def handle_message(self, websocket: WebSocketServerProtocol, message: str):
        """Dispatches incoming JSON messages from Unity."""
        try:
            data = json.loads(message)
        except json.JSONDecodeError:
            logger.warning(f"Received invalid non-JSON message: {message}")
            await self.send_to_client(websocket, {
                "type": "ERROR",
                "message": "Invalid JSON format"
            })
            return

        msg_type = data.get("type", "").upper()
        logger.info(f"Received message of type: {msg_type}")

        if msg_type == "COMMAND_TEXT":
            command = data.get("command", "")
            thread_id = data.get("thread_id")
            if command:
                await self.handle_command_text(command, thread_id=thread_id)
            else:
                await self.send_to_client(websocket, {"type": "ERROR", "message": "Missing 'command' parameter"})

        elif msg_type == "CONFIRMATION_RESPONSE":
            thread_id = data.get("thread_id")
            confirmed = bool(data.get("confirmed", False))
            if thread_id:
                await self.handle_confirmation_response(thread_id, confirmed)
            else:
                await self.send_to_client(websocket, {"type": "ERROR", "message": "Missing 'thread_id' parameter"})

        elif msg_type == "PING":
            await self.send_to_client(websocket, {"type": "PONG", "timestamp": data.get("timestamp")})

        elif msg_type == "GET_PENDING":
            await self.send_to_client(websocket, {
                "type": "PENDING_CONFIRMATIONS",
                "pending": self.pending_confirmations
            })

        else:
            logger.warning(f"Unknown message type received: {msg_type}")
            await self.send_to_client(websocket, {
                "type": "ERROR",
                "message": f"Unknown message type: {msg_type}"
            })

    async def client_handler(self, websocket: WebSocketServerProtocol):
        """Manages the connection lifecycle for a single client."""
        await self.register_client(websocket)
        try:
            async for message in websocket:
                await self.handle_message(websocket, message)
        except websockets.exceptions.ConnectionClosed as e:
            logger.info(f"Connection closed by client: {e.code} ({e.reason})")
        except Exception as e:
            logger.error(f"Error handling client message: {e}", exc_info=True)
        finally:
            await self.unregister_client(websocket)

    async def start(self):
        """Starts the WebSocket server."""
        self._running = True
        logger.info(f"Starting MagXR WebSocket Server on ws://{self.host}:{self.port}...")
        self.server = await websockets.serve(
            self.client_handler,
            self.host,
            self.port
        )
        logger.info(f"MagXR WebSocket Server is LIVE and listening on ws://{self.host}:{self.port}")

    async def stop(self):
        """Stops the WebSocket server and disconnects clients."""
        self._running = False
        if self.server:
            self.server.close()
            await self.server.wait_closed()
            logger.info("MagXR WebSocket Server stopped.")

    async def serve_forever(self):
        """Runs the server indefinitely until interrupted."""
        await self.start()
        try:
            await asyncio.Future()  # run forever
        except asyncio.CancelledError:
            pass
        finally:
            await self.stop()
