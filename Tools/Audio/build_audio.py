"""Rebuild game SFX from documented CC0 recordings. Requires numpy, scipy, soundfile.

First run: downloaded source packs live in .utmp/audio-source. Used originals and
their licenses are copied into Assets/ThirdParty/PrototypeAudio for later rebuilds.
No neural synthesis is used: resampling, filtering, envelopes and layered mixing only.
"""
from pathlib import Path
import hashlib
import json
import shutil
import numpy as np
import soundfile as sf
from scipy import signal

ROOT = Path(__file__).resolve().parents[2]
INPUT = ROOT / '.utmp/audio-source'
ORIGINALS = ROOT / 'Assets/ThirdParty/PrototypeAudio'
OUTPUT = ROOT / 'Assets/_game/Resources/Audio/Clips'
RATE = 44100
used = {}
report = []


def source(pack, name):
    relative = Path(pack) / name
    saved = ORIGINALS / 'Sources' / relative
    if not saved.exists():
        matches = list((INPUT / pack).rglob(name)) if pack != 'freesound' else [INPUT / name]
        if not matches or not matches[0].exists():
            raise FileNotFoundError(relative)
        saved.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(matches[0], saved)
    used[str(relative)] = hashlib.sha256(saved.read_bytes()).hexdigest()
    audio, rate = sf.read(saved, always_2d=True)
    audio = np.mean(audio, axis=1)
    if rate != RATE:
        audio = signal.resample_poly(audio, RATE, rate)
    return audio


def low(audio, hz):
    return signal.sosfilt(signal.butter(2, hz, fs=RATE, output='sos'), audio)


def high(audio, hz):
    return signal.sosfilt(signal.butter(2, hz, fs=RATE, btype='highpass', output='sos'), audio)


def speed(audio, ratio):
    return np.interp(np.arange(0, len(audio), ratio), np.arange(len(audio)), audio)


def trim(audio, limit=2.0):
    active = np.flatnonzero(np.abs(audio) > max(0.002, np.max(np.abs(audio)) * 0.012))
    if not len(active):
        raise ValueError('Silent source')
    first = max(0, active[0] - int(RATE * 0.003))
    last = min(len(audio), active[-1] + int(RATE * 0.025), first + int(limit * RATE))
    return fade(audio[first:last])


def fade(audio, attack=0.002, release=0.05):
    audio = audio.copy()
    a = min(len(audio), int(RATE * attack))
    r = min(len(audio), int(RATE * release))
    audio[:a] *= np.linspace(0, 1, a)
    audio[-r:] *= np.linspace(1, 0, r)
    return audio


def mix(*layers):
    length = max(int(start * RATE) + len(audio) for start, audio, gain in layers)
    result = np.zeros(length)
    for start, audio, gain in layers:
        begin = int(start * RATE)
        result[begin:begin + len(audio)] += audio * gain
    return result


def write(name, audio, peak=0.75):
    audio = high(audio - np.mean(audio), 30)
    audio = np.tanh(audio * 1.15)
    audio = fade(audio, release=0.035)
    audio *= peak / max(np.max(np.abs(audio)), 1e-9)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    sf.write(OUTPUT / (name + '.wav'), audio, RATE, subtype='PCM_16')
    report.append({'name': name, 'seconds': round(len(audio) / RATE, 3),
                   'peak_dbfs': round(20 * np.log10(np.max(np.abs(audio))), 2),
                   'rms_dbfs': round(20 * np.log10(np.sqrt(np.mean(audio ** 2))), 2)})


def segments(audio, minimum_gap):
    envelope = np.sqrt(signal.convolve(audio ** 2, np.ones(441) / 441, mode='same'))
    peaks, _ = signal.find_peaks(envelope, height=envelope.max() * 0.16,
                                 distance=int(RATE * minimum_gap), prominence=envelope.max() * 0.10)
    return peaks


def bell(frequency, duration, level=1):
    t = np.arange(int(duration * RATE)) / RATE
    return level * (np.sin(t * frequency * 2 * np.pi)
                    + 0.18 * np.sin(t * frequency * 4 * np.pi)) * np.exp(-t * 6)


def main():
    shot = source('freesound', 'revolver.mp3')
    shot_peaks = segments(shot, 0.60)
    if len(shot_peaks) < 3:
        raise ValueError('Could not isolate revolver shots')
    for index, peak in enumerate(shot_peaks[:5]):
        start = max(0, peak - int(RATE * 0.035))
        attack = shot[start:start + int(RATE * 0.32)]
        body = low(speed(attack, 0.77), 1300)
        write(f'Shot_{index + 1}', mix((0, low(high(attack, 90), 6800), 0.85), (0.004, body, 0.24)))

    explosion = trim(source('freesound', 'explosion.mp3'), 0.9)
    for index, ratio in enumerate([0.9, 1.0, 1.08]):
        body = low(speed(explosion, ratio), 5200)
        write(f'Explosion_{index + 1}', mix((0, body, 1), (0.015, low(speed(body, 0.68), 380), 0.50)), 0.8)

    cloth = source('freesound', 'cloth.mp3')
    cloth_peaks = segments(cloth, 0.70)
    swishes = []
    for peak in cloth_peaks[:4]:
        start = max(0, peak - int(RATE * 0.09))
        swishes.append(trim(cloth[start:start + int(RATE * 0.30)], 0.30))
    if not swishes:
        raise ValueError('No cloth swishes')
    leather = trim(source('rpg', 'clothBelt.ogg'), 0.45)
    coins = trim(source('rpg', 'handleCoins.ogg'), 0.38)
    wood = trim(source('impact', 'impactWood_heavy_000.ogg'), 0.3)
    for index in range(3):
        whoosh = low(speed(swishes[index % len(swishes)], 0.85), 4500)
        write(f'Swap_{index + 1}', mix((0, whoosh, 1), (0.045, low(coins, 2400), 0.13)), 0.55)
        unfold = mix((0.035, low(speed(leather, 0.8), 4000), 0.65),
                     (0.11, low(speed(whoosh, 0.78), 4200), 1.0),
                     (0.23, low(speed(coins, 0.65), 1900), 0.32),
                     (0.30, low(speed(wood, 0.7), 1000), 0.44),
                     (0.49, low(speed(wood, 0.6), 1500), 0.85),
                     (0.56, low(speed(coins, 0.72), 1800), 0.30))
        write(f'Bandolier_{index + 1}', unfold, 0.77)

    enemy_voice = trim(source('freesound', 'enemy-pain.mp3'), 1.15)
    hero_voice = trim(source('freesound', 'hero-hurt.mp3'), 0.8)
    for index in range(4):
        punch = trim(source('impact', f'impactPunch_heavy_{index:03}.ogg'), 0.22)
        soft = trim(source('impact', f'impactSoft_medium_{index:03}.ogg'), 0.18)
        write(f'Hit_{index + 1}', mix((0, low(high(punch, 100), 3600), 0.8), (0.01, low(soft, 2000), 0.3)), 0.64)
        reaction = low(speed(enemy_voice[:int(RATE * 0.30)], 0.88 + index * 0.055), 2900)
        write(f'EnemyPain_{index + 1}', reaction, 0.6)
        death = low(speed(enemy_voice, 0.80 + index * 0.07), 3400)
        write(f'EnemyDeath_{index + 1}', mix((0, death, 0.70), (0.12, low(punch, 750), 0.12)), 0.65)
        write(f'HeroImpact_{index + 1}', low(punch, 2300), 0.7)
        write(f'HeroPain_{index + 1}', low(speed(hero_voice, 0.96 + index * 0.025), 4800), 0.65)
        metal = trim(source('impact', f'impactMetal_light_{index:03}.ogg'), 0.18)
        write(f'Ricochet_{index + 1}', low(high(speed(metal, 1.3), 850), 6500), 0.55)
    write('HeroDeath_1', mix((0, low(speed(hero_voice, 0.75), 3800), 0.7), (0.20, low(speed(leather, 0.7), 1500), 0.15)), 0.7)

    for index in range(3):
        click = trim(source('interface', f'select_{index + 1:03}.ogg'), 0.14)
        write(f'Select_{index + 1}', mix((0, low(click, 4000), 0.5), (0, low(swishes[index % len(swishes)], 3000), 0.25)), 0.5)
    latch = trim(source('rpg', 'metalLatch.ogg'), 0.22)
    click = trim(source('rpg', 'metalClick.ogg'), 0.15)
    write('Reload_1', mix((0, latch, 0.7), (0.17, low(coins, 3500), 0.4), (0.78, click, 0.9)), 0.55)

    # Short original tonal stingers, with sampled body rather than an arcade voice announcer.
    wave = mix((0, bell(392, 0.45), 0.45), (0.12, bell(493.88, 0.5), 0.42),
               (0.24, bell(587.33, 0.65), 0.40), (0, low(coins, 2200), 0.12))
    write('WaveWin_1', wave, 0.62)
    write('GameWin_1', mix((0, wave, 0.65), (0.48, bell(783.99, 1.0), 0.35),
                          (0.52, bell(587.33, 1), 0.28), (0.56, bell(493.88, 1), 0.2)), 0.68)
    write('Defeat_1', mix((0, bell(220, 0.7), 0.35), (0.18, bell(174.61, 0.8), 0.38),
                         (0.36, bell(146.83, 1.0), 0.35), (0, low(wood, 700), 0.15)), 0.62)

    for pack in ['impact', 'rpg', 'interface']:
        target = ORIGINALS / 'Licenses' / (pack + '-License.txt')
        target.parent.mkdir(parents=True, exist_ok=True)
        source_license = INPUT / pack / 'License.txt'
        if source_license.exists():
            shutil.copyfile(source_license, target)
    (ORIGINALS / 'source_hashes.json').write_text(json.dumps(used, indent=2), encoding='utf-8')
    (OUTPUT.parent / 'render_report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(f'Rendered {len(report)} cues/variants; {len(used)} documented source files.')
    print('Shot onset seconds:', [round(p / RATE, 3) for p in shot_peaks])


if __name__ == '__main__':
    main()
