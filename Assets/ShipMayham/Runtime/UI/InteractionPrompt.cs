namespace ShipMayham
{
    /// <summary>Logical player controls shown in the HUD; the glyph depends on the device in use.</summary>
    public enum HudControl : byte { None, Interact, Check, Carry, Throw, Jump, Fire, Reload, Camera, Release, Look }

    public static class HudControls
    {
        public static string Label(HudControl control, bool gamepad)
        {
            switch (control)
            {
                case HudControl.Interact:
                case HudControl.Release: return gamepad ? "A" : "E";
                case HudControl.Check: return gamepad ? "Y" : "F";
                case HudControl.Carry: return gamepad ? "B" : "G";
                case HudControl.Throw:
                case HudControl.Fire: return gamepad ? "RT" : "LMB";
                case HudControl.Jump: return gamepad ? "X" : "SPACE";
                case HudControl.Reload: return gamepad ? "X" : "R";
                case HudControl.Camera: return gamepad ? "Y" : "C";
                case HudControl.Look: return gamepad ? "RS" : "MOUSE";
                default: return string.Empty;
            }
        }
    }

    public enum PromptTone : byte { Normal, Info, Blocked }

    /// <summary>A control paired with a short verb, e.g. [G] Lift.</summary>
    public readonly struct PromptHint
    {
        public readonly HudControl Control;
        public readonly string Label;
        public PromptHint(HudControl control, string label) { Control = control; Label = label; }
        public bool IsEmpty => Control == HudControl.None || string.IsNullOrEmpty(Label);
    }

    /// <summary>
    /// What the crosshair is pointing at and what the player can do about it. A targeted prompt
    /// is drawn as a card at the crosshair; a contextual one (no target) is drawn as a hint strip.
    /// </summary>
    public readonly struct InteractionPrompt
    {
        public readonly string Title, Action, Detail, Cost;
        public readonly HudControl Key;
        public readonly PromptTone Tone;
        public readonly PromptHint Hint1, Hint2;
        public readonly bool Targeted;

        public InteractionPrompt(string title, string action, HudControl key, string detail = null, string cost = null,
            PromptTone tone = PromptTone.Normal, PromptHint hint1 = default, PromptHint hint2 = default, bool targeted = true)
        {
            Title = title; Action = action; Key = key; Detail = detail; Cost = cost; Tone = tone;
            Hint1 = hint1; Hint2 = hint2; Targeted = targeted;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Title) && string.IsNullOrEmpty(Action) && Hint1.IsEmpty && Hint2.IsEmpty;

        /// <summary>An object the crosshair is on that the player can act on.</summary>
        public static InteractionPrompt Target(string title, string action, string detail = null, string cost = null,
            HudControl key = HudControl.Interact, PromptTone tone = PromptTone.Normal, PromptHint hint1 = default, PromptHint hint2 = default) =>
            new InteractionPrompt(title, action, key, detail, cost, tone, hint1, hint2, true);

        /// <summary>Always-available controls for the current activity (helm, cannon, carrying...).</summary>
        public static InteractionPrompt Context(string title, PromptHint primary, PromptHint hint1 = default, PromptHint hint2 = default) =>
            new InteractionPrompt(title, primary.Label, primary.Control, null, null, PromptTone.Normal, hint1, hint2, false);

        /// <summary>A short non-interactive tip, such as an out-of-range object or "land first".</summary>
        public static InteractionPrompt Note(string text) =>
            new InteractionPrompt(null, text, HudControl.None, null, null, PromptTone.Info, default, default, false);

        public bool Equals(in InteractionPrompt other) =>
            Title == other.Title && Action == other.Action && Detail == other.Detail && Cost == other.Cost && Key == other.Key &&
            Tone == other.Tone && Targeted == other.Targeted &&
            Hint1.Control == other.Hint1.Control && Hint1.Label == other.Hint1.Label &&
            Hint2.Control == other.Hint2.Control && Hint2.Label == other.Hint2.Label;
    }
}
