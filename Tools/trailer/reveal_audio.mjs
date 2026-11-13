// World-reveal trailer sound (2026-11-07): score, Dhaka sound design and mix, all synthesised here.
// No samples, no third-party music, no voices. The DSP, instruments, sound design, meter and limiter are the
// trailer v1 code (Tools/trailer/audio.mjs); only the score and the mix below are new.
//
//   node reveal_audio.mjs render <outDir>   five music stems + SFX stem (float WAV) + master (24-bit WAV) + loudness.json
//   node reveal_audio.mjs measure <file.wav> | selftest
//
// Timings follow Production/Trailer_2026/Editing/edl_reveal_v1.json. The pulse runs at 100 BPM (beat 0.6 s) from
// 24 s, so every cut in the pulse and the acceleration lands on a beat. Master: -14 LUFS, -1.5 dBTP ceiling.
import fs from 'node:fs';
import path from 'node:path';

const SR = 48000, DUR = 85, N = SR * DUR, TAU = 2 * Math.PI;
const TARGET_LUFS = -14, CEIL_DBTP = -1.5;

let seed = 1;
function rnd() { seed = (seed + 0x6D2B79F5) | 0; let t = Math.imul(seed ^ (seed >>> 15), 1 | seed); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }
const rr = (a, b) => a + (b - a) * rnd();
const mtof = m => 440 * Math.pow(2, (m - 69) / 12);
const db = d => Math.pow(10, d / 20);
const ease = u => u <= 0 ? 0 : u >= 1 ? 1 : u * u * (3 - 2 * u);
/** Piecewise-linear automation through [time, value] points. */
function curve(pts) {
  return x => {
    if (x <= pts[0][0]) return pts[0][1];
    for (let k = 1; k < pts.length; k++) if (x <= pts[k][0]) { const [t0, v0] = pts[k - 1], [t1, v1] = pts[k]; return t1 === t0 ? v1 : v0 + (v1 - v0) * (x - t0) / (t1 - t0); }
    return pts[pts.length - 1][1];
  };
}

// ---------------------------------------------------------------- DSP building blocks
class BQ { // RBJ cookbook biquad; set() keeps the state, so cutoffs can move
  constructor() { this.b0 = 1; this.b1 = this.b2 = this.a1 = this.a2 = 0; this.x1 = this.x2 = this.y1 = this.y2 = 0; }
  set(type, f, q = 0.707, gainDb = 0) {
    f = Math.min(Math.max(f, 10), SR * 0.45);
    const w = TAU * f / SR, c = Math.cos(w), s = Math.sin(w), al = s / (2 * q), A = Math.pow(10, gainDb / 40);
    let b0, b1, b2, a0, a1, a2;
    if (type === 'lp') { b0 = (1 - c) / 2; b1 = 1 - c; b2 = b0; a0 = 1 + al; a1 = -2 * c; a2 = 1 - al; }
    else if (type === 'hp') { b0 = (1 + c) / 2; b1 = -(1 + c); b2 = b0; a0 = 1 + al; a1 = -2 * c; a2 = 1 - al; }
    else if (type === 'bp') { b0 = al; b1 = 0; b2 = -al; a0 = 1 + al; a1 = -2 * c; a2 = 1 - al; }
    else { b0 = 1 + al * A; b1 = -2 * c; b2 = 1 - al * A; a0 = 1 + al / A; a1 = -2 * c; a2 = 1 - al / A; } // peak
    this.b0 = b0 / a0; this.b1 = b1 / a0; this.b2 = b2 / a0; this.a1 = a1 / a0; this.a2 = a2 / a0;
    return this;
  }
  p(x) { const y = this.b0 * x + this.b1 * this.x1 + this.b2 * this.x2 - this.a1 * this.y1 - this.a2 * this.y2; this.x2 = this.x1; this.x1 = x; this.y2 = this.y1; this.y1 = y; return y; }
}
class Saw { // polyBLEP
  constructor() { this.p = rnd(); }
  next(f) {
    const dt = f / SR, t = this.p; let y = 2 * t - 1;
    if (t < dt) { const x = t / dt; y -= x + x - x * x - 1; } else if (t > 1 - dt) { const x = (t - 1) / dt; y -= x * x + x + x + 1; }
    this.p += dt; if (this.p >= 1) this.p -= 1;
    return y;
  }
}
class Pink { constructor() { this.b0 = this.b1 = this.b2 = 0; } next() { const w = rnd() * 2 - 1; this.b0 = 0.99765 * this.b0 + w * 0.099046; this.b1 = 0.963 * this.b1 + w * 0.2965164; this.b2 = 0.57 * this.b2 + w * 1.0526913; return (this.b0 + this.b1 + this.b2 + w * 0.1848) * 0.25; } }
class Brown { constructor() { this.b = 0; } next() { this.b = (this.b + 0.02 * (rnd() * 2 - 1)) / 1.02; return this.b * 3.5; } }

/** Freeverb network; adds its return for the mono sum `m` into `out`. */
function reverbInto(m, out, fb, damp, spread, wet) {
  const sc = SR / 44100;
  const cb = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617].map(d => new Float32Array(Math.round((d + spread) * sc)));
  const ab = [556, 441, 341, 225].map(d => new Float32Array(Math.round((d + spread) * sc)));
  const ci = new Int32Array(8), cs = new Float64Array(8), ai = new Int32Array(4);
  for (let n = 0; n < N; n++) {
    const x = m[n] * 0.015; let y = 0;
    for (let k = 0; k < 8; k++) { const b = cb[k], o = b[ci[k]]; cs[k] = o * (1 - damp) + cs[k] * damp; b[ci[k]] = x + cs[k] * fb; if (++ci[k] >= b.length) ci[k] = 0; y += o; }
    for (let k = 0; k < 4; k++) { const b = ab[k], o = b[ai[k]]; b[ai[k]] = y + o * 0.5; if (++ai[k] >= b.length) ai[k] = 0; y = o - y; }
    out[n] += y * 3 * wet;
  }
}

class Bus {
  constructor(fb = 0.86, damp = 0.35) { this.fb = fb; this.damp = damp; this.L = new Float32Array(N); this.R = new Float32Array(N); this.sL = new Float32Array(N); this.sR = new Float32Array(N); }
  /** Mono voice: fn(i, x) gives sample i (x = seconds into the voice); pan -1..1 or a function of x. */
  voice(t, dur, fn, gain = 1, pan = 0, send = 0) {
    const i0 = Math.round(t * SR), n = Math.round(dur * SR), pf = typeof pan === 'function' ? pan : null;
    let gl = 0, gr = 0;
    const setPan = p => { const a = (Math.max(-1, Math.min(1, p)) + 1) * Math.PI / 4; gl = Math.cos(a) * gain; gr = Math.sin(a) * gain; };
    if (!pf) setPan(pan);
    for (let i = 0; i < n; i++) {
      const j = i0 + i; if (j >= N) break;
      if (pf && (i & 63) === 0) setPan(pf(i / SR));
      const x = fn(i, i / SR);
      if (j < 0) continue;
      const l = x * gl, r = x * gr;
      this.L[j] += l; this.R[j] += r;
      if (send) { this.sL[j] += l * send; this.sR[j] += r * send; }
    }
  }
  /** Stereo voice: fn(i, x, o) writes o[0] and o[1]. */
  stereo(t, dur, fn, gain = 1, send = 0) {
    const i0 = Math.round(t * SR), n = Math.round(dur * SR), o = [0, 0];
    for (let i = 0; i < n; i++) {
      const j = i0 + i; if (j >= N) break;
      fn(i, i / SR, o);
      if (j < 0) continue;
      const l = o[0] * gain, r = o[1] * gain;
      this.L[j] += l; this.R[j] += r;
      if (send) { this.sL[j] += l * send; this.sR[j] += r * send; }
    }
  }
  /** Reverb return into the dry signal, 25 Hz high-pass; frees the sends. */
  finish(wet = 1) {
    if (wet > 0) {
      for (let i = 0; i < N; i++) this.sL[i] = (this.sL[i] + this.sR[i]) * 0.5;
      reverbInto(this.sL, this.L, this.fb, this.damp, 0, wet);
      reverbInto(this.sL, this.R, this.fb, this.damp, 23, wet);
    }
    this.sL = this.sR = null;
    for (const ch of [this.L, this.R]) { const h = new BQ().set('hp', 25, 0.707); for (let i = 0; i < N; i++) ch[i] = h.p(ch[i]); }
    return this;
  }
}

// ---------------------------------------------------------------- instruments
function pad(bus, t, dur, notes, o = {}) {
  const { att = 2, rel = 2, gain = 0.1, c0 = 800, c1 = c0, det = 0.12, trem = 0, tr0 = 6, tr1 = tr0, cresc = 0, formant = false, send = 0.6, cut = false } = o;
  const side = () => notes.flatMap(m => [0, 1].map(() => ({ o: new Saw(), f: mtof(m) * Math.pow(2, rr(-1, 1) * det / 12) })));
  const vs = [side(), side()], norm = 1 / Math.sqrt(vs[0].length);
  const f1 = [new BQ(), new BQ()], f2 = [new BQ(), new BQ()];
  const fm = formant ? [0, 1].map(() => [[700, 1], [1150, 0.6], [2600, 0.28]].map(([f, g]) => ({ q: new BQ().set('bp', f, 5), g }))) : null;
  let tp = 0;
  bus.stereo(t, dur, (i, x, out) => {
    const u = x / dur;
    if ((i & 63) === 0 && !fm) { const c = c0 * Math.pow(c1 / c0, u); for (let s = 0; s < 2; s++) { f1[s].set('lp', c, 0.6); f2[s].set('lp', c, 0.9); } }
    let e = ease(x / att) * (cut ? (x > dur - 0.012 ? (dur - x) / 0.012 : 1) : ease((dur - x) / rel));
    if (cresc) e *= 0.2 + 0.8 * Math.pow(u, cresc);
    if (trem) { tp += TAU * (tr0 + (tr1 - tr0) * u) / SR; e *= 1 - trem * (0.5 + 0.5 * Math.sin(tp)); }
    for (let s = 0; s < 2; s++) {
      let y = 0; for (const v of vs[s]) y += v.o.next(v.f);
      y *= norm;
      if (fm) { let z = 0; for (const b of fm[s]) z += b.q.p(y) * b.g; y = z * 2.2; } else y = f2[s].p(f1[s].p(y));
      out[s] = y * e;
    }
  }, gain, send);
}
function drone(bus, t, dur, m, { att = 3, rel = 2, gain = 0.2, pulse = 0, pulseRate = 1, cut = false } = {}) {
  const f = mtof(m); let p = 0, q = 0;
  bus.voice(t, dur, (i, x) => {
    p += TAU * f / SR; q += TAU * pulseRate / SR;
    let e = ease(x / att) * (cut ? (x > dur - 0.01 ? (dur - x) / 0.01 : 1) : ease((dur - x) / rel));
    if (pulse) e *= 1 - pulse * (0.5 + 0.5 * Math.cos(q));
    return (Math.sin(p) + 0.25 * Math.sin(2 * p)) * e;
  }, gain);
}
function boom(bus, t, { gain = 0.5, f0 = 75, f1 = 32, ptau = 0.2, tau = 1.1, dur = 4, noise = 0.35, send = 0.25, drive = 1.5 } = {}) {
  let ph = 0; const lp = new BQ().set('lp', 220, 0.7), k = Math.tanh(drive);
  bus.voice(t, dur, (i, x) => {
    ph += TAU * (f1 + (f0 - f1) * Math.exp(-x / ptau)) / SR;
    const e = Math.exp(-x / tau) * Math.min(1, x / 0.003);
    const nz = lp.p(rnd() * 2 - 1) * Math.exp(-x / 0.3) * noise * 3;
    return Math.tanh(drive * (Math.sin(ph) * e + nz)) / k;
  }, gain, 0, send);
}
function kick(bus, t, gain = 0.5) {
  let ph = 0;
  bus.voice(t, 0.6, (i, x) => {
    ph += TAU * (46 + 110 * Math.exp(-x / 0.035)) / SR;
    const click = x < 0.004 ? (rnd() * 2 - 1) * (1 - x / 0.004) * 0.4 : 0;
    return Math.tanh(1.8 * Math.sin(ph) * Math.exp(-x / 0.22) * Math.min(1, x / 0.001)) + click;
  }, gain, 0, 0.05);
}
function snare(bus, t, gain = 0.3, pan = 0) {
  const hp = new BQ().set('hp', 900, 0.7), bp = new BQ().set('bp', 2400, 0.8); let ph = 0;
  bus.voice(t, 0.45, (i, x) => { ph += TAU * 185 / SR; const n = rnd() * 2 - 1; return (hp.p(n) * 0.7 + bp.p(n) * 0.6) * Math.exp(-x / 0.12) + Math.sin(ph) * Math.exp(-x / 0.06) * 0.6; }, gain, pan, 0.35);
}
function tom(bus, t, f0, gain = 0.4, pan = 0, tau = 0.45) {
  let ph = 0; const lp = new BQ().set('lp', 900, 0.7);
  bus.voice(t, tau * 4, (i, x) => { ph += TAU * f0 * (1 + 0.7 * Math.exp(-x / 0.05)) / SR; return Math.tanh(1.4 * Math.sin(ph) * Math.exp(-x / tau)) + lp.p(rnd() * 2 - 1) * Math.exp(-x / 0.04) * 0.5; }, gain, pan, 0.3);
}
function hat(bus, t, gain = 0.08, open = false, pan = 0.2) {
  const a = new BQ().set('hp', 7500, 0.7), b = new BQ().set('hp', 7500, 0.7), tau = open ? 0.16 : 0.028;
  bus.voice(t, tau * 5, (i, x) => b.p(a.p(rnd() * 2 - 1)) * Math.exp(-x / tau), gain, pan, 0.15);
}
function crash(bus, t, gain = 0.15, tau = 1.6) {
  const hp = [new BQ().set('hp', 3500, 0.7), new BQ().set('hp', 3300, 0.7)], pk = [new BQ().set('peak', 5200, 1.5, 6), new BQ().set('peak', 6100, 1.5, 6)];
  bus.stereo(t, tau * 4, (i, x, o) => { const e = Math.min(1, x / 0.002) * Math.exp(-x / tau); for (let s = 0; s < 2; s++) o[s] = pk[s].p(hp[s].p(rnd() * 2 - 1)) * e; }, gain, 0.4);
}
function heart(bus, t, gain = 0.4) {
  for (const [dt, g] of [[0, 1], [0.27, 0.7]]) {
    let ph = 0; const lp = new BQ().set('lp', 120, 0.8);
    bus.voice(t + dt, 0.5, (i, x) => { ph += TAU * (40 + 25 * Math.exp(-x / 0.03)) / SR; const e = Math.min(1, x / 0.006) * Math.exp(-x / 0.09); return Math.sin(ph) * e + lp.p(rnd() * 2 - 1) * e * 0.8; }, gain * g, 0, 0.05);
  }
}
function tick(bus, t, gain = 0.06, pan = 0) {
  const bp = new BQ().set('bp', 3800, 6);
  bus.voice(t, 0.06, (i, x) => bp.p(i < 30 ? rnd() * 2 - 1 : 0) * Math.exp(-x / 0.012) * 4, gain, pan, 0.2);
}
function brass(bus, t, dur, notes, { gain = 0.15, cpk = 3200, cend = 900, att = 0.025, rel = 0.35, send = 0.5, drive = 1.6, det = 0.15 } = {}) {
  const side = () => notes.flatMap(m => [0, 1, 2].map(() => ({ o: new Saw(), f: mtof(m) * Math.pow(2, rr(-1, 1) * det / 12) })));
  const vs = [side(), side()], norm = 1 / Math.sqrt(vs[0].length), k = Math.tanh(drive);
  const f1 = [new BQ(), new BQ()], f2 = [new BQ(), new BQ()];
  bus.stereo(t, dur + rel, (i, x, out) => {
    if ((i & 31) === 0) { const c = x < att ? 250 + (cpk - 250) * x / att : cend + (cpk - cend) * Math.exp(-(x - att) / 0.22); for (let s = 0; s < 2; s++) { f1[s].set('lp', c, 0.7); f2[s].set('lp', c, 1.0); } }
    const e = Math.min(1, x / att) * (x > dur ? Math.max(0, 1 - (x - dur) / rel) : 1);
    for (let s = 0; s < 2; s++) { let y = 0; for (const v of vs[s]) y += v.o.next(v.f); out[s] = Math.tanh(drive * f2[s].p(f1[s].p(y * norm)) * e) / k; }
  }, gain, send);
}
function riser(bus, t, dur, { gain = 0.25, f0 = 250, f1 = 7000, base = 50, cut = true, send = 0.5 } = {}) {
  const bp = [new BQ(), new BQ()], lp = [new BQ(), new BQ()], tones = [0, 7, 12, 19].map(k => [new Saw(), new Saw(), mtof(base + k)]);
  bus.stereo(t, dur, (i, x, out) => {
    const u = x / dur;
    if ((i & 31) === 0) { const f = f0 * Math.pow(f1 / f0, u); bp[0].set('bp', f, 2); bp[1].set('bp', f * 1.05, 2); const c = 300 * Math.pow(25, u); lp[0].set('lp', c, 1); lp[1].set('lp', c, 1); }
    const e = Math.pow(u, 2.2) * (cut ? (x > dur - 0.008 ? (dur - x) / 0.008 : 1) : 1), gl = Math.pow(2, u);
    let a = 0, b = 0; for (const [s1, s2, f] of tones) { a += s1.next(f * gl); b += s2.next(f * gl * 1.003); }
    out[0] = (bp[0].p(rnd() * 2 - 1) * 1.5 + lp[0].p(a * 0.25)) * e;
    out[1] = (bp[1].p(rnd() * 2 - 1) * 1.5 + lp[1].p(b * 0.25)) * e;
  }, gain, send);
}
/** Reverse-cymbal style swell that ends exactly on tEnd. */
function swell(bus, tEnd, dur, gain = 0.2) {
  const hp = [new BQ().set('hp', 2500, 0.7), new BQ().set('hp', 2500, 0.7)];
  bus.stereo(tEnd - dur, dur, (i, x, o) => { const e = Math.pow(x / dur, 3.5) * (x > dur - 0.006 ? (dur - x) / 0.006 : 1); o[0] = hp[0].p(rnd() * 2 - 1) * e; o[1] = hp[1].p(rnd() * 2 - 1) * e; }, gain, 0.6);
}
function whoosh(bus, tc, dur = 0.6, gain = 0.2, dir = 1) {
  const bp = new BQ();
  bus.voice(tc - dur / 2, dur, (i, x) => { const u = x / dur, s = Math.sin(Math.PI * u); if ((i & 31) === 0) bp.set('bp', 300 * Math.pow(12, s), 1.2); return bp.p(rnd() * 2 - 1) * s * s * 2; }, gain, x => dir * (2 * x / dur - 1) * 0.8, 0.3);
}
function bell(bus, t, m, { dur = 5, gain = 0.1, ratio = 3.5, index = 2.5, tau = 1.4, pan = 0, send = 0.7 } = {}) {
  const f = mtof(m); let pc = 0, pm = 0;
  bus.voice(t, dur, (i, x) => { pm += TAU * f * ratio / SR; pc += TAU * f / SR; return Math.sin(pc + index * Math.exp(-x / 0.5) * Math.sin(pm)) * Math.exp(-x / tau) * Math.min(1, x / 0.002); }, gain, pan, send);
}
function piano(bus, t, m, { dur = 4, gain = 0.12, pan = 0, send = 0.6 } = {}) {
  const f = mtof(m), hs = [[1, 1, 2.2], [2.003, 0.45, 1.3], [3.01, 0.22, 0.8], [4.02, 0.1, 0.5], [5.04, 0.05, 0.35]], ph = hs.map(() => 0), lp = new BQ().set('lp', 2200, 0.7);
  bus.voice(t, dur, (i, x) => {
    let y = 0; for (let k = 0; k < hs.length; k++) { ph[k] += TAU * f * hs[k][0] / SR; y += Math.sin(ph[k]) * hs[k][1] * Math.exp(-x / hs[k][2]); }
    return lp.p(y * Math.min(1, x / 0.004) + (x < 0.02 ? (rnd() * 2 - 1) * (1 - x / 0.02) * 0.15 : 0));
  }, gain, pan, send);
}
function pluck(bus, t, m, { dur = 1.6, gain = 0.1, pan = 0, send = 0.4, damp = 0.995 } = {}) { // Karplus-Strong
  const L = Math.max(2, Math.round(SR / mtof(m))), buf = new Float32Array(L); let prev = 0;
  for (let k = 0; k < L; k++) { prev = prev * 0.5 + (rnd() * 2 - 1) * 0.5; buf[k] = prev; }
  let idx = 0;
  bus.voice(t, dur, (i, x) => { const y = buf[idx], nx = buf[(idx + 1) % L]; buf[idx] = damp * 0.5 * (y + nx); idx = (idx + 1) % L; return y * (x > dur - 0.05 ? (dur - x) / 0.05 : 1); }, gain, pan, send);
}
function arp(bus, t, m, cutoff, { dur = 0.12, gain = 0.05, pan = 0, send = 0.35 } = {}) {
  const o = new Saw(), o2 = new Saw(), f = mtof(m), lp = new BQ(), lp2 = new BQ();
  bus.voice(t, dur + 0.08, (i, x) => {
    if ((i & 31) === 0) { const c = cutoff * (0.35 + 0.65 * Math.exp(-x / 0.05)); lp.set('lp', c, 1.4); lp2.set('lp', c, 0.7); }
    const e = Math.min(1, x / 0.003) * (x > dur ? Math.max(0, 1 - (x - dur) / 0.08) : 1) * Math.exp(-x / 0.25);
    return lp2.p(lp.p((o.next(f) + o2.next(f * 1.006)) * 0.5)) * e;
  }, gain, pan, send);
}
function bassNote(bus, t, m, dur, { gain = 0.2, c0 = 1200, c1 = 180, drive = 2 } = {}) {
  const o = new Saw(), f = mtof(m), lp = new BQ(), lp2 = new BQ(), k = Math.tanh(drive); let ph = 0;
  bus.voice(t, dur + 0.04, (i, x) => {
    if ((i & 31) === 0) { const c = c1 + (c0 - c1) * Math.exp(-x / 0.07); lp.set('lp', c, 1.1); lp2.set('lp', c, 0.7); }
    ph += TAU * f / SR;
    const e = Math.min(1, x / 0.003) * (x > dur ? Math.max(0, 1 - (x - dur) / 0.04) : 1);
    return Math.tanh(drive * (lp2.p(lp.p(o.next(f))) * 0.7 + Math.sin(ph) * 0.6) * e) / k;
  }, gain);
}
function blip(bus, t, f, gain) {
  let ph = 0; const d = rr(0.015, 0.04);
  bus.voice(t, d, (i, x) => { ph += f / SR; return ((ph % 1) < 0.5 ? 1 : -1) * (1 - x / d); }, gain, rr(-0.7, 0.7), 0.4);
}

// ---------------------------------------------------------------- Dhaka sound design
function cityBed(bus, level, cutoff) {
  const p = [new Pink(), new Pink()], br = new Brown(), lp = [new BQ(), new BQ()], rum = new BQ().set('lp', 160, 0.7);
  let g = 0, mod = 1;
  bus.stereo(0, DUR, (i, x, o) => {
    if ((i & 63) === 0) { g = db(level(x)); const c = cutoff(x); lp[0].set('lp', c, 0.6); lp[1].set('lp', c * 1.04, 0.6); mod = 0.75 + 0.25 * Math.sin(TAU * x / 7.3) * Math.sin(TAU * x / 3.1 + 1); }
    if (g < 1e-5) { o[0] = o[1] = 0; return; }
    const r = rum.p(br.next()) * 1.5;
    o[0] = (lp[0].p(p[0].next()) + r) * g * mod; o[1] = (lp[1].p(p[1].next()) + r) * g * mod;
  }, 1, 0.15);
}
/** Dual-tone car/bus horn, `beeps` short blasts. */
function horn(bus, t, f, dur, { gain = 0.05, pan = 0, cutoff = 3000, beeps = 1, gap = 0.12, send = 0.4 } = {}) {
  for (let b = 0; b < beeps; b++) {
    const o1 = new Saw(), o2 = new Saw(), lp = new BQ().set('lp', cutoff, 0.7), hp = new BQ().set('hp', 250, 0.7), pk = new BQ().set('peak', f * 2.5, 2, 6);
    bus.voice(t + b * (dur + gap), dur, (i, x) => {
      const w = 1 + 0.004 * Math.sin(TAU * 6 * x), e = Math.min(1, x / 0.012) * Math.min(1, (dur - x) / 0.03);
      return lp.p(hp.p(pk.p(Math.tanh(1.5 * (o1.next(f * w) + o2.next(f * 1.26 * w)))))) * e * 0.5;
    }, gain, pan, send);
  }
}
/** Cycle-rickshaw bell: a fast clapper rattle on a small dome, two rings. */
function rickshawBell(bus, t, { gain = 0.05, pan = 0, send = 0.35, rings = 2, cutoff = 12000 } = {}) {
  const parts = [[2350, 1, 0.35], [3720, 0.6, 0.22], [5210, 0.45, 0.15], [6880, 0.25, 0.1]];
  const dec = parts.map(p => Math.exp(-1 / (p[2] * SR))), ph = parts.map(() => 0), amp = parts.map(() => 0), lp = new BQ().set('lp', cutoff, 0.7);
  const strikes = [];
  for (let r = 0; r < rings; r++) { const n = 5 + Math.floor(rnd() * 3); for (let k = 0; k < n; k++) strikes.push(r * 0.42 + k * (0.038 + rr(-0.004, 0.004))); }
  let si = 0;
  bus.voice(t, rings * 0.42 + 0.8, (i, x) => {
    while (si < strikes.length && x >= strikes[si]) { for (let k = 0; k < parts.length; k++) amp[k] = Math.min(1.2, amp[k] + parts[k][1] * rr(0.5, 1)); si++; }
    let y = 0; for (let k = 0; k < parts.length; k++) { ph[k] += TAU * parts[k][0] / SR; amp[k] *= dec[k]; y += Math.sin(ph[k]) * amp[k]; }
    return lp.p(y * 0.4);
  }, gain, pan, send);
}
/** CNG auto-rickshaw: two-stroke putter passing across. */
function cng(bus, t, dur, { gain = 0.06, rate = 42, panFrom = -0.8, panTo = 0.8, cutoff = 2500 } = {}) {
  const bp1 = new BQ().set('bp', 260, 3), bp2 = new BQ().set('bp', 900, 4), lp = new BQ().set('lp', cutoff, 0.7);
  let ph = 0, env = 0;
  bus.voice(t, dur, (i, x) => {
    ph += rate * (1 + 0.1 * Math.sin(TAU * 0.7 * x) + 0.04 * (rnd() - 0.5)) / SR;
    if (ph >= 1) { ph -= 1; env = rr(0.7, 1); }
    env *= 0.9985;
    const pulse = env * (rnd() * 2 - 1), a = Math.sin(Math.PI * x / dur);
    return lp.p(bp1.p(pulse) * 2 + bp2.p(pulse) * 1.2) * a * a;
  }, gain, x => panFrom + (panTo - panFrom) * x / dur, 0.2);
}
/** Mains buzz (50 Hz grid, so the buzz sits at 100 Hz and up). */
function hum(bus, level) {
  const hs = [[100, 1], [200, 0.6], [300, 0.45], [400, 0.2], [600, 0.12]], ph = hs.map(() => 0); let g = 0;
  bus.voice(0, DUR, (i, x) => {
    if ((i & 63) === 0) g = db(level(x));
    if (g < 1e-5) return 0;
    let y = 0; for (let k = 0; k < hs.length; k++) { ph[k] += TAU * hs[k][0] * (1 + 0.0005 * Math.sin(TAU * 0.3 * x)) / SR; y += Math.sin(ph[k]) * hs[k][1]; }
    return Math.tanh(1.5 * y) * g * (0.9 + 0.1 * Math.sin(TAU * 1.7 * x));
  }, 1, 0.15, 0.1);
}
function wind(bus, level) {
  const b = [new Brown(), new Brown()], bp = [new BQ(), new BQ()]; let g = 0;
  bus.stereo(0, DUR, (i, x, o) => {
    if ((i & 63) === 0) { g = db(level(x)); const f = 350 + 200 * Math.sin(TAU * x / 5.3) + 120 * Math.sin(TAU * x / 2.1); bp[0].set('bp', f, 1.5); bp[1].set('bp', f * 1.1, 1.5); }
    if (g < 1e-5) { o[0] = o[1] = 0; return; }
    for (let s = 0; s < 2; s++) o[s] = bp[s].p(b[s].next()) * 3 * g;
  }, 1, 0.2);
}
function rain(bus, level) {
  const hp = [0, 1].map(() => new BQ().set('hp', 600, 0.7)), lp = [9000, 8500].map(f => new BQ().set('lp', f, 0.7)), roar = [0, 1].map(() => new BQ().set('lp', 1400, 0.6));
  const drop = [new BQ(), new BQ()], da = [0, 0]; let g = 0;
  bus.stereo(0, DUR, (i, x, o) => {
    if ((i & 63) === 0) g = db(level(x));
    if (g < 1e-5) { o[0] = o[1] = 0; return; }
    for (let s = 0; s < 2; s++) {
      let y = lp[s].p(hp[s].p(rnd() * 2 - 1)) * 0.6 + roar[s].p(rnd() * 2 - 1) * 0.5;
      if (rnd() < 0.0009) { da[s] = rr(0.3, 1); drop[s].set('bp', rr(1500, 6500), 4); }
      da[s] *= 0.992; y += drop[s].p((rnd() * 2 - 1) * da[s]) * 2.5;
      o[s] = y * g;
    }
  }, 1, 0.12);
}
function thunder(bus, t, { gain = 0.5, dur = 7 } = {}) {
  const br = [new Brown(), new Brown()], lp = [new BQ().set('lp', 260, 0.7), new BQ().set('lp', 240, 0.7)], crack = new BQ().set('hp', 1500, 0.7);
  const bumps = [[0.05, 1, 0.15]]; for (let k = 0; k < 9; k++) bumps.push([rr(0.2, dur * 0.6), rr(0.3, 0.9), rr(0.2, 0.8)]);
  const envAt = x => { let e = 0; for (const [bt, ba, bw] of bumps) { const d = (x - bt) / bw; if (d > -3 && d < 8) e += ba * (d < 0 ? Math.exp(-d * d) : Math.exp(-d)); } return e; };
  let peak = 0; for (let x = 0; x < dur; x += 0.01) peak = Math.max(peak, envAt(x));
  bus.stereo(t, dur, (i, x, o) => {
    const env = envAt(x) / peak * ease((dur - x) / 1.5);
    const c = x < 0.25 ? crack.p(rnd() * 2 - 1) * Math.exp(-x / 0.05) * 0.8 : 0;
    for (let s = 0; s < 2; s++) o[s] = lp[s].p(br[s].next() * 3) * env + c;
  }, gain, 0.5);
}
/** Electrical arcing from a failing sign. */
function crackle(bus, t0, t1, density, gain) {
  const hp = new BQ().set('hp', 2000, 0.7); let a = 0;
  bus.voice(t0, t1 - t0, (i, x) => { if (rnd() < density / SR) a = rr(0.4, 1); a *= 0.9965; return hp.p((rnd() * 2 - 1) * a * (0.6 + 0.4 * Math.sin(TAU * 100 * x))); }, gain, 0.3, 0.2);
}
function step(bus, t, gain, pan) { // foot on a wet road
  let ph = 0; const lp = new BQ().set('lp', 220, 0.8), bp = new BQ().set('bp', 2800, 1.2);
  bus.voice(t, 0.25, (i, x) => {
    ph += TAU * 75 / SR;
    const th = (Math.sin(ph) * 0.6 + lp.p(rnd() * 2 - 1) * 1.5) * Math.exp(-x / 0.035) * Math.min(1, x / 0.002);
    return th + (x > 0.012 ? bp.p(rnd() * 2 - 1) * Math.exp(-(x - 0.012) / 0.06) * 0.8 : 0);
  }, gain, pan, 0.3);
}
/** Wordless two-way radio: squelch beep, gated static, end chirp. */
function radio(bus, t, dur, gain) {
  const bp = new BQ().set('bp', 1500, 0.6), hp = new BQ().set('hp', 400, 0.7), lp = new BQ().set('lp', 3000, 0.7), segs = [];
  for (let s = 0.12; s < dur - 0.15;) { const on = rr(0.05, 0.25), off = rr(0.03, 0.12); segs.push([s, s + on]); s += on + off; }
  let ph = 0;
  bus.voice(t, dur, (i, x) => {
    let g = 0.25; for (const [a, b] of segs) if (x >= a && x < b) { g = 1; break; }
    let beep = 0;
    if (x < 0.09) { ph += TAU * 1050 / SR; beep = Math.sin(ph) * 0.6; } else if (x > dur - 0.12) { ph += TAU * (1400 - 3000 * (x - dur + 0.12)) / SR; beep = Math.sin(ph) * 0.4; }
    return lp.p(hp.p(Math.tanh(3 * bp.p(rnd() * 2 - 1) * g))) * 0.8 + beep;
  }, gain, -0.3, 0.25);
}
function bikePass(bus, tc, { gain = 0.12, span = 2.4, f = 85 } = {}) {
  const o = new Saw(), o2 = new Saw(), lp = new BQ(), hp = new BQ().set('hp', 60, 0.7);
  bus.voice(tc - span / 2, span, (i, x) => {
    const d = (x - span / 2) / 0.35, a = 1 / (1 + d * d * 0.6);
    if ((i & 63) === 0) lp.set('lp', 600 + 2400 * a, 0.8);
    const ff = f * (1 - 0.1 * Math.tanh(d)) * (1 + 0.03 * Math.sin(TAU * 9 * x));
    return hp.p(lp.p(Math.tanh(2 * (o.next(ff) + 0.5 * o2.next(ff * 2.01) + (rnd() * 2 - 1) * 0.3)))) * a;
  }, gain, x => Math.tanh((x - span / 2) / 0.4) * 0.85, 0.25);
}
function siren(bus, t, dur, { gain = 0.03, cutoff = 1800 } = {}) {
  const o = new Saw(), lp = new BQ().set('lp', cutoff, 0.7), bp = new BQ().set('bp', 1100, 1);
  bus.voice(t, dur, (i, x) => { const w = 0.5 - 0.5 * Math.cos(TAU * x / 3.2); return lp.p(bp.p(o.next(650 + 650 * w))) * ease(x / 1.5) * ease((dur - x) / 2); }, gain, x => -0.6 + 0.4 * x / dur, 0.9);
}
function drip(bus, t, gain = 0.08, pan = 0) {
  let ph = 0;
  bus.voice(t, 0.15, (i, x) => { ph += TAU * (900 + 1400 * Math.min(1, x / 0.03)) / SR; return Math.sin(ph) * Math.exp(-x / 0.03) * Math.min(1, x / 0.001); }, gain, pan, 0.8);
}
function metal(bus, t, { gain = 0.1, dur = 6, base = 170 } = {}) {
  const parts = [[1, 1, 2.5], [2.76, 0.55, 1.8], [5.4, 0.4, 1.3], [8.93, 0.25, 0.9], [13.34, 0.15, 0.6]], ph = [parts.map(() => 0), parts.map(() => 0)];
  bus.stereo(t, dur, (i, x, o) => {
    for (let s = 0; s < 2; s++) { let y = 0; for (let k = 0; k < parts.length; k++) { ph[s][k] += TAU * base * parts[k][0] * (s ? 1.004 : 1) / SR; y += Math.sin(ph[s][k]) * parts[k][1] * Math.exp(-x / parts[k][2]); } o[s] = y * 0.4 * Math.min(1, x / 0.001); }
  }, gain, 0.6);
}
/** A neon tube striking: buzz that stutters on, then settles. */
function neon(bus, t, dur, gain) {
  const on = [[0, 0.05], [0.13, 0.16], [0.34, 0.37], [0.42, 0.5], [0.62, 99]], hp = new BQ().set('hp', 180, 0.7), sp = new BQ().set('hp', 3000, 0.7); let ph = 0;
  bus.voice(t, dur, (i, x) => {
    ph += TAU * 100 / SR;
    let lit = 0, edge = 0; for (const [a, b] of on) { if (x >= a && x < b) lit = 1; if (Math.abs(x - a) < 0.006) edge = 1; }
    const level = x < 0.62 ? 1 : 0.35 + 0.65 * Math.exp(-(x - 0.62) / 0.8);
    return hp.p(Math.tanh(4 * Math.sin(ph)) * 0.5 * lit * level) + sp.p((rnd() * 2 - 1) * edge) * 1.5;
  }, gain, 0, 0.4);
}
function glitch(bus, t, gain) {
  for (let k = 0; k < 7; k++) {
    const f = rr(300, 2400), d = 0.025; let ph = 0;
    bus.voice(t + k * 0.045, d, (i, x) => { ph += f / SR; return ((ph % 1) < 0.5 ? 1 : -1) * 0.5 * (1 - x / d) + (rnd() * 2 - 1) * 0.2; }, gain * (1 - k / 7), k % 2 ? 0.5 : -0.5, 0.3);
  }
}

// ---------------------------------------------------------------- the score (world reveal, D minor, 100 BPM pulse)
const CH = {
  Dm: { root: 38, pad: [50, 57, 62, 65], arp: [62, 65, 69, 74, 77, 74, 69, 65] },
  Bb: { root: 34, pad: [46, 53, 58, 62], arp: [58, 62, 65, 70, 74, 70, 65, 62] },
  Gm: { root: 31, pad: [43, 50, 55, 58], arp: [55, 58, 62, 67, 70, 67, 62, 58] },
  Eb: { root: 39, pad: [51, 55, 58, 63], arp: [63, 67, 70, 75, 79, 75, 70, 67] },
  A: { root: 33, pad: [45, 52, 57, 61, 64], arp: [57, 61, 64, 69, 73, 69, 64, 61] },
};
// Acceleration act: one chord per cut (edl_reveal_v1 C12-C24), all on the 100 BPM grid.
const PROG = [[50.4, 2.4, 'Dm'], [52.8, 1.8, 'Bb'], [54.6, 1.8, 'Gm'], [56.4, 1.8, 'Eb'], [58.2, 1.2, 'Dm'], [59.4, 1.2, 'Bb'], [60.6, 1.2, 'Gm'], [61.8, 0.9, 'Eb'], [62.7, 0.9, 'A'], [63.6, 2.4, 'Dm']];
const chordAt = t => CH[PROG.find(([s, d]) => t >= s - 1e-9 && t < s + d - 1e-9)[2]];
const CUTS = [52.8, 54.6, 56.4, 58.2, 59.4, 60.6, 61.8, 62.7, 63.6, 64.2, 64.8, 65.4];
const STATEMENTS = [52.8, 56.4, 60.6];
const THEME = [[0, 74], [0.6, 75], [1.2, 74], [1.8, 72], [2.6, 69]];   // D Eb D C A: the night motif
const STOP1 = 37.8, STOP2 = 49.8, DROP = 66.0, TITLE = 67.2, CARD = 77.4;

function stemPads() {
  const b = new Bus(0.88, 0.3);
  pad(b, 1.2, 7.4, [74, 81], { att: 3.5, rel: 1.2, gain: 0.03, c0: 1200, c1: 2200, send: 0.95 });                      // a thin cold air over the black
  pad(b, 8.4, 8.5, [38, 50, 57, 62], { att: 2.5, rel: 0.4, gain: 0.07, c0: 320, c1: 820, send: 0.8 });                 // first reveal: low strings, Dm
  pad(b, 16.8, 3.05, [46, 53, 58, 62, 65], { att: 0.06, rel: 0.3, gain: 0.07, c0: 1100, c1: 700 });                    // towers: Bb
  pad(b, 19.8, 1.85, [43, 50, 55, 58, 62], { att: 0.06, rel: 0.3, gain: 0.07, c0: 1000, c1: 650 });                    // Gm
  pad(b, 21.6, 2.45, [39, 51, 55, 58, 63], { att: 0.06, rel: 0.3, gain: 0.07, c0: 900, c1: 600 });                     // Eb
  pad(b, 24.0, STOP1 - 24.0, [38, 50, 57, 62, 63], { att: 0.5, cut: true, gain: 0.065, c0: 380, c1: 1500, trem: 0.3, tr0: 1.667, tr1: 3.333, cresc: 1.1 }); // the pulse: Eb rubs the D
  pad(b, 38.4, 3.0, [38, 45, 50, 53], { att: 0.05, rel: 0.5, gain: 0.08, c0: 800, c1: 300 });                          // the gangster: low, closed
  pad(b, 42.0, 3.0, [46, 53, 57, 62, 65, 69], { att: 0.08, rel: 0.5, gain: 0.07, c0: 1600, c1: 900, send: 0.8 });     // the saviour: Bbmaj7, open
  pad(b, 45.6, STOP2 - 45.6, [38, 39, 50, 51, 56, 62], { att: 0.05, cut: true, gain: 0.07, c0: 450, c1: 3200, trem: 0.6, tr0: 5, tr1: 12, cresc: 1.5, send: 0.5 }); // the devil: tremolo cluster
  for (const [s, d, c] of PROG) pad(b, s, s === 63.6 ? DROP - s : d + 0.05, CH[c].pad, { att: 0.04, rel: 0.25, gain: 0.045, c0: 1100 + 90 * (s - 50.4), c1: 1100 + 90 * (s + d - 50.4), send: 0.45, cut: s === 63.6 });
  pad(b, TITLE + 0.2, 4.0, [38, 50, 57, 62, 65, 69, 76], { att: 1.8, rel: 1.0, gain: 0.065, formant: true, send: 0.8 }); // choir: Dm add9
  pad(b, 71.2, 2.6, [46, 50, 57, 62, 65, 69], { att: 0.8, rel: 1.0, gain: 0.065, formant: true, send: 0.8 });          // Bbmaj7
  pad(b, 73.6, 2.2, [43, 50, 55, 58, 62, 67], { att: 0.8, rel: 1.0, gain: 0.065, formant: true, send: 0.8 });          // Gm
  pad(b, 75.6, 3.2, [38, 45, 50, 57, 62], { att: 0.6, rel: 2.4, gain: 0.065, formant: true, send: 0.9 });              // open fifths on D
  pad(b, CARD, 6.0, [74, 81], { att: 1.5, rel: 1.5, gain: 0.025, c0: 1800, send: 0.95 });                             // the card: the cold air again
  return b.finish(1);
}
function stemPerc() {
  const p = new Bus(0.84, 0.4);
  boom(p, 8.4, { gain: 0.32, f0: 55, f1: 28, tau: 1.8, dur: 5, noise: 0.2 });
  boom(p, 16.8, { gain: 0.42 }); tom(p, 16.8, 70, 0.3);
  boom(p, 19.8, { gain: 0.3, tau: 0.8 }); tom(p, 19.8, 62, 0.35);
  tom(p, 21.6, 58, 0.22, 0.2, 0.4);
  for (let t = 24.0; t < STOP1 - 1e-6; t += 1.2) heart(p, t, 0.22 + 0.25 * (t - 24) / 13.8);
  for (let t = 30.0; t < STOP1 - 1e-6; t += 1.2) kick(p, t, 0.3 + 0.2 * (t - 30) / 7.8);
  for (let t = 30.0; t < STOP1 - 1e-6; t += 0.3) tick(p, t, 0.04 + 0.03 * (t - 30) / 7.8, Math.round((t - 30) / 0.3) % 2 ? 0.3 : -0.3);
  for (let t = 33.0; t < STOP1 - 1e-6; t += 0.15) hat(p, t, Math.round((t - 33) / 0.15) % 4 === 0 ? 0.06 : 0.035, false, 0.25);
  for (let t = 35.4; t < STOP1 - 1e-6; t += t < 36.6 ? 0.6 : 0.3) tom(p, t, 58, 0.25 + 0.2 * (t - 35.4) / 2.4, rr(-0.2, 0.2), 0.5);
  for (let t = 36.6; t < STOP1 - 0.02;) { const u = (t - 36.6) / 1.2; snare(p, t, 0.06 + 0.24 * u); t += u < 0.5 ? 0.075 : 0.0375; }
  boom(p, 38.4, { gain: 0.55, f0: 85, f1: 30, tau: 1.4 }); tom(p, 38.4, 55, 0.45); crash(p, 38.4, 0.1);
  boom(p, 42.0, { gain: 0.4, f0: 70, f1: 32, tau: 1.2 }); tom(p, 42.0, 62, 0.35);
  boom(p, 45.6, { gain: 0.6, f0: 70, f1: 26, tau: 2.4, dur: 6 }); tom(p, 45.6, 48, 0.5, 0, 0.7);
  for (let t = 46.8; t < STOP2 - 1e-6;) { const u = (t - 46.8) / 3; heart(p, t, 0.3 + 0.2 * u); t += 1.0 - 0.5 * u; }
  for (let t = 48.0; t < STOP2 - 1e-6; t += 0.3) tick(p, t, 0.05 + 0.04 * (t - 48) / 1.8, Math.round((t - 48) / 0.3) % 2 ? 0.3 : -0.3);
  boom(p, 50.4, { gain: 0.5, f0: 90, f1: 30, tau: 1.6, dur: 5 }); crash(p, 50.4, 0.16, 2.2);
  for (let bar = 0; bar < 7; bar++) {                 // 50.4-66 groove, 100 BPM half-time; 16ths are 0.15 s
    const t0 = 50.4 + bar * 2.4;
    for (const s of [0, 6, 10]) kick(p, t0 + s * 0.15, 0.5);
    snare(p, t0 + 1.2, 0.32);
    for (let s = 0; s < 16; s += bar >= 5 ? 1 : 2) { const t = t0 + s * 0.15; if (t < DROP - 1e-6) hat(p, t, s % 4 === 0 ? 0.07 : 0.045, s === 14 && bar % 2 === 1, 0.25); }
  }
  for (const t of CUTS) { tom(p, t, 50, 0.42, 0, 0.6); boom(p, t, { gain: 0.2, tau: 0.6, dur: 2.5 }); }
  for (let k = 0; k < 8; k++) { const t = 63.6 + k * 0.3; tom(p, t, 60 + k * 6, 0.32 + 0.03 * k, k % 2 ? 0.3 : -0.3, 0.4); }
  for (let t = 64.2; t < DROP - 0.02;) { const u = (t - 64.2) / 1.8; snare(p, t, 0.1 + 0.3 * u); t += u < 0.5 ? 0.075 : 0.0375; }
  boom(p, TITLE, { gain: 0.6, f0: 95, f1: 28, tau: 2.2, dur: 7 }); crash(p, TITLE, 0.14, 2.6); tom(p, TITLE, 45, 0.5, 0, 0.9);
  boom(p, CARD, { gain: 0.26, f0: 60, f1: 28, ptau: 0.5, tau: 2.0, dur: 5, noise: 0 });
  return p.finish(1);
}
function stemBass() {
  const b = new Bus(0.7, 0.5);
  drone(b, 1.2, 22.9, 26, { att: 5, rel: 0.4, gain: 0.22 });
  drone(b, 24.0, STOP1 - 24.0, 26, { att: 0.3, gain: 0.26, cut: true, pulse: 0.5, pulseRate: 0.8333 });
  for (let t = 30.0; t < STOP1 - 1e-6; t += 0.3) { const u = (t - 30) / 7.8; bassNote(b, t, 38, 0.2, { gain: 0.06 + 0.12 * u, c0: 500 + 1300 * u, c1: 130 }); }
  drone(b, 38.4, 3.0, 26, { att: 0.05, rel: 0.5, gain: 0.26 });
  bassNote(b, 42.0, 34, 2.8, { gain: 0.16, c0: 700, c1: 180 });
  drone(b, 45.6, STOP2 - 45.6, 26, { att: 0.05, gain: 0.28, cut: true, pulse: 0.6, pulseRate: 1.25 });
  const pat = [0, 0, 12, 0, 0, 12, 0, 7];
  for (let t = 50.4; t < DROP - 1e-6; t += 0.3) {
    const c = chordAt(t), k = Math.round((t - 50.4) / 0.3), step = k % 8;
    bassNote(b, t, c.root + pat[step], 0.24, { gain: 0.2, c0: t >= 63.6 ? 900 + 500 * (t - 63.6) : 900, c1: 140 });
  }
  for (let t = 64.8; t < DROP - 1e-6; t += 0.15) bassNote(b, t + 0.075, 38 + 12 * ((Math.round((t - 64.8) / 0.15) % 2)), 0.07, { gain: 0.12, c0: 2200, c1: 300 });
  drone(b, TITLE, CARD - TITLE - 0.4, 26, { att: 0.05, rel: 2.5, gain: 0.22 });
  bassNote(b, TITLE, 38, 3.8, { gain: 0.16, c0: 600, c1: 200 });
  bassNote(b, 71.2, 34, 2.3, { gain: 0.16, c0: 600, c1: 200 });
  bassNote(b, 73.6, 31, 1.9, { gain: 0.16, c0: 600, c1: 200 });
  bassNote(b, 75.6, 38, 3.0, { gain: 0.14, c0: 600, c1: 200 });
  boom(b, CARD, { gain: 0.3, f0: 85, f1: 28, ptau: 0.6, tau: 1.6, dur: 4, noise: 0, send: 0 });   // AXIOM sub drop
  return b.finish(0);
}
function stemSynths() {
  const s = new Bus(0.87, 0.25);
  for (const [dt, m] of THEME) bell(s, 11.6 + dt, m, { gain: 0.055, tau: 2.0, send: 0.9, pan: -0.2 });          // "...that never sleeps": the motif
  for (const [dt, m] of THEME) bell(s, 20.0 + dt, m - 12, { gain: 0.05, tau: 2.2, ratio: 2.0, index: 1.5, send: 0.85, pan: 0.2 });
  for (let t = 30.0; t < STOP1 - 0.1;) { blip(s, t, rr(600, 2600), 0.025); t += rr(0.2, 0.9) * (1 - 0.6 * (t - 30) / 7.8); }
  for (let t = 33.0; t < STOP1 - 1e-6; t += 0.3) { const k = Math.round((t - 33) / 0.3); arp(s, t, CH.Dm.arp[k % 8], 500 + 2500 * (t - 33) / 4.8, { gain: 0.03 + 0.02 * (t - 33) / 4.8, pan: k % 2 ? 0.4 : -0.4 }); }
  let wp = 0;
  s.voice(45.6, STOP2 - 45.6, (i, x) => { wp += TAU * (3100 + 300 * x / 4.2) / SR; return Math.sin(wp) * Math.pow(x / 4.2, 1.5); }, 0.006, 0.1, 0.3); // a thin whine under the devil
  for (let t = 50.4; t < DROP - 1e-6; t += 0.15) {
    const k = Math.round((t - 50.4) / 0.15), cut = t < 63.6 ? 600 + 3000 * (t - 50.4) / 13.2 : 3600 + 2400 * (t - 63.6) / 2.4;
    arp(s, t, chordAt(t).arp[k % 8], cut, { gain: 0.045, pan: k % 2 ? 0.45 : -0.45 });
  }
  const theme2 = [[0, 74], [0.8, 75], [1.6, 74], [2.4, 72], [3.4, 69], [4.6, 77], [5.4, 75], [6.2, 74], [7.0, 72], [7.8, 74]];
  for (const [dt, m] of theme2) bell(s, 69.6 + dt, m, { gain: 0.055, tau: 2.2, send: 0.9, pan: rr(-0.3, 0.3) });
  for (const [dt, m] of THEME) piano(s, 72.0 + dt * 1.15, m - 12, { gain: 0.12, dur: 4 });
  swell(s, CARD, 1.0, 0.12);                                // AXIOM signature: D6, A5, E6 chimes
  bell(s, CARD + 0.02, 86, { gain: 0.07, tau: 2.5, send: 0.9, pan: -0.3 });
  bell(s, CARD + 0.20, 81, { gain: 0.07, tau: 2.5, send: 0.9, pan: 0.3 });
  bell(s, CARD + 0.38, 88, { gain: 0.06, tau: 3.0, send: 0.95, pan: 0 });
  piano(s, 80.4, 50, { gain: 0.1, dur: 4.5 });
  return s.finish(1);
}
function stemHits() {
  const h = new Bus(0.88, 0.3);
  swell(h, 8.4, 1.4, 0.1); brass(h, 8.4, 1.0, [38, 45, 50], { gain: 0.08, rel: 1.6, send: 0.8, cpk: 1800, cend: 500 });
  swell(h, 16.8, 1.0, 0.14); brass(h, 16.8, 0.8, [38, 45, 50, 53], { gain: 0.16, rel: 1.2, send: 0.7 });
  brass(h, 19.8, 0.6, [34, 46, 53, 58], { gain: 0.14, rel: 1.0, send: 0.7 });
  riser(h, 34.0, STOP1 - 34.0, { gain: 0.22, base: 50 });
  brass(h, 38.4, 1.4, [26, 38, 45, 50, 53], { gain: 0.24, cpk: 3500, cend: 700, rel: 1.4, drive: 2.2 });
  brass(h, 42.0, 1.2, [34, 46, 53, 58, 65], { gain: 0.17, cpk: 2200, cend: 700, rel: 1.6, drive: 1.8 });
  brass(h, 45.6, 1.8, [26, 38, 41, 45, 50], { gain: 0.26, cpk: 3000, cend: 600, rel: 2.0, drive: 2.4 });
  riser(h, 46.8, STOP2 - 46.8, { gain: 0.18, base: 50 });
  swell(h, 50.4, 1.0, 0.2);
  brass(h, 50.4, 2.2, [26, 38, 45, 50], { gain: 0.3, att: 0.12, cpk: 1800, cend: 650, rel: 1.5, drive: 2.6 });
  for (const t of CUTS) brass(h, t, 0.45, chordAt(t).pad.map(m => m - 12), { gain: STATEMENTS.includes(t) ? 0.2 : 0.15, rel: 0.6 });
  riser(h, 61.8, DROP - 61.8, { gain: 0.3, base: 57 });
  swell(h, TITLE, 0.9, 0.24);
  brass(h, TITLE, 2.2, [26, 33, 38, 45, 50, 57], { gain: 0.34, cpk: 4000, cend: 700, rel: 2.5, drive: 2.6 });
  swell(h, CARD, 1.0, 0.1);
  return h.finish(1);
}
function stemSfx() {
  const x = new Bus(0.8, 0.45);
  cityBed(x, curve([[0, -90], [1.2, -50], [8.3, -48], [8.6, -36], [16.8, -34], [24, -30], [37.75, -30], [37.8, -120], [38.4, -120], [38.45, -41], [41.4, -120], [42.0, -120], [42.05, -39], [45.0, -120], [45.6, -120], [45.65, -47], [49.8, -120], [50.4, -120], [50.45, -34], [65.95, -34], [66.0, -120], [68.4, -120], [70.0, -46], [76.5, -48], [77.4, -120]]),
    curve([[0, 400], [8.3, 400], [8.6, 1600], [24, 2600], [37.8, 2600], [38.4, 1200], [42, 2200], [45.6, 700], [50.4, 2600], [66, 2600], [68.4, 600], [77, 500]]));
  wind(x, curve([[0, -120], [8.4, -120], [10.5, -46], [16.6, -46], [17, -120], [42.0, -120], [42.1, -44], [45.0, -120], [45.6, -120], [45.7, -46], [49.8, -120], [50.4, -120], [50.5, -43], [58.2, -43], [58.4, -120], [68.4, -120], [70, -47], [76.5, -50], [77.4, -120]]));
  hum(x, curve([[0, -120], [38.4, -120], [38.5, -50], [41.4, -120], [64.8, -120], [64.85, -46], [65.4, -120], [68.4, -120], [70, -52], [76.5, -56], [77.4, -120]]));
  horn(x, 10.2, 330, 0.5, { gain: 0.012, pan: 0.5, cutoff: 700 }); horn(x, 14.1, 410, 0.3, { gain: 0.012, pan: -0.6, cutoff: 700, beeps: 2 });
  for (const [t, f, d, n, pan] of [[24.6, 470, 0.18, 3, -0.4], [26.1, 300, 0.6, 1, 0.5], [27.9, 520, 0.12, 4, 0.2], [29.4, 390, 0.25, 2, -0.7], [31.3, 560, 0.1, 5, 0.6], [33.2, 320, 0.45, 2, -0.2], [34.9, 480, 0.15, 3, 0.4], [36.4, 410, 0.3, 1, -0.5]])
    horn(x, t, f, d, { gain: 0.035, pan, cutoff: 3500, beeps: n, gap: 0.09 });
  for (const [t, pan] of [[25.4, 0.3], [28.7, -0.5], [32.0, 0.6], [35.6, -0.3]]) rickshawBell(x, t, { gain: 0.03, pan });
  cng(x, 25.0, 4.5, { gain: 0.045, panFrom: -0.9, panTo: 0.8 }); cng(x, 31.6, 3.5, { gain: 0.03, panFrom: 0.8, panTo: -0.3, rate: 38, cutoff: 1500 });
  bikePass(x, 30.6, { gain: 0.1 }); siren(x, 33.0, 4.7, { gain: 0.02 });
  metal(x, 45.6, { gain: 0.06, dur: 4 });
  for (const t of STATEMENTS) whoosh(x, t - 0.1, 0.45, 0.13, t % 2 ? 1 : -1);
  bikePass(x, 60.9, { gain: 0.1, f: 95 }); horn(x, 61.95, 380, 0.35, { gain: 0.04, pan: 0.5, cutoff: 2500 });
  neon(x, 64.8, 0.6, 0.05);
  whoosh(x, DROP - 0.18, 0.5, 0.14, -1);
  metal(x, TITLE, { gain: 0.08, dur: 8 }); neon(x, 68.4, 3.0, 0.05);
  glitch(x, CARD, 0.04);
  return x.finish(1);
}
// [name, builder, trim dB] - the trims set the balance between stems.
const STEMS = [['music_1_drones_pads', stemPads, 2], ['music_2_percussion', stemPerc, -11], ['music_3_bass', stemBass, -1], ['music_4_synths_arps', stemSynths, 0], ['music_5_swells_hits', stemHits, -7], ['sfx_dhaka_sound_design', stemSfx, 5]];

// ---------------------------------------------------------------- BS.1770-4 meter, true peak, limiter
function kweight(x) {
  const y = new Float32Array(x.length);
  const s1 = [1.53512485958697, -2.69169618940638, 1.19839281085285, -1.69065929318241, 0.73248077421585], s2 = [1, -2, 1, -1.99004745483398, 0.99007225036621];
  let a1 = 0, a2 = 0, b1 = 0, b2 = 0, c1 = 0, c2 = 0, d1 = 0, d2 = 0;
  for (let i = 0; i < x.length; i++) {
    const v = x[i], u = s1[0] * v + s1[1] * a1 + s1[2] * a2 - s1[3] * b1 - s1[4] * b2; a2 = a1; a1 = v; b2 = b1; b1 = u;
    const w = s2[0] * u + s2[1] * c1 + s2[2] * c2 - s2[3] * d1 - s2[4] * d2; c2 = c1; c1 = u; d2 = d1; d1 = w;
    y[i] = w;
  }
  return y;
}
const lufs = ms => -0.691 + 10 * Math.log10(ms);
const mean = a => a.reduce((s, v) => s + v, 0) / a.length;
function loudness(L, R, sr = SR) {
  const kl = kweight(L), kr = kweight(R), step = sr / 10, nb = Math.floor(L.length / step), sq = new Float64Array(nb);
  for (let b = 0; b < nb; b++) { let s = 0; for (let i = b * step; i < (b + 1) * step; i++) s += kl[i] * kl[i] + kr[i] * kr[i]; sq[b] = s / step; }
  const mom = [], st = [];
  for (let b = 3; b < nb; b++) mom.push((sq[b] + sq[b - 1] + sq[b - 2] + sq[b - 3]) / 4);
  for (let b = 29; b < nb; b++) { let s = 0; for (let k = 0; k < 30; k++) s += sq[b - k]; st.push(s / 30); }
  const abs = mom.filter(m => lufs(m) > -70);
  const I = abs.length ? (() => { const rel = lufs(mean(abs)) - 10, g = abs.filter(m => lufs(m) > rel); return lufs(mean(g)); })() : -Infinity;
  const sa = st.filter(m => lufs(m) > -70);
  let LRA = 0;
  if (sa.length) { const rel = lufs(mean(sa)) - 20, g = sa.filter(m => lufs(m) > rel).map(lufs).sort((a, b) => a - b); const pc = q => g[Math.min(g.length - 1, Math.round(q * (g.length - 1)))]; LRA = pc(0.95) - pc(0.10); }
  const perSecond = []; for (let s = 1; s * 10 <= nb; s++) { const k = s * 10 - 30; perSecond.push(k >= 0 ? +lufs(st[k]).toFixed(1) : null); }
  return { I, maxM: lufs(Math.max(...mom)), maxS: lufs(Math.max(...st)), LRA, shortTermEndingEachSecond: perSecond };
}
// 4x oversampling polyphase interpolator (24 taps per phase, Blackman-windowed sinc). Value i covers (i, i+1).
const TAPS = 24, UP = 4;
const PHASES = (() => {
  const Lt = TAPS * UP, c = (Lt - 1) / 2, h = new Float64Array(Lt), out = [];
  for (let n = 0; n < Lt; n++) { const x = (n - c) / UP, s = x === 0 ? 1 : Math.sin(Math.PI * x) / (Math.PI * x); h[n] = s * (0.42 - 0.5 * Math.cos(TAU * n / (Lt - 1)) + 0.08 * Math.cos(2 * TAU * n / (Lt - 1))); }
  for (let p = 0; p < UP; p++) { const a = new Float64Array(TAPS); let sum = 0; for (let k = 0; k < TAPS; k++) { a[k] = h[k * UP + p]; sum += a[k]; } for (let k = 0; k < TAPS; k++) a[k] /= sum; out.push(a); }
  return out;
})();
function truePeakEnvelope(x) {
  const n = x.length, out = new Float32Array(n), D = TAPS / 2;
  for (let i = 0; i < n; i++) {
    let m = Math.abs(x[i]);
    for (let p = 0; p < UP; p++) {
      const a = PHASES[p]; let s = 0;
      for (let k = 0; k < TAPS; k++) { const j = i - k + D; if (j >= 0 && j < n) s += a[k] * x[j]; }
      if (Math.abs(s) > m) m = Math.abs(s);
    }
    out[i] = m;
  }
  return out;
}
const dbfs = v => 20 * Math.log10(Math.max(v, 1e-12));
function maxOf(a) { let m = 0; for (let i = 0; i < a.length; i++) if (a[i] > m) m = a[i]; return m; }
/**
 * Look-ahead true-peak limiter. tpl/tpr: true-peak envelopes of the unscaled input (gain is linear, so they scale).
 * Gain = box average over the last `la` samples of a released, forward-looking minimum, which never exceeds what any peak needs.
 */
function limiterGain(tpl, tpr, gain, ceilDb, laMs = 2, relMs = 150) {
  const n = tpl.length, c = db(ceilDb), la = Math.round(laMs * SR / 1000), g = new Float32Array(n);
  for (let i = 0; i < n; i++) { const p = Math.max(tpl[i], tpr[i]) * gain; g[i] = p > c ? c / p : 1; }
  const m = new Float32Array(n), dq = new Int32Array(n); let h = 0, t = 0;   // m[i] = min(g[i..i+la])
  for (let i = n - 1; i >= 0; i--) { while (t > h && g[dq[t - 1]] >= g[i]) t--; dq[t++] = i; while (dq[h] > i + la) h++; m[i] = g[dq[h]]; }
  const rc = Math.exp(-1 / (relMs * SR / 1000)); let s = 1;
  for (let i = 0; i < n; i++) { s = Math.min(m[i], 1 - (1 - s) * rc); m[i] = s; }
  const out = new Float32Array(n); let acc = 0;
  for (let i = 0; i < n; i++) { acc += m[i]; if (i > la) acc -= m[i - la - 1]; out[i] = acc / Math.min(i + 1, la + 1); }
  return out;
}

// ---------------------------------------------------------------- WAV
function writeWav(file, L, R, { gain = 1, float = false } = {}) {
  const n = L.length, bps = float ? 4 : 3, buf = Buffer.alloc(44 + n * 2 * bps);
  buf.write('RIFF', 0); buf.writeUInt32LE(36 + n * 2 * bps, 4); buf.write('WAVE', 8); buf.write('fmt ', 12);
  buf.writeUInt32LE(16, 16); buf.writeUInt16LE(float ? 3 : 1, 20); buf.writeUInt16LE(2, 22); buf.writeUInt32LE(SR, 24);
  buf.writeUInt32LE(SR * 2 * bps, 28); buf.writeUInt16LE(2 * bps, 32); buf.writeUInt16LE(bps * 8, 34); buf.write('data', 36); buf.writeUInt32LE(n * 2 * bps, 40);
  let o = 44;
  for (let i = 0; i < n; i++) for (const v of [L[i] * gain, R[i] * gain]) {
    if (float) { buf.writeFloatLE(v, o); o += 4; } else { buf.writeIntLE(Math.round(Math.max(-1, Math.min(1, v)) * 8388607), o, 3); o += 3; }
  }
  fs.writeFileSync(file, buf);
}
function readWav(file) {
  const b = fs.readFileSync(file); let o = 12, fmt = null, data = null;
  while (o + 8 <= b.length) { const id = b.toString('ascii', o, o + 4), sz = b.readUInt32LE(o + 4); if (id === 'fmt ') fmt = { tag: b.readUInt16LE(o + 8), ch: b.readUInt16LE(o + 10), sr: b.readUInt32LE(o + 12), bits: b.readUInt16LE(o + 22) }; if (id === 'data') data = [o + 8, sz]; o += 8 + sz + (sz & 1); }
  const { tag, ch, sr, bits } = fmt, bps = bits / 8, n = Math.floor(data[1] / (bps * ch)), L = new Float32Array(n), R = new Float32Array(n);
  const rd = p => tag === 3 ? b.readFloatLE(p) : bits === 16 ? b.readInt16LE(p) / 32768 : bits === 24 ? b.readIntLE(p, 3) / 8388608 : b.readInt32LE(p) / 2147483648;
  for (let i = 0; i < n; i++) { const p = data[0] + i * bps * ch; L[i] = rd(p); R[i] = ch > 1 ? rd(p + bps) : L[i]; }
  return { L, R, sr };
}

// ---------------------------------------------------------------- commands
function render(outDir) {
  fs.mkdirSync(outDir, { recursive: true });
  const mixL = new Float32Array(N), mixR = new Float32Array(N), stems = [];
  for (const [name, fn, trim] of STEMS) {
    seed = [...name].reduce((h, c) => (h * 31 + c.charCodeAt(0)) | 0, 7);
    const t0 = Date.now(), b = fn(), k = db(trim);
    for (let i = 0; i < N; i++) { b.L[i] *= k; b.R[i] *= k; }
    for (let i = 0; i < N; i++) { mixL[i] += b.L[i]; mixR[i] += b.R[i]; }
    const m = loudness(b.L, b.R);
    console.log(`${name}: ${m.I.toFixed(1)} LUFS raw, max short-term ${m.maxS.toFixed(1)}, sample peak ${dbfs(Math.max(maxOf(b.L.map(Math.abs)), maxOf(b.R.map(Math.abs)))).toFixed(1)} dBFS (${((Date.now() - t0) / 1000).toFixed(1)} s)`);
    stems.push({ name, L: b.L, R: b.R, rawI: m.I });
  }
  const { outL, outR, gain, m, tp, sp, minLim, limited } = masterize(mixL, mixR);
  writeWav(path.join(outDir, 'DAD_WorldReveal_v1_master_48k24.wav'), outL, outR);
  for (const s of stems) writeWav(path.join(outDir, `DAD_WorldReveal_v1_stem_${s.name}.wav`), s.L, s.R, { gain, float: true });
  const report = {
    file: 'DAD_WorldReveal_v1_master_48k24.wav', sampleRate: SR, seconds: N / SR, samples: N,
    integratedLUFS: +m.I.toFixed(2), truePeakDBTP: +tp.toFixed(2), samplePeakDBFS: +sp.toFixed(2), maxMomentaryLUFS: +m.maxM.toFixed(1), maxShortTermLUFS: +m.maxS.toFixed(1), loudnessRangeLU: +m.LRA.toFixed(1),
    target: { integratedLUFS: TARGET_LUFS, ceilingDBTP: CEIL_DBTP }, stemGainDB: +dbfs(gain).toFixed(2),
    limiter: { maxReductionDB: +(-dbfs(minLim)).toFixed(2), secondsReduced: +(limited / SR).toFixed(2) },
    stems: stems.map(s => ({ name: s.name, rawLUFS: +s.rawI.toFixed(1) })),
    note: 'Stems are float WAV at the master gain before the limiter; their sum through the limiter is the master.',
    shortTermEndingEachSecond: m.shortTermEndingEachSecond,
  };
  fs.writeFileSync(path.join(outDir, 'loudness.json'), JSON.stringify(report, null, 1));
  console.log(`master: ${m.I.toFixed(2)} LUFS integrated, ${tp.toFixed(2)} dBTP true peak, LRA ${m.LRA.toFixed(1)} LU, max short-term ${m.maxS.toFixed(1)}, limiter max ${report.limiter.maxReductionDB} dB over ${report.limiter.secondsReduced} s`);
}
/** Gain to the target loudness, then the true-peak limiter; repeats until the limited result is on target. */
function masterize(mixL, mixR) {
  const n = mixL.length, raw = loudness(mixL, mixR), tpl = truePeakEnvelope(mixL), tpr = truePeakEnvelope(mixR);
  console.log(`raw mix: ${raw.I.toFixed(2)} LUFS, true peak ${dbfs(Math.max(maxOf(tpl), maxOf(tpr))).toFixed(2)} dBTP`);
  let gain = db(TARGET_LUFS - raw.I), lim, m;
  const outL = new Float32Array(n), outR = new Float32Array(n);
  for (let it = 0; it < 4; it++) {
    lim = limiterGain(tpl, tpr, gain, CEIL_DBTP);
    for (let i = 0; i < n; i++) { outL[i] = mixL[i] * gain * lim[i]; outR[i] = mixR[i] * gain * lim[i]; }
    m = loudness(outL, outR);
    console.log(`pass ${it + 1}: gain ${dbfs(gain).toFixed(2)} dB -> ${m.I.toFixed(2)} LUFS`);
    if (Math.abs(m.I - TARGET_LUFS) < 0.05) break;
    gain *= db(TARGET_LUFS - m.I);
  }
  let minLim = 1, limited = 0; for (let i = 0; i < n; i++) { if (lim[i] < minLim) minLim = lim[i]; if (lim[i] < 0.999) limited++; }
  const tp = dbfs(Math.max(maxOf(truePeakEnvelope(outL)), maxOf(truePeakEnvelope(outR))));
  const sp = dbfs(Math.max(maxOf(outL.map(Math.abs)), maxOf(outR.map(Math.abs))));
  return { outL, outR, gain, m, tp, sp, minLim, limited };
}
function measure(file) {
  const { L, R, sr } = readWav(file);
  if (sr !== SR) throw new Error(`meter is set up for ${SR} Hz, file is ${sr} Hz`);
  const m = loudness(L, R), tp = dbfs(Math.max(maxOf(truePeakEnvelope(L)), maxOf(truePeakEnvelope(R))));
  console.log(JSON.stringify({ file: path.basename(file), seconds: +(L.length / sr).toFixed(4), integratedLUFS: +m.I.toFixed(2), truePeakDBTP: +tp.toFixed(2), maxMomentaryLUFS: +m.maxM.toFixed(1), maxShortTermLUFS: +m.maxS.toFixed(1), loudnessRangeLU: +m.LRA.toFixed(1) }));
}
function selftest() {
  const sine = (secs, dBFS, f = 1000, phase = 0) => { const n = secs * SR, a = new Float32Array(n), A = db(dBFS); for (let i = 0; i < n; i++) a[i] = A * Math.sin(TAU * f * i / SR + phase); return a; };
  const cat = (...p) => { const n = p.reduce((s, a) => s + a.length, 0), o = new Float32Array(n); let k = 0; for (const a of p) { o.set(a, k); k += a.length; } return o; };
  const cases = [
    ['3341 #1 stereo 1 kHz at -23 dBFS', sine(20, -23), -23],
    ['3341 #2 stereo 1 kHz at -33 dBFS', sine(20, -33), -33],
    ['3341 #3 -36/-23/-36 dBFS (gating)', cat(sine(10, -36), sine(60, -23), sine(10, -36)), -23],
    ['3341 #4 -72/-36/-23/-36/-72 dBFS', cat(sine(10, -72), sine(10, -36), sine(60, -23), sine(10, -36), sine(10, -72)), -23],
  ];
  let ok = true;
  for (const [name, x, want] of cases) { const I = loudness(x, x).I, pass = Math.abs(I - want) <= 0.1; ok &&= pass; console.log(`${pass ? 'PASS' : 'FAIL'} ${name}: ${I.toFixed(2)} LUFS (want ${want} +-0.1)`); }
  // True peak: fs/4 sine at 45 degrees has sample peaks at -3.01 dB but a true peak of 0 dBTP (3341 #15 style).
  const tpx = sine(2, 0, SR / 4, Math.PI / 4), tp = dbfs(maxOf(truePeakEnvelope(tpx))), sp = dbfs(maxOf(tpx.map(Math.abs)));
  const tpPass = tp > -0.4 && tp < 0.2; ok &&= tpPass;
  console.log(`${tpPass ? 'PASS' : 'FAIL'} true peak, 12 kHz sine at 45 deg: ${tp.toFixed(2)} dBTP (sample peak ${sp.toFixed(2)} dBFS; want 0, +0.2/-0.4)`);
  const tp2 = dbfs(maxOf(truePeakEnvelope(sine(2, -6, 997)))), p2 = tp2 > -6.1 && tp2 < -5.9; ok &&= p2;
  console.log(`${p2 ? 'PASS' : 'FAIL'} true peak, 997 Hz sine at -6 dBFS: ${tp2.toFixed(2)} dBTP`);
  process.exitCode = ok ? 0 : 1;
}

const [cmd, arg] = process.argv.slice(2);
if (cmd === 'render') render(arg);
else if (cmd === 'measure') measure(arg);
else if (cmd === 'selftest') selftest();
else { console.error('usage: node reveal_audio.mjs render <outDir> | measure <file.wav> | selftest'); process.exitCode = 2; }
