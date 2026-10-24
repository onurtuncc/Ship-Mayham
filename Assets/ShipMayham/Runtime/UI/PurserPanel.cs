using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>
    /// The purser's desk: boarding passengers or starting the next voyage, and buying ship upgrades. Opens when the local
    /// crew member talks to the Manifest NPC and releases the cursor until it is closed.
    /// </summary>
    public sealed class PurserPanel
    {
        private struct UpgradeRow { public StationKind Kind; public Text Name, Level; public Button Buy; public Text Cost; }

        private static readonly (StationKind kind, string name, int max)[] Upgrades =
        {
            (StationKind.EngineUpgrade, "ENGINE", 2),
            (StationKind.CapacityUpgrade, "SEATS", 2),
            (StationKind.ComfortUpgrade, "COMFORT", 1),
        };

        private readonly PrototypeHud hud;
        private readonly GameObject root;
        private readonly Text coinsText, passengerText;
        private readonly Button boardButton;
        private readonly GameObject upgradeSection, preparationSection;
        private readonly Text preparationStatus;
        private readonly Button[] preparations = new Button[6];
        private bool showPreparation;
        private readonly UpgradeRow[] rows = new UpgradeRow[3];
        private bool wasOpen;
        private int signature = int.MinValue;

        public PurserPanel(PrototypeHud hud, Transform parent)
        {
            this.hud = hud;
            var dim = HudKit.Box(parent, "PurserPanel", new Color(.02f, .05f, .09f, .5f), false, true);
            root = dim.gameObject;
            HudKit.Stretch(dim.rectTransform);

            var card = HudKit.Box(dim.transform, "Card", HudKit.PanelStrong, true, true);
            HudKit.Anchor(card.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(720, 0));
            HudKit.Column(card.rectTransform, 12, new RectOffset(44, 44, 34, 34));

            var header = HudKit.Node(card.transform, "Header");
            HudKit.Size(header, height: 56);
            HudKit.Row(header, 16);
            var title = HudKit.Label(header, "Title", 40, HudKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.text = "PURSER PEARL";
            HudKit.Size(title, flexibleWidth: 1);
            coinsText = HudKit.Label(header, "Coins", 30, HudKit.Coin, TextAnchor.MiddleRight, FontStyle.Bold);
            HudKit.Size(coinsText, width: 200);

            Caption(card.transform, "PASSENGERS");
            passengerText = HudKit.Label(card.transform, "Passengers", 21, HudKit.TextMuted);
            passengerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudKit.Size(passengerText, height: 30);
            boardButton = HudKit.MakeButton(card.transform, "Board", string.Empty, HudKit.Accent, 28);
            boardButton.GetComponentInChildren<Text>().color = new Color(.1f, .08f, .02f);
            HudKit.Size(boardButton, height: 66);
            boardButton.onClick.AddListener(Board);

            Spacer(card.transform, 6);
            Caption(card.transform, "SHIP UPGRADES");
            var tabs = HudKit.Node(card.transform, "PreparationTabs"); HudKit.Row(tabs, 10); HudKit.Size(tabs, height: 38);
            var upgradesTab = HudKit.MakeButton(tabs, "UpgradesTab", "UPGRADES", new Color(.2f,.4f,.5f), 20);
            var prepTab = HudKit.MakeButton(tabs, "GearTab", "GEAR / PASSAGE", new Color(.3f,.35f,.5f), 20);
            HudKit.Size(upgradesTab, flexibleWidth: 1); HudKit.Size(prepTab, flexibleWidth: 1);
            upgradesTab.onClick.AddListener(() => { showPreparation = false; ApplyTabs(); });
            prepTab.onClick.AddListener(() => { showPreparation = true; ApplyTabs(); });
            var upgradesNode = HudKit.Node(card.transform, "UpgradeOptions"); HudKit.Column(upgradesNode, 10, new RectOffset());
            upgradeSection = upgradesNode.gameObject;
            for (int i = 0; i < rows.Length; i++) rows[i] = BuildUpgradeRow(upgradesNode, i);
            var prepNode = HudKit.Node(card.transform, "AdventureOptions"); HudKit.Column(prepNode, 8, new RectOffset()); preparationSection = prepNode.gameObject;
            preparationStatus = HudKit.Label(prepNode, "PreparationStatus", 18, HudKit.TextMuted); HudKit.Size(preparationStatus, height: 44);
            var labels = new[] { "SERVICE TRAY — 90", "WORKSHOP — 90", "DECORATIVE PENNANT — 60", "SAFE WESTERN PASSAGE — +12", "RISKY EASTERN SHORTCUT — +25", "DIRECT PRACTICE PASSAGE" };
            for (int i = 0; i < preparations.Length; i++)
            {
                int index = i;
                preparations[i] = HudKit.MakeButton(prepNode, "Preparation" + i, labels[i], new Color(.2f,.4f,.5f), 20);
                HudKit.Size(preparations[i], height: 40);
                preparations[i].onClick.AddListener(() => hud.Session.PrepareVoyageRpc((PreparationAction)index));
            }
            ApplyTabs();

            Spacer(card.transform, 4);
            var close = HudKit.MakeButton(card.transform, "Close", "CLOSE", new Color(1, 1, 1, .14f), 24);
            HudKit.Size(close, height: 54);
            close.onClick.AddListener(() => hud.View.ClosePurser());
            root.SetActive(false);
        }

        private static void Caption(Transform parent, string text)
        {
            var label = HudKit.Label(parent, "Caption", 17, HudKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            label.text = text;
            HudKit.Size(label, height: 24);
        }

        private void ApplyTabs()
        {
            upgradeSection.SetActive(!showPreparation); preparationSection.SetActive(showPreparation);
        }

        private static void Spacer(Transform parent, float height) => HudKit.Size(HudKit.Node(parent, "Spacer"), height: height);

        private UpgradeRow BuildUpgradeRow(Transform parent, int index)
        {
            var (kind, displayName, _) = Upgrades[index];
            var background = HudKit.Box(parent, "Upgrade" + displayName, new Color(1, 1, 1, .07f));
            HudKit.Size(background, height: 68);
            HudKit.Row(background.rectTransform, 14).padding = new RectOffset(20, 14, 8, 8);
            var name = HudKit.Label(background.transform, "Name", 26, HudKit.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            HudKit.Size(name, flexibleWidth: 1);
            var level = HudKit.Label(background.transform, "Level", 22, HudKit.TextMuted, TextAnchor.MiddleRight);
            HudKit.Size(level, width: 110);
            var buy = HudKit.MakeButton(background.transform, "Buy", string.Empty, new Color(.25f, .5f, .7f), 22);
            HudKit.Size(buy, width: 190);
            var cost = buy.GetComponentInChildren<Text>();
            buy.onClick.AddListener(() => hud.Session.BuyUpgradeRpc(kind));
            return new UpgradeRow { Kind = kind, Name = name, Level = level, Buy = buy, Cost = cost };
        }

        private void Board()
        {
            var session = hud.Session;
            for (int i = 0; i < session.Stations.Length; i++)
                if (session.Stations[i].Kind == StationKind.Manifest) { session.InteractRpc(i, false); return; }
        }

        public void Tick()
        {
            bool open = hud.View != null && hud.View.PurserOpen;
            HudKit.SetActive(root, open);
            if (!open) { wasOpen = false; return; }
            var s = hud.Session.Voyage.Value;
            int sig = System.HashCode.Combine((int)s.Phase, s.Boarded, s.Wallet, s.EngineLevel, s.CapacityLevel, s.ComfortLevel, s.DockJobActive ? 1 : 0, s.Voyage);
            sig = sig * 31 + s.OwnedEquipment * 7 + (int)s.Equipment + (s.OwnedPennant ? 1 : 0) + (int)hud.Session.Journey.Value.Course * 11;
            if (sig != signature || !wasOpen)
            {
                signature = sig;
                Apply(s);
            }
            if (!wasOpen)
            {
                wasOpen = true;
                // Gamepad users reach the buttons through UI navigation, so something must be selected.
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(boardButton.gameObject);
            }
        }

        private void Apply(in VoyageState s)
        {
            bool prepare = s.Phase == VoyagePhase.Arrived || s.Phase == VoyagePhase.Boarding && s.Boarded == 0;
            preparationStatus.text = $"Fitted: {s.Equipment} | Passage: {hud.Session.Journey.Value.Course}\nTray: 2 orders. Workshop: +2 kits / 3-kit bench.";
            HudKit.SetLabel(preparations[0], (s.OwnedEquipment & 2) != 0 ? "FIT OWNED SERVICE TRAY" : "BUY SERVICE TRAY — 90");
            HudKit.SetLabel(preparations[1], (s.OwnedEquipment & 4) != 0 ? "FIT OWNED WORKSHOP" : "BUY WORKSHOP — 90");
            HudKit.SetLabel(preparations[2], s.OwnedPennant ? "SHOW / HIDE OWNED PENNANT" : "BUY DECORATIVE PENNANT — 60");
            for (int i = 0; i < preparations.Length; i++) preparations[i].interactable = prepare && (i < 3 || s.Phase == VoyagePhase.Boarding);
            coinsText.text = s.Wallet + " coins";
            bool boarding = s.Phase == VoyagePhase.Boarding;
            bool settled = s.Phase == VoyagePhase.Arrived || s.Phase == VoyagePhase.Failed;
            if (settled)
            {
                passengerText.text = "The trip is over. Settle up and get ready for the next one.";
                HudKit.SetLabel(boardButton, "START NEW VOYAGE");
                boardButton.interactable = true;
            }
            else if (boarding && s.Boarded == 0)
            {
                passengerText.text = s.DockJobActive ? "Finish the dock job before boarding passengers." : "Passengers are waiting on the quay.";
                HudKit.SetLabel(boardButton, "BOARD PASSENGERS");
                boardButton.interactable = !s.DockJobActive;
            }
            else
            {
                passengerText.text = $"{s.Boarded} passengers are aboard. Take the helm to depart.";
                HudKit.SetLabel(boardButton, "PASSENGERS ABOARD");
                boardButton.interactable = false;
            }

            bool atPort = s.Phase != VoyagePhase.Sailing;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                int level = row.Kind == StationKind.EngineUpgrade ? s.EngineLevel : row.Kind == StationKind.CapacityUpgrade ? s.CapacityLevel : s.ComfortLevel;
                int max = Upgrades[i].max;
                row.Name.text = Upgrades[i].name;
                row.Level.text = $"LEVEL {level}/{max}";
                if (level >= max)
                {
                    row.Cost.text = "MAXED";
                    row.Buy.interactable = false;
                    continue;
                }
                int cost = VoyageSimulation.UpgradeCost(row.Kind, s);
                row.Cost.text = $"BUY  {cost}";
                row.Buy.interactable = atPort && s.Wallet >= cost;
            }
        }
    }
}
