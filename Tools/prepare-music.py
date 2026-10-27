"""Decode original OGGs without cuts, resampling or added silence.

Requires soundfile only when preparing/replacing music; normal C# builds use the
checked-in compressed PCM assets and need no Python or third-party audio codec.
"""
import argparse
import gzip
import hashlib
import json
import shutil
import struct
from pathlib import Path

import soundfile as sf

TRACKS = {
    "pupsolet": "Тема Пупсолета.ogg",
    "illya": "Тема Илли.ogg",
    "runtso": "Тема Рунцо.ogg",
    "shuten": "Тема Шутен.ogg",
    "mimicrator": "Мимикратор.ogg",
}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    originals = args.output / "source"
    originals.mkdir(exist_ok=True)
    manifest = []
    for key, filename in TRACKS.items():
        source = args.source / filename
        original = source.read_bytes()
        if source.resolve() != (originals / filename).resolve():
            shutil.copyfile(source, originals / filename)
        with sf.SoundFile(str(source)) as song:
            rate, channels, frames = song.samplerate, song.channels, song.frames
            if channels not in (1, 2):
                raise ValueError("Only mono and stereo PCM are supported")
            destination = args.output / (key + ".pcm.gz")
            written = 0
            with destination.open("wb") as raw:
                with gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0) as out:
                    out.write(struct.pack("<8sIHHI", b"SVOIPCM1", rate, channels, 16, frames * channels * 2))
                    while song.tell() < frames:
                        data = song.buffer_read(min(65536, frames - song.tell()), dtype="int16")
                        out.write(data)
                        written += len(data)
            if written != frames * channels * 2:
                raise ValueError("Decoded sample count differs from the original")
        manifest.append({
            "id": key, "original": filename, "source_sha256": hashlib.sha256(original).hexdigest(),
            "sample_rate": rate, "channels": channels, "frames": frames,
            "duration_seconds": frames / rate, "pcm_bytes": written,
            "asset_sha256": hashlib.sha256(destination.read_bytes()).hexdigest(),
        })
        print(key, frames, rate, channels, destination.stat().st_size, flush=True)
    (args.output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
