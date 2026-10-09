# MagXR Unity Integration Guide (Step 5)

This directory contains the production-ready C# scripts for integrating **Unity (Quest 3 via Quest Link / Wi-Fi)** with the Python AI Agent WebSocket server (`ws://127.0.0.1:8080`).

---

## 1. Files Overview

| Script | Purpose |
|---|---|
| [`MagnifierController.cs`](file:///run/media/moin/Research/MagXR/unity/MagnifierController.cs) | Implements the **3 magnification modes** (Selective Zoom, Image Zoom, Full-View Zoom), applies smooth view transitions via `Mathf.SmoothDamp` / `Vector3.Lerp`, and manages spatial checkpoints. |
| [`MagXRWebSocketClient.cs`](file:///run/media/moin/Research/MagXR/unity/MagXRWebSocketClient.cs) | Asynchronous, thread-safe WebSocket client connecting to `ws://127.0.0.1:8080`. Dispatches payloads on Unity's main thread and transmits confirmation responses back to Python. |
| [`WorldSpaceConfirmationCanvas.cs`](file:///run/media/moin/Research/MagXR/unity/WorldSpaceConfirmationCanvas.cs) | Floating world-space UI canvas parented to the VR controller or virtual loupe. Displays the proposed command summary from the LangGraph `interrupt()` breakpoint. |

---

## 2. Quick Setup in Unity

### Step A: Copy Scripts to Unity Project
Copy the `unity/` folder into your Unity project's `Assets/Scripts/` directory:
```
YourUnityProject/
  Assets/
    Scripts/
      MagXR/
        MagnifierController.cs
        MagXRWebSocketClient.cs
        WorldSpaceConfirmationCanvas.cs
```

### Step B: Configure GameObject Hierarchy
1. Create an empty GameObject named **`[MagXR Manager]`** in your scene.
2. Add components:
   - **`MagnifierController`**
   - **`MagXRWebSocketClient`**
3. In the Inspector of `MagnifierController`:
   - Assign **Main Camera** (your VR camera).
   - If using **Selective Zoom (Loupe)**:
     - Assign **Loupe Secondary Camera** (camera rendering to a `RenderTexture`).
     - Assign **Loupe Mesh Transform** (the virtual magnifying glass object).
   - If using **Image Zoom**:
     - Assign **Map Plane Transform** (the 2D map GameObject).
4. In the Inspector of `MagXRWebSocketClient`:
   - Server URI: `ws://127.0.0.1:8080` (or `ws://<PC_LOCAL_IP>:8080` if Quest 3 is running standalone on Wi-Fi).

### Step C: Configure World-Space Confirmation Canvas
1. Create a World-Space Canvas: `GameObject -> UI -> Canvas`.
2. Set `Render Mode = World Space`.
3. Add `WorldSpaceConfirmationCanvas` to the canvas.
4. Assign text fields (Summary, Target, Zoom Level, Speed) and buttons (Confirm, Cancel).
5. Drag your Right Controller Anchor (or Loupe Transform) into `Follow Target`.

---

## 3. How to Run & Verify

1. **Start the Python WebSocket Server:**
   ```bash
   uv run python websocketServer.py
   ```
2. **Press Play in Unity:**
   - The Unity console will display:
     `[MagXR WebSocket] Connected successfully to ws://127.0.0.1:8080`
3. **Trigger a Voice Command:**
   - Speak into your Quest 3 headset, or trigger via Python CLI:
     ```bash
     uv run python agentRunner.py --live
     ```
   - In Unity VR, the floating confirmation canvas will appear showing the parsed action.
   - Click **Confirm** (or press `Y` in Editor) $\rightarrow$ the magnification controller will smoothly interpolate zoom and position!

---

## 4. Standard JSON Payloads Dispatched (Step 5 Specification)

### Set Zoom
```json
{"action": "SET_ZOOM", "factor": 3.5, "speed": "fast", "duration": 0.2}
```
*Unity `MagnifierController` applies `Mathf.SmoothDamp` to transition current zoom to `3.5x` in `0.2` seconds.*

### Move to Target
```json
{"action": "MOVE_TO_TARGET", "target": "red cube", "speed": "fast", "duration": 0.2}
```
*Unity `MagnifierController` applies `Vector3.SmoothDamp` / `Vector3.Lerp` to shift the focus to the GameObject named `"red cube"`.*

### Create Checkpoint
```json
{"action": "CREATE_CHECKPOINT", "name": "Checkpoint Alpha", "timestamp": 1791024688.0}
```
*Unity saves current camera position, rotation, magnification level, and mode.*

### Reset Magnification
```json
{"action": "RESET_ZOOM", "factor": 1.0, "speed": "normal", "duration": 0.5}
```
*Unity resets the view back to 1.0x over 0.5 seconds.*
