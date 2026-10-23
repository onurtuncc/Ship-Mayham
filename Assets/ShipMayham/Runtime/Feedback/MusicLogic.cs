using UnityEngine;

namespace ShipMayham
{
    /// <summary>What the music should feel like right now.</summary>
    public enum MusicMood : byte { Harbor, Sailing, Tension, Lost }

    /// <summary>The four looping layers that are mixed together; they all have the same length so they stay in step.</summary>
    public enum MusicLayer : byte { Harbor, Pad, Pulse, Tension }

    /// <summary>The few facts about the voyage that decide the mood. Menu and lobby (no voyage yet) use the default.</summary>
    public struct MusicInput
    {
        public bool InVoyage;
        public VoyagePhase Phase;
        public bool Leak, PowerOut, ShipEventActive;
        public PiratePhase Pirate;
    }

    /// <summary>Pure decisions of the adaptive music: which mood, how loud each layer is, how fast the mix moves.</summary>
    public static class MusicLogic
    {
        public const int LayerCount = 4;
        /// <summary>Layer volume change per second; a full fade takes a couple of seconds so moods blend rather than cut.</summary>
        public const float FadePerSecond = .5f;

        public static MusicMood MoodFor(in MusicInput input)
        {
            if (!input.InVoyage) return MusicMood.Harbor;
            switch (input.Phase)
            {
                case VoyagePhase.Failed: return MusicMood.Lost;
                case VoyagePhase.Sailing:
                    bool trouble = input.Leak || input.PowerOut || input.ShipEventActive ||
                                   input.Pirate == PiratePhase.Warning || input.Pirate == PiratePhase.Attacking;
                    return trouble ? MusicMood.Tension : MusicMood.Sailing;
                default: return MusicMood.Harbor;
            }
        }

        /// <summary>Target volume (0..1) of each layer for a mood, indexed by <see cref="MusicLayer"/>.</summary>
        public static void Targets(MusicMood mood, float[] volumes)
        {
            for (int i = 0; i < volumes.Length; i++) volumes[i] = 0;
            switch (mood)
            {
                case MusicMood.Harbor: volumes[(int)MusicLayer.Harbor] = .9f; break;
                case MusicMood.Sailing: volumes[(int)MusicLayer.Pad] = .6f; volumes[(int)MusicLayer.Pulse] = .55f; break;
                case MusicMood.Tension:
                    volumes[(int)MusicLayer.Pad] = .35f; volumes[(int)MusicLayer.Pulse] = .3f; volumes[(int)MusicLayer.Tension] = .85f; break;
                // A lost ship gets silence (the sad sting is a sound effect); the mix returns with the next voyage.
            }
        }

        /// <summary>Moves the current layer volumes toward their targets.</summary>
        public static void Step(float[] current, float[] targets, float dt)
        {
            for (int i = 0; i < current.Length; i++) current[i] = Mathf.MoveTowards(current[i], targets[i], FadePerSecond * dt);
        }
    }
}
