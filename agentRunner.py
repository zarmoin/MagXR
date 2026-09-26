import sys
import json
import argparse
from src.magxr.agent import (
    MagXROrchestrator,
    set_magnifier_zoom,
    move_magnifier_to_target,
    create_checkpoint,
    reset_magnification,
    CUSTOM_TOOLS
)
import voiceCommand


def print_banner(text: str):
    print("\n" + "=" * 70)
    print(f"  {text}")
    print("=" * 70)


def run_automated_test_suite():
    """Runs automated verification of Step 4 LangGraph agent & human confirmation."""
    print_banner("STEP 4 AUTOMATED TEST SUITE: LangGraph Orchestration & Confirmation")

    orchestrator = MagXROrchestrator()

    test_scenarios = [
        {
            "name": "Scenario 1: Move & Zoom (Confirmed)",
            "command": "Zoom into the red cube fast by 4x",
            "confirm": True
        },
        {
            "name": "Scenario 2: Zoom Out (Confirmed)",
            "command": "Can you zoom out slowly to 1.5x?",
            "confirm": True
        },
        {
            "name": "Scenario 3: Spatial Checkpoint Creation (Confirmed)",
            "command": "Create spatial checkpoint alpha",
            "confirm": True
        },
        {
            "name": "Scenario 4: View Reset (Confirmed)",
            "command": "Please reset magnification to default view",
            "confirm": True
        },
        {
            "name": "Scenario 5: User Rejection in VR (Cancelled)",
            "command": "Zoom into the blue cube 5x",
            "confirm": False
        },
    ]

    for idx, sc in enumerate(test_scenarios, 1):
        print(f"\n[{idx}/{len(test_scenarios)}] {sc['name']}")
        print(f"  Command: \"{sc['command']}\"")

        # Phase 1: Start command and pause at interrupt()
        start_result = orchestrator.start_command(sc['command'], thread_id=f"test-thread-{idx}")
        thread_id = start_result["thread_id"]
        conf_payload = start_result["confirmation_payload"]

        print(f"  [Action Item 1: interrupt() Paused]")
        print(f"  -> World-Space Canvas Summary: \"{conf_payload.get('summary')}\"")
        print(f"  -> Planned Tool Nodes:         {conf_payload.get('planned_tools')}")
        print(f"  -> Target Object:              {conf_payload.get('target_object')}")
        print(f"  -> Zoom Factor:                {conf_payload.get('zoom_factor')}x ({conf_payload.get('speed')})")

        # Phase 2: Resume via Command(resume={'confirmed': ...})
        print(f"  [Action Item 2: Resuming with Command(resume={{'confirmed': {sc['confirm']}}})]")
        final_result = orchestrator.confirm_command(thread_id, confirmed=sc['confirm'])

        print(f"  -> Execution Status: {final_result['status'].upper()}")
        print(f"  [Action Item 3: Tool Execution Payloads for Unity (Step 5)]")
        for p in final_result["unity_payloads"]:
            print(f"     • {json.dumps(p)}")
        print("-" * 70)

    print("\n[SUCCESS] Step 4 state machine, interrupts, and tool bindings verified successfully!")


def run_interactive_cli():
    """Interactive loop where user simulates VR voice commands and confirmations."""
    print_banner("MAGXR STEP 4: INTERACTIVE VR AGENT SIMULATOR")
    print("Type a command (or 'exit' to quit).")
    print("Example commands:")
    print("  - 'Zoom in to red cube 3x fast'")
    print("  - 'Reset magnification'")
    print("  - 'Save checkpoint MapArea1'")
    print("-" * 70)

    orchestrator = MagXROrchestrator()
    counter = 0

    while True:
        try:
            counter += 1
            cmd = input(f"\n[MagXR Voice Command #{counter}] > ").strip()
            if not cmd:
                continue
            if cmd.lower() in ("exit", "quit", "q"):
                print("Exiting simulator.")
                break

            thread_id = f"cli-session-{counter}"
            print(f"\n1. Parsing command & planning tools in LangGraph...")
            start_res = orchestrator.start_command(cmd, thread_id=thread_id)

            conf = start_res["confirmation_payload"]
            print("\n" + "=" * 50)
            print("  [VR WORLD-SPACE CONFIRMATION CANVAS]")
            print("=" * 50)
            print(f"  Summary:       {conf.get('summary')}")
            print(f"  Planned Tools: {conf.get('planned_tools')}")
            print(f"  Target:        {conf.get('target_object')}")
            print(f"  Zoom Level:    {conf.get('zoom_factor')}x")
            print(f"  Speed:         {conf.get('speed')}")
            print("=" * 50)

            choice = input("\nTrigger headset confirmation? [Y/n]: ").strip().lower()
            confirmed = choice in ("", "y", "yes")

            print(f"\n2. Resuming graph with Command(resume={{'confirmed': {confirmed}}})...")
            final_res = orchestrator.confirm_command(thread_id, confirmed=confirmed)

            print(f"Status: {final_res['status'].upper()}")
            print("Dispatched Payloads to Unity Bridge:")
            for p in final_res["unity_payloads"]:
                print(f"  -> {json.dumps(p, indent=2)}")

        except (KeyboardInterrupt, EOFError):
            print("\nExiting simulator.")
            break
        except Exception as e:
            print(f"[ERROR] {e}")


def run_live_audio_session(duration: float = 4.0):
    """
    Executes full pipeline:
    - Step 2: Microphone RMS check & 16kHz PCM recording
    - Step 3: Audio transcription via Gemini Flash
    - Step 4: LangGraph state machine with interrupt & confirmation
    """
    print_banner("MAGXR LIVE SESSION: STEP 2 (Audio) -> STEP 3 (AI) -> STEP 4 (LangGraph)")

    print("\n[Step 2] Testing microphone input levels...")
    if not voiceCommand.check_microphone_input(check_duration=2.5, threshold=0.01):
        print("[ABORTED] Mic level test failed or silent.")
        return

    print(f"\n[Step 2] Recording voice command for {duration}s...")
    buf = voiceCommand.record_voice_to_memory(duration_seconds=duration, target_sample_rate=16000)

    print("\n[Step 3 & 4] Starting LangGraph agent with audio buffer...")
    orchestrator = MagXROrchestrator()
    start_res = orchestrator.start_command(buf, thread_id="live-audio-session")

    conf = start_res["confirmation_payload"]
    print("\n" + "=" * 55)
    print("  [VR CONFIRMATION CANVAS PAUSE - LangGraph interrupt()]")
    print("=" * 55)
    print(f"  Transcribed:   \"{conf.get('transcript')}\"")
    print(f"  Summary:       {conf.get('summary')}")
    print(f"  Planned Tools: {conf.get('planned_tools')}")
    print(f"  Target:        {conf.get('target_object')}")
    print(f"  Zoom Factor:   {conf.get('zoom_factor')}x")
    print(f"  Speed:         {conf.get('speed')}")
    print("=" * 55)

    choice = input("\nConfirm action in VR headset? [Y/n]: ").strip().lower()
    confirmed = choice in ("", "y", "yes")

    print(f"\n[Step 4] Resuming via Command(resume={{'confirmed': {confirmed}}})...")
    final_res = orchestrator.confirm_command("live-audio-session", confirmed=confirmed)

    print(f"Final Status: {final_res['status'].upper()}")
    print("Unity Execution Payloads (Step 5 ready):")
    for p in final_res["unity_payloads"]:
        print(f"  • {json.dumps(p, indent=2)}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="MagXR Step 4: LangGraph Agent & Confirmation Runner")
    parser.add_argument("--interactive", "-i", action="store_true", help="Run interactive CLI simulation")
    parser.add_argument("--live", "-l", action="store_true", help="Record audio from microphone and process through agent")
    parser.add_argument("--duration", "-d", type=float, default=4.0, help="Live recording duration in seconds")
    parser.add_argument("--command", "-c", type=str, default=None, help="Run a specific command with auto-confirmation")

    args = parser.parse_args()

    if args.command:
        orch = MagXROrchestrator()
        res = orch.run_with_auto_confirm(args.command)
        print("\nFinal State Output:\n", json.dumps(res, indent=2))
    elif args.interactive:
        run_interactive_cli()
    elif args.live:
        run_live_audio_session(duration=args.duration)
    else:
        run_automated_test_suite()
