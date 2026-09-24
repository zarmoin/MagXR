import io
import os
import sys
import logging
from typing import Literal, Optional, List
from dotenv import load_dotenv
from pydantic import BaseModel, Field

# Silence AFC and logger noise from google-genai
try:
    from google.genai.models import Models
    Models._logged_afc_warning = True
except Exception:
    pass
logging.getLogger("google_genai").setLevel(logging.ERROR)

from google import genai
from google.genai import types
from langchain_google_genai import ChatGoogleGenerativeAI

# Load environment variables (.env)
load_dotenv()

# Active Flash models supported on Google AI Studio Free Tier
# Ordered by availability and quota efficiency
FALLBACK_MODELS: List[str] = [
    os.getenv("GEMINI_MODEL", "gemini-3.5-flash-lite"),
    "gemini-3.7-flash",
    "gemini-3.8-flash",
    "gemini-flash-latest",
    "gemini-3.5-flash",
    "gemini-3.6-flash"
]
# Deduplicate while preserving order
FALLBACK_MODELS = list(dict.fromkeys(FALLBACK_MODELS))
DEFAULT_MODEL = FALLBACK_MODELS[0]


class MagnificationPrompt(BaseModel):
    """
    Step 3.2: Validated Pydantic schema capturing structured magnification parameters.
    Matches Step 3.2 specifications in the MagXR Project Roadmap.
    """
    direction: Literal["in", "out", "reset"] = Field(
        description="Direction of magnification: 'in' to zoom in, 'out' to zoom out, or 'reset' to return to default view (1.0x)."
    )
    zoom_factor: float = Field(
        default=1.0,
        ge=1.0,
        le=10.0,
        description="Magnification level factor, bounded between 1.0 (no zoom) and 10.0 (maximum zoom)."
    )
    zooming_speed: Literal["slow", "normal", "fast"] = Field(
        default="normal",
        description="Speed of zooming transition: 'slow', 'normal', or 'fast'."
    )
    target_object: Optional[str] = Field(
        default=None,
        description="Target scene identifier or object name to magnify (e.g., 'red cube', 'blue cube', 'map', 'checkpoint A')."
    )
    transcript: Optional[str] = Field(
        default="",
        description="Raw transcribed user speech command."
    )


def get_gemini_api_key() -> str:
    """Retrieves GEMINI_API_KEY from environment or .env file."""
    api_key = os.getenv("GEMINI_API_KEY")
    if not api_key:
        raise ValueError(
            "GEMINI_API_KEY is not set. Please set GEMINI_API_KEY in your .env file or environment."
        )
    return api_key


def transcribe_audio(
    audio_buffer: io.BytesIO | bytes,
    model: Optional[str] = None,
    mime_type: str = "audio/wav"
) -> str:
    """
    Step 3.1: Transcribe the audio buffer using Gemini Flash via direct inline byte transfer.
    Automatically retries with fallback models if quota limits, 503 unavailability, or deprecations occur.
    """
    api_key = get_gemini_api_key()
    client = genai.Client(api_key=api_key)

    raw_bytes = audio_buffer.getvalue() if isinstance(audio_buffer, io.BytesIO) else audio_buffer
    if not raw_bytes:
        raise ValueError("Audio buffer is empty.")

    prompt = (
        "You are an accurate speech-to-text transcriber for an XR voice magnification application. "
        "Transcribe the user's spoken command precisely into text. "
        "Output ONLY the transcribed spoken text without any explanations, markdown, or timestamps. "
        "If there is no intelligible speech, respond with an empty string."
    )

    models_to_try = [model] if model else FALLBACK_MODELS
    last_error = None

    for m in models_to_try:
        try:
            response = client.models.generate_content(
                model=m,
                contents=[
                    types.Part.from_bytes(data=raw_bytes, mime_type=mime_type),
                    prompt
                ]
            )
            return (response.text or "").strip()
        except Exception as e:
            last_error = e
            # Try next model if any remote API issue (quota, busy server 503, not found 404, etc.)
            print(f"[Notice] Model '{m}' transcription notice ({type(e).__name__}). Trying fallback model...")
            continue

    raise RuntimeError(f"All Gemini models exhausted for transcription: {last_error}") from last_error


def parse_magnification_prompt(
    transcript: str,
    model: Optional[str] = None
) -> MagnificationPrompt:
    """
    Step 3.3: Enforce structured extraction using llm.with_structured_output(MagnificationPrompt)
    via langchain-google-genai.
    """
    if not transcript or not transcript.strip():
        raise ValueError("Cannot parse an empty transcript.")

    api_key = get_gemini_api_key()
    models_to_try = [model] if model else FALLBACK_MODELS
    last_error = None

    system_instructions = (
        "You are an AI assistant parsing voice commands for an XR magnification system. "
        "Extract the structured magnification parameters from the user's command according to this schema:\n"
        "- direction: 'in' (zoom in / enlarge / magnify), 'out' (zoom out / shrink / reduce), or 'reset' (reset view / default view / unzoom).\n"
        "- zoom_factor: Float between 1.0 and 10.0. Default to 2.0 for 'in' if unspecified, 1.0 for 'reset', or extract explicit numbers (e.g., '3x' -> 3.0).\n"
        "- zooming_speed: 'slow', 'normal', or 'fast'. Default is 'normal' unless specified.\n"
        "- target_object: The specific target object, color, label, or area mentioned (e.g., 'red cube', 'blue sphere', 'map', 'checkpoint 1'), or null if none.\n"
        "- transcript: Keep the original transcript text."
    )

    for m in models_to_try:
        try:
            llm = ChatGoogleGenerativeAI(
                model=m,
                google_api_key=api_key
            )
            structured_llm = llm.with_structured_output(MagnificationPrompt)
            parsed: MagnificationPrompt = structured_llm.invoke(
                f"{system_instructions}\n\nSpoken Command: \"{transcript}\""
            )
            if not parsed.transcript:
                parsed.transcript = transcript
            return parsed
        except Exception as e:
            last_error = e
            # Seamless fallback on any transient API error (429, 503, etc.)
            print(f"[Notice] Model '{m}' LangChain parsing notice ({type(e).__name__}). Trying fallback model...")
            continue

    raise RuntimeError(f"All Gemini models exhausted for structured extraction: {last_error}") from last_error


def process_audio_command(
    audio_buffer: io.BytesIO | bytes,
    model: Optional[str] = None
) -> tuple[str, MagnificationPrompt]:
    """
    Executes Step 3 end-to-end:
    1. Transcribes audio buffer via Gemini inline bytes.
    2. Parses transcript into validated MagnificationPrompt via LangChain structured output.
    """
    transcript = transcribe_audio(audio_buffer, model=model)
    if not transcript:
        return "", MagnificationPrompt(
            direction="reset",
            zoom_factor=1.0,
            zooming_speed="normal",
            target_object=None,
            transcript=""
        )

    parsed = parse_magnification_prompt(transcript, model=model)
    return transcript, parsed


def parse_audio_direct_multimodal(
    audio_buffer: io.BytesIO | bytes,
    model: Optional[str] = None,
    mime_type: str = "audio/wav"
) -> MagnificationPrompt:
    """
    Alternative ultra-low-latency one-step extraction: sends audio bytes directly
    to Gemini with MagnificationPrompt Pydantic response schema.
    """
    api_key = get_gemini_api_key()
    client = genai.Client(api_key=api_key)

    raw_bytes = audio_buffer.getvalue() if isinstance(audio_buffer, io.BytesIO) else audio_buffer
    if not raw_bytes:
        raise ValueError("Audio buffer is empty.")

    system_prompt = (
        "Listen to the user's spoken voice command for XR magnification. "
        "Accurately transcribe the speech into 'transcript' and extract the structured parameters: "
        "direction ('in', 'out', or 'reset'), zoom_factor (1.0 to 10.0), zooming_speed ('slow', 'normal', 'fast'), "
        "and target_object (string name or null)."
    )

    models_to_try = [model] if model else FALLBACK_MODELS
    last_error = None

    for m in models_to_try:
        try:
            response = client.models.generate_content(
                model=m,
                contents=[
                    types.Part.from_bytes(data=raw_bytes, mime_type=mime_type),
                    system_prompt
                ],
                config=types.GenerateContentConfig(
                    response_mime_type="application/json",
                    response_schema=MagnificationPrompt
                )
            )
            return MagnificationPrompt.model_validate_json(response.text)
        except Exception as e:
            last_error = e
            print(f"[Notice] Model '{m}' direct multimodal notice ({type(e).__name__}). Trying fallback...")
            continue

    raise RuntimeError(f"All Gemini models exhausted: {last_error}") from last_error
