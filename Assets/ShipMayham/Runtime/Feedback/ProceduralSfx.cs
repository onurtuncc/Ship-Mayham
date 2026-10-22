using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    public enum SfxKind { Coin, Pop, Chime, Fanfare, Thud, Boom, Splash, Whoosh, Sad, Alarm, Sparkle, Horn, Gull }

    /// <summary>
    /// Small synthesized sound set, generated once on first use. The prototype ships no audio files, so the feedback layer
    /// builds its own short clips (sine/noise with envelopes), which also keeps the WebGL download unchanged.
    /// </summary>
    public static class ProceduralSfx
    {
        private const int SampleRate = 44100;
        private static readonly Dictionary<SfxKind, AudioClip> Cache = new Dictionary<SfxKind, AudioClip>();
        private static AudioClip seaLoop;

        public static AudioClip Get(SfxKind kind)
        {
            if (!Cache.TryGetValue(kind, out var clip) || clip == null)
            {
                clip = Build(kind);
                Cache[kind] = clip;
            }
            return clip;
        }

        /// <summary>Seamless 4 s loop of filtered noise with slow swell, used as the sea bed.</summary>
        public static AudioClip SeaLoop()
        {
            if (seaLoop != null) return seaLoop;
            const int seconds = 4;
            int count = SampleRate * seconds;
            var data = new float[count];
            var rng = new System.Random(7);
            float low = 0, low2 = 0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                low += (noise - low) * .035f;            // rolling low-pass for a distant wash
                low2 += (low - low2) * .05f;
                // Slow swell; the loop seam is hidden by the crossfade below.
                float swell = .55f + .45f * Mathf.Sin(2 * Mathf.PI * t / 4f) * Mathf.Sin(2 * Mathf.PI * t / 2f + .6f);
                data[i] = low2 * 6f * swell;
            }
            seaLoop = CreateClip("SeaLoop", Crossfade(data, SampleRate / 4));
            return seaLoop;
        }

        private static AudioClip Build(SfxKind kind)
        {
            switch (kind)
            {
                case SfxKind.Coin: return Tone("Coin", .34f, (t, d) => Env(t, d, .004f, 5) * (Sine(t, 1318) * .55f + Sine(t, 1976) * .35f * (t > .07f ? 1 : 0) + Sine(t, 2637) * .12f));
                case SfxKind.Pop: return Tone("Pop", .14f, (t, d) => Env(t, d, .002f, 22) * Sine(t * (1 + 1.4f * (1 - t / d)), 520) * .9f);
                case SfxKind.Sparkle: return Tone("Sparkle", .5f, (t, d) => Env(t, d, .003f, 6) * (Sine(t, 2093) * .3f + Sine(t, 2794) * .25f * Mathf.Abs(Mathf.Sin(t * 38)) + Sine(t, 3322) * .18f));
                case SfxKind.Chime: return Arpeggio("Chime", new[] { 659.3f, 830.6f, 987.8f }, .11f, .5f, 4.5f);
                case SfxKind.Fanfare: return Arpeggio("Fanfare", new[] { 523.3f, 659.3f, 784f, 1046.5f, 784f, 1046.5f, 1318.5f }, .13f, .9f, 3f);
                case SfxKind.Sad: return Arpeggio("Sad", new[] { 392f, 349.2f, 311.1f, 261.6f }, .22f, .7f, 3f);
                case SfxKind.Alarm: return Tone("Alarm", .42f, (t, d) => Env(t, d, .004f, 2.2f) * Mathf.Sign(Sine(t, 8)) * Sine(t, Mathf.Lerp(620, 880, t < .21f ? 0 : 1)) * .45f);
                case SfxKind.Horn: return Tone("Horn", .85f, (t, d) => Env(t, d, .05f, 1.6f) * (Sine(t, 146.8f) * .6f + Sine(t, 220f) * .4f + Sine(t, 293.7f) * .15f));
                case SfxKind.Gull: return Tone("Gull", .55f, (t, d) =>
                {
                    float sweep = 1500 + 700 * Mathf.Sin(Mathf.Clamp01(t / .22f) * Mathf.PI) - 500 * Mathf.Clamp01((t - .3f) / .25f);
                    float phase = 2 * Mathf.PI * (sweep * t);
                    return Env(t, d, .02f, 4.2f) * (Mathf.Sin(phase) * .5f + Mathf.Sin(phase * 2.01f) * .22f) * (.75f + .25f * Mathf.Sin(t * 70));
                });
                case SfxKind.Thud: return Noisy("Thud", .32f, 130, 70, 11, .55f);
                case SfxKind.Boom: return Noisy("Boom", .7f, 85, 40, 5.5f, .9f);
                case SfxKind.Splash: return Noisy("Splash", .5f, 1900, 5200, 6, 0f);
                case SfxKind.Whoosh: return Noisy("Whoosh", .6f, 600, 2600, 3, 0f, rise: true);
                default: return Tone("Silence", .05f, (t, d) => 0);
            }
        }

        private static AudioClip Tone(string name, float seconds, Func<float, float, float> sample)
        {
            int count = (int)(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(sample((float)i / SampleRate, seconds), -1f, 1f);
            return CreateClip(name, data);
        }

        private static AudioClip Arpeggio(string name, float[] notes, float step, float seconds, float decay)
        {
            return Tone(name, seconds, (t, d) =>
            {
                float sum = 0;
                for (int n = 0; n < notes.Length; n++)
                {
                    float local = t - n * step;
                    if (local < 0) continue;
                    sum += Env(local, d, .004f, decay) * (Sine(local, notes[n]) * .5f + Sine(local, notes[n] * 2) * .16f);
                }
                return sum * .6f;
            });
        }

        // Low-passed noise burst whose cutoff glides between two frequencies, optionally with a sine body (thuds and booms).
        private static AudioClip Noisy(string name, float seconds, float startHz, float endHz, float decay, float body, bool rise = false)
        {
            var rng = new System.Random(name.GetHashCode());
            float lp = 0;
            return Tone(name, seconds, (t, d) =>
            {
                float k = t / d;
                float hz = Mathf.Lerp(startHz, endHz, k);
                float alpha = Mathf.Clamp01(hz * 2 * Mathf.PI / SampleRate * 6);
                float noise = (float)(rng.NextDouble() * 2 - 1);
                lp += (noise - lp) * alpha;
                float env = rise ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(k)) : Env(t, d, .002f, decay);
                return env * (lp * (body > 0 ? 1.6f : 2.4f) + body * Sine(t, Mathf.Lerp(95, 45, k)));
            });
        }

        private static float Sine(float t, float hz) => Mathf.Sin(2 * Mathf.PI * hz * t);

        private static float Env(float t, float duration, float attack, float decay) => Mathf.Clamp01(t / attack) * Mathf.Exp(-decay * t);

        // The first samples fade in from the discarded tail, so the wrap from the last kept sample to sample 0 is continuous.
        private static float[] Crossfade(float[] data, int overlap)
        {
            for (int i = 0; i < overlap; i++)
                data[i] = Mathf.Lerp(data[data.Length - overlap + i], data[i], (float)i / overlap);
            var trimmed = new float[data.Length - overlap];
            Array.Copy(data, trimmed, trimmed.Length);
            return trimmed;
        }

        private static AudioClip CreateClip(string name, float[] data)
        {
            var clip = AudioClip.Create("Sfx_" + name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
