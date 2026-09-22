import io
import math
import time
import numpy as np
import sounddevice as sd
from scipy.io.wavfile import write
from scipy.signal import resample_poly

def get_linux_input_device():
    """Identifies the best input device under Linux (PulseAudio/PipeWire/ALSA)."""
    devices = sd.query_devices()
    
    # Priority 1: Check standard sound server entries
    for idx, dev in enumerate(devices):
        name = dev.get('name', '').lower()
        if dev.get('max_input_channels', 0) > 0:
            if any(k in name for k in ['pulse', 'pipewire', 'default']):
                return idx, dev
                
    # Priority 2: Standard default input device
    default_idx = sd.default.device[0]
    if default_idx is not None and default_idx >= 0:
        dev = sd.query_devices(default_idx, 'input')
        if dev.get('max_input_channels', 0) > 0:
            return default_idx, dev

    # Priority 3: First available hardware device with inputs
    for idx, dev in enumerate(devices):
        if dev.get('max_input_channels', 0) > 0:
            return idx, dev
            
    return None, None


def check_microphone_input(check_duration: float = 2.5, threshold: float = 0.01) -> bool:
    """Action Item 2: Pre-check microphone volume using native Linux device parameters."""
    dev_idx, dev_info = get_linux_input_device()
    if dev_idx is None:
        print("[ERROR] No input audio devices detected by ALSA/PortAudio.")
        return False

    native_rate = int(dev_info.get('default_samplerate', 44100))
    channels = min(1, int(dev_info.get('max_input_channels', 1)))
    dev_name = dev_info.get('name', 'Default Mic')

    print(f"\n[Mic Test] Monitoring input for {check_duration}s via: '{dev_name}' (Device #{dev_idx} @ {native_rate}Hz)...")
    print("Speak into your microphone...")

    block_samples = int(native_rate * 0.1)
    max_peak = 0.0
    start_time = time.time()

    try:
        with sd.InputStream(device=dev_idx, channels=channels, samplerate=native_rate, dtype="float32") as stream:
            while time.time() - start_time < check_duration:
                data, _ = stream.read(block_samples)
                peak = float(np.max(np.abs(data)))
                if peak > max_peak:
                    max_peak = peak
                
                bars = int(peak * 40)
                print(f"\rLevel: [{'#' * min(bars, 40):<40}] Peak: {peak:.3f}", end="", flush=True)
    except Exception as e:
        print(f"\n[ERROR] InputStream access failed: {e}")
        return False

    print("\n")
    if max_peak < threshold:
        print("[WARNING] Low or no audio detected. Run 'alsamixer' in terminal to ensure mic capture volume is unmuted.")
        return False

    print(f"[SUCCESS] Audio input detected successfully (Max Peak: {max_peak:.3f}).")
    return True


def record_voice_to_memory(duration_seconds: float = 4.0, target_sample_rate: int = 16000) -> io.BytesIO:
    """
    Action Item 1: Records audio, resamples to 16 kHz mono, and writes 
    16-bit PCM WAV into an in-memory BytesIO buffer.
    """
    dev_idx, dev_info = get_linux_input_device()
    native_rate = int(dev_info.get('default_samplerate', 44100))
    channels = min(1, int(dev_info.get('max_input_channels', 1)))

    print(f"Recording voice input for {duration_seconds} seconds...")
    audio_data = sd.rec(
        int(duration_seconds * native_rate),
        samplerate=native_rate,
        channels=channels,
        dtype="float32",
        device=dev_idx
    )
    sd.wait()
    print("Recording finished.")

    # Flatten to 1D mono array if multi-channel
    if audio_data.ndim > 1:
        audio_data = audio_data[:, 0]
    else:
        audio_data = audio_data.flatten()

    # Resample to target 16 kHz if native device rate differs
    if native_rate != target_sample_rate:
        gcd = math.gcd(native_rate, target_sample_rate)
        up = target_sample_rate // gcd
        down = native_rate // gcd
        resampled_audio = resample_poly(audio_data, up, down)
    else:
        resampled_audio = audio_data

    # Convert to 16-bit PCM WAV in memory
    scaled_audio = np.clip(resampled_audio * 32767, -32768, 32767).astype(np.int16)
    byte_buffer = io.BytesIO()
    write(byte_buffer, target_sample_rate, scaled_audio)
    byte_buffer.seek(0)

    return byte_buffer


if __name__ == "__main__":
    if check_microphone_input(check_duration=2.5, threshold=0.01):
        wav_buffer = record_voice_to_memory(duration_seconds=4.0, target_sample_rate=16000)
        buffer_size = len(wav_buffer.getvalue())
        print(f"[Result] Captured {buffer_size} bytes in-memory buffer (16 kHz, 16-bit mono PCM).")
        print("[Status] Step 2 completed successfully.")