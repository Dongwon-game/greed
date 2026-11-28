import { writeFileSync } from 'node:fs';

const rate = 44100;
const bpm = 120;
const beat = 60 / bpm;
const assets = new URL('../Assets/Resources/GreedLast/RunAudio/', import.meta.url);
let left;
let right;
function prepare(seconds) {
  left = new Float64Array(Math.round(rate * seconds));
  right = new Float64Array(left.length);
}
const pi2 = 2 * Math.PI;
let randomState = 483281;
function noise() {
  randomState = (1664525 * randomState + 1013904223) >>> 0;
  return randomState / 2147483648 - 1;
}
function tone(midi) {
  return 440 * Math.pow(2, (midi - 69) / 12);
}
function add(when, duration, pan, volume, voice) {
  const begin = Math.round(when * rate);
  const size = Math.round(duration * rate);
  const l = Math.sqrt((1 - pan) / 2) * volume;
  const r = Math.sqrt((1 + pan) / 2) * volume;
  for (let i = 0; i < size; i++) {
    const k = (begin + i) % left.length;
    const t = i / rate;
    const end = Math.min(1, (duration - t) * 120);
    const sample = voice(t) * end;
    left[k] += l * sample;
    right[k] += r * sample;
  }
}
function frameDrum(when, volume, rim = false) {
  let air = 0, body = 0;
  add(when, rim ? 0.14 : 0.37, rim ? 0.17 : -0.08, volume, t => {
    const white = noise();
    air += (white - air) * 0.28;
    body += (white - body) * (rim ? 0.42 : 0.13);
    const strike = Math.exp(-t * (rim ? 44 : 57)) * (air - body * 0.63);
    const base = rim ? 157 : 72;
    const phase = pi2 * (t * base + (rim ? 1.3 : 1.15) * (1 - Math.exp(-t * 26)));
    const skin = 0.79 * Math.sin(phase) + 0.18 * Math.sin(phase * 1.55)
      + 0.08 * Math.sin(phase * 2.15) * Math.exp(-t * 9);
    return Math.min(1, t * 950) * (skin * Math.exp(-t * (rim ? 34 : 13.8)) + strike * (rim ? 0.68 : 0.24));
  });
}
function harp(when, midi, volume, pan = -0.28) {
  const key = tone(midi);
  const partials = [1, 2.003, 3.008, 4.018, 5.035, 6.06];
  const weights = [0.58, 0.21, 0.095, 0.069, 0.038, 0.027];
  const decay = [4.9, 7.2, 12, 18, 28, 42];
  add(when, 1.16, pan, volume, t => {
    let sample = 0;
    for (let i = 0; i < partials.length; i++) {
      sample += weights[i] * Math.sin(pi2 * key * partials[i] * t) * Math.exp(-decay[i] * t);
    }
    return (sample + noise() * Math.exp(-t * 170) * 0.045) * Math.min(1, t * 185);
  });
}
function pipe(when, midi, duration, volume) {
  const f = tone(midi);
  let breath = 0;
  add(when, duration, 0.31, volume, t => {
    const fade = Math.min(1, t * 12) * Math.min(1, (duration - t) * 7);
    breath += (noise() - breath) * 0.18;
    const phase = pi2 * (f * t - 0.16 * (1 - Math.exp(-t * 8))) + 0.22 * Math.sin(pi2 * 4.6 * t);
    return fade * (Math.sin(phase) * 0.77 + Math.sin(phase * 2) * 0.13
      + Math.sin(phase * 3) * 0.09 + breath * 0.22);
  });
}
function sistrum(when, volume, pan = 0.2) {
  let previous = 0;
  add(when, 0.31, pan, volume, t => {
    const white = noise();
    const bright = (white - previous) * 0.38;
    previous = white;
    const shake = Math.exp(-t * 29) + 0.53 * Math.exp(-Math.abs(t - 0.052) * 110)
      + 0.37 * Math.exp(-Math.abs(t - 0.095) * 130);
    const metal = 0.25 * Math.sin(pi2 * 1269 * t) + 0.17 * Math.sin(pi2 * 2043 * t)
      + 0.1 * Math.sin(pi2 * 3187 * t);
    return Math.min(1, t * 500) * shake * (bright + metal);
  });
}
prepare(32 * beat);
for (let bar = 0; bar < 8; bar++) {
  const start = bar * 4 * beat;
  frameDrum(start, 0.16);
  frameDrum(start + beat * 2, 0.10);
  frameDrum(start + beat * 1.5, 0.097, true);
  frameDrum(start + beat * 3, 0.084, true);
  if (bar % 2 === 1) sistrum(start + beat * 3.5, 0.040);
  if (bar >= 4) {
    frameDrum(start + beat * 0.75, 0.038, true);
    frameDrum(start + beat * 2.75, 0.047, true);
  }
  const choices = [[50, 57, 61, 62], [50, 58, 55, 57], [48, 55, 58, 61], [50, 57, 55, 53]];
  const notes = choices[bar % choices.length];
  harp(start, notes[0], 0.17);
  harp(start + beat, notes[1], 0.105);
  harp(start + beat * 2.5, notes[2], 0.115);
  harp(start + beat * 3.5, notes[3], 0.084);
}
pipe(0.54, 62, 0.63, 0.085);
pipe(1.17, 65, 0.74, 0.071);
pipe(2.29, 67, 0.65, 0.077);
pipe(3.04, 61, 0.7, 0.085);
pipe(6.82, 64, 0.64, 0.08);
pipe(7.48, 62, 0.41, 0.079);
pipe(10.27, 65, 0.67, 0.082);
pipe(10.94, 67, 0.58, 0.071);
pipe(14.08, 58, 0.73, 0.087);
pipe(14.80, 57, 0.7, 0.076);

const wetLeft = left.slice();
const wetRight = right.slice();
for (const [delay, amount] of [[0.042, 0.135], [0.071, 0.094], [0.113, 0.074], [0.181, 0.044], [0.29, 0.025]]) {
  const offset = Math.round(delay * rate);
  for (let i = 0; i < left.length; i++) {
    const earlier = (i + left.length - offset) % left.length;
    left[i] += wetRight[earlier] * amount;
    right[i] += wetLeft[earlier] * amount;
  }
}
writeWave('PyramidRunLoop', 0.78);

for (const [channel, pan] of [['Left', -0.68], ['Center', 0], ['Right', 0.68]]) {
  prepare(0.36);
  sistrum(0, 0.38, pan);
  writeWave('Trap' + channel, 0.73);
}
prepare(0.16);
frameDrum(0, 0.14, true);
writeWave('TimingTap', 0.68);

prepare(0.75);
harp(0, 74, 0.30, -0.11);
harp(0.07, 81, 0.22, 0.19);
writeWave('EscapeExact', 0.71);

prepare(0.52);
harp(0, 69, 0.25, 0);
writeWave('EscapeGood', 0.66);

prepare(0.43);
frameDrum(0, 0.32);
frameDrum(0.066, 0.07, true);
writeWave('TrapHit', 0.76);

function writeWave(name, targetPeak) {
  let peak = 0;
  for (let i = 0; i < left.length; i++) {
    peak = Math.max(peak, Math.abs(left[i]), Math.abs(right[i]));
  }
  const gain = targetPeak / peak;
  const wav = Buffer.alloc(44 + left.length * 4);
  wav.write('RIFF', 0);
  wav.writeUInt32LE(wav.length - 8, 4);
  wav.write('WAVEfmt ', 8);
  wav.writeUInt32LE(16, 16);
  wav.writeUInt16LE(1, 20);
  wav.writeUInt16LE(2, 22);
  wav.writeUInt32LE(rate, 24);
  wav.writeUInt32LE(rate * 4, 28);
  wav.writeUInt16LE(4, 32);
  wav.writeUInt16LE(16, 34);
  wav.write('data', 36);
  wav.writeUInt32LE(left.length * 4, 40);
  for (let i = 0; i < left.length; i++) {
    const end = name === 'PyramidRunLoop' ? 1 : Math.min(1, (left.length - i - 1) / (rate * 0.022));
    wav.writeInt16LE(Math.round(left[i] * gain * end * 32767), 44 + i * 4);
    wav.writeInt16LE(Math.round(right[i] * gain * end * 32767), 46 + i * 4);
  }
  writeFileSync(new URL(name + '.wav', assets), wav);
}
