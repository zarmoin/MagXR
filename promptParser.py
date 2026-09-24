import sys
import argparse
from src.magxr.prompt_parser import (
    MagnificationPrompt,
    parse_magnification_prompt,
    transcribe_audio,
    process_audio_command,
    parse_audio_direct_multimodal,
    DEFAULT_MODEL
)
import voiceCommand


def run_test_suite():
    """Validates prompt parsing across standard XR voice magnification test cases."""
    test_cases = [
        "Zoom into the red cube fast by 4x",
        "Can you zoom out slowly to 1.5x?",
        "Please reset magnification to default view",
        "Magnify the high resolution map plane 5 times",
        "Zoom in normal speed on target blue cube",
        "Zoom out"
    ]

    print("\n" + "=" * 65)
    print("  RUNNING STEP 3 TEST SUITE: LangChain Structured Output")
    print(f"  Model: {DEFAULT_MODEL}")
    print("=" * 65 + "\n")

    for i, test_input in enumerate(test_cases, 1):
        print(f"[{i}/{len(test_cases)}] Input Command: \"{test_input}\"")
        try:
            result: MagnificationPrompt = parse_magnification_prompt(test_input)
            print("  -> Parsed Pydantic Schema:")
            print(f"     • Direction:     {result.direction}")
            print(f"     • Zoom Factor:   {result.zoom_factor}x")
            print(f"     • Speed:         {result.zooming_speed}")
            print(f"     • Target Object: {result.target_object}")
            print(f"     • JSON Payload:  {result.model_dump_json()}")
        except Exception as e:
            print(f"  [ERROR] Parsing failed: {e}")
        print("-" * 65)


def run_live_audio_pipeline(duration: float = 4.0):
    """
    Executes end-to-end Step 2 + Step 3 pipeline:
    1. Pre-checks microphone with RMS & peak levels (Step 2.2).
    2. Records audio buffer to in-memory BytesIO (Step 2.1).
    3. Transcribes via Gemini Flash (Step 3.1).
    4. Parses into validated MagnificationPrompt via LangChain structured output (Step 3.2 & 3.3).
    """
    print("\n" + "=" * 65)
    print("  MAGXR: STEP 2 & STEP 3 END-TO-END PIPELINE")
    print("=" * 65)

    print("\n[Step 2.2] Checking audio input levels...")
    if not voiceCommand.check_microphone_input(check_duration=2.5, threshold=0.01):
        print("\n[ABORTED] Microphone level test failed or silent. Check microphone settings.")
        return

    print(f"\n[Step 2.1] Recording voice command ({duration}s)...")
    audio_buffer = voiceCommand.record_voice_to_memory(duration_seconds=duration, target_sample_rate=16000)
    print(f"[Captured] {len(audio_buffer.getvalue())} bytes in-memory audio.")

    print("\n[Step 3.1 & 3.3] Transcribing and parsing via Gemini Flash...")
    transcript, prompt = process_audio_command(audio_buffer)

    print("\n" + "=" * 65)
    print("  RESULT: PARSED MAGNIFICATION PROMPT")
    print("=" * 65)
    print(f"  • Raw Transcript: {transcript}")
    print(f"  • Direction:      {prompt.direction}")
    print(f"  • Zoom Factor:    {prompt.zoom_factor}x")
    print(f"  • Zooming Speed:  {prompt.zooming_speed}")
    print(f"  • Target Object:  {prompt.target_object}")
    print(f"  • Ready JSON:     {prompt.model_dump_json(indent=2)}")
    print("=" * 65 + "\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="MagXR Step 3: Transcription & Prompt Parsing")
    parser.add_argument(
        "--live",
        action="store_true",
        help="Record live audio from microphone and process through Step 2 & 3"
    )
    parser.add_argument(
        "--duration",
        type=float,
        default=4.0,
        help="Recording duration in seconds for live audio (default: 4.0)"
    )
    parser.add_argument(
        "--command",
        type=str,
        default=None,
        help="Test a specific voice command string directly"
    )

    args = parser.parse_args()

    if args.command:
        res = parse_magnification_prompt(args.command)
        print("\nParsed Result:\n", res.model_dump_json(indent=2))
    elif args.live:
        run_live_audio_pipeline(duration=args.duration)
    else:
        run_test_suite()
