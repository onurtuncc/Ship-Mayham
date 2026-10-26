using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>
    /// The harbor master's chart: where the ship may sail next, what each island asks of the captain and what the log says to do.
    /// Opens when a crew member talks to the harbor master; the cursor is free until it is closed.
    /// </summary>
    public sealed class MapPanel
    {
        private struct IslandRow { public Text Name, Detail, State; public Button Select; public Text SelectLabel; }

        private readonly PrototypeHud hud;
        private readonly GameObject root;
        private readonly Text logText, headerCoins;
        private readonly IslandRow[] rows = new IslandRow[Campaign.Count];
        private readonly Button cycleButton;
        private readonly GameObject cycleRoot;
        private bool wasOpen;
        private int signature = int.MinValue;

        public MapPanel(PrototypeHud hud, Transform parent)
        {
            this.hud = hud;
            var dim = HudKit.Box(parent, "MapPanel", new Color(.02f, .05f, .09f, .5f), false, true);
            root = dim.gameObject;
            HudKit.Stretch(dim.rectTransform);

            var card = HudKit.Box(dim.transform, "Card", HudKit.PanelStrong, true, true);
            HudKit.Anchor(card.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(860, 0));
            HudKit.Column(card.rectTransform, 10, new RectOffset(40, 40, 30, 30));

            var header = HudKit.Node(card.transform, "Header");
            HudKit.Size(header, height: 52);
            HudKit.Row(header, 16);
            var title = HudKit.Label(header, "Title", 36, HudKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.text = "HARBOR MASTER";
            HudKit.Size(title, flexibleWidth: 1);
            headerCoins = HudKit.Label(header, "Coins", 28, HudKit.Coin, TextAnchor.MiddleRight, FontStyle.Bold);
            HudKit.Size(headerCoins, width: 200);

            logText = HudKit.Label(card.transform, "Log", 21, HudKit.Ink);
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudKit.Size(logText, height: 58);

            for (int i = 0; i < Campaign.Count; i++) rows[i] = BuildRow(card.transform, i);

            cycleButton = HudKit.MakeButton(card.transform, "NewCycle", "NEW CYCLE: A HARDER BLACK PENNANT", new Color(.7f, .45f, .15f), 22);
            cycleRoot = cycleButton.gameObject;
            HudKit.Size(cycleButton, height: 50);
            cycleButton.onClick.AddListener(() => hud.Session.StartNewCycleRpc());

            var close = HudKit.MakeButton(card.transform, "Close", "CLOSE", new Color(1, 1, 1, .14f), 24);
            HudKit.Size(close, height: 52);
            close.onClick.AddListener(() => hud.View.CloseMap());
            root.SetActive(false);
        }

        private IslandRow BuildRow(Transform parent, int island)
        {
            var background = HudKit.Box(parent, "Island" + island, new Color(1, 1, 1, .07f));
            HudKit.Size(background, height: 72);
            HudKit.Row(background.rectTransform, 14).padding = new RectOffset(20, 14, 6, 6);
            var texts = HudKit.Node(background.transform, "Texts");
            HudKit.Column(texts, 0, new RectOffset());
            HudKit.Size(texts, flexibleWidth: 1);
            var name = HudKit.Label(texts, "Name", 24, HudKit.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            HudKit.Size(name, height: 30);
            var detail = HudKit.Label(texts, "Detail", 17, HudKit.TextMuted, TextAnchor.UpperLeft);
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudKit.Size(detail, height: 34);
            var state = HudKit.Label(background.transform, "State", 18, HudKit.TextMuted, TextAnchor.MiddleRight);
            HudKit.Size(state, width: 150);
            var select = HudKit.MakeButton(background.transform, "Select", "SET COURSE", new Color(.25f, .5f, .7f), 20);
            HudKit.Size(select, width: 170);
            select.onClick.AddListener(() => hud.Session.SelectDestinationRpc(island));
            return new IslandRow { Name = name, Detail = detail, State = state, Select = select, SelectLabel = select.GetComponentInChildren<Text>() };
        }

        public void Tick()
        {
            bool open = hud.View != null && hud.View.MapOpen;
            HudKit.SetActive(root, open);
            if (!open) { wasOpen = false; return; }
            var s = hud.Session.Voyage.Value;
            int sig = System.HashCode.Combine(s.Island, s.Target, s.TaskMask, s.Reputation, (int)s.Phase, s.Boarded, s.Victory ? 1 : 0, s.Wallet);
            sig = sig * 31 + s.VisitedMask * 7 + s.Cycle;
            if (sig != signature || !wasOpen)
            {
                signature = sig;
                Apply(s);
            }
            if (!wasOpen)
            {
                wasOpen = true;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cycleRoot.activeSelf ? cycleRoot : rows[s.Target].Select.gameObject);
            }
        }

        private void Apply(in VoyageState s)
        {
            headerCoins.text = s.Wallet + " coins";
            logText.text = "LOG: " + Campaign.Objective(s.TaskMask, s.Reputation, s.Victory);
            int here = VoyageSimulation.DockedIsland(s);
            bool canPlot = s.Phase == VoyagePhase.Boarding && s.Boarded == 0;
            Vector2 hereWorld = Campaign.World(here);
            for (int i = 0; i < Campaign.Count; i++)
            {
                var def = Campaign.Island(i);
                var row = rows[i];
                bool open = Campaign.Unlocked(i, s.TaskMask, s.Reputation, s.Victory), isHere = i == here, chosen = i == s.Target && !isHere;
                bool known = Campaign.Has(s.VisitedMask, i) || open;
                row.Name.text = known ? def.Name : "Unknown waters";
                string task = def.Task == IslandTask.None ? string.Empty : Campaign.Has(s.TaskMask, i) ? "  •  task done" : "  •  " + def.TaskText;
                row.Detail.text = !known ? "Not on any chart yet." : isHere ? def.Blurb : $"{def.Blurb}{task}";
                float distance = Vector2.Distance(hereWorld, def.World);
                row.State.text = isHere ? "YOU ARE HERE" : !open ? Campaign.LockReason(i, s.TaskMask, s.Reputation, s.Victory) :
                    $"{distance:0} m{(def.FareBonus > 0 ? $"  •  +{def.FareBonus} fare" : string.Empty)}";
                row.State.color = isHere ? HudKit.Accent : open ? HudKit.TextMuted : HudKit.Warning;
                row.State.fontSize = !open && !isHere ? 14 : 18;
                row.SelectLabel.text = isHere ? "HERE" : !open ? "LOCKED" : chosen ? "COURSE SET" : "SET COURSE";
                row.Select.interactable = canPlot && open && !isHere && !chosen;
            }
            HudKit.SetActive(cycleRoot, s.Victory);
            cycleButton.interactable = s.Victory && s.Boarded == 0;
        }
    }
}
