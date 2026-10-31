"""Prepare the complete user-provided doctor theme, preserving its sample rate.

Uses numpy/soundfile only for asset preparation; no runtime codec dependency.
Ten millisecond edge fades prevent loop clicks; fixed peak headroom prevents
PCM16 clipping. The shared AudioLevels policy calibrates runtime loudness.
"""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import struct
import sys
import wave

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT.parent / 'audio-tools'))
import numpy as np
import soundfile as sf

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path, nargs='?', default=ROOT/'assets/music/source/doctors-original.mp3')
    args = parser.parse_args()
    original = ROOT/'assets/music/source/doctors-original.mp3'
    if args.source.resolve() != original.resolve():
        shutil.copyfile(args.source, original)
    data, rate = sf.read(original, dtype='float64', always_2d=True)
    frames, channels = data.shape
    assert channels in (1, 2) and frames > rate
    peak = float(np.max(np.abs(data)))
    gain = min(1., .98 / peak)
    data *= gain
    edge = round(rate * .010)
    ramp = np.linspace(0, 1, edge)[:, None]
    data[:edge] *= ramp
    data[-edge:] *= ramp[::-1]
    pcm = np.clip(np.rint(data * 32768), -32768, 32767).astype('<i2')
    raw = pcm.tobytes()
    destination = ROOT/'assets/music/doctors.pcm.gz'
    payload = struct.pack('<8sIHHI', b'SVOIPCM1', rate, channels, 16, len(raw)) + raw
    with destination.open('wb') as f:
        with gzip.GzipFile(filename='', fileobj=f, mode='wb', mtime=0) as out:
            out.write(payload)
    assert gzip.decompress(destination.read_bytes()) == payload
    assert not np.any(pcm[0]) and not np.any(pcm[-1])
    entry = dict(id='doctors', original=original.name, title='Владимир Трошин — Люди в белых халатах',
        source_sha256=hashlib.sha256(original.read_bytes()).hexdigest(), sample_rate=rate,
        channels=channels, frames=frames, duration_seconds=frames/rate, trim_start_seconds=0,
        edge_fade_seconds=.010, preparation_gain=gain, pcm_bytes=len(raw),
        asset_sha256=hashlib.sha256(destination.read_bytes()).hexdigest(),
        pcm_sha256=hashlib.sha256(raw).hexdigest(),
        rms=float(np.sqrt(np.mean((pcm.astype(float)/32768)**2))),
        peak=float(np.max(np.abs(pcm.astype(float))))/32768,
        clipped_samples=int(np.count_nonzero((pcm == 32767) | (pcm == -32768))))
    manifest_path = ROOT/'assets/music/manifest.json'
    manifest = [x for x in json.loads(manifest_path.read_text(encoding='utf-8-sig')) if x['id'] != 'doctors']
    manifest.append(entry)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    preview = ROOT/'outputs/doctors-theme'; preview.mkdir(parents=True, exist_ok=True)
    with wave.open(str(preview/'doctors-full.wav'), 'wb') as out:
        out.setnchannels(channels); out.setsampwidth(2); out.setframerate(rate); out.writeframes(raw)
    (preview/'verification.json').write_text(json.dumps(entry,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(entry,ensure_ascii=False,indent=2))

if __name__ == '__main__':
    main()
