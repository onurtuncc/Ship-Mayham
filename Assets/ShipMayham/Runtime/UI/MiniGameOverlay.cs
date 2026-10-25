using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>
    /// The window of the skill games (lock, hammer, fishing rod, shovel). It plays the game the host opened, with the keyboard,
    /// mouse or a gamepad, and reports how it went. While it is open the crew member stands still and the look is frozen.
    /// </summary>
    public sealed class MiniGameOverlay
    {
        public const float ResultSeconds = 1.3f;

        private readonly PrototypeHud hud;
        private readonly GameObject root;
        private readonly Text title, hint, status;
        private readonly RectTransform lockGroup, hammerGroup, fishGroup, digGroup;
        // lockpick
        private readonly RectTransform lockDial, pickRect;
        private readonly Image turnFill, tensionFill;
        private readonly Image[] pickDots = new Image[LockpickGame.MaxPicks];
        // hammer
        private readonly RectTransform hammerWindow, hammerMarker;
        private readonly Image[] nailDots = new Image[HammerGame.NailsNeeded], missDots = new Image[HammerGame.MissesAllowed];
        // fishing
        private readonly RectTransform fishBar, fishFish, fishTrack;
        private readonly Image fishProgress;
        // dig
        private readonly Image digFill, digTime;

        private MiniGameKind kind;
        private int target;
        private LockpickGame lockpick;
        private HammerGame hammer;
        private FishingGame fishing;
        private DigGame dig;
        private float pickAngle, clock, resultTimer, shake;
        private bool reported;
        private MiniGameOutcome outcome;
        private int score;

        public bool IsOpen { get; private set; }
        public MiniGameKind Kind => kind;
        public bool Finished => reported;

        public MiniGameOverlay(PrototypeHud hud, Transform parent)
        {
            this.hud = hud;
            var dim = HudKit.Box(parent, "MiniGame", new Color(.02f, .05f, .09f, .62f), false, false);
            root = dim.gameObject;
            HudKit.Stretch(dim.rectTransform);

            var card = HudKit.Box(dim.transform, "Card", HudKit.PanelStrong);
            HudKit.Anchor(card.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(760, 520));
            title = HudKit.Label(card.transform, "Title", 40, HudKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudKit.Anchor(title.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -26), new Vector2(700, 56));
            hint = HudKit.Label(card.transform, "Hint", 21, HudKit.TextMuted, TextAnchor.MiddleCenter);
            HudKit.Anchor(hint.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 62), new Vector2(700, 60));
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            status = HudKit.Label(card.transform, "Status", 30, HudKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudKit.Anchor(status.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(700, 44));

            // ---- Lock: a round face, a pick that swings and a cylinder that turns
            lockGroup = HudKit.Node(card.transform, "Lock");
            HudKit.Anchor(lockGroup, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(660, 300));
            var face = HudKit.Dot(lockGroup, "Face", new Color(.32f, .26f, .16f), 250);
            HudKit.Anchor(face.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-120, 0), new Vector2(250, 250));
            lockDial = HudKit.Node(face.transform, "Dial");
            HudKit.Anchor(lockDial, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(170, 170));
            var dialDisc = HudKit.Dot(lockDial, "Disc", new Color(.85f, .7f, .3f), 170);
            HudKit.Stretch(dialDisc.rectTransform);
            var keyhole = HudKit.Box(lockDial, "Keyhole", new Color(.12f, .09f, .05f));
            HudKit.Anchor(keyhole.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(20, 70));
            pickRect = HudKit.Node(face.transform, "Pick");
            HudKit.Anchor(pickRect, new Vector2(.5f, .5f), new Vector2(.5f, 0f), Vector2.zero, new Vector2(10, 125));
            var pickBar = HudKit.Box(pickRect, "Bar", new Color(.82f, .85f, .9f));
            HudKit.Stretch(pickBar.rectTransform);
            turnFill = Meter(lockGroup, "Turn", "TURN", new Vector2(150, 50), new Color(.45f, .85f, .5f));
            tensionFill = Meter(lockGroup, "Tension", "STRAIN", new Vector2(150, -20), new Color(.95f, .45f, .3f));
            for (int i = 0; i < pickDots.Length; i++)
            {
                pickDots[i] = HudKit.Dot(lockGroup, "Pick" + i, new Color(.82f, .85f, .9f), 22);
                HudKit.Anchor(pickDots[i].rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(110 + i * 34, -90), new Vector2(22, 22));
            }

            // ---- Hammer: a plank with a green patch and a swinging marker
            hammerGroup = HudKit.Node(card.transform, "Hammer");
            HudKit.Anchor(hammerGroup, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(660, 300));
            var plank = HudKit.Box(hammerGroup, "Plank", new Color(.5f, .34f, .18f));
            HudKit.Anchor(plank.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 30), new Vector2(600, 90));
            var window = HudKit.Box(plank.transform, "Window", new Color(.4f, .85f, .45f, .9f));
            hammerWindow = window.rectTransform;
            var marker = HudKit.Box(plank.transform, "Marker", new Color(1, 1, 1, .95f));
            hammerMarker = marker.rectTransform;
            for (int i = 0; i < nailDots.Length; i++)
            {
                nailDots[i] = HudKit.Dot(hammerGroup, "Nail" + i, new Color(1, 1, 1, .2f), 30);
                HudKit.Anchor(nailDots[i].rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-60 + i * 60, -70), new Vector2(30, 30));
            }
            for (int i = 0; i < missDots.Length; i++)
            {
                missDots[i] = HudKit.Dot(hammerGroup, "Miss" + i, new Color(1, 1, 1, .2f), 20);
                HudKit.Anchor(missDots[i].rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(190 + i * 30, -70), new Vector2(20, 20));
            }

            // ---- Fishing: a vertical reeling bar and a catch meter
            fishGroup = HudKit.Node(card.transform, "Fishing");
            HudKit.Anchor(fishGroup, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(660, 320));
            var track = HudKit.Box(fishGroup, "Track", new Color(.15f, .35f, .5f, .9f));
            fishTrack = track.rectTransform;
            HudKit.Anchor(fishTrack, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-40, 0), new Vector2(90, 300));
            var bar = HudKit.Box(track.transform, "Bar", new Color(.4f, .85f, .45f, .85f));
            fishBar = bar.rectTransform;
            var fish = HudKit.Dot(track.transform, "Fish", new Color(1f, .75f, .3f), 30);
            fishFish = fish.rectTransform;
            fishProgress = Meter(fishGroup, "Catch", "CATCH", new Vector2(190, 0), new Color(.95f, .8f, .3f));

            // ---- Dig: a hole that fills and a clock
            digGroup = HudKit.Node(card.transform, "Dig");
            HudKit.Anchor(digGroup, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(660, 300));
            digFill = Meter(digGroup, "Depth", "DEPTH", new Vector2(0, 40), new Color(.8f, .6f, .3f), wide: true);
            digTime = Meter(digGroup, "Time", "TIME", new Vector2(0, -50), new Color(.5f, .75f, .95f), wide: true);

            root.SetActive(false);
        }

        private static Image Meter(RectTransform parent, string name, string caption, Vector2 position, Color color, bool wide = false)
        {
            float width = wide ? 440 : 260;
            var label = HudKit.Label(parent, name + "Label", 18, HudKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            label.text = caption;
            HudKit.Anchor(label.rectTransform, new Vector2(.5f, .5f), new Vector2(0, .5f), position + new Vector2(wide ? -width * .5f : 0, 22), new Vector2(160, 22));
            var back = HudKit.Box(parent, name, new Color(1, 1, 1, .16f));
            HudKit.Anchor(back.rectTransform, new Vector2(.5f, .5f), new Vector2(wide ? .5f : 0, .5f), position + new Vector2(0, -8), new Vector2(width, 20));
            var fill = HudKit.Box(back.transform, "Fill", color);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0, 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            return fill;
        }

        private static void SetFill(Image fill, float amount)
        {
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(amount), 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        }

        public void Open(MiniGameStart start)
        {
            if (IsOpen) return;
            kind = start.Kind;
            target = start.Target;
            clock = 0;
            pickAngle = 0;
            resultTimer = 0;
            reported = false;
            lockpick = null; hammer = null; fishing = null; dig = null;
            switch (kind)
            {
                case MiniGameKind.Lockpick: lockpick = new LockpickGame(start.Seed, start.Difficulty); title.text = "PICK THE LOCK"; hint.text = "Move the mouse (or the stick) to swing the pick. Hold SPACE / E / click to turn it. Feel for where the lock gives; forcing a jammed lock snaps the pick."; break;
                case MiniGameKind.Hammer: hammer = new HammerGame(start.Seed, start.Difficulty); title.text = "NAIL THE PLANK"; hint.text = "Press SPACE / E / click when the white marker is over the green patch. Three nails hold the plank; three misses split it."; break;
                case MiniGameKind.Fishing: fishing = new FishingGame(start.Seed, start.Difficulty); title.text = "FISHING"; hint.text = "Wait for the float to dip, then press SPACE / E / click to strike. Hold to lift the green bar, let go to lower it: keep it on the fish."; break;
                case MiniGameKind.Dig: dig = new DigGame(start.Difficulty); title.text = "DIG!"; hint.text = "Tap SPACE / E / click as fast as you can: the sand keeps sliding back in."; break;
                default: return;
            }
            lockGroup.gameObject.SetActive(lockpick != null);
            hammerGroup.gameObject.SetActive(hammer != null);
            fishGroup.gameObject.SetActive(fishing != null);
            digGroup.gameObject.SetActive(dig != null);
            status.text = string.Empty;
            root.SetActive(true);
            IsOpen = true;
            if (hud != null && hud.View != null) hud.View.MiniGameOpen = true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            root.SetActive(false);
            if (hud != null && hud.View != null) hud.View.MiniGameOpen = false;
        }

        /// <summary>Stops the game without playing it out, for example when the session ends. Reports the abandonment once.</summary>
        public void Abandon()
        {
            if (!IsOpen) return;
            if (!reported) Report(MiniGameOutcome.Abandoned, 0);
            Close();
        }

        private static bool ActionPressed(Keyboard keyboard, Mouse mouse, Gamepad pad) =>
            keyboard?.spaceKey.wasPressedThisFrame == true || keyboard?.eKey.wasPressedThisFrame == true ||
            mouse?.leftButton.wasPressedThisFrame == true || pad?.buttonSouth.wasPressedThisFrame == true;

        private static bool ActionHeld(Keyboard keyboard, Mouse mouse, Gamepad pad) =>
            keyboard?.spaceKey.isPressed == true || keyboard?.eKey.isPressed == true ||
            mouse?.leftButton.isPressed == true || pad?.buttonSouth.isPressed == true;

        public void Tick(float dt)
        {
            if (!IsOpen) return;
            dt = Mathf.Min(dt, .05f);
            clock += dt;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            if (reported)
            {
                resultTimer -= dt;
                if (resultTimer <= 0) Close();
                return;
            }
            bool cancel = keyboard?.escapeKey.wasPressedThisFrame == true || pad?.buttonEast.wasPressedThisFrame == true || !(GameHud.CursorLockedOverride ?? Cursor.lockState == CursorLockMode.Locked) && Application.isFocused;
            if (cancel) { Abandon(); return; }

            bool pressed = ActionPressed(keyboard, mouse, pad), held = ActionHeld(keyboard, mouse, pad);
            if (lockpick != null) TickLock(dt, held, mouse, pad);
            else if (hammer != null) TickHammer(dt, pressed);
            else if (fishing != null) TickFishing(dt, pressed, held);
            else if (dig != null) TickDig(dt, pressed);
        }

        private void Finish(MiniGameOutcome result, int points, string text, Color color)
        {
            Report(result, points);
            status.text = text;
            status.color = color;
            resultTimer = ResultSeconds;
        }

        private void Report(MiniGameOutcome result, int points)
        {
            if (reported) return;
            reported = true;
            outcome = result;
            score = points;
            if (hud != null && hud.Session != null && hud.Session.IsSpawned)
                hud.Session.MiniGameResultRpc(kind, target, result, points, Mathf.RoundToInt(clock * 1000f));
        }

        private void TickLock(float dt, bool turning, Mouse mouse, Gamepad pad)
        {
            if (!turning || lockpick.Turn < .02f)
            {
                float delta = mouse != null ? mouse.delta.ReadValue().x * .14f : 0;
                if (pad != null) delta += pad.leftStick.ReadValue().x * 100f * dt;
                pickAngle = Mathf.Clamp(pickAngle + delta, -LockpickGame.AngleRange, LockpickGame.AngleRange);
            }
            lockpick.Tick(pickAngle, turning, dt);
            if (lockpick.PickJustBroke) shake = .35f;
            shake = Mathf.Max(0, shake - dt);
            float jitter = (lockpick.Tension > .05f ? Random.Range(-1f, 1f) * lockpick.Tension * 4f : 0) + (shake > 0 ? Random.Range(-1f, 1f) * 6f : 0);
            pickRect.localRotation = Quaternion.Euler(0, 0, -pickAngle + jitter);
            lockDial.localRotation = Quaternion.Euler(0, 0, -lockpick.Turn * 75f);
            SetFill(turnFill, lockpick.Turn);
            SetFill(tensionFill, lockpick.Tension);
            for (int i = 0; i < pickDots.Length; i++) pickDots[i].color = i < lockpick.PicksLeft ? new Color(.82f, .85f, .9f) : new Color(1, 1, 1, .15f);
            if (lockpick.Opened) Finish(MiniGameOutcome.Succeeded, lockpick.Score, "CLICK! IT OPENS", HudKit.Good);
            else if (lockpick.Failed) Finish(MiniGameOutcome.Failed, 0, "OUT OF PICKS", HudKit.Danger);
        }

        private void TickHammer(float dt, bool pressed)
        {
            hammer.Tick(dt);
            if (pressed)
            {
                var strike = hammer.Hit();
                if (strike == HammerGame.Strike.Miss) shake = .25f;
            }
            shake = Mathf.Max(0, shake - dt);
            const float width = 600f;
            hammerWindow.anchorMin = hammerWindow.anchorMax = new Vector2(hammer.WindowCenter, .5f);
            hammerWindow.sizeDelta = new Vector2(width * hammer.WindowHalf * 2f, 80);
            hammerWindow.anchoredPosition = Vector2.zero;
            hammerMarker.anchorMin = hammerMarker.anchorMax = new Vector2(hammer.Marker, .5f);
            hammerMarker.sizeDelta = new Vector2(10, 100);
            hammerMarker.anchoredPosition = Vector2.zero;
            for (int i = 0; i < nailDots.Length; i++) nailDots[i].color = i < hammer.NailsDriven ? new Color(.85f, .85f, .9f) : new Color(1, 1, 1, .2f);
            for (int i = 0; i < missDots.Length; i++) missDots[i].color = i < hammer.Misses ? HudKit.Danger : new Color(1, 1, 1, .2f);
            if (hammer.Succeeded) Finish(MiniGameOutcome.Succeeded, hammer.Score, "NAILED IT", HudKit.Good);
            else if (hammer.Failed) Finish(MiniGameOutcome.Failed, 0, "THE PLANK SPLIT", HudKit.Danger);
        }

        private void TickFishing(float dt, bool pressed, bool held)
        {
            if (pressed) fishing.Hook();
            fishing.Tick(dt, held);
            const float height = 300f;
            switch (fishing.State)
            {
                case FishingGame.Phase.Waiting: status.text = "Waiting for a bite..."; status.color = HudKit.TextMuted; break;
                case FishingGame.Phase.Bite: status.text = "!!!  STRIKE  !!!"; status.color = HudKit.Accent; break;
                case FishingGame.Phase.Reeling: status.text = "Keep the bar on the fish"; status.color = HudKit.Ink; break;
            }
            bool reeling = fishing.State == FishingGame.Phase.Reeling;
            fishBar.gameObject.SetActive(reeling);
            fishFish.gameObject.SetActive(reeling || fishing.State == FishingGame.Phase.Bite);
            float bob = fishing.State == FishingGame.Phase.Waiting ? Mathf.Sin(clock * 2f) * .03f : fishing.State == FishingGame.Phase.Bite ? Mathf.Sin(clock * 30f) * .05f : 0;
            float fishY = reeling ? fishing.FishPosition : .5f + bob;
            fishFish.anchorMin = fishFish.anchorMax = new Vector2(.5f, Mathf.Clamp01(fishY));
            fishFish.anchoredPosition = Vector2.zero;
            fishFish.sizeDelta = new Vector2(30, 30);
            fishBar.anchorMin = fishBar.anchorMax = new Vector2(.5f, Mathf.Clamp01(fishing.Bar));
            fishBar.sizeDelta = new Vector2(80, height * FishingGame.BarSize);
            fishBar.anchoredPosition = Vector2.zero;
            SetFill(fishProgress, reeling ? fishing.Progress : 0);
            if (fishing.Succeeded) Finish(MiniGameOutcome.Succeeded, fishing.Score, fishing.Fish == FishKind.Boot ? "AN OLD BOOT!" : fishing.Fish == FishKind.Big ? "A BIG ONE!" : "GOT IT!", HudKit.Good);
            else if (fishing.Finished) Finish(MiniGameOutcome.Failed, 0, (fishing.LostReason ?? "It got away.").ToUpperInvariant(), HudKit.Danger);
        }

        private void TickDig(float dt, bool pressed)
        {
            if (pressed) dig.Press();
            dig.Tick(dt);
            SetFill(digFill, dig.Depth);
            SetFill(digTime, 1f - dig.Elapsed / DigGame.TimeLimit);
            if (dig.Succeeded) Finish(MiniGameOutcome.Succeeded, dig.Score, "YOU STRUCK SOMETHING!", HudKit.Good);
            else if (dig.Failed) Finish(MiniGameOutcome.Failed, 0, "THE SAND WON", HudKit.Danger);
        }
    }
}
