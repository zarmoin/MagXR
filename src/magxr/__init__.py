from src.magxr.prompt_parser import (
    MagnificationPrompt,
    parse_magnification_prompt,
    transcribe_audio,
    process_audio_command,
    parse_audio_direct_multimodal,
)

__all__ = [
    "MagnificationPrompt",
    "parse_magnification_prompt",
    "transcribe_audio",
    "process_audio_command",
    "parse_audio_direct_multimodal",
]

def main() -> None:
    print("MagXR: AI-Driven XR Magnification and Checkpoint Assistance")
