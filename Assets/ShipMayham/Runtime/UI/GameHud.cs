using UnityEngine;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>
    /// In-voyage heads-up display. Layout: status top-left, objective top-centre, resources and map top-right,
    /// crosshair prompt in the centre, hints and notices at the bottom, port and report panels at the sides.
    /// </summary>
    public sealed class GameHud
    {
        private readonly PrototypeHud hud;
        private readonly GameObject root;

        // Top-left
        private readonly Text voyageTitle, phaseText, routeText, hullValue, statusLine;
        private readonly Image phaseChip, hullFill;
        private readonly AlertList alerts;
        private readonly ReportPanel report;
        // Top-centre
        private readonly Text objective, bannerText;
        private readonly GameObject objectiveRoot, bannerRoot;
        // Top-right
        private readonly ResourcePill coins, reputation, food, drink;
        private readonly Text sessionText;
        private readonly GameObject sessionRoot;
        private readonly MiniMap map;
        // Centre / bottom
        private readonly Crosshair crosshair;
        private readonly PromptCard promptCard;
        private readonly HintRow contextHints;
        private readonly Text contextTitle, noticeText, carryText, resumeText;
        private readonly CanvasGroup noticeGroup;
        private readonly GameObject contextRoot, carryRoot, resumeRoot;
        private readonly HarborPanel harbor;
        private readonly HelpOverlay help;
        private readonly PurserPanel purser;
        private readonly PauseMenu pause;
        private readonly SignalLayer signals;
        private readonly SignalWheel wheel;
        private readonly TipCard tips;
        private readonly System.Func<TipContext> tipContext;

        private int voyageSignature = int.MinValue, objectiveSignature = int.MinValue, alertSignature = int.MinValue, carrySignature = int.MinValue;
        private readonly ResourcePill[] pills;
        private string shownNotice;

        /// <summary>Lets preview captures and tests show the aiming UI without a real pointer lock.</summary>
        public static bool? CursorLockedOverride;
        /// <summary>Forces the crosshair prompt, for the same purpose as <see cref="CursorLockedOverride"/>.</summary>
        public static InteractionPrompt? PromptOverride;

        public GameHud(PrototypeHud hud, Transform canvas)
        {
            this.hud = hud;
            var screen = HudKit.Node(canvas, "GameHud");
            root = screen.gameObject;
            HudKit.Stretch(screen);

            // ---- Top-left: voyage status, alerts, report
            var left = HudKit.Node(screen, "Left");
            HudKit.Anchor(left, new Vector2(0, 1), new Vector2(0, 1), new Vector2(HudKit.Margin, -HudKit.Margin), new Vector2(420, 0));
            HudKit.Column(left, 12, new RectOffset(0, 0, 0, 0)).childAlignment = TextAnchor.UpperLeft;

            var card = HudKit.Box(left, "VoyageCard", HudKit.Panel);
            HudKit.Column(card.rectTransform, 8, new RectOffset(22, 22, 16, 18), false);
            var titleRow = HudKit.Node(card.transform, "TitleRow");
            HudKit.Size(titleRow, height: 40);
            HudKit.Row(titleRow, 12);
            voyageTitle = HudKit.Label(titleRow, "Voyage", 30, HudKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            HudKit.Size(voyageTitle, flexibleWidth: 1);
            phaseChip = HudKit.Box(titleRow, "Phase", HudKit.Info);
            HudKit.Row(phaseChip.rectTransform, 0, TextAnchor.MiddleCenter).padding = new RectOffset(14, 14, 4, 4);
            phaseText = HudKit.Label(phaseChip.transform, "Text", 17, new Color(.05f, .08f, .12f), TextAnchor.MiddleCenter, FontStyle.Bold, false);
            routeText = HudKit.Label(card.transform, "Route", 21, HudKit.TextMuted);
            HudKit.Size(routeText, height: 28);
            var hullRow = HudKit.Node(card.transform, "HullRow");
            HudKit.Size(hullRow, height: 28);
            HudKit.Row(hullRow, 12);
            var hullLabel = HudKit.Label(hullRow, "HullLabel", 17, HudKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            hullLabel.text = "HULL";
            HudKit.Size(hullLabel, width: 48);
            var hullBar = HudKit.Box(hullRow, "Bar", new Color(1, 1, 1, .14f));
            HudKit.Size(hullBar, height: 14, flexibleWidth: 1);
            hullFill = HudKit.Box(hullBar.transform, "Fill", HudKit.Good);
            hullFill.rectTransform.anchorMin = Vector2.zero; hullFill.rectTransform.anchorMax = Vector2.one;
            hullFill.rectTransform.offsetMin = hullFill.rectTransform.offsetMax = Vector2.zero;
            hullValue = HudKit.Label(hullRow, "HullValue", 20, HudKit.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            HudKit.Size(hullValue, width: 96);
            statusLine = HudKit.Label(card.transform, "Status", 20, HudKit.Ink);
            HudKit.Size(statusLine, height: 28);

            alerts = new AlertList(left, "Alerts", 6);
            report = new ReportPanel(left);

            // ---- Top-centre: objective + banner
            var center = HudKit.Node(screen, "Center");
            HudKit.Anchor(center, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -HudKit.Margin), new Vector2(0, 0));
            HudKit.Column(center, 10, new RectOffset(0, 0, 0, 0)).childAlignment = TextAnchor.UpperCenter;
            center.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var objectiveBox = HudKit.Box(center, "Objective", HudKit.Panel);
            objectiveRoot = objectiveBox.gameObject;
            HudKit.Row(objectiveBox.rectTransform, 12, TextAnchor.MiddleCenter).padding = new RectOffset(28, 28, 12, 14);
            var goal = HudKit.Label(objectiveBox.transform, "Goal", 15, HudKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            goal.text = "GOAL";
            objective = HudKit.Label(objectiveBox.transform, "Text", 24, HudKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            var banner = HudKit.Box(center, "Banner", new Color(.45f, .08f, .08f, .92f));
            bannerRoot = banner.gameObject;
            HudKit.Row(banner.rectTransform, 0, TextAnchor.MiddleCenter).padding = new RectOffset(26, 26, 12, 14);
            bannerText = HudKit.Label(banner.transform, "Text", 22, HudKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            bannerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudKit.Size(bannerText, width: 760);
            banner.gameObject.SetActive(false);

            // ---- Top-right: resources, session, minimap
            var right = HudKit.Node(screen, "Right");
            HudKit.Anchor(right, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-HudKit.Margin, -HudKit.Margin), new Vector2(0, 0));
            var rightColumn = HudKit.Column(right, 12, new RectOffset(0, 0, 0, 0));
            rightColumn.childAlignment = TextAnchor.UpperRight;
            rightColumn.childForceExpandWidth = false;
            right.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var pillRow = HudKit.Node(right, "Pills");
            HudKit.Row(pillRow, 10, TextAnchor.UpperRight);
            coins = new ResourcePill(pillRow, "Coins", "COINS", HudKit.Coin);
            reputation = new ResourcePill(pillRow, "Reputation", "REPUTATION", HudKit.Accent);
            food = new ResourcePill(pillRow, "Food", "FOOD", HudKit.Food);
            drink = new ResourcePill(pillRow, "Drink", "DRINKS", HudKit.Drink);
            pills = new[] { coins, reputation, food, drink };
            var info = HudKit.Node(right, "Info");
            HudKit.Size(info, height: 44);
            HudKit.Row(info, 12, TextAnchor.MiddleRight);
            sessionText = HudKit.Label(info, "Session", 20, HudKit.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            sessionRoot = sessionText.gameObject;
            var helpHint = HudKit.Label(info, "HelpHint", 18, HudKit.TextMuted, TextAnchor.MiddleRight);
            helpHint.text = "F1  Controls";
            var leave = HudKit.MakeButton(info, "Leave", "LEAVE", new Color(.75f, .3f, .3f, .9f), 18);
            HudKit.Size(leave, width: 96, height: 40);
            leave.onClick.AddListener(hud.LeaveSession);
            map = new MiniMap(right);

            // ---- Bottom-right: harbor
            var bottomRight = HudKit.Node(screen, "BottomRight");
            HudKit.Anchor(bottomRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-HudKit.Margin, HudKit.Margin), new Vector2(380, 0));
            HudKit.Column(bottomRight, 0, new RectOffset(0, 0, 0, 0)).childAlignment = TextAnchor.LowerRight;
            harbor = new HarborPanel(hud.Session, bottomRight);

            // ---- Centre: crosshair + prompt
            crosshair = new Crosshair(screen);
            promptCard = new PromptCard(screen);

            // ---- Bottom-centre: context hints + notice
            var bottom = HudKit.Node(screen, "Bottom");
            HudKit.Anchor(bottom, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(0, 0));
            HudKit.Column(bottom, 14, new RectOffset(0, 0, 0, 0)).childAlignment = TextAnchor.LowerCenter;
            bottom.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var noticeBox = HudKit.Box(bottom, "Notice", HudKit.Panel);
            noticeGroup = noticeBox.gameObject.AddComponent<CanvasGroup>();
            noticeGroup.blocksRaycasts = false;
            HudKit.Row(noticeBox.rectTransform, 0, TextAnchor.MiddleCenter).padding = new RectOffset(28, 28, 12, 14);
            noticeText = HudKit.Label(noticeBox.transform, "Text", 24, HudKit.Ink, TextAnchor.MiddleCenter);
            noticeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudKit.Size(noticeText, width: 900);
            noticeBox.gameObject.SetActive(false);
            var contextBox = HudKit.Box(bottom, "Context", HudKit.PanelStrong);
            contextRoot = contextBox.gameObject;
            HudKit.Row(contextBox.rectTransform, 26, TextAnchor.MiddleCenter).padding = new RectOffset(28, 28, 12, 12);
            contextTitle = HudKit.Label(contextBox.transform, "Title", 20, HudKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            contextHints = new HintRow(contextBox.transform, "Hints", 3, 20, TextAnchor.MiddleCenter);
            contextBox.gameObject.SetActive(false);

            // ---- Bottom-left: carried item
            var carry = HudKit.Box(screen, "Carry", HudKit.Panel);
            carryRoot = carry.gameObject;
            HudKit.Anchor(carry.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(HudKit.Margin, HudKit.Margin), new Vector2(0, 0));
            HudKit.Column(carry.rectTransform, 0, new RectOffset(22, 22, 12, 14)).childForceExpandWidth = false;
            carry.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var carryCaption = HudKit.Label(carry.transform, "Caption", 15, HudKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            carryCaption.text = "CARRYING";
            carryText = HudKit.Label(carry.transform, "Item", 28, HudKit.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            carry.gameObject.SetActive(false);

            // ---- Resume-look pill
            var resume = HudKit.Box(screen, "Resume", HudKit.PanelStrong);
            resumeRoot = resume.gameObject;
            HudKit.Anchor(resume.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -230), new Vector2(0, 0));
            HudKit.Row(resume.rectTransform, 0, TextAnchor.MiddleCenter, true).padding = new RectOffset(30, 30, 14, 16);
            resumeText = HudKit.Label(resume.transform, "Text", 24, HudKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            resume.gameObject.SetActive(false);

            help = new HelpOverlay(screen);
            signals = new SignalLayer(screen);
            wheel = new SignalWheel(screen);
            if (hud.Session != null) hud.Session.SignalReceived += signals.Add;
            purser = new PurserPanel(hud, screen);
            pause = new PauseMenu(hud, screen);
            tips = new TipCard(screen);
            tipContext = BuildTipContext;
            screen.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            HudKit.SetActive(root, visible);
            if (!visible) tips.Reset();
        }

        /// <summary>Stops listening to the session (the HUD is going away).</summary>
        public void Dispose()
        {
            if (hud.Session != null) hud.Session.SignalReceived -= signals.Add;
        }

        public void ToggleHelp() => help.Toggle();

        public bool HelpVisible => help.Visible;

        public void Tick(float dt, bool gamepad)
        {
            var session = hud.Session;
            var view = hud.View;
            var s = session.Voyage.Value;
            int local = session.LocalCrewIndex;
            bool locked = CursorLockedOverride ?? Cursor.lockState == CursorLockMode.Locked;
            bool firstPerson = view != null && view.IsFirstPerson && local >= 0;

            TickVoyageCard(s);
            TickResources(dt, s);
            TickObjective(session, s, local);
            TickAlerts(session, s, local, gamepad);
            report.Tick(s, session);
            map.Tick(session, local);
            harbor.Tick(local, locked);
            TickSession();
            TickBanner();

            // Crosshair and prompt are only meaningful while the mouse look is captured.
            var prompt = PromptOverride ?? (firstPerson ? view.Prompt : default);
            bool aiming = firstPerson && locked && !view.ChaseCameraActive;
            crosshair.Root.gameObject.SetActive(aiming);
            if (aiming) crosshair.Tick(dt, prompt.Targeted && !prompt.IsEmpty, prompt.Tone);
            bool showCard = aiming && prompt.Targeted && !prompt.IsEmpty;
            promptCard.Tick(dt, showCard, prompt, gamepad);
            TickContextHints(firstPerson && locked, prompt, gamepad);
            TickNotice(session, dt);
            TickCarry(session, local);
            TickResume(firstPerson && !locked && !view.MenuOpen && !view.PurserOpen);
            help.Tick(gamepad);
            purser.Tick();
            pause.Tick();
            signals.Tick(hud);
            wheel.Tick(view);
            tips.Tick(Time.unscaledTime, tipContext, gamepad, view != null && (view.MenuOpen || view.PurserOpen || view.WheelOpen) || help.Visible);
            for (int i = 0; i < pills.Length; i++) pills[i].Tick(dt);
        }

        private TipContext BuildTipContext()
        {
            var session = hud.Session;
            var s = session.Voyage.Value;
            int local = session.LocalCrewIndex;
            var context = new TipContext
            {
                InVoyage = local >= 0 && s.SessionPhase == SessionPhase.Voyage,
                Phase = s.Phase,
                Boarded = s.Boarded,
                DistanceToPort = Vector2.Distance(s.Position, VoyageSimulation.Destination),
                Leak = s.Leak != 0,
            };
            var hazards = session.Hazards.Value;
            context.HazardWarning = hazards.Wave == WavePhase.Warning || NearestShoal(s, hazards, out _) >= 0;
            if (!context.InVoyage) return context;
            var crew = session.Crew[local];
            context.AtHelm = crew.AtHelm;
            context.AtCannon = crew.AtCannon;
            context.OnShore = crew.OnShore;
            context.Carrying = crew.Carrying != ServiceItem.None || session.LocalCarriedPassengerIndex >= 0;
            var pirate = session.Encounter.Value.Pirate;
            context.PiratesNear = pirate == PiratePhase.Warning || pirate == PiratePhase.Attacking;
            for (int i = 0; i < session.Passengers.Count; i++)
            {
                var passenger = session.Passengers[i];
                if (passenger.Active && passenger.Request != ServiceItem.None) { context.OpenOrders = true; break; }
            }
            var target = hud.View != null ? hud.View.Target : default;
            context.LookingAtPassenger = target.Kind == InteractionKind.Passenger;
            return context;
        }

        private void TickVoyageCard(in VoyageState s)
        {
            float dist = Vector2.Distance(s.Position, VoyageSimulation.Destination);
            int sig = HashCode(s.Voyage, (int)s.Phase, (int)s.Route, Mathf.RoundToInt(s.Health), (int)s.HullShield,
                Mathf.RoundToInt(s.Speed * 2), Mathf.RoundToInt(dist / 5), s.Boarded);
            sig = sig * 31 + s.CheckedTickets + s.Served * 7;
            if (sig == voyageSignature) return;
            voyageSignature = sig;
            voyageTitle.text = "VOYAGE " + s.Voyage;
            Color chip = s.Phase == VoyagePhase.Boarding ? HudKit.Info : s.Phase == VoyagePhase.Sailing ? HudKit.Accent :
                s.Phase == VoyagePhase.Arrived ? HudKit.Good : HudKit.Danger;
            phaseChip.color = chip;
            phaseText.text = s.Phase.ToString().ToUpperInvariant();
            routeText.text = VoyageSimulation.RouteName(s.Route);
            float hull = Mathf.Clamp01(s.Health / 100f);
            hullFill.rectTransform.anchorMax = new Vector2(hull, 1);
            hullFill.color = hull > .6f ? HudKit.Good : hull > .3f ? HudKit.Warning : HudKit.Danger;
            hullValue.text = s.HullShield > 0 ? $"{s.Health:0}% +{s.HullShield:0}" : $"{s.Health:0}%";
            statusLine.text = s.Phase == VoyagePhase.Sailing
                ? $"{s.Speed:0.0} m/s   •   {dist:0} m to port   •   {s.Heading:0}°"
                : s.Phase == VoyagePhase.Boarding
                    ? $"{s.Boarded} passengers   •   tickets {s.CheckedTickets}/{s.Boarded}"
                    : $"{s.Boarded} passengers   •   {s.Served} orders served";
        }

        private void TickResources(float dt, in VoyageState s)
        {
            coins.Set(s.Wallet); reputation.Set(s.Reputation); food.Set(s.FoodStock); drink.Set(s.DrinkStock);
        }

        private void TickBanner()
        {
            bool show = hud.HasMigrationError || hud.ConnectionLost;
            HudKit.SetActive(bannerRoot, show);
            if (!show) return;
            HudKit.SetText(bannerText, hud.Status);
            bannerRoot.GetComponent<Image>().color = hud.HasMigrationError ? new Color(.5f, .1f, .1f, .94f) : new Color(.55f, .34f, .05f, .94f);
        }

        private void TickSession()
        {
            bool online = hud.HasOnlineCode;
            HudKit.SetActive(sessionRoot, online);
            if (online) HudKit.SetText(sessionText, "CODE " + hud.JoinCode + (hud.IsOnlineHost ? "  •  HOST" : string.Empty));
        }

        private void TickObjective(PrototypeSession session, in VoyageState s, int local)
        {
            bool onShore = local >= 0 && session.Crew[local].OnShore;
            bool atHelm = local >= 0 && session.Crew[local].AtHelm;
            float dist = Vector2.Distance(s.Position, VoyageSimulation.Destination);
            int sig = HashCode((int)s.Phase, s.Boarded, onShore ? 1 : 0, atHelm ? 1 : 0, Mathf.RoundToInt(dist / 10), s.DockJobActive ? 1 : 0);
            if (sig != objectiveSignature)
            {
                objectiveSignature = sig;
                string text;
                if (onShore) text = s.DockJobActive ? "Finish the town job, then board the ship" : "Shop at the stalls, then return to the gangway";
                else switch (s.Phase)
                {
                    case VoyagePhase.Boarding:
                        text = s.Boarded == 0 ? "Talk to the purser to board passengers" : atHelm ? "Everyone aboard? Depart from the helm" : "Take the helm when the crew is ready";
                        break;
                    case VoyagePhase.Sailing:
                        text = dist < VoyageSimulation.DockRadius * 3 ? "Almost there: slow down and dock" : "Keep passengers happy on the way to port";
                        break;
                    default:
                        text = "Talk to the purser to start the next voyage";
                        break;
                }
                objective.text = text;
            }
        }

        private void TickAlerts(PrototypeSession session, in VoyageState s, int local, bool gamepad)
        {
            var e = session.Encounter.Value;
            var ev = session.ActiveEvent.Value;
            bool voyageOpen = s.Phase == VoyagePhase.Boarding || s.Phase == VoyagePhase.Sailing;
            bool atCrane = local >= 0 && session.Crew[local].AtCrane;
            bool craneInTarget = atCrane && Vector2.Distance(VoyageSimulation.CraneLandingPoint(s), VoyageSimulation.CraneBarge) <= VoyageSimulation.CraneLandingRadius;
            var hz = session.Hazards.Value;
            int shoalIndex = NearestShoal(s, hz, out float shoalGap);
            int sig = HashCode(s.Leak, s.RepairSteps, s.ReadyRepairKits, e.PowerOut ? 1 : 0, e.PowerRepairSteps, (int)e.Pirate);
            sig = sig * 31 + HashCode(hz.Wave == WavePhase.Warning ? 1 : 0, Mathf.CeilToInt(hz.WaveRemaining), hz.Wave == WavePhase.Warning ? Mathf.RoundToInt(HazardLogic.TurnToFace(s.Heading, hz.WaveBearing) / 5f) : 0, shoalIndex, Mathf.RoundToInt(shoalGap / 3f));
            sig = sig * 31 + HashCode(Mathf.CeilToInt(e.WarningRemaining), Mathf.RoundToInt(e.PirateHealth), Mathf.RoundToInt(e.EnemyShotRemaining * 2), e.Ammo, e.MagazineAmmo, e.ReadyAmmo);
            sig = sig * 31 + HashCode(Mathf.RoundToInt(e.ReloadRemaining * 2), e.CannonLoaded ? 1 : 0, (int)ev.Phase, (int)ev.Kind, ev.Progress, ev.TargetPassenger, Mathf.CeilToInt(ev.Remaining));
            sig = sig * 31 + HashCode(Mathf.RoundToInt(ev.CoolingTime * 2), atCrane ? 1 : 0, craneInTarget ? 1 : 0, s.CraneHolding ? 1 : 0, s.DockJobProgress, gamepad ? 1 : 0, voyageOpen ? 1 : 0);
            if (sig == alertSignature) return;
            alertSignature = sig;

            alerts.Begin();
            if (voyageOpen && s.Phase == VoyagePhase.Sailing) AddHazards(s, hz, shoalIndex, shoalGap);
            if (atCrane)
            {
                alerts.Add(craneInTarget ? HudKit.Good : HudKit.Info, $"CRANE   {s.DockJobProgress}/{VoyageSimulation.DockJobGoal} landed",
                    s.CraneHolding ? craneInTarget ? "Inside the ring. Land it now!" : "Move the hook over the green barge ring." : "Lift a crate from the load pad.");
            }
            if (voyageOpen && s.Leak != 0)
                alerts.Add(HudKit.Danger, "HULL LEAK", $"Patch at the repair bench ({s.RepairSteps}/4), or use a quick kit ({s.ReadyRepairKits} ready).");
            if (voyageOpen && e.PowerOut)
                alerts.Add(HudKit.Warning, "POWER OUT", $"Reset the breakers {e.PowerRepairSteps}/3. Food and drinks are blocked.");
            if (voyageOpen && e.Pirate == PiratePhase.Warning)
                alerts.Add(HudKit.Danger, "PIRATES INBOUND", $"Contact in {e.WarningRemaining:0}s. Man the cannon and load it!");
            else if (voyageOpen && e.Pirate == PiratePhase.Attacking)
                alerts.Add(HudKit.Danger, "PIRATE ATTACK", $"Enemy ship {e.PirateHealth:0}%. Next shot in {e.EnemyShotRemaining:0.0}s.");
            if (voyageOpen && (e.Pirate == PiratePhase.Warning || e.Pirate == PiratePhase.Attacking))
            {
                string rack = e.ReloadRemaining > 0 ? $"{e.ReadyAmmo}+1" : e.ReadyAmmo.ToString();
                string state = e.ReloadRemaining > 0 ? $"reloading {e.ReloadRemaining:0.0}s" : e.CannonLoaded ? "loaded" : "empty";
                alerts.Add(HudKit.Info, "CANNON", $"{state}   •   rack {rack}/{VoyageSimulation.CannonBufferCapacity}   •   magazine {e.MagazineAmmo}   •   ammo {e.Ammo}/12");
            }
            if (voyageOpen && ev.Phase == VoyageEventPhase.Active) AddEvent(ev, gamepad);
            alerts.End();
        }

        /// <summary>The shoal the captain should watch for, or -1. Only meaningful while sailing.</summary>
        private static int NearestShoal(in VoyageState s, in HazardState hz, out float gap)
        {
            gap = 0;
            if (s.Phase != VoyagePhase.Sailing) return -1;
            int count = ShoalSet.For(hz.Seed, s.Route);
            return HazardLogic.NearestAhead(s.Position, s.Heading, ShoalSet.Shoals, count, out gap);
        }

        private void AddHazards(in VoyageState s, in HazardState hz, int shoalIndex, float shoalGap)
        {
            if (hz.Wave == WavePhase.Warning)
            {
                int turn = Mathf.RoundToInt(HazardLogic.TurnToFace(s.Heading, hz.WaveBearing));
                string how = Mathf.Abs(turn) <= HazardLogic.WaveSafeAngle ? "Hold this heading!" : turn > 0 ? $"Turn right {turn}°." : $"Turn left {-turn}°.";
                alerts.Add(HudKit.Danger, $"ROGUE WAVE   {Mathf.CeilToInt(hz.WaveRemaining)}s", $"Coming from the {HazardLogic.Compass(hz.WaveBearing)}. Point the bow at it. {how}");
            }
            if (shoalIndex >= 0)
                alerts.Add(HudKit.Warning, $"SHOAL AHEAD   {shoalGap:0} m", "Steer around the rocks. A slow ship takes less damage.");
        }

        private void AddEvent(in VoyageEventState e, bool gamepad)
        {
            string timer = $"{e.Remaining:0}s left";
            string target = $"passenger {e.TargetPassenger + 1}";
            string check = HudControls.Label(HudControl.Check, gamepad), carry = HudControls.Label(HudControl.Carry, gamepad);
            string title, body;
            switch (e.Kind)
            {
                case VoyageEventKind.ContrabandInspection: title = "CONTRABAND"; body = $"Someone clutches a suitcase. Check their ticket [{check}]. {timer}"; break;
                case VoyageEventKind.GalleyRush: title = "GALLEY RUSH"; body = $"Serve orders {e.Progress}/{e.Goal}. {timer}"; break;
                case VoyageEventKind.LooseCargo: title = "LOOSE CARGO"; body = $"Fix the repair panel {e.Progress}/{e.Goal}. {timer}"; break;
                case VoyageEventKind.VipService: title = "VIP SERVICE"; body = $"Serve {target}'s order. {timer}"; break;
                case VoyageEventKind.TicketAudit: title = "TICKET AUDIT"; body = $"Check different tickets [{check}] {e.Progress}/{e.Goal}. {timer}"; break;
                case VoyageEventKind.FuseOverload: title = "FUSE OVERLOAD"; body = $"Reset the power breakers {e.Progress}/{e.Goal}. {timer}"; break;
                case VoyageEventKind.EngineOverheat: title = "ENGINE HOT"; body = $"Stay under 1 m/s for 8s ({e.CoolingTime:0.0}/8) or vent twice at repairs. {timer}"; break;
                case VoyageEventKind.SeatShuffle: title = "SEAT SHUFFLE"; body = $"Carry {target} 1.5 m from their seat [{carry}], then put them down. {timer}"; break;
                default: title = "SHIP EVENT"; body = "Something is happening on deck."; break;
            }
            alerts.Add(HudKit.Info, title + "   +" + e.Reward, body);
        }

        private void TickContextHints(bool visible, in InteractionPrompt prompt, bool gamepad)
        {
            bool show = visible && !prompt.Targeted && !prompt.IsEmpty;
            HudKit.SetActive(contextRoot, show);
            if (!show) return;
            HudKit.SetText(contextTitle, string.IsNullOrEmpty(prompt.Title) ? string.Empty : prompt.Title.ToUpperInvariant());
            HudKit.SetActive(contextTitle.gameObject, !string.IsNullOrEmpty(prompt.Title));
            var primary = new PromptHint(prompt.Key, prompt.Action);
            contextHints.Set(gamepad, primary, prompt.Hint1, prompt.Hint2);
        }

        private void TickNotice(PrototypeSession session, float dt)
        {
            float remaining = session.NoticeUntil - Time.unscaledTime;
            bool show = remaining > 0 && !string.IsNullOrEmpty(session.Notice);
            var box = noticeGroup.gameObject;
            HudKit.SetActive(box, show);
            if (!show) return;
            if (!ReferenceEquals(shownNotice, session.Notice)) { shownNotice = session.Notice; noticeText.text = shownNotice; }
            noticeGroup.alpha = Mathf.Clamp01(remaining / 1.5f);
        }

        private void TickCarry(PrototypeSession session, int local)
        {
            if (local < 0) { HudKit.SetActive(carryRoot, false); return; }
            int passenger = session.LocalCarriedPassengerIndex;
            var item = session.Crew[local].Carrying;
            int units = session.Crew[local].ServiceUnits;
            int sig = passenger >= 0 ? 1000 + passenger : (int)item + units * 128;
            bool show = passenger >= 0 || item != ServiceItem.None;
            HudKit.SetActive(carryRoot, show);
            if (!show || sig == carrySignature) return;
            carrySignature = sig;
            carryText.text = passenger >= 0 ? $"Passenger {passenger + 1}" : ItemName(item) + (units > 1 ? $" x{units}" : string.Empty);
        }

        private void TickResume(bool show)
        {
            HudKit.SetActive(resumeRoot, show);
            if (show) HudKit.SetText(resumeText, Application.platform == RuntimePlatform.WebGLPlayer
                ? "Click the game to resume looking" : "Click or press Esc to resume looking");
        }

        public static string ItemName(ServiceItem item)
        {
            switch (item)
            {
                case ServiceItem.Food: return "Food";
                case ServiceItem.Drink: return "Drink";
                case ServiceItem.Cargo: return "Cargo crate";
                case ServiceItem.Ammo: return "Cannonball";
                case ServiceItem.LuggageA: return "Bag A";
                case ServiceItem.LuggageB: return "Bag B";
                case ServiceItem.LuggageC: return "Bag C";
                case ServiceItem.RepairKit: return "Patch kit";
                case ServiceItem.SalvageA: return "Salvage 1";
                case ServiceItem.SalvageB: return "Salvage 2";
                case ServiceItem.SalvageC: return "Salvage 3";
                case ServiceItem.CraneCrate: return "Fish crate";
                case ServiceItem.Extinguisher: return "Fire extinguisher";
                case ServiceItem.TemporaryPlug: return "Temporary leak plug";
                default: return string.Empty;
            }
        }

        private static int HashCode(int a, int b = 0, int c = 0, int d = 0, int e = 0, int f = 0, int g = 0, int h = 0)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + a; hash = hash * 31 + b; hash = hash * 31 + c; hash = hash * 31 + d;
                hash = hash * 31 + e; hash = hash * 31 + f; hash = hash * 31 + g; hash = hash * 31 + h;
                return hash;
            }
        }
    }
}
