from src.magxr.prompt_parser import (
    MagnificationPrompt,
    parse_magnification_prompt,
    transcribe_audio,
    process_audio_command,
    parse_audio_direct_multimodal,
)
from src.magxr.agent import (
    MagXROrchestrator,
    build_magxr_graph,
    set_magnifier_zoom,
    move_magnifier_to_target,
    create_checkpoint,
    reset_magnification,
    CUSTOM_TOOLS,
)

__all__ = [
    # Step 3
    "MagnificationPrompt",
    "parse_magnification_prompt",
    "transcribe_audio",
    "process_audio_command",
    "parse_audio_direct_multimodal",
    # Step 4
    "MagXROrchestrator",
    "build_magxr_graph",
    "set_magnifier_zoom",
    "move_magnifier_to_target",
    "create_checkpoint",
    "reset_magnification",
    "CUSTOM_TOOLS",
]

def main() -> None:
    print("MagXR: AI-Driven XR Magnification and Checkpoint Assistance")
