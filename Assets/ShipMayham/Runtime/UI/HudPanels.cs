using UnityEngine;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>North-up route map at sea, town map ashore.</summary>
    public sealed class MiniMap
    {
        private struct Marker { public GameObject Root; public Image Dot; public Text Label; }
        private const float MapWidth = 190f, MapHeight = 270f, Scale = 1.1f;
        private readonly Marker[] markers = new Marker[20];
        private readonly Text title;
        private int used;

        public RectTransform Root { get; }

        public MiniMap(Transform parent)
        {
            var card = HudKit.Box(parent, "MiniMap", HudKit.Panel);
            Root = card.rectTransform;
            HudKit.Size(card, width: MapWidth * Scale + 20, height: MapHeight * Scale + 20);
            var inner = HudKit.Node(Root, "Inner");
            HudKit.Anchor(inner, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(MapWidth, MapHeight));
            inner.localScale = Vector3.one * Scale;
            title = HudKit.Label(inner, "Title", 16, HudKit.TextMuted, TextAnchor.UpperLeft, FontStyle.Bold);
            HudKit.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -4), new Vector2(MapWidth - 12, 22));
            for (int i = 0; i < markers.Length; i++)
            {
                var node = HudKit.Node(inner, "Marker" + i);
                HudKit.Anchor(node, new Vector2(0, 1), new Vector2(.5f, .5f), Vector2.zero, new Vector2(12, 12));
                var dot = HudKit.Dot(node, "Dot", Color.white, 12);
                HudKit.Stretch(dot.rectTransform);
                var label = HudKit.Label(node, "Label", 14, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                HudKit.Anchor(label.rectTransform, new Vector2(1, .5f), new Vector2(0, .5f), new Vector2(3, 0), new Vector2(80, 16));
                markers[i] = new Marker { Root = node.gameObject, Dot = dot, Label = label };
                node.gameObject.SetActive(false);
            }
        }

        public void Tick(PrototypeSession session, int local)
        {
            used = 0;
            var s = session.Voyage.Value;
            if (local >= 0 && session.Crew[local].OnShore)
            {
                HudKit.SetText(title, session.Harbor.PortIndex == 0 ? "BRASS QUAY" : "NORTH HAVEN");
                Town(VoyageSimulation.HarborPoint(HarborAction.BoardShip), "SHIP", Color.white);
                Town(VoyageSimulation.HarborPoint(HarborAction.BuyFood), "FOOD", HudKit.Food);
                Town(VoyageSimulation.HarborPoint(HarborAction.BuyDrink), "DRINK", HudKit.Drink);
                Town(VoyageSimulation.HarborPoint(HarborAction.MaintainHull), "HULL", HudKit.Good);
                Town(VoyageSimulation.HarborPoint(HarborAction.HarborMaster), "OFFICE", HudKit.Accent);
                Town(VoyageSimulation.HarborPoint(HarborAction.PaintShop), "PAINT", new Color(.9f, .5f, 1f));
                if (s.DockJobType == DockJobKind.CraneLoading)
                {
                    Town(VoyageSimulation.HarborPoint(HarborAction.FishMarket), "FISH", HudKit.Drink);
                    Town(VoyageSimulation.HarborPoint(HarborAction.CraneLoadingPad), "PAD", HudKit.Accent);
                    Town(VoyageSimulation.CraneBarge, "BARGE", HudKit.Good);
                }
                if (s.DockJobActive && s.DockJobType == DockJobKind.SalvageRun)
                    for (int i = 0; i < HarborTownLayout.SalvageCount; i++)
                        if ((s.DockJobDeliveredMask & (1 << i)) == 0) Town(HarborTownLayout.SalvagePoint(i), "S" + (i + 1), HudKit.Accent);
                Town(session.Crew[local].Position, null, Color.white, 15);
            }
            else
            {
                HudKit.SetText(title, "ROUTE");
                Place(60, 48, "DESTINATION", HudKit.Accent, false, 12);
                Place(60, MapHeight - 22, "DEPARTURE", HudKit.TextMuted, false, 12);
                Place(90 + s.Position.x * .75f, 235 - s.Position.y * .9f, null, Color.white, true, 15);
                var encounter = session.Encounter.Value;
                if (encounter.Pirate == PiratePhase.Warning || encounter.Pirate == PiratePhase.Attacking)
                {
                    Vector2 pirate = encounter.PiratePosition;
                    Place(Mathf.Clamp(90 + pirate.x * .75f, 10, MapWidth - 10), Mathf.Clamp(235 - pirate.y * .9f, 30, MapHeight - 12), "PIRATES", HudKit.Danger, true, 13);
                }
            }
            for (int i = used; i < markers.Length; i++) HudKit.SetActive(markers[i].Root, false);
        }

        private void Town(Vector2 point, string label, Color color, float size = 12)
        {
            float x = 8 + (point.x - HarborTownLayout.Bounds.xMin) / HarborTownLayout.Bounds.width * 133;
            float y = 40 + (HarborTownLayout.Bounds.yMax - point.y) / HarborTownLayout.Bounds.height * 205;
            Place(x, y, label, color, true, size);
        }

        private void Place(float x, float y, string label, Color color, bool showDot, float size)
        {
            if (used >= markers.Length) return;
            var marker = markers[used++];
            HudKit.SetActive(marker.Root, true);
            ((RectTransform)marker.Root.transform).anchoredPosition = new Vector2(x, -y);
            marker.Dot.enabled = showDot;
            marker.Dot.color = color;
            marker.Dot.rectTransform.sizeDelta = new Vector2(size, size);
            marker.Label.color = color;
            bool hasLabel = !string.IsNullOrEmpty(label);
            HudKit.SetActive(marker.Label.gameObject, hasLabel);
            if (hasLabel && marker.Label.text != label) marker.Label.text = label;
            // Labels without a dot (route ends) are centred on their anchor instead of hanging off it.
            var labelRect = marker.Label.rectTransform;
            labelRect.anchoredPosition = showDot ? new Vector2(3 + size * .35f, 0) : new Vector2(-40, 0);
        }
    }

    /// <summary>Route, job and paint choices at the harbor. Only the sections that are usable right now are shown.</summary>
    public sealed class HarborPanel
    {
        private readonly PrototypeSession session;
        private readonly GameObject root;
        private readonly Text header, status, jobInfo, tip, mouseHint, paintTitle;
        private readonly GameObject routeSection, jobSection, paintSection, cancelRoot, jobButtonsRoot, tipRoot;
        private readonly Button[] routeButtons = new Button[3];
        private readonly Button[] paintButtons = new Button[3];
        private readonly Button[] jobButtons = new Button[4];
        private readonly Button cancelButton;
        private int signature = int.MinValue;

        public RectTransform Root { get; }

        public HarborPanel(PrototypeSession session, Transform parent)
        {
            this.session = session;
            var card = HudKit.Box(parent, "HarborPanel", HudKit.Panel);
            Root = card.rectTransform;
            root = card.gameObject;
            HudKit.Size(card, width: 380);
            HudKit.Column(Root, 10, new RectOffset(22, 22, 18, 20), false);

            header = HudKit.Label(Root, "Header", 24, HudKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            HudKit.Size(header, height: 30);
            status = HudKit.Label(Root, "Status", 19, HudKit.TextMuted);
            status.horizontalOverflow = HorizontalWrapMode.Wrap;

            routeSection = Section(Root, "Route", "CHOOSE A ROUTE");
            var routeRow = ButtonRow(routeSection.transform, 3, 62, routeButtons);
            routeButtons[0].onClick.AddListener(() => session.SelectRouteRpc(RouteProfile.CoastalFerry));
            routeButtons[1].onClick.AddListener(() => session.SelectRouteRpc(RouteProfile.MarketRun));
            routeButtons[2].onClick.AddListener(() => session.SelectRouteRpc(RouteProfile.StormPassage));
            _ = routeRow;

            jobSection = Section(Root, "Job", "DOCK JOB");
            jobInfo = HudKit.Label(jobSection.transform, "Info", 19, HudKit.Ink);
            jobInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            jobButtonsRoot = HudKit.Node(jobSection.transform, "Buttons").gameObject;
            var grid = jobButtonsRoot.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(160, 52); grid.spacing = new Vector2(8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
            string[] jobLabels = { "CARGO RELAY", "BAG SORT", "SALVAGE", "CRANE LOADING" };
            DockJobKind[] jobKinds = { DockJobKind.CargoRelay, DockJobKind.LuggageSort, DockJobKind.SalvageRun, DockJobKind.CraneLoading };
            for (int i = 0; i < 4; i++)
            {
                var kind = jobKinds[i];
                jobButtons[i] = HudKit.MakeButton(jobButtonsRoot.transform, "Job" + i, jobLabels[i], new Color(.25f, .5f, .7f), 18);
                jobButtons[i].onClick.AddListener(() => session.SelectDockJobRpc(kind));
            }
            cancelButton = HudKit.MakeButton(jobSection.transform, "Cancel", "CANCEL JOB", new Color(.75f, .3f, .3f), 20);
            cancelRoot = cancelButton.gameObject;
            HudKit.Size(cancelButton, height: 46);
            cancelButton.onClick.AddListener(() => session.SelectDockJobRpc(session.Voyage.Value.DockJobType));

            paintSection = Section(Root, "Paint", "SHIP PAINT");
            paintTitle = paintSection.GetComponentInChildren<Text>();
            ButtonRow(paintSection.transform, 3, 52, paintButtons);
            paintButtons[0].onClick.AddListener(() => session.SelectLiveryRpc(ShipLivery.Classic));
            paintButtons[1].onClick.AddListener(() => session.SelectLiveryRpc(ShipLivery.Seafoam));
            paintButtons[2].onClick.AddListener(() => session.SelectLiveryRpc(ShipLivery.Sunset));

            tip = HudKit.Label(Root, "Tip", 19, HudKit.TextMuted);
            tip.horizontalOverflow = HorizontalWrapMode.Wrap;
            tipRoot = tip.gameObject;
            mouseHint = HudKit.Label(Root, "MouseHint", 17, HudKit.Info, TextAnchor.MiddleLeft, FontStyle.Italic);
            mouseHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            card.gameObject.SetActive(false);
        }

        private static GameObject Section(Transform parent, string name, string caption)
        {
            var node = HudKit.Node(parent, name);
            HudKit.Column(node, 8, new RectOffset(0, 0, 4, 0), false);
            var label = HudKit.Label(node, "Caption", 16, HudKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            label.text = caption;
            HudKit.Size(label, height: 22);
            return node.gameObject;
        }

        private static RectTransform ButtonRow(Transform parent, int count, float height, Button[] buttons)
        {
            var row = HudKit.Node(parent, "Buttons");
            HudKit.Size(row, height: height);
            var layout = HudKit.Row(row, 8);
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            string[] labels = { "A", "B", "C" };
            for (int i = 0; i < count; i++) buttons[i] = HudKit.MakeButton(row, "Button" + i, labels[i], new Color(.25f, .5f, .7f), 18);
            return row;
        }

        public void Tick(int local, bool cursorLocked)
        {
            var s = session.Voyage.Value;
            bool visible = local >= 0 && session.Harbor != null && session.Harbor.IsOpen && session.Crew[local].OnShore;
            HudKit.SetActive(root, visible);
            if (!visible) return;

            var crew = session.Crew[local];
            bool ashore = crew.OnShore;
            bool atOffice = ashore && Vector2.Distance(crew.Position, VoyageSimulation.HarborPoint(HarborAction.HarborMaster)) < 4;
            bool atPaint = ashore && Vector2.Distance(crew.Position, VoyageSimulation.HarborPoint(HarborAction.PaintShop)) < 4;
            int crewAshore = 0;
            foreach (var member in session.Crew) if (member.Active && member.OnShore) crewAshore++;

            int sig = 17;
            sig = sig * 31 + (int)s.Phase; sig = sig * 31 + s.Boarded; sig = sig * 31 + s.Reputation; sig = sig * 31 + (int)s.Route;
            sig = sig * 31 + (int)s.HullShield; sig = sig * 31 + (s.DockJobActive ? 1 + (int)s.DockJobType : 0); sig = sig * 31 + s.DockJobProgress;
            sig = sig * 31 + s.DockJobDeliveredMask; sig = sig * 31 + s.DockJobsCompleted; sig = sig * 31 + (int)s.Livery;
            sig = sig * 31 + s.CraneStock; sig = sig * 31 + s.CraneReady; sig = sig * 31 + (s.CraneHolding ? 1 : 0);
            sig = sig * 31 + (ashore ? 1 : 0) + (atOffice ? 2 : 0) + (atPaint ? 4 : 0) + (cursorLocked ? 8 : 0);
            sig = sig * 31 + crewAshore + session.Harbor.PortIndex * 7 + (int)s.LastRescueCosts;
            if (sig == signature) return;
            signature = sig;

            HudKit.SetText(header, session.Harbor.PortIndex == 0 ? "BRASS QUAY" : "NORTH HAVEN");
            HudKit.SetText(status, $"Route: {VoyageSimulation.RouteName(s.Route)}\nCrew ashore: {crewAshore}   •   Hull shield {s.HullShield:0}/20");

            bool canChoose = s.Phase == VoyagePhase.Boarding && s.Boarded == 0 && atOffice;
            HudKit.SetActive(routeSection, atOffice);
            if (atOffice)
            {
                int marketRep = VoyageSimulation.RouteReputation(RouteProfile.MarketRun);
                int stormRep = VoyageSimulation.RouteReputation(RouteProfile.StormPassage);
                HudKit.SetLabel(routeButtons[0], "COAST");
                HudKit.SetLabel(routeButtons[1], s.Reputation >= marketRep ? "MARKET" : $"MARKET\nREP {marketRep}");
                HudKit.SetLabel(routeButtons[2], s.Reputation >= stormRep ? "STORM" : $"STORM\nREP {stormRep}");
                routeButtons[0].interactable = canChoose;
                routeButtons[1].interactable = canChoose && s.Reputation >= marketRep;
                routeButtons[2].interactable = canChoose && s.Reputation >= stormRep;
            }

            bool jobVisible = atOffice || (s.DockJobActive && ashore);
            HudKit.SetActive(jobSection, jobVisible);
            if (jobVisible)
            {
                HudKit.SetText(jobInfo, JobText(s));
                HudKit.SetActive(jobButtonsRoot, !s.DockJobActive);
                HudKit.SetActive(cancelRoot, s.DockJobActive);
                foreach (var button in jobButtons) button.interactable = atOffice;
                HudKit.SetLabel(jobButtons[3], "CRANE LOADING\n+45 coins");
            }

            HudKit.SetActive(paintSection, atPaint);
            if (atPaint)
            {
                HudKit.SetLabel(paintButtons[0], "CLASSIC");
                HudKit.SetLabel(paintButtons[1], s.Reputation >= 2 ? "SEAFOAM" : "SEAFOAM\nREP 2");
                HudKit.SetLabel(paintButtons[2], s.Reputation >= 5 ? "SUNSET" : "SUNSET\nREP 5");
                paintButtons[1].interactable = s.Reputation >= 2;
                paintButtons[2].interactable = s.Reputation >= 5;
                paintTitle.text = $"SHIP PAINT: {s.Livery.ToString().ToUpperInvariant()}";
            }

            HudKit.SetActive(tipRoot, !jobVisible && !atPaint);
            tip.text = ashore ? "Visit the port office to pick a route or a town job." : "Step ashore from the gangway to shop and take jobs.";
            bool needsMouse = (atOffice || atPaint || (s.DockJobActive && ashore)) && cursorLocked;
            HudKit.SetActive(mouseHint.gameObject, needsMouse);
            mouseHint.text = Application.platform == RuntimePlatform.WebGLPlayer ? "Press Esc to use the mouse" : "Press Esc to use the mouse, click to resume";
        }

        private static string JobText(in VoyageState s)
        {
            if (!s.DockJobActive)
                return s.DockJobsCompleted > 0 ? $"Jobs completed this trip: {s.DockJobsCompleted}. Pick another:" : "Pick a job for bonus coins:";
            string progress = $"{VoyageSimulation.DockJobName(s.DockJobType)}  {s.DockJobProgress}/{VoyageSimulation.DockJobGoal}";
            switch (s.DockJobType)
            {
                case DockJobKind.CraneLoading:
                    return $"{progress}\nFish market: {s.CraneStock} crates. Load pad {s.CraneReady}/{VoyageSimulation.CranePadCapacity}.\nCarry to the pad, then land them on the barge.";
                case DockJobKind.LuggageSort:
                    return $"{progress}\nTag A → {VoyageSimulation.LuggageDesk(s, ServiceItem.LuggageA)}\nTag B → {VoyageSimulation.LuggageDesk(s, ServiceItem.LuggageB)}\nTag C → {VoyageSimulation.LuggageDesk(s, ServiceItem.LuggageC)}";
                case DockJobKind.SalvageRun:
                    return $"{progress}\nFind S1 to S3 on the town map and bring them to the port office. +36 coins";
                default:
                    return $"{progress}\nCarry crates from the food stall to the hull dock.";
            }
        }
    }

    /// <summary>End-of-voyage summary that stays up until the next voyage is prepared.</summary>
    public sealed class ReportPanel
    {
        private readonly GameObject root;
        private readonly Text title, footer;
        private readonly StatRows rows;
        private int signature = int.MinValue;

        public RectTransform Root { get; }

        public ReportPanel(Transform parent)
        {
            var card = HudKit.Box(parent, "VoyageReport", HudKit.PanelStrong);
            Root = card.rectTransform;
            root = card.gameObject;
            HudKit.Size(card, width: 420);
            HudKit.Column(Root, 4, new RectOffset(26, 26, 20, 22), false);
            title = HudKit.Label(Root, "Title", 30, HudKit.Good, TextAnchor.MiddleLeft, FontStyle.Bold);
            HudKit.Size(title, height: 44);
            rows = new StatRows(Root, 12);
            footer = HudKit.Label(Root, "Footer", 19, HudKit.TextMuted);
            footer.horizontalOverflow = HorizontalWrapMode.Wrap;
            card.gameObject.SetActive(false);
        }

        public void Tick(in VoyageState s)
        {
            bool visible = s.Phase == VoyagePhase.Arrived || s.Phase == VoyagePhase.Failed;
            HudKit.SetActive(root, visible);
            if (!visible) return;
            int sig = (int)s.Phase * 31 + s.LastNet;
            sig = sig * 31 + s.LastTickets + s.LastServices * 3 + s.LastTips * 5 + s.LastBounty * 7 + s.LastEventBonus * 11 + s.LastRouteBonus * 13 + s.LastCosts * 17;
            sig = sig * 31 + s.LastUnpaid + s.LastMissing * 3 + s.LastRescueCosts * 5 + s.LastReputation * 7;
            if (sig == signature) return;
            signature = sig;

            bool failed = s.Phase == VoyagePhase.Failed;
            title.text = failed ? "VOYAGE FAILED" : "VOYAGE COMPLETE";
            title.color = failed ? HudKit.Danger : HudKit.Good;
            rows.Begin();
            rows.Add("Tickets", Coins(s.LastTickets));
            if (s.LastServices != 0) rows.Add("Orders served", Coins(s.LastServices));
            if (s.LastTips != 0) rows.Add("Tips", Coins(s.LastTips));
            if (s.LastBounty != 0) rows.Add("Pirate bounty", Coins(s.LastBounty));
            if (s.LastEventBonus != 0) rows.Add("Ship events", Coins(s.LastEventBonus));
            if (s.LastRouteBonus != 0) rows.Add("Route bonus", Coins(s.LastRouteBonus));
            if (s.LastCosts != 0) rows.Add("Costs", "-" + s.LastCosts, HudKit.Danger);
            if (s.LastRescueCosts != 0) rows.Add("Rescue costs", "-" + s.LastRescueCosts, HudKit.Danger);
            rows.Add("NET", (s.LastNet >= 0 ? "+" : string.Empty) + s.LastNet, s.LastNet >= 0 ? HudKit.Coin : HudKit.Danger);
            if (s.LastReputation != 0) rows.Add("Reputation", "+" + s.LastReputation, HudKit.Accent);
            if (s.LastUnpaid != 0) rows.Add("Unpaid fares", s.LastUnpaid.ToString(), HudKit.Warning);
            if (s.LastMissing != 0) rows.Add("Passengers lost", s.LastMissing.ToString(), HudKit.Danger);
            rows.End();
            footer.text = "Visit the manifest desk to prepare the next voyage.";
        }

        private static string Coins(int amount) => (amount >= 0 ? "+" : string.Empty) + amount;
    }

    /// <summary>F1 controls reference.</summary>
    public sealed class HelpOverlay
    {
        private readonly GameObject root;
        private readonly Text left, right;
        private bool builtForGamepad, built;

        public HelpOverlay(Transform parent)
        {
            var card = HudKit.Box(parent, "Help", HudKit.PanelStrong, true, true);
            root = card.gameObject;
            HudKit.Anchor(card.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1000, 0));
            HudKit.Column(card.rectTransform, 16, new RectOffset(44, 44, 32, 36));
            var title = HudKit.Label(card.transform, "Title", 34, HudKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.text = "CONTROLS";
            HudKit.Size(title, height: 46);
            var columns = HudKit.Node(card.transform, "Columns");
            HudKit.Row(columns, 48, TextAnchor.UpperLeft).childForceExpandHeight = false;
            left = HudKit.Label(columns, "Left", 22, HudKit.Ink, TextAnchor.UpperLeft, FontStyle.Normal, false);
            right = HudKit.Label(columns, "Right", 22, HudKit.Ink, TextAnchor.UpperLeft, FontStyle.Normal, false);
            foreach (var text in new[] { left, right }) { text.horizontalOverflow = HorizontalWrapMode.Wrap; text.lineSpacing = 1.25f; HudKit.Size(text, flexibleWidth: 1); }
            var legend = HudKit.Label(card.transform, "Legend", 20, HudKit.TextMuted);
            legend.text = "Orange = food   •   Blue = drink   •   Green = repair and gangway.   All crew must be aboard to depart.";
            legend.horizontalOverflow = HorizontalWrapMode.Wrap;
            var close = HudKit.Label(card.transform, "Close", 19, HudKit.TextMuted, TextAnchor.MiddleRight);
            close.text = "F1 to close";
            card.gameObject.SetActive(false);
        }

        public bool Visible => root.activeSelf;

        public void Toggle() => root.SetActive(!root.activeSelf);

        public void Hide() => HudKit.SetActive(root, false);

        public void Tick(bool gamepad)
        {
            if (!root.activeSelf || (built && builtForGamepad == gamepad)) return;
            built = true; builtForGamepad = gamepad;
            string K(HudControl c) => $"<b><color=#FFC847>{HudControls.Label(c, gamepad)}</color></b>";
            string move = gamepad ? "<b><color=#FFC847>LS</color></b>" : "<b><color=#FFC847>WASD</color></b>";
            left.text =
                "<b>ON DECK</b>\n" +
                $"{move}  Move      {K(HudControl.Look)}  Look\n" +
                $"{K(HudControl.Jump)}  Jump\n" +
                $"{K(HudControl.Interact)}  Use stations, serve and buy\n\n" +
                "<b>PASSENGERS</b>\n" +
                $"{K(HudControl.Check)}  Check ticket / collect fare\n" +
                $"{K(HudControl.Carry)}  Lift or put down\n" +
                $"{K(HudControl.Throw)}  Throw (hurts happiness)";
            right.text =
                "<b>HELM</b>\n" +
                $"{move}  Steer and throttle\n" +
                $"{K(HudControl.Jump)}  Depart or dock\n" +
                $"{K(HudControl.Camera)}  Switch camera\n\n" +
                "<b>CANNON</b>\n" +
                $"{K(HudControl.Look)}  Aim      {K(HudControl.Fire)}  Fire\n" +
                $"{K(HudControl.Reload)}  Reload\n" +
                $"{K(HudControl.Release)}  Leave station\n\n" +
                "<b>CURSOR</b>\n" +
                (gamepad ? "<b><color=#FFC847>START</color></b>  Release or capture cursor" : "<b><color=#FFC847>ESC</color></b>  Release cursor for menus");
        }
    }
}
