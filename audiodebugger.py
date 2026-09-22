import sounddevice as sd
import numpy as np

print("=== Sounddevice Audio Host APIs & Devices ===")
devices = sd.query_devices()
input_indices = []

for idx, dev in enumerate(devices):
    if dev.get('max_input_channels', 0) > 0:
        input_indices.append(idx)
        print(f"[{idx}] {dev['name']} (Max Inputs: {dev['max_input_channels']}, Default SR: {dev['default_samplerate']})")

default_in = sd.default.device[0]
print(f"\nCurrent Default Input Device Index: {default_in}")

if not input_indices:
    print("\n[CRITICAL ERROR] No recording devices detected by the operating system.")
else:
    print("\n--- Testing Detected Input Devices ---")
    for idx in input_indices:
        try:
            sr = int(devices[idx]['default_samplerate'])
            data = sd.rec(int(1.0 * sr), samplerate=sr, channels=1, dtype='float32', device=idx)
            sd.wait()
            peak = float(np.max(np.abs(data)))
            print(f"Device [{idx}] '{devices[idx]['name']}': OK (1s Peak: {peak:.4f})")
        except Exception as e:
            print(f"Device [{idx}] '{devices[idx]['name']}': FAILED ({e})")