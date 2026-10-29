"""Original layered SFX v2. Python + numpy + Pillow + soundfile. No samples."""
from pathlib import Path
import sys, json, csv, zipfile, hashlib
ROOT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parents[2] / 'work/card-sfx-v2'
import numpy as np
import soundfile as sf
from PIL import Image, ImageDraw, ImageFont
SR = 44100
HIT = .5 / 1.2
GUARD = .2 / 1.2


def noise(n, rng, low, high):
    m = 1 << (2*n-1).bit_length()
    f = np.fft.rfftfreq(m, 1/SR)
    a = np.fft.irfft(np.fft.rfft(rng.normal(size=m)) *
        (1-np.exp(-(f/low)**4)) * np.exp(-(f/high)**6),m)[:n]
    return a / max(a.std(),1e-9)


def env(t, decay, rise=.001):
    return (1-np.exp(-t/rise))*np.exp(-t/decay)


def window(t, start, end, power=1):
    p = np.clip((t-start)/(end-start),0,1)
    return np.sin(np.pi*p)**power * ((t>start)&(t<end))


def delay(x, seconds):
    k=round(seconds*SR)
    return np.pad(x,((k,0),(0,0)) if x.ndim==2 else (k,0))[:len(x)]


def tone(t, freq, decay, gain=1, rise=.001):
    return gain*np.sin(2*np.pi*freq*t)*env(t,decay,rise)


def chirp(t, first, last, rate, decay, gain=1):
    phase=2*np.pi*(last*t+(first-last)*rate*(1-np.exp(-t/rate)))
    return gain*np.sin(phase)*env(t,decay)


def modes(t, frequencies, decay, rng):
    x=np.zeros_like(t)
    for i,f in enumerate(frequencies):
        x+=tone(t,f,decay/(1+i*.19),1/(1+i*.55))
    return x


def impact(t, rng, kind):
    n=len(t)
    # Dense, sustained body + several physical material resonances, not just a click.
    x=chirp(t,170,58,.025,.12,.95)
    x+=.38*noise(n,rng,70,420)*env(t,.105)
    if kind=='wood':
        x+=.7*modes(t,[188,281,447,716,1091,1640],.095,rng)
        x+=.32*noise(n,rng,380,6500)*env(t,.047)
        x+=delay(.2*noise(n,rng,900,8000)*env(t,.028),.016)
    elif kind=='ink':
        x+=.55*noise(n,rng,420,8200)*env(t,.085)
        x+=.33*modes(t,[370,829,1731,2910],.047,rng)
        for at in [.025,.048,.081]:
            x+=delay(.13*noise(n,rng,1300,7500)*env(t,.019),at)
    elif kind=='coin':
        x+=.38*noise(n,rng,800,10000)*env(t,.035)
        x+=.35*modes(t,[1560,2471,3657,4981,6930,8723],.15,rng)
        x+=delay(.11*modes(t,[1730,3100,5643],.075,rng),.045)
    elif kind=='magic':
        x+=.5*noise(n,rng,400,9000)*env(t,.082)
        x+=chirp(t,1300,190,.033,.14,.45)
        x+=tone(t,117,.13,.33)
    else:
        x+=.25*noise(n,rng,600,7000)*env(t,.045)
    return x


def room(x, rng, amount=.3, decay=.23):
    # Unequal dense early reflections, followed by diffused tails in two channels.
    y=np.column_stack([x,x])
    for channel in range(2):
        for i in range(52):
            at=.019+i*.009+rng.uniform(-.0025,.0025)
            gain=amount*.17*np.exp(-at/decay)*rng.choice([-1,1])
            y[:,channel]+=delay(x,at)*gain
    return y


def shimmer(t, rng, attack=True):
    # Detuned, slowly moving harmonic cloud; individual partials stay quiet.
    x=np.zeros((len(t),2))
    base=196 if attack else 220
    cloud=env(t,.30 if attack else .40,.038)
    for f,g in [(base,.27),(base*1.5,.16),(base*2,.14),(base*3,.095),(base*4,.06),(base*6,.035)]:
        for ch in range(2):
            detune=1+(.0016 if ch else -.0016)
            phase=2*np.pi*f*detune*t+.11*np.sin(2*np.pi*(2.3+ch*.3)*t)
            x[:,ch]+=g*np.sin(phase)*cloud
    # Inharmonic, scattered micro-bells: a wash of sparkling detail, no melody.
    for i in range(40):
        at=rng.uniform(.015,.70)
        f=float(rng.choice([1568,2093,2349,3136,4186,4698]))*rng.uniform(.995,1.005)
        bell=tone(t,f,rng.uniform(.06,.17),rng.uniform(.022,.062),.003)
        bell+=tone(t,f*2.756,.035,.012,.002)
        p=rng.uniform(-.8,.8)
        x[:,0]+=delay(bell,at)*np.sqrt((1-p)/2)
        x[:,1]+=delay(bell,at)*np.sqrt((1+p)/2)
    return x


SPECS=[
    ('attack_01_shuten_coin','Шутен · натяжение → свист монеты → попадание',1.05,HIT,'coin'),
    ('attack_02_illya_heavy','Илля · тяжёлый взмах и деревянный удар',.95,HIT,'wood'),
    ('attack_03_runco_ink','Рунцо · резкий росчерк и плотный всплеск',1.0,HIT,'ink'),
    ('attack_04_pupsolet_spell','Пупсолет · накопление и выброс магии',1.85,HIT,'magic'),
    ('defense_01_shuten_credit','Шутен · взмах кредитки и жёсткий блок',.8,GUARD,'credit'),
    ('defense_02_illya_shield','Илля · подъём щита и гулкий блок',.85,GUARD,'shield'),
    ('defense_03_runco_notebook','Рунцо · разворот страниц и удар в обложку',.8,GUARD,'notebook'),
    ('defense_04_pupsolet_barrier','Пупсолет · раскрытие волшебного барьера',1.8,GUARD,'barrier'),
]


def synth(index, spec):
    stem,label,duration,hit,kind=spec
    rng=np.random.default_rng(2810202600+index*97)
    t=np.arange(round(duration*SR))/SR
    n=len(t)
    if kind=='coin':
        # Friction of a stretched elastic: rising harmonics and roughness.
        f=150+650*np.clip(t/.31,0,1)**1.7
        phase=2*np.pi*np.cumsum(f)/SR
        strain=(np.sin(phase)+.36*np.sin(phase*2.017)+.15*np.sin(phase*3.07))
        strain*=window(t,.012,.316,.75)*(.11+.17*t/.32)
        strain+=.12*noise(n,rng,700,4400)*window(t,.045,.316,1.4)*(1+.23*np.sin(2*np.pi*39*t))
        # Mechanical release, air cutting whistle and spin/Doppler until impact.
        release=delay(chirp(t,1250,210,.012,.028,.47),.309)
        flight=window(t,.309,HIT+.012,.7)
        freq=3300-2100*np.clip((t-.309)/.125,0,1)
        whistle=np.sin(2*np.pi*np.cumsum(freq)/SR+.75*np.sin(2*np.pi*54*t))
        whoosh=.22*whistle*flight+.24*noise(n,rng,1800,10500)*flight
        coin_hit=delay(impact(t,rng,'coin'),hit)
        dry=strain+release+whoosh+coin_hit
        stereo=room(dry,rng,.27,.16)
        pan=np.clip((t-.31)/.11,0,1)*1.1-.55
        stereo[:,0]+=whoosh*(.3-pan*.25)
        stereo[:,1]+=whoosh*(.3+pan*.25)
        parts={'01_tension':strain+release,'02_flight_whistle':whoosh,'03_coin_impact':coin_hit}
    elif kind in ['wood','ink']:
        weight=.48 if kind=='wood' else .38
        wind=weight*noise(n,rng,160,4300)*window(t,.14,hit+.018,1.4)
        wind+=.11*noise(n,rng,2400,9500)*window(t,.25,hit,1.5)
        dry=wind+delay(impact(t,rng,kind),hit)
        stereo=room(dry,rng,.32,.15)
        parts={}
    elif kind=='magic':
        gather=window(t,.015,hit+.02,1.2)
        # Soft reverse swell into a broad, weighty, bright release.
        f=130+1050*np.clip(t/hit,0,1)**2
        phase=2*np.pi*np.cumsum(f)/SR
        charge=(.19*np.sin(phase)+.10*np.sin(phase*1.501))*gather
        charge+=.16*noise(n,rng,800,7500)*gather
        dry=charge+delay(impact(t,rng,'magic'),hit)
        stereo=room(dry,rng,.68,.38)
        sparkle=shimmer(t,rng,True)
        stereo+=delay(sparkle,hit)*1.5
        # A few anticipatory specks suggest magic before release.
        stereo+=shimmer(t,rng,True)*gather[:,None]*.24
        stereo+=room(delay(np.mean(sparkle,axis=1),hit),rng,.85,.4)*.4
        parts={}
    elif kind=='barrier':
        charge=.16*noise(n,rng,750,5200)*window(t,.008,hit+.04,1.5)
        charge+=chirp(t,180,700,.095,.16,.18)*window(t,0,hit+.06)
        core=chirp(t,120,72,.05,.15,.7)+.13*noise(n,rng,600,9000)*env(t,.10,.004)
        dry=charge+delay(core,hit)
        stereo=room(dry,rng,.6,.38)
        cloud=shimmer(t,rng,False)
        stereo+=delay(cloud,hit)*1.3
        stereo+=room(delay(cloud.mean(axis=1),hit),rng,.75,.38)*.55
        parts={}
    else:
        rise=.2*noise(n,rng,450,6200)*window(t,.012,hit+.012,.8)
        if kind=='shield':
            core=impact(t,rng,'wood')*.84
            core+=tone(t,126,.14,.42)
        elif kind=='notebook':
            rise+=.16*noise(n,rng,2200,11000)*window(t,.025,hit,.8)
            core=impact(t,rng,'ink')*.65+.28*noise(n,rng,300,4000)*env(t,.03)
        else:
            core=impact(t,rng,'coin')*.62
            core+=.24*modes(t,[780,1630,3070],.07,rng)
        stereo=room(rise+delay(core,hit),rng,.32,.16)
        parts={}
    # Saturation raises density while retaining impact contrast and headroom.
    stereo=np.tanh(stereo*1.7)
    # Low-frequency rejection; linear phase, padded to keep wrap outside the file.
    m=1 << (2*n-1).bit_length()
    freqs=np.fft.rfftfreq(m,1/SR)
    filt=(1-np.exp(-(freqs/38)**4))*np.exp(-(freqs/15000)**12)
    for ch in range(2):
        stereo[:,ch]=np.fft.irfft(np.fft.rfft(stereo[:,ch],m)*filt,m)[:n]
    stereo-=stereo.mean(axis=0)
    k=round(.006*SR)
    stereo[:k]*=np.sin(np.linspace(0,np.pi/2,k))[:,None]**2
    k=round(.16*SR)
    stereo[-k:]*=np.cos(np.linspace(0,np.pi/2,k))[:,None]**2
    peak_db=-1.8 if index<4 else -3.0
    gain=10**(peak_db/20)/np.max(np.abs(stereo))
    stereo*=gain
    # Codec guard is included in the 417/167ms synchronization reference.
    stereo[:round(.005*SR)] = 0
    stereo[-1]=0
    return stereo,parts


def write(stem,x,folder='wav-stereo'):
    sf.write(ROOT/folder/(stem+'.wav'),x,SR,subtype='PCM_16')


def write_ogg(path,x):
    # Keep native encoder stack usage bounded for longer stereo review reels.
    with sf.SoundFile(path,'w',samplerate=SR,channels=x.shape[1],format='OGG',
            subtype='VORBIS',compression_level=.02) as f:
        for start in range(0,len(x),4096): f.write(x[start:start+4096])


def waves(signals):
    im=Image.new('RGB',(1550,1510),'#101827');d=ImageDraw.Draw(im)
    font=lambda sz:ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',sz)
    d.text((42,24),'СВОИ ЛЮДИ / SFX v2 — WEIGHT · AIR · MAGIC',font=font(30),fill='#f1e4c9')
    d.text((42,74),'Красная отметка — попадание / получение блока. Шкала 0–2 секунды.',font=font(19),fill='#b3bfd1')
    for i,(spec,x) in enumerate(zip(SPECS,signals)):
        row,col=i%4,i//4
        left,top=42+col*770,133+row*335
        color='#f1ac77' if i<4 else '#6edad0'
        d.text((left,top),spec[0],font=font(21),fill=color)
        d.text((left,top+33),spec[1],font=font(16),fill='#bec9da')
        px,py,w,h=left,top+77,700,170
        d.rectangle((px,py,px+w,py+h),fill='#19263b')
        for sec in np.arange(0,2.01,.25):
            q=px+round(sec/2*w)
            d.line((q,py,q,py+h),fill='#304159')
            d.text((q-8,py+h+5),f'{sec:.2f}',font=font(12),fill='#9caec5')
        xx=px+spec[3]/2*w
        d.line((xx,py,xx,py+h),fill='#fc6d79',width=2)
        for j in range(w):
            a=int(j/w*2*SR);b=min(len(x),int((j+1)/w*2*SR))
            if a<b:
                d.line((px+j,py+h/2-x[a:b].max()*h/2,px+j,py+h/2-x[a:b].min()*h/2),fill=color)
        d.text((left,top+287),f'{spec[2]:.2f} s  |  '+('ATTACK -1.8 dBFS' if i<4 else 'GUARD -3 dBFS'),font=font(17),fill='#a4b4c8')
    im.save(ROOT/'waveforms-v2.png')


def verify(signals):
    rows=[]
    for spec in SPECS:
        for folder,ext,channels in [('wav-stereo','wav',2),('wav-mono','wav',1),('ogg','ogg',2)]:
            p=ROOT/folder/(spec[0]+'.'+ext)
            x,sr=sf.read(p,always_2d=True);peak=np.max(np.abs(x))
            truepeak=max(np.max(np.abs(np.fft.irfft(np.fft.rfft(x[:,ch]),len(x)*4)*4)) for ch in range(channels))
            ref,_=sf.read(ROOT/'wav-stereo'/(spec[0]+'.wav'),always_2d=True)
            snr=10*np.log10(np.sum(ref*ref)/max(1e-20,np.sum((ref-x)**2))) if ext=='ogg' else None
            hit=round(spec[3]*sr)
            energy=float(np.sqrt(np.mean(x[hit:hit+int(.1*sr)]**2)))
            row=dict(file=str(p.relative_to(ROOT)).replace('\\','/'),channels=channels,sample_rate=sr,
                duration_s=round(len(x)/sr,4),peak_dbfs=round(float(20*np.log10(peak)),2),
                estimated_true_peak_dbfs=round(float(20*np.log10(truepeak)),2),
                impact_100ms_rms_dbfs=round(20*np.log10(energy),2),
                dc_offset=float(np.max(np.abs(x.mean(axis=0)))),
                tail_peak_dbfs=round(float(20*np.log10(max(1e-12,np.max(np.abs(x[-int(.01*sr):]))))),2),
                ogg_roundtrip_snr_db=round(float(snr),2) if snr is not None else None,
                clipped_samples=int(np.sum(np.abs(x)>=1)),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
            assert sr==SR and x.shape==(round(spec[2]*SR),channels)
            assert np.isfinite(x).all() and peak<.97 and truepeak<.99
            assert row['clipped_samples']==0 and row['dc_offset']<.003
            assert np.max(np.abs(x[-int(.01*sr):]))<.004
            assert np.max(np.abs(x[0]))<.004 and np.max(np.abs(x[-1]))<.004
            if snr is not None:assert snr>22
            rows.append(row)
    (ROOT/'verification.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf-8')
    with (ROOT/'verification.csv').open('w',encoding='utf-8-sig',newline='') as f:
        w=csv.DictWriter(f,fieldnames=rows[0].keys());w.writeheader();w.writerows(rows)
    # Compare the 100 ms around the attack impact to v1, excluding preparation and tails.
    old=['attack_01_shuten_strike','attack_02_illya_wood','attack_03_runco_ink','attack_04_pupsolet_arcane']
    comparison=[]
    for i,stem in enumerate(old):
        old_file=ROOT.parent/'card-sfx-v1'/'wav'/(stem+'.wav')
        if not old_file.exists(): continue
        a,_=sf.read(old_file)
        h=round(HIT*SR);b=signals[i][h:h+round(.1*SR)].mean(axis=1)
        a=a[round(.008*SR):round(.108*SR)]
        comparison.append({'hero':SPECS[i][0],'impact_100ms_rms_gain_db':round(float(10*np.log10(np.mean(b*b)/np.mean(a*a))),2)})
    (ROOT/'v1-v2-comparison.json').write_text(json.dumps(comparison,indent=2),encoding='utf-8')
    return rows,comparison


def main():
    for folder in ['wav-stereo','wav-mono','ogg','preview','coin-layers']:(ROOT/folder).mkdir(parents=True,exist_ok=True)
    signals=[]
    for i,spec in enumerate(SPECS):
        x,parts=synth(i,spec);signals.append(x)
        write(spec[0],x)
        write(spec[0],x.mean(axis=1),'wav-mono')
        write_ogg(ROOT/'ogg'/(spec[0]+'.ogg'),x)
        if parts:
            for name,layer in parts.items():
                layer*=.65/max(np.max(np.abs(layer)),1e-8)
                layer[:round(.005*SR)] = 0;layer[-1]=0
                write(name,layer,'coin-layers')
    # Foreground review reel: coin, offensive magic, barrier, then remaining pairs.
    seq=[0,3,7,1,5,2,6,4];chunks=[np.zeros((round(.15*SR),2))];chapters=[];at=.15
    for i in seq:
        chapters.append({'start_s':round(at,3),'name':SPECS[i][0]})
        chunks.extend([signals[i],np.zeros((round(.65*SR),2))]);at+=len(signals[i])/SR+.65
    reel=np.concatenate(chunks)
    write('all_8_v2',reel,'preview')
    write_ogg(ROOT/'preview'/'all_8_v2.ogg',reel)
    for i,name in [(0,'coin_throw_v2'),(3,'magic_attack_v2'),(7,'magic_barrier_v2')]:write(name,signals[i],'preview')
    (ROOT/'preview'/'chapters.json').write_text(json.dumps(chapters,indent=2),encoding='utf-8')
    waves(signals)
    rows,comparison=verify(signals)
    with zipfile.ZipFile(ROOT/'Svoi-card-sfx-v2.zip','w',zipfile.ZIP_DEFLATED) as z:
        for p in sorted(ROOT.rglob('*')):
            if p.is_file() and p.suffix in ['.wav','.ogg','.png','.json','.csv','.md','.py']:z.write(p,p.relative_to(ROOT))
    print(json.dumps({'verified':len(rows),'preview_seconds':round(len(reel)/SR,2),'v1_v2':comparison,
        'min_ogg_snr':min(r['ogg_roundtrip_snr_db'] for r in rows if r['ogg_roundtrip_snr_db'] is not None)},ensure_ascii=False,indent=2))


if __name__=='__main__':main()
