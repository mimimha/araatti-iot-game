"""무쌍 효과음 중 **라이브러리에서 못 찾은 것**을 직접 만든다.

CC0 팩에는 "칼로 살을 베는 소리" 와 "리듬게임 타격음" 이 없었다 (임팩트 · 스퀴시 · 휘두르는
바람 소리는 있어도, 베는 소리는 없다). 세 가지를 합성으로 만든다.

```text
tentacleCut    슥- 하고 베는 소리. 잡음을 위에서 아래로 필터로 쓸어내리고 살 맞는 저음을 얹는다
noteHit        비트세이버식 "퉁". 저음이 아래로 떨어지며 때리는 소리 + 짧은 잡음 어택
stingerClear   클리어 팡파르. 도-미-솔-도 올라간 뒤 화음으로 마무리
```

팀이 직접 만든 것이므로 외부 라이선스가 없다. (AUDIO.md 5장)
카운트다운 톤은 `warriors_countdown_tones.py` 에 따로 있다.
"""
import math
import os
import random
import struct
import wave

RATE = 44100
DEST = r"C:\S15P21C101\unity\UnderTheSea\Assets\Game\Audio\Warriors"


def write(path, samples):
    data = [int(max(-1.0, min(1.0, s)) * 32767) for s in samples]
    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(struct.pack("<%dh" % len(data), *data))
    return len(data) / RATE


def lowpass_sweep(noise, hz_from, hz_to):
    """한 극 저역통과. 차단 주파수를 hz_from 에서 hz_to 로 훑어 '슥-' 하고 어두워지게 한다."""
    out = []
    y = 0.0
    n = len(noise)
    for i, x in enumerate(noise):
        t = i / max(1, n - 1)
        fc = hz_from * ((hz_to / hz_from) ** t)
        a = 1.0 - math.exp(-2.0 * math.pi * fc / RATE)
        y += a * (x - y)
        out.append(y)
    return out


def slash(path, length=0.34):
    """칼이 살을 가르는 소리 — 잡음 스윽 + 저음 퍽."""
    n = int(RATE * length)
    rng = random.Random(20260922)
    raw = [rng.uniform(-1.0, 1.0) for _ in range(n)]
    swept = lowpass_sweep(raw, 9000.0, 400.0)

    # 잡음이 한 극 필터를 지나면 많이 작아진다. 최대값으로 되돌려 놓는다.
    peak = max(abs(v) for v in swept) or 1.0
    swept = [v / peak for v in swept]

    out = []
    for i in range(n):
        t = i / n
        # 어택 3ms, 그 뒤 빠르게 잦아든다.
        env = min(1.0, i / (RATE * 0.003)) * math.exp(-5.0 * t) * (1.0 - t)
        # 살에 닿는 저음. 90Hz 가 0.12초 안에 사라진다.
        body = math.sin(2 * math.pi * 90 * i / RATE) * math.exp(-i / (RATE * 0.045))
        out.append(swept[i] * env * 0.85 + body * 0.5)

    return write(path, out)


def rhythm_hit(path, length=0.20):
    """비트세이버식 타격 — 음이 아래로 떨어지는 '퉁' + 짧은 잡음 어택."""
    n = int(RATE * length)
    rng = random.Random(7)
    out = []
    phase = 0.0

    for i in range(n):
        t = i / n
        # 220Hz 에서 65Hz 로 60ms 안에 떨어진다. 이 '떨어짐' 이 때리는 느낌을 만든다.
        drop = min(1.0, i / (RATE * 0.060))
        freq = 220.0 * ((65.0 / 220.0) ** drop)
        phase += 2 * math.pi * freq / RATE
        tone = math.sin(phase) + 0.3 * math.sin(2 * phase)

        body = tone * math.exp(-i / (RATE * 0.055))
        # 칼날이 스치는 짧은 잡음. 12ms 면 끝난다.
        click = rng.uniform(-1.0, 1.0) * math.exp(-i / (RATE * 0.012)) * 0.35

        out.append((body * 0.8 + click) * (1.0 - t * t) * 0.9)

    return write(path, out)


def pitched_hit(path, freq, length=0.28):
    """음정이 있는 리듬 타격 — 때리는 맛은 남기고 도 · 레 · 미 가 들리게.

    치자마자 음이 위에서 제자리로 **떨어져 들어온다**(1.4배 → 제 음, 20ms). 그 미끄러짐이
    타격감을 만들고, 그 뒤 제 음이 남아 멜로디가 들린다. 한 옥타브 아래를 깔아 무게를 준다.
    """
    n = int(RATE * length)
    rng = random.Random(int(freq))
    out = []
    phase = 0.0
    sub_phase = 0.0

    for i in range(n):
        t = i / n
        slide = min(1.0, i / (RATE * 0.020))
        now = freq * (1.4 ** (1.0 - slide))

        phase += 2 * math.pi * now / RATE
        sub_phase += 2 * math.pi * (now / 2) / RATE

        tone = math.sin(phase) + 0.30 * math.sin(2 * phase)
        sub = math.sin(sub_phase) * 0.45
        body = (tone + sub) * math.exp(-i / (RATE * 0.090))

        # 칼날이 닿는 순간의 짧은 잡음. 8ms 면 사라진다.
        click = rng.uniform(-1.0, 1.0) * math.exp(-i / (RATE * 0.008)) * 0.30

        out.append((body * 0.55 + click) * (1.0 - t * t) * 0.95)

    return write(path, out)


def victory(path):
    """클리어 팡파르 — 도 · 미 · 솔 · 도 로 올라간 뒤 네 음을 겹쳐 길게 끝낸다."""
    notes = [523.25, 659.25, 783.99, 1046.50]     # C5 E5 G5 C6
    step = 0.115
    tail = 1.05
    total = int(RATE * (step * len(notes) + tail))
    out = [0.0] * total

    def add(freq, start, dur, level, decay):
        begin = int(RATE * start)
        count = int(RATE * dur)
        for i in range(count):
            if begin + i >= total:
                break
            w = 2 * math.pi * freq * i / RATE
            # 3배음까지 — 순음보다 밝고 금관처럼 들린다.
            v = math.sin(w) + 0.35 * math.sin(2 * w) + 0.14 * math.sin(3 * w)
            env = min(1.0, i / (RATE * 0.006)) * math.exp(-i / (RATE * decay))
            out[begin + i] += v / 1.49 * env * level

    # 올라가는 네 음
    for k, f in enumerate(notes):
        add(f, k * step, step * 1.9, 0.30, 0.13)

    # 마지막 화음 — 네 음을 한꺼번에 길게
    last = step * len(notes)
    for f in notes:
        add(f, last, tail, 0.26, 0.42)

    peak = max(abs(v) for v in out) or 1.0
    return write(path, [v / peak * 0.85 for v in out])


print(f"  tentacleCut.wav   {slash(os.path.join(DEST, 'tentacleCut.wav')):.2f}s  베는 소리")
print(f"  stingerClear.wav  {victory(os.path.join(DEST, 'stingerClear.wav')):.2f}s  클리어 팡파르")

# 3라운드 노트 — **방향마다 음정을 준다.** 화살표를 도 · 미 · 솔 로 읽어 박자감이 생긴다.
# C5 · E5 · G5 의 장3화음이다. 온음 간격(도-레-미)보다 사이가 넓어 무엇을 쳤는지 훨씬 잘 갈리고,
# 아무 순서로 쳐도 세 음이 한 화음 안에 있어 막 두들겨도 어울린다.
for name, freq, label in (
    ("noteHitHorizontal", 523.25, "도 (가로베기)"),
    ("noteHitVertical", 659.25, "미 (세로베기)"),
    ("noteHitThrust", 783.99, "솔 (찌르기)"),
):
    length = pitched_hit(os.path.join(DEST, name + ".wav"), freq)
    print(f"  {name + '.wav':22s} {length:.2f}s  {freq:7.2f}Hz  {label}")
