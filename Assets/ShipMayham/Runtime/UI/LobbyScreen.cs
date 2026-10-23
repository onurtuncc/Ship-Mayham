using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>
    /// Waiting screen of an online voyage: the join code, the four crew slots with their ready state, and the
    /// Ready (everyone else) or Start (host) button. The ship idles behind it; nothing in the voyage runs yet.
    /// </summary>
    public sealed class LobbyScreen
    {
        private struct SlotRow { public Text Name, Tag, State; public Image Chip, Background; }

        private readonly PrototypeHud hud;
        private readonly GameObject root;
        private readonly SlotRow[] rows = new SlotRow[VoyageSimulation.MaxCrew];
        private readonly Text codeText, hintText, copyText;
        private readonly Button primaryButton;
        private readonly Text primaryLabel;
        private float copiedUntil;
        private int signature = int.MinValue;

        public LobbyScreen(PrototypeHud hud, Transform canvas)
        {
            this.hud = hud;
            var screen = HudKit.Node(canvas, "LobbyScreen");
            root = screen.gameObject;
            HudKit.Stretch(screen);

            var card = HudKit.Box(screen, "Card", HudKit.PanelStrong, true, true);
            HudKit.Anchor(card.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(760, 0));
            HudKit.Column(card.rectTransform, 12, new RectOffset(44, 44, 36, 36));

            var title = HudKit.Label(card.transform, "Title", 48, HudKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.text = "LOBBY";
            HudKit.Size(title, height: 60);

            var codeRow = HudKit.Node(card.transform, "CodeRow");
            HudKit.Size(codeRow, height: 66);
            HudKit.Row(codeRow, 16, TextAnchor.MiddleCenter);
            var caption = HudKit.Label(codeRow, "Caption", 18, HudKit.TextMuted, TextAnchor.MiddleRight, FontStyle.Bold);
            caption.text = "VOYAGE CODE";
            codeText = HudKit.Label(codeRow, "Code", 46, HudKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            var copy = HudKit.MakeButton(codeRow, "Copy", "COPY", new Color(1, 1, 1, .16f), 20);
            HudKit.Size(copy, width: 120, height: 48);
            copyText = copy.GetComponentInChildren<Text>();
            copy.onClick.AddListener(CopyCode);

            Spacer(card.transform, 6);
            for (int i = 0; i < rows.Length; i++) rows[i] = BuildRow(card.transform, i);

            hintText = HudKit.Label(card.transform, "Hint", 22, HudKit.TextMuted, TextAnchor.MiddleCenter);
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudKit.Size(hintText, height: 40);

            var actions = HudKit.Node(card.transform, "Actions");
            HudKit.Size(actions, height: 70);
            var layout = HudKit.Row(actions, 14);
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            var leave = HudKit.MakeButton(actions, "Leave", "LEAVE", new Color(.8f, .3f, .3f, .85f), 24);
            HudKit.Size(leave, width: 200);
            leave.onClick.AddListener(hud.LeaveSession);
            primaryButton = HudKit.MakeButton(actions, "Primary", string.Empty, HudKit.Accent, 28);
            HudKit.Size(primaryButton, flexibleWidth: 1);
            primaryLabel = primaryButton.GetComponentInChildren<Text>();
            primaryLabel.color = new Color(.1f, .08f, .02f);
            primaryButton.onClick.AddListener(OnPrimary);
            screen.gameObject.SetActive(false);
        }

        private static SlotRow BuildRow(Transform parent, int index)
        {
            var background = HudKit.Box(parent, "Slot" + index, new Color(1, 1, 1, .07f));
            HudKit.Size(background, height: 64);
            HudKit.Row(background.rectTransform, 14).padding = new RectOffset(20, 20, 8, 8);
            var number = HudKit.Label(background.transform, "Number", 26, HudKit.TextMuted, TextAnchor.MiddleCenter, FontStyle.Bold);
            number.text = (index + 1).ToString();
            HudKit.Size(number, width: 34);
            var name = HudKit.Label(background.transform, "Name", 28, HudKit.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            HudKit.Size(name, flexibleWidth: 1);
            var tag = HudKit.Label(background.transform, "Tag", 17, HudKit.Accent, TextAnchor.MiddleRight, FontStyle.Bold);
            HudKit.Size(tag, width: 70);
            var chip = HudKit.Box(background.transform, "State", HudKit.PanelLight);
            HudKit.Size(chip, width: 170, height: 40);
            var state = HudKit.Label(chip.transform, "Text", 19, HudKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold, false);
            HudKit.Stretch(state.rectTransform);
            return new SlotRow { Name = name, Tag = tag, State = state, Chip = chip, Background = background };
        }

        private static void Spacer(Transform parent, float height) => HudKit.Size(HudKit.Node(parent, "Spacer"), height: height);

        public void SetVisible(bool visible) => HudKit.SetActive(root, visible);

        private void CopyCode()
        {
            try { GUIUtility.systemCopyBuffer = hud.JoinCode; copiedUntil = Time.unscaledTime + 1.5f; }
            catch (System.Exception exception) { Debug.LogWarning("Could not copy the join code: " + exception.Message); }
        }

        private void OnPrimary()
        {
            var session = hud.Session;
            if (session == null || !session.IsSpawned) return;
            if (session.IsServer) session.StartVoyageRpc();
            else
            {
                int local = session.LocalCrewIndex;
                if (local >= 0 && local < session.Profiles.Count) session.SetReadyRpc(!session.Profiles[local].Ready);
            }
        }

        public void Tick()
        {
            var session = hud.Session;
            int local = session.LocalCrewIndex;
            bool amHost = session.IsServer;
            int waiting = 0, present = 0, sig = amHost ? 1 : 0;
            for (int i = 0; i < rows.Length; i++)
            {
                bool active = i < session.Crew.Count && session.Crew[i].Active;
                bool isHost = active && session.Crew[i].ClientId == NetworkManager.ServerClientId;
                bool ready = active && i < session.Profiles.Count && session.Profiles[i].Ready;
                if (active) present++;
                if (LobbyRules.WaitingOnPlayer(active, isHost, ready)) waiting++;
                sig = sig * 31 + (active ? 1 : 0) + (isHost ? 2 : 0) + (ready ? 4 : 0) + (i == local ? 8 : 0);
                if (active && i < session.Profiles.Count) sig = sig * 31 + session.Profiles[i].Name.GetHashCode();
            }
            bool copied = Time.unscaledTime < copiedUntil;
            sig = sig * 31 + (copied ? 1 : 0) + waiting * 3 + hud.JoinCode.GetHashCode();
            if (sig == signature) return;
            signature = sig;

            HudKit.SetText(codeText, string.IsNullOrEmpty(hud.JoinCode) ? "—" : hud.JoinCode);
            HudKit.SetText(copyText, copied ? "COPIED" : "COPY");
            for (int i = 0; i < rows.Length; i++) ApplyRow(i, session, local);

            if (amHost)
            {
                primaryLabel.text = "START VOYAGE";
                primaryButton.interactable = waiting == 0;
                hintText.text = waiting == 0
                    ? (present > 1 ? "Everyone is ready." : "You are the only one here. Share the code, or start alone.")
                    : $"Waiting for {waiting} {(waiting == 1 ? "player" : "players")} to get ready.";
            }
            else
            {
                bool ready = local >= 0 && local < session.Profiles.Count && session.Profiles[local].Ready;
                primaryLabel.text = ready ? "NOT READY" : "READY";
                primaryButton.interactable = local >= 0;
                hintText.text = ready ? "Waiting for the host to start the voyage." : "Press Ready when you are set.";
            }
        }

        private void ApplyRow(int i, PrototypeSession session, int local)
        {
            var row = rows[i];
            bool active = i < session.Crew.Count && session.Crew[i].Active;
            if (!active)
            {
                row.Name.text = "Waiting for a player...";
                row.Name.color = new Color(1, 1, 1, .35f);
                row.Tag.text = string.Empty;
                row.State.text = string.Empty;
                row.Chip.color = new Color(1, 1, 1, 0);
                row.Background.color = new Color(1, 1, 1, .04f);
                return;
            }
            bool isHost = session.Crew[i].ClientId == NetworkManager.ServerClientId;
            bool ready = i < session.Profiles.Count && session.Profiles[i].Ready;
            string name = LobbyRules.DisplayName(i < session.Profiles.Count ? session.Profiles[i].Name.ToString() : null, i);
            row.Name.text = i == local ? name + "  (you)" : name;
            row.Name.color = HudKit.Ink;
            row.Tag.text = isHost ? "HOST" : string.Empty;
            row.Background.color = i == local ? new Color(1f, .78f, .28f, .14f) : new Color(1, 1, 1, .08f);
            if (isHost) { row.State.text = "HOSTING"; row.Chip.color = new Color(HudKit.Accent.r, HudKit.Accent.g, HudKit.Accent.b, .28f); }
            else if (ready) { row.State.text = "READY"; row.Chip.color = new Color(HudKit.Good.r, HudKit.Good.g, HudKit.Good.b, .35f); }
            else { row.State.text = "NOT READY"; row.Chip.color = HudKit.PanelLight; }
        }
    }
}
