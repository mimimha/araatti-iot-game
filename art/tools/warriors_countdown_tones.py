"""레이싱 게임식 카운트다운 톤을 만든다.

3 · 2 · 1 은 같은 음(A5)으로 짧게 세 번, 출발은 한 옥타브 위(A6)로 길게 —
마리오 카트처럼 "삐 · 삐 · 삐 · 삐이----" 가 되는 관계다.
순음만 쓰면 얇아서 2 · 3 배음을 조금 섞어 몸통을 준다.

팀이 직접 만든 것이므로 외부 라이선스가 없다. (AUDIO.md 5장 — CC0 또는 팀이 권리를 가진 것)
"""
import math
import os
import struct
import wave

RATE = 44100
DEST = r"C:\S15P21C101\unity\UnderTheSea\Assets\Game\Audio\Warriors"


def tone(path, freq, length, hold, peak=0.55):
    """freq 헤르츠로 length 초. hold 초 동안 크기를 유지한 뒤 끝까지 잦아든다."""
    total = int(RATE * length)
    attack = int(RATE * 0.004)          # 4ms — 딱 하고 시작하되 툭 튀지는 않게
    hold_end = int(RATE * hold)
    samples = []

    for i in range(total):
        if i < attack:
            env = i / attack
        elif i < hold_end:
            env = 1.0
        else:
            # 남은 구간을 지수로 잦아들게. 끝에서 정확히 0 이 되어 딸깍 소리가 없다.
            t = (i - hold_end) / max(1, total - hold_end)
            env = math.exp(-4.5 * t) * (1.0 - t)

        w = 2 * math.pi * freq * i / RATE
        wave_sum = math.sin(w) + 0.25 * math.sin(2 * w) + 0.08 * math.sin(3 * w)
        samples.append(int(max(-1.0, min(1.0, wave_sum / 1.33 * env * peak)) * 32767))

    with wave.open(path, "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(struct.pack("<%dh" % total, *samples))

    return length


# 3 · 2 · 1 — 짧고 또렷하게. A5.
t = tone(os.path.join(DEST, "countdownTick.wav"), 880.0, 0.22, 0.10)
print(f"  countdownTick.wav  880Hz  {t:.2f}s")

# 출발 — 한 옥타브 위로 길게 끌어 "가라" 가 되게. A6.
g = tone(os.path.join(DEST, "countdownGo.wav"), 1760.0, 0.70, 0.34)
print(f"  countdownGo.wav   1760Hz  {g:.2f}s")
