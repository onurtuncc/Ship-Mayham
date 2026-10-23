using System;

namespace ShipMayham
{
    public enum InteractionKind : byte { None, Station, Passenger, Harbor, Salvage }

    /// <summary>
    /// What the local crew member's crosshair is on, and the prompt that describes it. <see cref="Index"/> is the
    /// station index, the passenger index, the <see cref="HarborAction"/> or the salvage bundle index depending on <see cref="Kind"/>.
    /// A target with <see cref="InteractionKind.None"/> may still carry a contextual prompt (helm, cannon, carrying...).
    /// Equality compares what is targeted, not the prompt text.
    /// </summary>
    public readonly struct InteractionTarget : IEquatable<InteractionTarget>
    {
        public readonly InteractionKind Kind;
        public readonly int Index;
        public readonly InteractionPrompt Prompt;

        private InteractionTarget(InteractionKind kind, int index, InteractionPrompt prompt)
        {
            Kind = kind; Index = index; Prompt = prompt;
        }

        public static InteractionTarget None => default;
        public static InteractionTarget Context(InteractionPrompt prompt) => new InteractionTarget(InteractionKind.None, 0, prompt);
        public static InteractionTarget Station(int index, InteractionPrompt prompt = default) => new InteractionTarget(InteractionKind.Station, index, prompt);
        public static InteractionTarget Passenger(int index, InteractionPrompt prompt = default) => new InteractionTarget(InteractionKind.Passenger, index, prompt);
        public static InteractionTarget Harbor(HarborAction action, InteractionPrompt prompt = default) => new InteractionTarget(InteractionKind.Harbor, (int)action, prompt);
        public static InteractionTarget Salvage(int index, InteractionPrompt prompt = default) => new InteractionTarget(InteractionKind.Salvage, index, prompt);

        public bool IsNone => Kind == InteractionKind.None;
        public HarborAction HarborAction => (HarborAction)Index;
        public bool IsStation(int index) => Kind == InteractionKind.Station && Index == index;
        public bool IsPassenger(int index) => Kind == InteractionKind.Passenger && Index == index;

        /// <summary>The id <c>InteractRpc</c> expects: a station index, a passenger as <c>stationCount + index</c>, or -1.</summary>
        public int InteractionId(int stationCount) =>
            Kind == InteractionKind.Station ? Index : Kind == InteractionKind.Passenger ? stationCount + Index : -1;

        public bool Equals(InteractionTarget other) => Kind == other.Kind && Index == other.Index;
        public override bool Equals(object obj) => obj is InteractionTarget other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Index;
        public override string ToString() => IsNone ? "None" : $"{Kind}:{Index}";
    }
}
