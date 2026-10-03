import asyncio
import json
import sys


async def test_full_websocket_flow(uri: str = "ws://127.0.0.1:8080"):
    import websockets

    print(f"\n[Test Client] Connecting to {uri}...")
    async with websockets.connect(uri) as ws:
        # 1. Receive handshake
        msg = await ws.recv()
        data = json.loads(msg)
        print(f"[Handshake Received] {data}")
        assert data.get("type") == "SERVER_STATUS"

        # -------------------------------------------------------------
        # Test 1: Full Confirmation & Multi-Tool Execution (Move & Zoom)
        # -------------------------------------------------------------
        print("\n--- Test 1: Sending Command: 'Zoom into the red cube fast by 4x' ---")
        cmd_msg = {
            "type": "COMMAND_TEXT",
            "command": "Zoom into the red cube fast by 4x",
            "thread_id": "test-session-1"
        }
        await ws.send(json.dumps(cmd_msg))

        # Expect CONFIRMATION_REQUEST
        msg = await ws.recv()
        conf_req = json.loads(msg)
        print(f"[Confirmation Request Emitted to Unity]:\n  Summary: {conf_req.get('summary')}\n  Tools: {conf_req.get('planned_tools')}\n  Thread: {conf_req.get('thread_id')}")
        assert conf_req.get("type") == "CONFIRMATION_REQUEST"
        thread_id = conf_req.get("thread_id")

        # Simulate user pressing 'Confirm' on VR controller
        print(f"\n[Unity VR Controller] User confirmed action in headset!")
        confirm_resp = {
            "type": "CONFIRMATION_RESPONSE",
            "thread_id": thread_id,
            "confirmed": True
        }
        await ws.send(json.dumps(confirm_resp))

        # Expect dispatched tool payloads
        # Planned tools: move_magnifier_to_target and set_magnifier_zoom
        received_actions = []
        for _ in range(2):
            msg = await ws.recv()
            action_payload = json.loads(msg)
            print(f"[Unity Received Action Payload]: {action_payload}")
            received_actions.append(action_payload.get("action"))

        assert "MOVE_TO_TARGET" in received_actions
        assert "SET_ZOOM" in received_actions
        print(f"[Test 1 PASS] Both tool payloads received in Unity successfully: {received_actions}")

        # -------------------------------------------------------------
        # Test 2: User Rejection in VR (Cancellation)
        # -------------------------------------------------------------
        print("\n--- Test 2: Sending Command: 'Zoom in on blue cube 3x' (Rejection Test) ---")
        cmd_msg2 = {
            "type": "COMMAND_TEXT",
            "command": "Zoom in on blue cube 3x",
            "thread_id": "test-session-2"
        }
        await ws.send(json.dumps(cmd_msg2))

        # Expect CONFIRMATION_REQUEST
        msg = await ws.recv()
        conf_req2 = json.loads(msg)
        print(f"[Confirmation Request Emitted]: {conf_req2.get('summary')}")
        thread_id2 = conf_req2.get("thread_id")

        # Simulate user pressing 'Cancel' on VR controller
        print(f"[Unity VR Controller] User rejected action in headset!")
        reject_resp = {
            "type": "CONFIRMATION_RESPONSE",
            "thread_id": thread_id2,
            "confirmed": False
        }
        await ws.send(json.dumps(reject_resp))

        # Expect CONFIRMATION_CANCELLED payload
        msg = await ws.recv()
        cancel_payload = json.loads(msg)
        print(f"[Unity Received Cancellation]: {cancel_payload}")
        assert cancel_payload.get("action") == "CONFIRMATION_CANCELLED"
        print("[Test 2 PASS] Cancellation handled properly without executing tools!")

        # -------------------------------------------------------------
        # Test 3: Reset Command
        # -------------------------------------------------------------
        print("\n--- Test 3: Sending Reset Command: 'Reset magnification' ---")
        cmd_msg3 = {
            "type": "COMMAND_TEXT",
            "command": "Reset magnification",
            "thread_id": "test-session-3"
        }
        await ws.send(json.dumps(cmd_msg3))

        msg = await ws.recv()
        conf_req3 = json.loads(msg)
        print(f"[Confirmation Request Emitted]: {conf_req3.get('summary')}")
        await ws.send(json.dumps({
            "type": "CONFIRMATION_RESPONSE",
            "thread_id": conf_req3.get("thread_id"),
            "confirmed": True
        }))

        msg = await ws.recv()
        reset_payload = json.loads(msg)
        print(f"[Unity Received Reset Payload]: {reset_payload}")
        assert reset_payload.get("action") == "RESET_ZOOM"
        print("[Test 3 PASS] Reset magnification payload received successfully!")

    print("\n" + "=" * 65)
    print("  ALL WEBSOCKET NETWORK BRIDGE TESTS PASSED (100% SUCCESS)!")
    print("=" * 65 + "\n")


if __name__ == "__main__":
    asyncio.run(test_full_websocket_flow())
