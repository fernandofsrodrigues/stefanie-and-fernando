"""Original procedural result cues. No sampled or third-party audio input.
Generated for Stefanie & Fernando; retain this source with the game.
Run: python generate_results.py OUTPUT_DIRECTORY
"""
import math, struct, sys, wave, hashlib, json
from pathlib import Path
out=Path(sys.argv[1]);out.mkdir(parents=True,exist_ok=True)
rate=44100; duration=1.2
# A rising major triad resolves success; a restrained descending minor triad marks defeat.
sequences={'result_win':[(0,261.6256,.15),(.12,329.6276,.18),(.26,391.9954,.19),(.42,523.2511,.23)],
           'result_fail':[(0,261.6256,.19),(.18,195.9977,.20),(.37,155.5635,.23)]}
report=[]
for name,notes in sequences.items():
    samples=[]
    for i in range(round(rate*duration)):
        t=i/rate; v=0
        for onset,freq,gain in notes:
            a=t-onset
            if a<0:continue
            env=(1-math.exp(-a/0.012))*math.exp(-a/0.20)*min(1,max(0,(duration-t)/.06))
            v+=gain*env*(math.sin(2*math.pi*freq*a)+.20*math.sin(2*math.pi*2*freq*a)+.06*math.sin(2*math.pi*3*freq*a))
        samples.append(v)
    assert max(abs(v) for v in samples)<.65
    pcm=b''.join(struct.pack('<h',round(v*32767)) for v in samples)
    p=out/(name+'.wav')
    with wave.open(str(p),'wb') as f:f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes(pcm)
    report.append({'file':p.name,'seconds':duration,'sampleRate':rate,'channels':1,'pcmBits':16,'peakLinear':max(abs(v) for v in samples),'rms':math.sqrt(sum(v*v for v in samples)/len(samples)),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
print(json.dumps(report,indent=2))
