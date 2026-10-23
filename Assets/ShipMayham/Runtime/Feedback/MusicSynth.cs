using System;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Synthesizes the looping music layers from sine voices, so the game ships no audio files. Every layer has the same
    /// length (<see cref="LoopSeconds"/>) and every pitch is snapped to a whole number of cycles per loop, so a loop restarts
    /// without a click; note tails that cross the end of the loop wrap around to its start.
    /// </summary>
    public static class MusicSynth
    {
        public const int SampleRate = 22050;
        public const float Bpm = 92f, Beats = 16f;
        public static readonly float LoopSeconds = Beats * 60f / Bpm;
        public static int LoopSamples => Mathf.RoundToInt(LoopSeconds * SampleRate);
        private static float BeatSeconds => 60f / Bpm;

        // Four bars of four beats. Chords as MIDI note numbers.
        private static readonly int[][] SailingChords = { new[] { 48, 55, 59, 64 }, new[] { 45, 52, 55, 60 }, new[] { 53, 57, 60, 64 }, new[] { 43, 50, 55, 59 } };   // Cmaj7 Am7 Fmaj7 G6
        private static readonly int[][] HarborChords = { new[] { 53, 57, 60, 64 }, new[] { 48, 52, 55, 62 }, new[] { 50, 53, 57, 60 }, new[] { 46, 50, 53, 57 } };   // Fmaj7 Cadd9 Dm7 Bb
        private static readonly int[][] TensionChords = { new[] { 45, 52, 57 }, new[] { 46, 53, 58 }, new[] { 45, 52, 57 }, new[] { 44, 51, 56 } };                   // Am Bb Am G#

        public static float[] Build(MusicLayer layer)
        {
            switch (layer)
            {
                case MusicLayer.Harbor: return Harbor();
                case MusicLayer.Pad: return Pad(SailingChords, .22f);
                case MusicLayer.Pulse: return Pulse(SailingChords);
                default: return Tension();
            }
        }

        public static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        /// <summary>Snaps a frequency so it completes a whole number of cycles in one loop.</summary>
        public static float Snap(float hz) => Mathf.Max(1, Mathf.Round(hz * LoopSeconds)) / LoopSeconds;

        // ---------------------------------------------------------------- layers

        // Slowly breathing chord pad: each bar's chord swells in and out, overlapping its neighbours (also across the loop end).
        private static float[] Pad(int[][] chords, float level)
        {
            int n = LoopSamples;
            var data = new float[n];
            float bar = LoopSeconds / chords.Length;
            for (int c = 0; c < chords.Length; c++)
            {
                float centre = (c + .5f) * bar;
                foreach (int note in chords[c])
                {
                    float f1 = Snap(Hz(note)), f2 = Snap(Hz(note) * 1.004f);
                    for (int i = 0; i < n; i++)
                    {
                        float t = (float)i / SampleRate;
                        float w = BarWindow(t, centre, bar);
                        if (w <= 0) continue;
                        data[i] += w * (Mathf.Sin(2 * Mathf.PI * f1 * t) + Mathf.Sin(2 * Mathf.PI * f2 * t) * .8f) * level * .5f;
                    }
                }
            }
            return Normalize(data, .5f);
        }

        // Soft marimba-like arpeggio on eighth notes: a few harmonics with a quick decay.
        private static float[] Pulse(int[][] chords)
        {
            int n = LoopSamples;
            var data = new float[n];
            float eighth = BeatSeconds / 2f, bar = LoopSeconds / chords.Length;
            int[] pattern = { 0, 2, 1, 3, 2, 1, 3, 2 };
            int steps = (int)(Beats * 2);
            for (int step = 0; step < steps; step++)
            {
                int[] chord = chords[Mathf.Min(chords.Length - 1, (int)(step * eighth / bar))];
                int note = chord[pattern[step % pattern.Length] % chord.Length] + 12;
                float start = step * eighth, accent = step % 2 == 0 ? 1f : .7f;
                AddPluck(data, start, Snap(Hz(note)), .5f * accent, 7f);
            }
            return Normalize(data, .45f);
        }

        // Calm, sparse bells over the harbor chords: one note per beat on beats 1 and 3, a lower one on beat 2.
        private static float[] Harbor()
        {
            int n = LoopSamples;
            var data = new float[n];
            // A quiet pad underneath so the bells have somewhere to sit.
            var pad = Pad(HarborChords, .16f);
            for (int i = 0; i < n; i++) data[i] = pad[i] * .8f;
            float bar = LoopSeconds / HarborChords.Length;
            for (int c = 0; c < HarborChords.Length; c++)
            {
                var chord = HarborChords[c];
                AddBell(data, c * bar, Snap(Hz(chord[3] + 12)), .45f);
                AddBell(data, c * bar + BeatSeconds * 1.5f, Snap(Hz(chord[2] + 12)), .3f);
                AddBell(data, c * bar + BeatSeconds * 2f, Snap(Hz(chord[1] + 24)), .38f);
            }
            return Normalize(data, .5f);
        }

        // Low drone with a tremolo, a nervous 16th-note tick and a tritone stab every bar.
        private static float[] Tension()
        {
            int n = LoopSamples;
            var data = new float[n];
            float drone = Snap(Hz(33)), droneFifth = Snap(Hz(40)), tremolo = Snap(5.5f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float trem = .65f + .35f * Mathf.Sin(2 * Mathf.PI * tremolo * t);
                data[i] = (Mathf.Sin(2 * Mathf.PI * drone * t) * .8f + Mathf.Sin(2 * Mathf.PI * droneFifth * t) * .35f) * trem * .55f;
            }
            float sixteenth = BeatSeconds / 4f, bar = LoopSeconds / TensionChords.Length;
            int steps = (int)(Beats * 4);
            for (int step = 0; step < steps; step++)
                AddTick(data, step * sixteenth, step % 4 == 0 ? .5f : .22f, step % 4 == 0 ? 1500f : 2300f);
            for (int c = 0; c < TensionChords.Length; c++)
            {
                AddPluck(data, c * bar + BeatSeconds * 3f, Snap(Hz(TensionChords[c][1] + 12)), .45f, 6f);
                AddPluck(data, c * bar + BeatSeconds * 3f, Snap(Hz(TensionChords[c][0] + 18)), .35f, 6f);
            }
            return Normalize(data, .5f);
        }

        // ---------------------------------------------------------------- voices

        private static float BarWindow(float t, float centre, float bar)
        {
            // Circular distance so a chord at the end of the loop overlaps the start.
            float d = Mathf.Abs(t - centre);
            d = Mathf.Min(d, LoopSeconds - d);
            float x = d / bar;                                  // 0 at the chord's centre, 1 one bar away
            if (x >= 1) return 0;
            float w = .5f + .5f * Mathf.Cos(Mathf.PI * x);       // raised cosine, overlap-adds to a steady level
            return w;
        }

        private static void AddPluck(float[] data, float start, float hz, float level, float decay)
        {
            int n = data.Length;
            float length = 4f / decay;
            int count = Mathf.Min(n, Mathf.CeilToInt(length * SampleRate));
            int first = Mathf.RoundToInt(start * SampleRate);
            for (int k = 0; k < count; k++)
            {
                float t = (float)k / SampleRate;
                float env = Mathf.Clamp01(t / .004f) * Mathf.Exp(-decay * t);
                float v = (Mathf.Sin(2 * Mathf.PI * hz * t) + Mathf.Sin(4 * Mathf.PI * hz * t) * .35f + Mathf.Sin(6 * Mathf.PI * hz * t) * .12f) * env * level;
                data[(first + k) % n] += v;       // wraps: the tail of a late note rings into the start of the loop
            }
        }

        private static void AddBell(float[] data, float start, float hz, float level)
        {
            int n = data.Length;
            const float decay = 2.2f;
            int count = Mathf.Min(n, Mathf.CeilToInt(3f / decay * SampleRate));
            int first = Mathf.RoundToInt(start * SampleRate);
            for (int k = 0; k < count; k++)
            {
                float t = (float)k / SampleRate;
                float env = Mathf.Clamp01(t / .003f) * Mathf.Exp(-decay * t);
                float v = (Mathf.Sin(2 * Mathf.PI * hz * t) + Mathf.Sin(2 * Mathf.PI * hz * 2.76f * t) * .25f * Mathf.Exp(-5f * t)) * env * level;
                data[(first + k) % n] += v;
            }
        }

        private static void AddTick(float[] data, float start, float level, float hz)
        {
            int n = data.Length;
            int count = Mathf.CeilToInt(.03f * SampleRate);
            int first = Mathf.RoundToInt(start * SampleRate);
            for (int k = 0; k < count; k++)
            {
                float t = (float)k / SampleRate;
                data[(first + k) % n] += Mathf.Sin(2 * Mathf.PI * hz * t) * Mathf.Exp(-160f * t) * level * .35f;
            }
        }

        /// <summary>Scales the signal so its peak is exactly <paramref name="peak"/> (never amplifies silence).</summary>
        public static float[] Normalize(float[] data, float peak)
        {
            float max = 0;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max < 1e-6f) return data;
            float scale = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= scale;
            return data;
        }

        public static AudioClip ToClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
