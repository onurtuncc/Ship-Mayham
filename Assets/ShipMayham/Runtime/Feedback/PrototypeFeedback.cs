using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ShipMayham
{
    /// <summary>
    /// Client-side game feel: turns changes in the replicated session state into sound, particles, floating text, camera shake,
    /// passenger hops and big banners. Purely cosmetic and local; it never writes state and never needs a network message.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(PrototypeView))]
    public sealed class PrototypeFeedback : MonoBehaviour
    {
        private const float SfxVolume = .7f, SeaVolume = .22f;
        private const int Voices = 8;
        private static readonly Color Gold = new Color(1, .84f, .2f), Pink = new Color(1, .45f, .65f), Red = new Color(1, .3f, .25f), Cyan = new Color(.5f, .95f, 1f);

        public PrototypeView View;
        public FeedbackFx Fx;
        [Tooltip("Optional: passenger body roots that hop when delighted (same order as the passengers).")]
        public PrototypePassengerAnimator PassengerAnimator;
        public PrototypeCrewAnimator CrewAnimator;

        private FeedbackSnapshot previous, current;
        private readonly List<FeedbackEvent> events = new List<FeedbackEvent>(16);
        private AudioSource[] voices;
        private AudioSource sea;
        private int nextVoice;
        private readonly float[] lastPlayed = new float[System.Enum.GetValues(typeof(SfxKind)).Length];
        private PrototypeCombatView combat;
        private Vector3 lastCrewBall;

        // Camera shake: trauma decays, and the offset is applied only while the camera renders so game logic never sees it.
        private float trauma, flash;
        private Vector3 shakeOffset; private float shakeRoll;

        private struct Hop { public Transform Body; public float Age; public Vector3 Rest; }
        private readonly List<Hop> hops = new List<Hop>();

        private struct Banner { public string Title, Subtitle; public Color Color; public float Age; }
        private readonly Queue<Banner> bannerQueue = new Queue<Banner>();
        private Banner banner; private bool bannerActive;
        private GUIStyle titleStyle, subtitleStyle;

        private void Awake()
        {
            if (View == null) View = GetComponent<PrototypeView>();
            combat = GetComponent<PrototypeCombatView>();
            previous = FeedbackSnapshot.Create(); current = FeedbackSnapshot.Create();
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false; voices[i].spatialBlend = 0;
            }
            sea = gameObject.AddComponent<AudioSource>();
            sea.loop = true; sea.volume = 0; sea.spatialBlend = 0; sea.playOnAwake = false;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            if (View != null && View.Session != null) View.Session.SignalReceived += OnSignal;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            if (View != null && View.Session != null) View.Session.SignalReceived -= OnSignal;
        }

        // A crew member pinged or emoted: they gesture, and everyone hears a short cue (louder the closer they are).
        private void OnSignal(SignalMessage message)
        {
            Vector3 at = CrewPoint(message.Slot);
            if (CrewAnimator != null) CrewAnimator.PlayOneShot(message.Slot, Signals.PoseFor(message.Kind), Signals.GestureSeconds);
            switch (message.Kind)
            {
                case SignalKind.Ping: Sfx(SfxKind.Pop, .8f, 1.5f); break;
                case SignalKind.Help: Sfx(SfxKind.Alarm, .35f, 1.2f, at); break;
                case SignalKind.Thanks: Sfx(SfxKind.Chime, .7f, 1.1f, at); break;
                case SignalKind.OverHere: Sfx(SfxKind.Pop, .8f, 1.25f, at); break;
                case SignalKind.WatchOut: Sfx(SfxKind.Alarm, .5f, 1.4f, at); break;
            }
        }

        private void Update()
        {
            var session = View != null ? View.Session : null;
            bool live = session != null && session.IsSpawned;
            if (live && !sea.isPlaying)
            {
                if (sea.clip == null) sea.clip = ProceduralSfx.SeaLoop();
                sea.Play();
            }
            if (Keyboard.current?.mKey.wasPressedThisFrame == true) FeedbackSettings.Muted = !FeedbackSettings.Muted;
            sea.volume = Mathf.MoveTowards(sea.volume, live ? SeaVolume * FeedbackSettings.SfxGain : 0, Time.unscaledDeltaTime * .6f);
            Capture(live ? session : null, ref current);
            events.Clear();
            FeedbackDetector.Detect(previous, current, events);
            foreach (var feedbackEvent in events) Play(feedbackEvent);
            (previous, current) = (current, previous);
            trauma = Mathf.MoveTowards(trauma, 0, Time.unscaledDeltaTime * 1.6f);
            flash = Mathf.MoveTowards(flash, 0, Time.unscaledDeltaTime * 2.2f);
            UpdateHops();
            UpdateBanner();
        }

        // ---------------------------------------------------------------- state capture

        private void Capture(PrototypeSession session, ref FeedbackSnapshot snapshot)
        {
            snapshot.Valid = session != null;
            if (!snapshot.Valid) return;
            var voyage = session.Voyage.Value; var encounter = session.Encounter.Value; var voyageEvent = session.ActiveEvent.Value;
            snapshot.Phase = voyage.Phase; snapshot.Wallet = voyage.Wallet; snapshot.Reputation = voyage.Reputation;
            snapshot.Leak = voyage.Leak; snapshot.Health = voyage.Health; snapshot.HullShield = voyage.HullShield;
            snapshot.UpgradeLevels = voyage.EngineLevel + voyage.CapacityLevel + voyage.ComfortLevel;
            snapshot.DockJobsCompleted = voyage.DockJobsCompleted; snapshot.RescueCount = voyage.RescueCount; snapshot.CollectedFares = voyage.CollectedFares;
            snapshot.EventPhase = voyageEvent.Phase; snapshot.EventSerial = voyageEvent.Serial;
            var hazards = session.Hazards.Value;
            snapshot.WaveWarning = hazards.Wave == WavePhase.Warning; snapshot.WavesDodged = hazards.WavesDodged;
            snapshot.ShoalNear = false;
            if (voyage.Phase == VoyagePhase.Sailing)
            {
                int shoalCount = ShoalSet.For(hazards.Seed, voyage.Route);
                snapshot.ShoalNear = HazardLogic.NearestAhead(voyage.Position, voyage.Heading, ShoalSet.Shoals, shoalCount, out _) >= 0;
            }
            snapshot.Pirate = encounter.Pirate; snapshot.PirateHealth = encounter.PirateHealth;
            snapshot.CrewBallActive = encounter.CrewBall.Active; snapshot.PirateBallActive = encounter.PirateBall.Active;
            if (encounter.CrewBall.Active) lastCrewBall = encounter.CrewBall.Position;
            for (int i = 0; i < FeedbackSnapshot.MaxPassengers; i++)
            {
                if (i >= session.Passengers.Count) { snapshot.Passengers[i] = default; continue; }
                var p = session.Passengers[i];
                snapshot.Passengers[i] = new PassengerSnapshot { Active = p.Active, Request = p.Request, Reaction = p.Reaction, Motion = p.Motion, Carrier = p.CarrierSlot, Reacting = p.ReactionRemaining > 0 };
            }
            for (int i = 0; i < FeedbackSnapshot.MaxCrew; i++)
            {
                bool present = i < session.Crew.Count && session.Crew[i].Active && session.Crew[i].ClientId != ulong.MaxValue;
                snapshot.CrewActive[i] = present;
                snapshot.Carrying[i] = present ? session.Crew[i].Carrying : ServiceItem.None;
            }
        }

        // ---------------------------------------------------------------- reactions

        /// <summary>Plays the full reaction for one event. Public so tests and tools can trigger feedback directly.</summary>
        public void Play(FeedbackEvent e)
        {
            Vector3 ship = ShipPoint(), passenger = PassengerPoint(e.Slot), crew = CrewPoint(e.Slot);
            switch (e.Kind)
            {
                case FeedbackKind.CoinsEarned:
                    Fx.Burst(FxKind.Coins, ship, Mathf.Clamp(5 + e.Value / 8, 6, 24));
                    Fx.Popup($"+{e.Value}", ship + Vector3.up * .6f, Gold, 1.25f);
                    Sfx(SfxKind.Coin, 1, 1 + Mathf.Min(.25f, e.Value / 400f)); break;
                case FeedbackKind.PassengerBoarded:
                    Fx.Burst(FxKind.Sparkle, passenger, 8); Sfx(SfxKind.Pop, .8f, 1.05f, passenger); break;
                case FeedbackKind.ServiceDelivered:
                    Fx.Burst(FxKind.Sparkle, passenger, 12); Sfx(SfxKind.Pop, 1, 1.2f, passenger);
                    if (PassengerAnimator != null)
                    {
                        if (e.Value == (int)ServiceItem.Drink) PassengerAnimator.PlayOneShot(e.Slot, PassengerPose.Drinking, 3.2f);
                        else if (e.Value == (int)ServiceItem.Food) PassengerAnimator.PlayOneShot(e.Slot, PassengerPose.Clapping, 2.4f);
                    }
                    break;
                case FeedbackKind.PassengerThrown:
                    Fx.Burst(FxKind.Splash, passenger, 8); Sfx(SfxKind.Whoosh, .9f, 1.1f, passenger);
                    if (CrewAnimator != null) CrewAnimator.PlayOneShot(e.Value, CrewPose.Throw, 1.2f);
                    break;
                case FeedbackKind.PassengerLanded:
                    Fx.Burst(FxKind.Smoke, passenger, 10); Fx.Popup("Oof!", passenger + Vector3.up * .5f, Gold, 1.2f); Sfx(SfxKind.Thud, .9f, .85f, passenger);
                    AddTrauma(.12f); Babble.Say(passenger, e.Slot, BabbleMood.Grumpy, .7f); break;
                case FeedbackKind.PassengerThanked:
                    Fx.Burst(FxKind.Hearts, passenger); Fx.Popup("Thanks!", passenger + Vector3.up * .3f, Pink); Sfx(SfxKind.Chime, .9f, 1, passenger); StartHop(e.Slot);
                    Babble.Say(passenger, e.Slot, BabbleMood.Happy, .8f); break;
                case FeedbackKind.PassengerComplained:
                    Fx.Burst(FxKind.Anger, passenger); Fx.Popup("!!", passenger + Vector3.up * .3f, Red, 1.3f); Sfx(SfxKind.Thud, .6f, 1.3f, passenger);
                    Babble.Say(passenger, e.Slot, BabbleMood.Grumpy, .9f); break;
                case FeedbackKind.PassengerRescued:
                    Fx.Burst(FxKind.Splash, passenger, 24); Fx.Popup("Phew!", passenger + Vector3.up * .3f, Cyan); Sfx(SfxKind.Splash, .8f, 1, passenger); StartHop(e.Slot);
                    Babble.Say(passenger, e.Slot, BabbleMood.Curious, .8f); break;
                case FeedbackKind.HullDamaged:
                    AddTrauma(.35f + Mathf.Min(.45f, e.Value / 80f)); flash = Mathf.Max(flash, .55f);
                    Fx.Burst(FxKind.Smoke, ship, 10); Fx.Popup($"-{e.Value}", ship + Vector3.up, Red, 1.2f); Sfx(SfxKind.Thud, 1, .9f);
                    Haptics.Pulse(.5f + Mathf.Min(.4f, e.Value / 60f), .3f, .25f); break;
                case FeedbackKind.WaveWarning:
                    ShowBanner("ROGUE WAVE!", "Turn the bow toward it", Gold); AddTrauma(.15f); Sfx(SfxKind.Horn, .8f, .85f); break;
                case FeedbackKind.WaveDodged:
                    ShowBanner("DODGED!", null, Cyan); Fx.Burst(FxKind.Stars, ship + Vector3.up * 2, 12); Sfx(SfxKind.Chime);
                    if (CrewAnimator != null) CrewAnimator.PlayOneShotAll(CrewPose.Clap, 2.2f);
                    break;
                case FeedbackKind.ShoalAhead:
                    Fx.Popup("SHOAL!", ship + Vector3.up * 1.2f, Gold, 1.3f); Sfx(SfxKind.Horn, .6f, 1.25f); break;
                case FeedbackKind.LeakStarted:
                    flash = Mathf.Max(flash, .35f); Fx.Popup("LEAK!", ship + Vector3.up, Red, 1.3f); Sfx(SfxKind.Alarm, .8f); break;
                case FeedbackKind.LeakFixed:
                    Fx.Burst(FxKind.Sparkle, ship, 20); Fx.Popup("Fixed!", ship + Vector3.up, Cyan, 1.2f); Sfx(SfxKind.Chime); Sfx(SfxKind.Sparkle, .6f); break;
                case FeedbackKind.VoyageStarted:
                    ShowBanner("ANCHORS AWEIGH!", "Keep every passenger happy until port", Cyan); Sfx(SfxKind.Horn); Sfx(SfxKind.Whoosh, .6f); break;
                case FeedbackKind.VoyageArrived:
                    ShowBanner("VOYAGE COMPLETE!", e.Value > 0 ? $"+{e.Value} coins" : "Welcome to port", Gold);
                    Fx.Burst(FxKind.Confetti, ship + Vector3.up * 2, 90); Fx.Burst(FxKind.Stars, ship + Vector3.up * 3, 14);
                    Sfx(SfxKind.Fanfare); Sfx(SfxKind.Sparkle, .7f); break;
                case FeedbackKind.VoyageFailed:
                    ShowBanner("SHIP LOST", "The sea wins this time", Red); AddTrauma(.7f); Sfx(SfxKind.Sad); Haptics.Pulse(.8f, .4f, .5f); break;
                case FeedbackKind.ReputationGained:
                    Fx.Burst(FxKind.Stars, ship + Vector3.up * 2, 12); Fx.Popup($"+{e.Value} REP", ship + Vector3.up * 2.2f, new Color(1, .6f, 1f), 1.1f); Sfx(SfxKind.Sparkle, .8f); break;
                case FeedbackKind.UpgradePurchased:
                    ShowBanner("UPGRADED!", "Your ship got better", new Color(.75f, .5f, 1f));
                    Fx.Burst(FxKind.Stars, ship + Vector3.up * 1.5f, 16); Sfx(SfxKind.Chime); Sfx(SfxKind.Sparkle, .8f);
                    if (CrewAnimator != null) CrewAnimator.PlayOneShotAll(CrewPose.Salute, 2.2f);
                    break;
                case FeedbackKind.DockJobDone:
                    ShowBanner("JOB COMPLETE!", "Nice teamwork", Gold); Fx.Burst(FxKind.Confetti, CrewPoint(0) + Vector3.up, 50); Sfx(SfxKind.Fanfare, .8f); break;
                case FeedbackKind.EventResolved:
                    ShowBanner("NICE SAVE!", null, Cyan); Fx.Burst(FxKind.Stars, ship + Vector3.up * 2, 12); Sfx(SfxKind.Chime);
                    if (CrewAnimator != null) CrewAnimator.PlayOneShotAll(CrewPose.Clap, 2.6f);
                    break;
                case FeedbackKind.EventFailed:
                    ShowBanner("OH NO!", null, Red); AddTrauma(.3f); Sfx(SfxKind.Thud, .9f, .8f); break;
                case FeedbackKind.CannonFired:
                    {
                        Vector3 muzzle = combat != null && combat.MuzzleAnchor != null ? combat.MuzzleAnchor.position : ship;
                        Fx.Burst(FxKind.Smoke, muzzle, 14); Fx.Burst(FxKind.Sparkle, muzzle, 10); AddTrauma(.3f); Sfx(SfxKind.Boom);
                        Haptics.Pulse(.35f, .8f, .15f); break;
                    }
                case FeedbackKind.CannonballLanded:
                    Fx.Burst(FxKind.Splash, lastCrewBall, 26); Sfx(SfxKind.Splash, .8f, 1, lastCrewBall); break;
                case FeedbackKind.PirateHit:
                    {
                        Vector3 pirate = combat != null && combat.PirateRoot != null ? combat.PirateRoot.position + Vector3.up * 2 : ship;
                        Fx.Burst(FxKind.Stars, pirate, 10); Fx.Burst(FxKind.Smoke, pirate, 6); Fx.Popup("HIT!", pirate + Vector3.up, Gold, 1.4f); Sfx(SfxKind.Thud, 1, 1.1f, pirate); AddTrauma(.15f); break;
                    }
                case FeedbackKind.PirateDefeated:
                    ShowBanner("PIRATES DEFEATED!", "The seas are safe... for now", Gold);
                    Fx.Burst(FxKind.Confetti, ship + Vector3.up * 2, 90); Sfx(SfxKind.Fanfare);
                    if (CrewAnimator != null) CrewAnimator.PlayOneShotAll(CrewPose.Victory, 4f);
                    break;
                case FeedbackKind.ItemPickedUp:
                    Fx.Burst(FxKind.Sparkle, crew, 6); Sfx(SfxKind.Pop, .6f, .9f, crew);
                    if (CrewAnimator != null) CrewAnimator.PlayOneShot(e.Slot, CrewPose.PickUp, .9f);
                    break;
                case FeedbackKind.ItemDelivered:
                    Fx.Burst(FxKind.Sparkle, crew, 8); Sfx(SfxKind.Pop, .7f, 1.15f, crew); break;
            }
        }

        private Vector3 ShipPoint() => View != null && View.Session != null && View.Session.ShipRoot != null ? View.Session.ShipRoot.position + Vector3.up * 2.2f : Vector3.up * 2;

        private Vector3 PassengerPoint(int slot) =>
            View != null && slot >= 0 && slot < View.PassengerVisuals.Length && View.PassengerVisuals[slot] != null ? View.PassengerVisuals[slot].position + Vector3.up * 1.7f : ShipPoint();

        private Vector3 CrewPoint(int slot) =>
            View != null && slot >= 0 && slot < View.CrewRoots.Length && View.CrewRoots[slot] != null ? View.CrewRoots[slot].position + Vector3.up * 1.6f : ShipPoint();

        // ---------------------------------------------------------------- sound

        private void Sfx(SfxKind kind, float volume = 1, float pitch = 1, Vector3? at = null)
        {
            if (Time.unscaledTime - lastPlayed[(int)kind] < .05f) return;
            lastPlayed[(int)kind] = Time.unscaledTime;
            float gain = volume * FeedbackSettings.SfxGain;
            if (at.HasValue && View != null && View.Camera != null)
                gain *= Mathf.Clamp01(1.25f - Vector3.Distance(at.Value, View.Camera.transform.position) / 45f);
            if (gain <= .01f) return;
            var voice = voices[nextVoice++ % Voices];
            voice.pitch = pitch * Random.Range(.96f, 1.04f);
            voice.PlayOneShot(ProceduralSfx.Get(kind), gain * SfxVolume);
        }

        // ---------------------------------------------------------------- camera shake and flash

        private void AddTrauma(float amount) => trauma = Mathf.Min(1, trauma + amount * GameSettings.ShakeScale);

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (View == null || camera != View.Camera || trauma <= 0) return;
            float shake = trauma * trauma, t = Time.unscaledTime * 38;
            shakeOffset = camera.transform.TransformVector(new Vector3(Mathf.PerlinNoise(t, 1) - .5f, Mathf.PerlinNoise(t, 7) - .5f, 0)) * (.45f * shake);
            shakeRoll = (Mathf.PerlinNoise(t, 13) - .5f) * 5f * shake;
            camera.transform.position += shakeOffset;
            camera.transform.rotation *= Quaternion.Euler(0, 0, shakeRoll);
        }

        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (View == null || camera != View.Camera || shakeOffset == Vector3.zero && shakeRoll == 0) return;
            camera.transform.rotation *= Quaternion.Euler(0, 0, -shakeRoll);
            camera.transform.position -= shakeOffset;
            shakeOffset = Vector3.zero; shakeRoll = 0;
        }

        // ---------------------------------------------------------------- passenger hop

        private void StartHop(int slot)
        {
            if (PassengerAnimator == null || slot < 0 || slot >= PassengerAnimator.Animators.Length || PassengerAnimator.Animators[slot] == null) return;
            Transform body = PassengerAnimator.Animators[slot].transform.parent;
            if (body == null) return;
            for (int i = 0; i < hops.Count; i++) if (hops[i].Body == body) return;
            hops.Add(new Hop { Body = body, Age = 0, Rest = body.localPosition });
        }

        private void UpdateHops()
        {
            for (int i = hops.Count - 1; i >= 0; i--)
            {
                var hop = hops[i];
                hop.Age += Time.deltaTime;
                float t = hop.Age / .55f;
                if (hop.Body == null) { hops.RemoveAt(i); continue; }
                if (t >= 1) { hop.Body.localPosition = hop.Rest; hops.RemoveAt(i); continue; }
                hop.Body.localPosition = hop.Rest + Vector3.up * (.28f * Mathf.Sin(t * Mathf.PI));
                hops[i] = hop;
            }
        }

        // ---------------------------------------------------------------- banners and flash (IMGUI, drawn above the HUD)

        private void ShowBanner(string title, string subtitle, Color color)
        {
            if (bannerQueue.Count >= 3) return;
            bannerQueue.Enqueue(new Banner { Title = title, Subtitle = subtitle, Color = color });
        }

        private void UpdateBanner()
        {
            if (bannerActive)
            {
                banner.Age += Time.unscaledDeltaTime;
                if (banner.Age > 2.6f) bannerActive = false;
            }
            if (!bannerActive && bannerQueue.Count > 0) { banner = bannerQueue.Dequeue(); bannerActive = true; }
        }

        private void OnGUI()
        {
            if (flash > 0)
            {
                GUI.depth = -90;
                var previousColor = GUI.color;
                GUI.color = new Color(1, .15f, .1f, flash * .38f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = previousColor;
            }
            if (!bannerActive) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                subtitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            float t = banner.Age / 2.6f;
            float pop = t < .12f ? Mathf.Lerp(.4f, 1.15f, t / .12f) : t < .22f ? Mathf.Lerp(1.15f, 1f, (t - .12f) / .1f) : 1f;
            float alpha = Mathf.Clamp01(Mathf.Min(t / .06f, (1 - t) / .2f));
            GUI.depth = -100;
            titleStyle.fontSize = Mathf.RoundToInt(Screen.height * .085f * pop);
            subtitleStyle.fontSize = Mathf.RoundToInt(Screen.height * .036f);
            var area = new Rect(0, Screen.height * .14f, Screen.width, Screen.height * .12f);
            DrawShadowed(area, banner.Title, titleStyle, banner.Color, alpha);
            if (!string.IsNullOrEmpty(banner.Subtitle))
                DrawShadowed(new Rect(0, area.yMax - Screen.height * .01f, Screen.width, Screen.height * .06f), banner.Subtitle, subtitleStyle, Color.white, alpha);
        }

        private static void DrawShadowed(Rect rect, string text, GUIStyle style, Color color, float alpha)
        {
            var previous = GUI.color;
            float offset = Mathf.Max(2, style.fontSize * .06f);
            GUI.color = new Color(0, 0, 0, alpha * .75f);
            var shadow = rect; shadow.x += offset; shadow.y += offset;
            GUI.Label(shadow, text, style);
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }
    }
}
