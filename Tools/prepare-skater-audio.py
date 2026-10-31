"""Prepare the three complete user-provided MP3 files for the existing PCM mixer."""
from pathlib import Path
import gzip, hashlib, json, shutil, struct, sys
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT.parent/'audio-tools'))
import numpy as np
import soundfile as sf

def main():
    downloads=Path(sys.argv[1])
    effects=ROOT/'assets/sfx/skaters'; (effects/'source').mkdir(parents=True,exist_ok=True)
    specs=[('saber','freesound_community-lightsaber-ignition-6816.mp3',effects/'source/lightsaber-original.mp3',effects/'lightsaber.pcm.gz'),
           ('blaster','freesound_community-blaster-2-81267.mp3',effects/'source/blaster-original.mp3',effects/'blaster.pcm.gz'),
           ('skaters','star_wars_the_last_jedi_01 Main Title and Escape.mp3',ROOT/'assets/music/source/skaters-main-title-original.mp3',ROOT/'assets/music/skaters.pcm.gz')]
    result=[]
    for key,name,original,destination in specs:
        shutil.copyfile(downloads/name,original)
        data,rate=sf.read(original,dtype='float64',always_2d=True)
        frames,channels=data.shape; assert channels in (1,2)
        peak=float(np.max(np.abs(data))); gain=min(1.,.98/peak)
        if key!='skaters': gain=min(.18/float(np.sqrt(np.mean(data*data))),.65/peak)
        data*=gain
        edge=round(rate*.010); ramp=np.linspace(0,1,edge)[:,None]
        data[:edge]*=ramp; data[-edge:]*=ramp[::-1]
        pcm=np.clip(np.rint(data*32768),-32768,32767).astype('<i2'); raw=pcm.tobytes()
        with destination.open('wb') as target:
            with gzip.GzipFile(filename='',fileobj=target,mode='wb',mtime=0) as out:
                out.write(struct.pack('<8sIHHI',b'SVOIPCM1',rate,channels,16,len(raw))); out.write(raw)
        if key=='skaters':
            compressed=destination.read_bytes(); parts=[]
            for number,start in enumerate(range(0,len(compressed),40000000),1):
                part=destination.with_name(destination.name+'.part'+str(number)); part.write_bytes(compressed[start:start+40000000])
                parts.append(dict(file=part.name,bytes=part.stat().st_size,sha256=hashlib.sha256(part.read_bytes()).hexdigest()))
        assert gzip.decompress(destination.read_bytes())[20:]==raw
        assert not np.any(pcm[0]) and not np.any(pcm[-1])
        result.append(dict(id=key,original=original.name,source_sha256=hashlib.sha256(original.read_bytes()).hexdigest(),sample_rate=rate,channels=channels,frames=frames,duration_seconds=frames/rate,pcm_bytes=len(raw),trim_start_seconds=0,edge_fade_seconds=.01,preparation_gain=gain,asset_sha256=hashlib.sha256(destination.read_bytes()).hexdigest(),compressed_bytes=destination.stat().st_size,clipped_samples=int(np.count_nonzero((pcm==32767)|(pcm==-32768)))))
    result[2]['parts']=parts
    (ROOT/'assets/music/skaters.pcm.gz').unlink()
    (effects/'manifest.json').write_text(json.dumps(result[:2],indent=2),encoding='utf-8')
    music=ROOT/'assets/music/manifest.json'; entries=json.loads(music.read_text(encoding='utf-8-sig'))
    entries=[x for x in entries if x['id']!='skaters']+[result[2]]
    music.write_text(json.dumps(entries,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,indent=2))

if __name__=='__main__': main()
