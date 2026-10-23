using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class MusicTests
    {
        private static MusicInput Voyage(VoyagePhase phase, bool leak = false, bool power = false, bool evt = false, PiratePhase pirate = PiratePhase.Waiting) =>
            new MusicInput { InVoyage = true, Phase = phase, Leak = leak, PowerOut = power, ShipEventActive = evt, Pirate = pirate };

        [Test]
        public void TheMoodFollowsTheVoyage()
        {
            Assert.AreEqual(MusicMood.Harbor, MusicLogic.MoodFor(default), "Menu and lobby sound like the harbor.");
            Assert.AreEqual(MusicMood.Harbor, MusicLogic.MoodFor(Voyage(VoyagePhase.Boarding)));
            Assert.AreEqual(MusicMood.Harbor, MusicLogic.MoodFor(Voyage(VoyagePhase.Arrived)));
            Assert.AreEqual(MusicMood.Sailing, MusicLogic.MoodFor(Voyage(VoyagePhase.Sailing)));
            Assert.AreEqual(MusicMood.Lost, MusicLogic.MoodFor(Voyage(VoyagePhase.Failed)));
        }

        [Test]
        public void TroubleAtSeaRaisesTension()
        {
            Assert.AreEqual(MusicMood.Tension, MusicLogic.MoodFor(Voyage(VoyagePhase.Sailing, leak: true)));
            Assert.AreEqual(MusicMood.Tension, MusicLogic.MoodFor(Voyage(VoyagePhase.Sailing, power: true)));
            Assert.AreEqual(MusicMood.Tension, MusicLogic.MoodFor(Voyage(VoyagePhase.Sailing, evt: true)));
            Assert.AreEqual(MusicMood.Tension, MusicLogic.MoodFor(Voyage(VoyagePhase.Sailing, pirate: PiratePhase.Warning)));
            Assert.AreEqual(MusicMood.Tension, MusicLogic.MoodFor(Voyage(VoyagePhase.Sailing, pirate: PiratePhase.Attacking)));
            Assert.AreEqual(MusicMood.Sailing, MusicLogic.MoodFor(Voyage(VoyagePhase.Sailing, pirate: PiratePhase.Defeated)));
            Assert.AreEqual(MusicMood.Harbor, MusicLogic.MoodFor(Voyage(VoyagePhase.Boarding, leak: true)), "A leftover leak in port is not a crisis.");
        }

        [Test]
        public void EachMoodHasItsOwnMix()
        {
            var volumes = new float[MusicLogic.LayerCount];
            MusicLogic.Targets(MusicMood.Harbor, volumes);
            Assert.Greater(volumes[(int)MusicLayer.Harbor], 0);
            Assert.AreEqual(0, volumes[(int)MusicLayer.Pad] + volumes[(int)MusicLayer.Pulse] + volumes[(int)MusicLayer.Tension]);
            MusicLogic.Targets(MusicMood.Sailing, volumes);
            Assert.AreEqual(0, volumes[(int)MusicLayer.Harbor]);
            Assert.Greater(volumes[(int)MusicLayer.Pad], 0);
            Assert.Greater(volumes[(int)MusicLayer.Pulse], 0);
            Assert.AreEqual(0, volumes[(int)MusicLayer.Tension]);
            MusicLogic.Targets(MusicMood.Tension, volumes);
            Assert.Greater(volumes[(int)MusicLayer.Tension], volumes[(int)MusicLayer.Pad]);
            MusicLogic.Targets(MusicMood.Lost, volumes);
            Assert.AreEqual(0, volumes.Sum());
        }

        [Test]
        public void TheMixFadesInsteadOfCutting()
        {
            var current = new float[MusicLogic.LayerCount];
            var targets = new float[MusicLogic.LayerCount];
            MusicLogic.Targets(MusicMood.Sailing, targets);
            MusicLogic.Step(current, targets, .1f);
            Assert.AreEqual(MusicLogic.FadePerSecond * .1f, current[(int)MusicLayer.Pad], 1e-5f);
            Assert.Less(current[(int)MusicLayer.Pad], targets[(int)MusicLayer.Pad]);
            for (int i = 0; i < 100; i++) MusicLogic.Step(current, targets, .1f);
            Assert.AreEqual(targets[(int)MusicLayer.Pad], current[(int)MusicLayer.Pad], 1e-5f);
            MusicLogic.Targets(MusicMood.Lost, targets);
            for (int i = 0; i < 100; i++) MusicLogic.Step(current, targets, .1f);
            Assert.AreEqual(0, current.Sum(), 1e-5f);
        }

        [Test]
        public void PitchesCompleteWholeCyclesPerLoop()
        {
            foreach (int note in new[] { 33, 45, 55, 60, 72 })
            {
                float cycles = MusicSynth.Snap(MusicSynth.Hz(note)) * MusicSynth.LoopSeconds;
                Assert.AreEqual(Mathf.Round(cycles), cycles, 1e-3f, "MIDI " + note);
            }
        }

        [TestCase(MusicLayer.Harbor)]
        [TestCase(MusicLayer.Pad)]
        [TestCase(MusicLayer.Pulse)]
        [TestCase(MusicLayer.Tension)]
        public void EachLayerLoopsCleanlyAtTheSameLength(MusicLayer layer)
        {
            float[] data = MusicSynth.Build(layer);
            Assert.AreEqual(MusicSynth.LoopSamples, data.Length);
            float peak = data.Max(Mathf.Abs);
            Assert.That(peak, Is.InRange(.4f, .51f), "Normalised so layers sum without clipping.");
            float rms = Mathf.Sqrt(data.Sum(v => v * v) / data.Length);
            Assert.Greater(rms, .02f, "A layer is not silent.");

            // The step across the loop seam must be no bigger than the biggest ordinary step inside the signal.
            float seam = Mathf.Abs(data[0] - data[data.Length - 1]);
            float biggest = 0;
            for (int i = 1; i < data.Length; i++) biggest = Mathf.Max(biggest, Mathf.Abs(data[i] - data[i - 1]));
            Assert.LessOrEqual(seam, biggest + .001f, "The loop restarts without a click.");
            Assert.IsFalse(data.Any(float.IsNaN));
        }

        [Test]
        public void BabbleIsDeterministicAndPersonal()
        {
            float[] a = Babble.Synth(3, BabbleMood.Happy), b = Babble.Synth(3, BabbleMood.Happy);
            CollectionAssert.AreEqual(a, b, "The same character always sounds the same.");
            CollectionAssert.AreNotEqual(a, Babble.Synth(9, BabbleMood.Happy), "Different characters sound different.");
            Assert.IsFalse(Babble.Synth(3, BabbleMood.Grumpy).SequenceEqual(a), "A mood changes the voice.");
            Assert.Greater(a.Length, Babble.SampleRate / 6, "At least a fraction of a second of talk.");
            Assert.Less(a.Length, Babble.SampleRate * 2, "And not a speech.");
            Assert.AreEqual(.6f, a.Max(Mathf.Abs), .02f);
        }

        [Test]
        public void BabbleClipsAreCachedAndSayIsSafeOutsidePlayMode()
        {
            var clip = Babble.Clip(5, BabbleMood.Curious);
            Assert.AreSame(clip, Babble.Clip(5, BabbleMood.Curious));
            Assert.AreNotSame(clip, Babble.Clip(5, BabbleMood.Happy));
            Assert.DoesNotThrow(() => Babble.Say(Vector3.zero, 1, BabbleMood.Happy));
        }
    }
}
