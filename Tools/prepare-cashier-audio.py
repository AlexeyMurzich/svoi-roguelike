"""Prepare the user-provided two-second ElevenLabs glitch. Requires numpy/soundfile."""
from pathlib import Path
import argparse, gzip, hashlib, json, shutil, struct, sys, wave
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT.parent/'audio-tools'))
import numpy as np
import soundfile as sf

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--source',type=Path);args=parser.parse_args()
    folder=ROOT/'assets/sfx/cashier';source=folder/'source/elevenlabs-connection-lost.mp3'
    source.parent.mkdir(parents=True,exist_ok=True)
    if args.source: shutil.copyfile(args.source,source)
    x,sr=sf.read(source,always_2d=True)
    assert sr==44100 and x.shape==(88200,2),'Expected the approved complete 2-second stereo source'
    edge=round(.005*sr);ramp=np.linspace(0,1,edge)
    x[:edge]*=ramp[:,None];x[-edge:]*=ramp[::-1,None]
    gain=min(.18/np.sqrt(np.mean(x*x)),.65/np.max(np.abs(x)))
    x*=gain
    pcm=np.rint(np.clip(x,-1,1)*32767).astype('<i2').tobytes()
    payload=struct.pack('<8sIHHI',b'SVOIPCM1',sr,2,16,len(pcm))+pcm
    runtime=folder/'glitch.pcm.gz'
    with runtime.open('wb') as f:
        with gzip.GzipFile(filename='',mode='wb',fileobj=f,mtime=0) as out:out.write(payload)
    assert gzip.decompress(runtime.read_bytes())==payload
    preview=ROOT/'outputs/cashier-integration';preview.mkdir(parents=True,exist_ok=True)
    with wave.open(str(preview/'cashier-glitch.wav'),'wb') as out:
        out.setnchannels(2);out.setsampwidth(2);out.setframerate(sr);out.writeframes(pcm)
    sf.write(preview/'cashier-glitch.ogg',np.frombuffer(pcm,dtype='<i2').reshape(-1,2)/32768,sr,format='OGG',subtype='VORBIS')
    meta=dict(source='source/'+source.name,source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
        duration_seconds=2,sample_rate=sr,channels=2,frames=len(x),edge_fade_seconds=.005,
        static_gain=float(gain),rms=float(np.sqrt(np.mean(x*x))),peak=float(np.max(np.abs(x))),
        runtime='glitch.pcm.gz',pcm_sha256=hashlib.sha256(pcm).hexdigest(),
        runtime_sha256=hashlib.sha256(runtime.read_bytes()).hexdigest())
    (folder/'manifest.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
    print(json.dumps(meta,indent=2))

if __name__=='__main__':main()
