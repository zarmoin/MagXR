import asyncio
import argparse
import signal
import sys
from src.magxr.server import MagXRWebSocketServer
import voiceCommand


def main():
    parser = argparse.ArgumentParser(description="MagXR Step 5: Network Communication Bridge (WebSocket Server)")
    parser.add_argument("--host", type=str, default="127.0.0.1", help="Host IP to bind (default: 127.0.0.1)")
    parser.add_argument("--port", type=int, default=8080, help="Port to bind (default: 8080)")
    parser.add_argument("--all-interfaces", action="store_true", help="Bind to 0.0.0.0 to allow Quest on LAN/Wi-Fi to connect")

    args = parser.parse_args()
    host = "0.0.0.0" if args.all_interfaces else args.host

    print("\n" + "=" * 70)
    print("  MAGXR STEP 5: WEBSOCKET NETWORK BRIDGE TO UNITY")
    print(f"  Target URI: ws://{host}:{args.port}")
    print("=" * 70)
    print("  Ready to dispatch:")
    print("    • CONFIRMATION_REQUEST  -> Floating VR World-Space Canvas")
    print("    • SET_ZOOM              -> Unity MagnifierController.cs")
    print("    • MOVE_TO_TARGET        -> Unity MagnifierController.cs")
    print("    • CREATE_CHECKPOINT     -> Unity MagnifierController.cs")
    print("    • RESET_ZOOM            -> Unity MagnifierController.cs")
    print("=" * 70 + "\n")

    server = MagXRWebSocketServer(host=host, port=args.port)

    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)

    # Setup signal handlers
    stop_event = asyncio.Event()

    def signal_handler():
        print("\n[Server] Shutdown signal received. Closing WebSocket server...")
        stop_event.set()

    for sig in (signal.SIGINT, signal.SIGTERM):
        try:
            loop.add_signal_handler(sig, signal_handler)
        except NotImplementedError:
            pass  # Windows or limited environment

    async def run():
        await server.start()
        print(f"[Server Status] Server is actively listening on ws://{host}:{args.port}")
        print("[Instructions] Start Unity scene or run test client to connect.\n")
        await stop_event.wait()
        await server.stop()

    try:
        loop.run_until_complete(run())
    except KeyboardInterrupt:
        print("\n[Server] Exiting...")
    finally:
        loop.close()


if __name__ == "__main__":
    main()
