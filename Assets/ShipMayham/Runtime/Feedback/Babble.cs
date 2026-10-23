using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    public enum BabbleMood : byte { Happy, Grumpy, Curious }

    /// <summary>
    /// Wordless "talking" for passengers and NPCs (the Animal Crossing trick): a few short vowel-like syllables whose pitch and
    /// contour come from a per-character seed, so each person sounds like themselves. Synthesized, deterministic and cached.
    /// </summary>
    public static class Babble
    {
        public const int SampleRate = 22050;
        private const int Voices = 4;
        private static readonly Dictionary<int, AudioClip> Cache = new Dictionary<int, AudioClip>();
        private static AudioSource[] voices;
        private static Transform holder;
        private static int next;

        /// <summary>Seconds of babble for the given settings; used by tests.</summary>
        public static float[] Synth(int seed, BabbleMood mood)
        {
            var rng = new System.Random(seed * 7919 + (int)mood * 104729 + 13);
            float baseHz = 118f + (Mathf.Abs(seed) * 37 % 11) * 14f;                    // each character has their own voice pitch
            int syllables = 3 + rng.Next(3);
            var samples = new List<float>(SampleRate);
            float hz = baseHz * (mood == BabbleMood.Grumpy ? .8f : 1f);
            for (int s = 0; s < syllables; s++)
            {
                float length = .075f + (float)rng.NextDouble() * .045f;
                float step = mood == BabbleMood.Happy ? 1.07f : mood == BabbleMood.Grumpy ? .93f : (s == syllables - 1 ? 1.28f : 1.02f);
                float f1 = 350f + (float)rng.NextDouble() * 450f, f2 = 900f + (float)rng.NextDouble() * 1300f;   // a vowel-ish colour per syllable
                AddSyllable(samples, hz, length, f1, f2);
                hz = Mathf.Clamp(hz * step * (1f + ((float)rng.NextDouble() - .5f) * .12f), 70f, 520f);
                for (int k = 0, gap = (int)(.025f * SampleRate); k < gap; k++) samples.Add(0);
            }
            return MusicSynth.Normalize(samples.ToArray(), .6f);
        }

        private static void AddSyllable(List<float> samples, float hz, float seconds, float f1, float f2)
        {
            int count = Mathf.RoundToInt(seconds * SampleRate);
            float phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                phase += 2 * Mathf.PI * hz * (1f + .04f * Mathf.Sin(t * Mathf.PI)) / SampleRate;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) ;
                float v = 0;
                // A buzzy source shaped by two formant peaks.
                for (int h = 1; h <= 14; h++)
                {
                    float freq = h * hz;
                    float shape = Mathf.Exp(-Mathf.Pow((freq - f1) / 260f, 2)) + .6f * Mathf.Exp(-Mathf.Pow((freq - f2) / 420f, 2));
                    v += Mathf.Sin(h * phase) * shape / h;
                }
                samples.Add(v * env);
            }
        }

        public static AudioClip Clip(int seed, BabbleMood mood)
        {
            int key = (Mathf.Abs(seed) % 16) * 8 + (int)mood;
            if (!Cache.TryGetValue(key, out var clip) || clip == null)
            {
                clip = MusicSynth.ToClip("Babble", Synth(Mathf.Abs(seed) % 16, mood));
                clip.name = "Babble_" + key;
                Cache[key] = clip;
            }
            return clip;
        }

        /// <summary>Plays a short babble from a point in the world; louder when closer, silent when sound is off.</summary>
        public static void Say(Vector3 at, int seed, BabbleMood mood, float volume = 1f)
        {
            float gain = volume * FeedbackSettings.SfxGain;
            if (gain <= .01f || !Application.isPlaying) return;
            EnsureVoices();
            var voice = voices[next++ % Voices];
            voice.transform.position = at;
            voice.pitch = Random.Range(.97f, 1.03f);
            voice.PlayOneShot(Clip(seed, mood), gain);
        }

        private static void EnsureVoices()
        {
            if (voices != null && voices[0] != null) return;
            var go = new GameObject("BabbleVoices") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            holder = go.transform;
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var child = new GameObject("Voice" + i) { hideFlags = HideFlags.HideAndDontSave };
                child.transform.SetParent(holder, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = .85f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 2f; source.maxDistance = 22f;
                voices[i] = source;
            }
        }
    }
}
