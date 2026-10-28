"""Import original v2 masters, or rebuild runtime from checked-in WAVs. Stdlib only."""
import argparse, gzip, hashlib, json, shutil, struct, wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CARDS = ROOT / 'assets/sfx/cards'
FILES = {
    'shuten-attack': 'attack_01_shuten_coin', 'illya-attack': 'attack_02_illya_heavy',
    'runtso-attack': 'attack_03_runco_ink', 'pupsolet-attack': 'attack_04_pupsolet_spell',
    'shuten-guard': 'defense_01_shuten_credit', 'illya-guard': 'defense_02_illya_shield',
    'runtso-guard': 'defense_03_runco_notebook', 'pupsolet-guard': 'defense_04_pupsolet_barrier',
}
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--import-v2',type=Path);args=parser.parse_args()
    for folder in ['source','previews','runtime']:(CARDS/folder).mkdir(parents=True,exist_ok=True)
    manifest=[]
    for key,stem in FILES.items():
        wav=CARDS/'source'/(key+'.wav');ogg=CARDS/'previews'/(key+'.ogg')
        if args.import_v2:
            shutil.copyfile(args.import_v2/'wav-stereo'/(stem+'.wav'),wav)
            shutil.copyfile(args.import_v2/'ogg'/(stem+'.ogg'),ogg)
        with wave.open(str(wav),'rb') as source:
            assert source.getsampwidth()==2 and source.getnchannels()==2 and source.getframerate()==44100
            rate,channels,frames=source.getframerate(),source.getnchannels(),source.getnframes()
            pcm=source.readframes(frames)
        runtime=CARDS/'runtime'/(key+'.pcm.gz')
        payload=struct.pack('<8sIHHI',b'SVOIPCM1',rate,channels,16,len(pcm))+pcm
        with runtime.open('wb') as f:
            with gzip.GzipFile(filename='',mode='wb',fileobj=f,mtime=0) as out:out.write(payload)
        assert gzip.decompress(runtime.read_bytes())==payload
        manifest.append(dict(id=key,source='source/'+wav.name,preview='previews/'+ogg.name,
            runtime='runtime/'+runtime.name,sample_rate=rate,channels=channels,frames=frames,
            duration_seconds=frames/rate,action_seconds=.5/1.2 if key.endswith('attack') else .2/1.2,
            source_sha256=hashlib.sha256(wav.read_bytes()).hexdigest(),
            preview_sha256=hashlib.sha256(ogg.read_bytes()).hexdigest(),
            pcm_sha256=hashlib.sha256(pcm).hexdigest(),asset_sha256=hashlib.sha256(runtime.read_bytes()).hexdigest()))
    (CARDS/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Verified and packed',len(manifest),'card masters; PCM samples unchanged.')
if __name__=='__main__':main()
