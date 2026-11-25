import { VehicleKind, WeaponId } from './types';

export class DhakaAudioEngine {
  private ctx: AudioContext | null = null;
  private masterGain: GainNode | null = null;
  private engineOsc: OscillatorNode | null = null;
  private engineGain: GainNode | null = null;
  private rainGain: GainNode | null = null;
  private sirenOsc: OscillatorNode | null = null;
  private sirenGain: GainNode | null = null;
  private muted = false;
  private sirenPhase = 0;

  public isMuted(): boolean {
    return this.muted;
  }

  public unlock(): void {
    if (this.ctx) {
      if (this.ctx.state === 'suspended') {
        this.ctx.resume().catch(() => {});
      }
      return;
    }
    try {
      const AudioCtx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      if (!AudioCtx) return;
      this.ctx = new AudioCtx();
      this.masterGain = this.ctx.createGain();
      this.masterGain.gain.value = this.muted ? 0 : 0.42;
      this.masterGain.connect(this.ctx.destination);

      // Continuous vehicle engine oscillator (silent until driving)
      this.engineOsc = this.ctx.createOscillator();
      this.engineOsc.type = 'sawtooth';
      this.engineOsc.frequency.value = 55;
      const engFilter = this.ctx.createBiquadFilter();
      engFilter.type = 'lowpass';
      engFilter.frequency.value = 260;
      this.engineGain = this.ctx.createGain();
      this.engineGain.gain.value = 0;
      this.engineOsc.connect(engFilter);
      engFilter.connect(this.engineGain);
      this.engineGain.connect(this.masterGain);
      this.engineOsc.start();

      // Continuous monsoon rain filtered noise
      const bufferSize = this.ctx.sampleRate * 2;
      const noiseBuf = this.ctx.createBuffer(1, bufferSize, this.ctx.sampleRate);
      const data = noiseBuf.getChannelData(0);
      for (let i = 0; i < bufferSize; i++) {
        data[i] = Math.random() * 2 - 1;
      }
      const rainSrc = this.ctx.createBufferSource();
      rainSrc.buffer = noiseBuf;
      rainSrc.loop = true;
      const rainFilter = this.ctx.createBiquadFilter();
      rainFilter.type = 'bandpass';
      rainFilter.frequency.value = 1100;
      rainFilter.Q.value = 0.6;
      this.rainGain = this.ctx.createGain();
      this.rainGain.gain.value = 0;
      rainSrc.connect(rainFilter);
      rainFilter.connect(this.rainGain);
      this.rainGain.connect(this.masterGain);
      rainSrc.start();

      // Continuous DMP police siren oscillator
      this.sirenOsc = this.ctx.createOscillator();
      this.sirenOsc.type = 'triangle';
      this.sirenOsc.frequency.value = 620;
      this.sirenGain = this.ctx.createGain();
      this.sirenGain.gain.value = 0;
      this.sirenOsc.connect(this.sirenGain);
      this.sirenGain.connect(this.masterGain);
      this.sirenOsc.start();
    } catch {
      // Headless or restricted audio environments fail gracefully
    }
  }

  public setMuted(m: boolean): void {
    this.muted = m;
    if (this.masterGain && this.ctx) {
      this.masterGain.gain.setTargetAtTime(m ? 0 : 0.42, this.ctx.currentTime, 0.04);
    }
  }

  public toggleMute(): boolean {
    this.setMuted(!this.muted);
    return this.muted;
  }

  public silenceLoops(): void {
    if (!this.ctx) return;
    const t = this.ctx.currentTime;
    this.engineGain?.gain.setTargetAtTime(0, t, 0.05);
    this.rainGain?.gain.setTargetAtTime(0, t, 0.05);
    this.sirenGain?.gain.setTargetAtTime(0, t, 0.05);
  }

  public updateAmbient(
    dt: number,
    inVehicle: boolean,
    vehicleKind: VehicleKind | null,
    speedRatio: number,
    rainIntensity: number,
    sirenActive: boolean,
  ): void {
    if (!this.ctx || this.muted) return;
    const t = this.ctx.currentTime;

    if (this.engineOsc && this.engineGain) {
      if (inVehicle && vehicleKind && vehicleKind !== 'rickshaw') {
        const baseFreq =
          vehicleKind === 'cng'
            ? 68
            : vehicleKind === 'supercar'
              ? 76
              : vehicleKind === 'bike'
                ? 88
                : vehicleKind === 'launch'
                  ? 36
                  : vehicleKind === 'speedboat'
                    ? 110
                    : vehicleKind === 'jetski'
                      ? 145
                      : vehicleKind === 'bus' || vehicleKind === 'truck'
                        ? 44
                        : 58;
        this.engineOsc.type = vehicleKind === 'cng' || vehicleKind === 'jetski' ? 'square' : 'sawtooth';
        const pitchMult = vehicleKind === 'supercar' ? 2.75 : 1.9;
        this.engineOsc.frequency.setTargetAtTime(baseFreq + speedRatio * baseFreq * pitchMult, t, 0.05);
        const gainLevel = vehicleKind === 'supercar' ? 0.07 + speedRatio * 0.09 : 0.05 + speedRatio * 0.06;
        this.engineGain.gain.setTargetAtTime(gainLevel, t, 0.07);
      } else {
        this.engineGain.gain.setTargetAtTime(0, t, 0.08);
      }
    }

    if (this.rainGain) {
      this.rainGain.gain.setTargetAtTime(rainIntensity * 0.14, t, 0.25);
    }

    if (this.sirenOsc && this.sirenGain) {
      if (sirenActive) {
        this.sirenPhase += dt * 4.5;
        const freq = 640 + Math.sin(this.sirenPhase) * 210;
        this.sirenOsc.frequency.setTargetAtTime(freq, t, 0.03);
        this.sirenGain.gain.setTargetAtTime(0.045, t, 0.1);
      } else {
        this.sirenGain.gain.setTargetAtTime(0, t, 0.1);
      }
    }
  }

  public playWeapon(weapon: WeaponId): void {
    if (!this.ctx || !this.masterGain || this.muted) return;
    const t = this.ctx.currentTime;
    const pitchJitter = 0.94 + Math.random() * 0.12;

    if (weapon === 'fists') {
      const osc = this.ctx.createOscillator();
      const gain = this.ctx.createGain();
      osc.type = 'triangle';
      osc.frequency.setValueAtTime(190 * pitchJitter, t);
      osc.frequency.exponentialRampToValueAtTime(55, t + 0.11);
      gain.gain.setValueAtTime(0.28, t);
      gain.gain.exponentialRampToValueAtTime(0.001, t + 0.12);
      osc.connect(gain);
      gain.connect(this.masterGain);
      osc.start(t);
      osc.stop(t + 0.13);
      return;
    }

    // Percussive gunshot noise burst + low punch
    const dur = weapon === 'shotgun' ? 0.28 : weapon === 'pistol' ? 0.14 : 0.09;
    const buf = this.ctx.createBuffer(1, Math.floor(this.ctx.sampleRate * dur), this.ctx.sampleRate);
    const ch = buf.getChannelData(0);
    for (let i = 0; i < ch.length; i++) {
      ch[i] = (Math.random() * 2 - 1) * Math.exp(-i / (ch.length * 0.28));
    }
    const src = this.ctx.createBufferSource();
    src.buffer = buf;
    const filter = this.ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.value = weapon === 'shotgun' ? 900 : weapon === 'pistol' ? 1600 : 2100;
    const gain = this.ctx.createGain();
    gain.gain.setValueAtTime(weapon === 'shotgun' ? 0.55 : 0.36, t);
    gain.gain.exponentialRampToValueAtTime(0.001, t + dur);
    src.connect(filter);
    filter.connect(gain);
    gain.connect(this.masterGain);
    src.start(t);

    // Sub thump
    const sub = this.ctx.createOscillator();
    const subGain = this.ctx.createGain();
    sub.type = 'sine';
    sub.frequency.setValueAtTime((weapon === 'shotgun' ? 130 : 170) * pitchJitter, t);
    sub.frequency.exponentialRampToValueAtTime(36, t + 0.12);
    subGain.gain.setValueAtTime(0.32, t);
    subGain.gain.exponentialRampToValueAtTime(0.001, t + 0.13);
    sub.connect(subGain);
    subGain.connect(this.masterGain);
    sub.start(t);
    sub.stop(t + 0.14);
  }

  public playHorn(kind: VehicleKind): void {
    if (!this.ctx || !this.masterGain || this.muted) return;
    const t = this.ctx.currentTime;
    if (kind === 'rickshaw') {
      // Classic Dhaka bicycle rickshaw double bell "tring-tring!"
      [0, 0.12].forEach((offset) => {
        const osc = this.ctx!.createOscillator();
        const gain = this.ctx!.createGain();
        osc.type = 'sine';
        osc.frequency.setValueAtTime(1680, t + offset);
        gain.gain.setValueAtTime(0.18, t + offset);
        gain.gain.exponentialRampToValueAtTime(0.001, t + offset + 0.11);
        osc.connect(gain);
        gain.connect(this.masterGain!);
        osc.start(t + offset);
        osc.stop(t + offset + 0.12);
      });
      return;
    }

    if (kind === 'launch') {
      // Majestic low-frequency dual marine horn
      [92, 116].forEach((f) => {
        const osc = this.ctx!.createOscillator();
        const gain = this.ctx!.createGain();
        osc.type = 'sawtooth';
        osc.frequency.setValueAtTime(f, t);
        gain.gain.setValueAtTime(0.18, t);
        gain.gain.exponentialRampToValueAtTime(0.001, t + 1.2);
        osc.connect(gain);
        gain.connect(this.masterGain!);
        osc.start(t);
        osc.stop(t + 1.25);
      });
      return;
    }

    const freqs =
      kind === 'bus' || kind === 'truck'
        ? [185, 233]
        : kind === 'cng'
          ? [440, 554]
          : kind === 'speedboat'
            ? [480, 620]
            : kind === 'jetski'
              ? [640, 780]
              : [360, 450];
    freqs.forEach((f) => {
      const osc = this.ctx!.createOscillator();
      const gain = this.ctx!.createGain();
      osc.type = 'sawtooth';
      osc.frequency.setValueAtTime(f, t);
      gain.gain.setValueAtTime(0.11, t);
      gain.gain.exponentialRampToValueAtTime(0.001, t + 0.32);
      osc.connect(gain);
      gain.connect(this.masterGain!);
      osc.start(t);
      osc.stop(t + 0.34);
    });
  }

  public playWaterSplash(intensity = 1.0): void {
    if (!this.ctx || !this.masterGain || this.muted) return;
    const t = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.type = 'triangle';
    osc.frequency.setValueAtTime(120, t);
    osc.frequency.exponentialRampToValueAtTime(45, t + 0.28);
    gain.gain.setValueAtTime(Math.min(0.25, 0.08 * intensity), t);
    gain.gain.exponentialRampToValueAtTime(0.001, t + 0.32);
    osc.connect(gain);
    gain.connect(this.masterGain);
    osc.start(t);
    osc.stop(t + 0.35);
  }

  public playImpact(heavy = false): void {
    if (!this.ctx || !this.masterGain || this.muted) return;
    const t = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.type = 'triangle';
    osc.frequency.setValueAtTime(heavy ? 120 : 180, t);
    osc.frequency.exponentialRampToValueAtTime(32, t + (heavy ? 0.22 : 0.11));
    gain.gain.setValueAtTime(heavy ? 0.42 : 0.22, t);
    gain.gain.exponentialRampToValueAtTime(0.001, t + (heavy ? 0.24 : 0.12));
    osc.connect(gain);
    gain.connect(this.masterGain);
    osc.start(t);
    osc.stop(t + 0.25);
  }

  public playClick(): void {
    if (!this.ctx || !this.masterGain || this.muted) return;
    const t = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(1400, t);
    osc.frequency.exponentialRampToValueAtTime(400, t + 0.04);
    gain.gain.setValueAtTime(0.12, t);
    gain.gain.exponentialRampToValueAtTime(0.001, t + 0.04);
    osc.connect(gain);
    gain.connect(this.masterGain);
    osc.start(t);
    osc.stop(t + 0.045);
  }

  public playChime(victory = false): void {
    if (!this.ctx || !this.masterGain || this.muted) return;
    const t = this.ctx.currentTime;
    const notes = victory ? [523.25, 659.25, 783.99, 1046.5] : [587.33, 880];
    notes.forEach((freq, idx) => {
      const osc = this.ctx!.createOscillator();
      const gain = this.ctx!.createGain();
      osc.type = 'triangle';
      osc.frequency.setValueAtTime(freq, t + idx * 0.09);
      gain.gain.setValueAtTime(0.18, t + idx * 0.09);
      gain.gain.exponentialRampToValueAtTime(0.001, t + idx * 0.09 + 0.26);
      osc.connect(gain);
      gain.connect(this.masterGain!);
      osc.start(t + idx * 0.09);
      osc.stop(t + idx * 0.09 + 0.28);
    });
  }

  public playThunder(): void {
    if (!this.ctx || !this.masterGain || this.muted) return;
    const t = this.ctx.currentTime;
    const dur = 1.4;
    const buf = this.ctx.createBuffer(1, Math.floor(this.ctx.sampleRate * dur), this.ctx.sampleRate);
    const ch = buf.getChannelData(0);
    for (let i = 0; i < ch.length; i++) {
      ch[i] = (Math.random() * 2 - 1) * Math.exp(-i / (ch.length * 0.4));
    }
    const src = this.ctx.createBufferSource();
    src.buffer = buf;
    const filter = this.ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.value = 180;
    const gain = this.ctx.createGain();
    gain.gain.setValueAtTime(0.55, t);
    gain.gain.exponentialRampToValueAtTime(0.001, t + dur);
    src.connect(filter);
    filter.connect(gain);
    gain.connect(this.masterGain);
    src.start(t);
  }

  public playTelegraphCue(): void {
    if (!this.ctx || !this.masterGain || this.muted) return;
    const t = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.type = 'sine';
    osc.frequency.setValueAtTime(340, t);
    osc.frequency.exponentialRampToValueAtTime(680, t + 0.22);
    gain.gain.setValueAtTime(0.1, t);
    gain.gain.exponentialRampToValueAtTime(0.001, t + 0.24);
    osc.connect(gain);
    gain.connect(this.masterGain);
    osc.start(t);
    osc.stop(t + 0.25);
  }

  public dispose(): void {
    this.silenceLoops();
    try {
      this.engineOsc?.stop();
      this.sirenOsc?.stop();
    } catch {
      // Ignore if already stopped
    }
    if (this.ctx) {
      this.ctx.close().catch(() => {});
      this.ctx = null;
    }
  }
}
