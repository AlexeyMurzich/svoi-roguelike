"""Prepare the user-supplied Babai theme from exactly 00:32 to EOF.

Requires numpy and soundfile; runtime builds use the checked-in PCM asset.
Original MP3 is preserved unchanged. Only 10ms edge fades prevent loop clicks;
one fixed peak headroom factor prevents PCM16 clipping. No resampling,
silence removal or duration change is applied.
"""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import struct

import numpy as np
import soundfile as sf


def main():
    repo = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, nargs="?",
                        default=repo / "assets/music/source/babai-original.mp3")
    parser.add_argument("--preview", type=Path, default=repo / "outputs/babai-theme")
    args = parser.parse_args()
    root = repo / "assets/music"
    original = root / "source/babai-original.mp3"
    if args.source.resolve() != original.resolve():
        shutil.copyfile(args.source, original)
    with sf.SoundFile(original) as song:
        rate, channels, total = song.samplerate, song.channels, song.frames
        start = 32 * rate
        if channels not in (1, 2) or total <= start:
            raise ValueError("Expected mono/stereo song longer than 32 seconds")
        song.seek(start)
        data = song.read(dtype="float64", always_2d=True)
    source_peak = float(np.max(np.abs(data)))
    headroom = min(1.0, .98 / source_peak)
    data *= headroom
    edge = round(rate * .010)
    data[:edge] *= np.linspace(0, 1, edge)[:, None]
    data[-edge:] *= np.linspace(1, 0, edge)[:, None]
    pcm = np.clip(np.rint(data * 32768), -32768, 32767).astype("<i2")
    frames = len(pcm)
    raw = pcm.tobytes()
    destination = root / "babai.pcm.gz"
    with destination.open("wb") as target:
        with gzip.GzipFile(filename="", fileobj=target, mode="wb", mtime=0) as out:
            out.write(struct.pack("<8sIHHI", b"SVOIPCM1", rate, channels, 16, len(raw)))
            out.write(raw)
    args.preview.mkdir(parents=True, exist_ok=True)
    sf.write(args.preview / "babai-from-00m32s.wav", pcm, rate, subtype="PCM_16")
    with sf.SoundFile(args.preview / "babai-from-00m32s.ogg", "w", samplerate=rate, channels=channels, format="OGG", subtype="VORBIS") as preview:
        for offset in range(0, frames, 65536):
            preview.write(pcm[offset:offset+65536])
    entry = dict(id="babai", original=original.name,
                 source_sha256=hashlib.sha256(original.read_bytes()).hexdigest(),
                 source_frames=total, trim_start_seconds=32, trim_start_frame=start,
                 edge_fade_seconds=.010, source_decoded_peak=source_peak,
                 preparation_gain=headroom, sample_rate=rate, channels=channels,
                 frames=frames, duration_seconds=frames/rate, pcm_bytes=len(raw),
                 asset_sha256=hashlib.sha256(destination.read_bytes()).hexdigest())
    manifest_path = root / "manifest.json"
    manifest = [item for item in json.loads(manifest_path.read_text(encoding="utf-8"))
                if item["id"] != "babai"]
    manifest.append(entry)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    # Verify the exact runtime payload and preview, including zero-valued loop ends.
    unpacked = gzip.decompress(destination.read_bytes())
    assert unpacked[20:] == raw and frames == total - start
    assert not np.any(pcm[0]) and not np.any(pcm[-1])
    decoded, preview_rate = sf.read(args.preview / "babai-from-00m32s.wav", dtype="int16", always_2d=True)
    assert preview_rate == rate and np.array_equal(decoded, pcm)
    entry.update(peak=float(np.max(np.abs(pcm.astype(float))))/32768,
                 rms=float(np.sqrt(np.mean((pcm.astype(float)/32768)**2))),
                 clipped_samples=int(np.count_nonzero((pcm == 32767) | (pcm == -32768))))
    (args.preview / "verification.json").write_text(json.dumps(entry, indent=2) + "\n")
    print(json.dumps(entry, indent=2))


if __name__ == "__main__":
    main()
