using UnityEngine;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>
    /// Title screen shown until a voyage session is spawned: main menu, join-by-code and settings pages,
    /// plus the connection status and a cancel button while a connection is being made.
    /// </summary>
    public sealed class MenuScreen
    {
        public enum Page { Main, Join, Settings }

        private readonly PrototypeHud hud;
        private readonly GameObject root;
        private readonly GameObject[] pages = new GameObject[3];
        private readonly Button[] actionButtons;
        private readonly InputField codeField, nicknameField;
        private readonly Button joinButton, cancelButton;
        private readonly SettingsPanel settingsPanel;
        private readonly Text statusText;
        private bool suppressCodeEvent;
        private Page current;

        public Page Current => current;

        public MenuScreen(PrototypeHud hud, Transform canvas)
        {
            this.hud = hud;
            var screen = HudKit.Node(canvas, "MenuScreen");
            root = screen.gameObject;
            HudKit.Stretch(screen);
            var dim = HudKit.Box(screen, "Dim", new Color(.02f, .05f, .09f, .55f), false, true);
            HudKit.Stretch(dim.rectTransform);

            var buttons = new System.Collections.Generic.List<Button>();

            // ---- Main
            var main = NewPage(screen, Page.Main, "SHIP MAYHAM", "Crew a chaotic ferry with up to four friends");
            var nicknameCaption = HudKit.Label(main, "NicknameCaption", 16, HudKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            nicknameCaption.text = "YOUR NICKNAME";
            HudKit.Size(nicknameCaption, height: 20);
            nicknameField = BuildInput(main, "Nickname", "NICKNAME", PlayerNickname.Current, NicknameLimit, 28, InputField.CharacterValidation.None);
            nicknameField.onEndEdit.AddListener(value => nicknameField.text = PlayerNickname.Set(value));
            Spacer(main, 6);
            if (WorldSave.TryPeekStored(out var saved)) BuildContinueBlock(main, saved, buttons);
            buttons.Add(AddButton(main, "Host", "HOST ONLINE", HudKit.Accent, 28, 70, () => { WorldSave.Discard(); hud.ConnectOnline(true); }, true));
            buttons.Add(AddButton(main, "JoinOnline", "JOIN ONLINE", new Color(.22f, .5f, .78f), 26, 62, () => Show(Page.Join)));
            buttons.Add(AddButton(main, "Solo", "PLAY SOLO", new Color(1, 1, 1, .14f), 24, 56, () => { WorldSave.Discard(); hud.ConnectSolo(); }));
            buttons.Add(AddButton(main, "Settings", "SETTINGS", new Color(1, 1, 1, .14f), 24, 56, () => Show(Page.Settings)));
#if !UNITY_WEBGL
            buttons.Add(AddButton(main, "Quit", "QUIT", new Color(.8f, .3f, .3f, .55f), 22, 48, Quit));
#endif

            // ---- Join
            var join = NewPage(screen, Page.Join, "JOIN A VOYAGE", "Enter the code your friend shares from their lobby");
            codeField = BuildInput(join, "Code", "VOYAGE CODE", string.Empty, 8, 34, InputField.CharacterValidation.Alphanumeric);
            codeField.onValueChanged.AddListener(OnCodeChanged);
            joinButton = AddButton(join, "Join", "JOIN", new Color(.22f, .5f, .78f), 28, 66, () => hud.ConnectOnline(false), true);
            buttons.Add(joinButton);
            buttons.Add(AddButton(join, "BackFromJoin", "BACK", new Color(1, 1, 1, .14f), 22, 50, () => Show(Page.Main)));

            // ---- Settings
            var settings = NewPage(screen, Page.Settings, "SETTINGS", null);
            settingsPanel = new SettingsPanel(settings);
            if (WorldSave.Exists) buttons.Add(BuildEraseButton(settings, "EraseSave", "ERASE SAVED WORLD", "SAVED WORLD ERASED", WorldSave.Clear));
            if (CareerProgressStore.Exists) buttons.Add(BuildEraseButton(settings, "EraseCareer", "RESTART THE CAMPAIGN", "CAMPAIGN RESTARTS NEXT GAME", CareerProgressStore.Clear));
            buttons.Add(AddButton(settings, "BackFromSettings", "BACK", new Color(1, 1, 1, .14f), 22, 50, () => Show(Page.Main)));
            actionButtons = buttons.ToArray();

            // ---- Connection status under the card
            var status = HudKit.Node(screen, "Connection");
            HudKit.Anchor(status, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 70), new Vector2(760, 0));
            HudKit.Column(status, 10, new RectOffset(0, 0, 0, 0)).childAlignment = TextAnchor.LowerCenter;
            statusText = HudKit.Label(status, "Status", 24, HudKit.Ink, TextAnchor.MiddleCenter);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            cancelButton = HudKit.MakeButton(status, "Cancel", "CANCEL", new Color(.8f, .3f, .3f, .85f), 22);
            HudKit.Size(cancelButton, height: 48, width: 220);
            cancelButton.onClick.AddListener(hud.LeaveSession);

            var version = HudKit.Label(screen, "Version", 18, new Color(1, 1, 1, .5f), TextAnchor.LowerLeft);
            HudKit.Anchor(version.rectTransform, Vector2.zero, Vector2.zero, new Vector2(HudKit.Margin, HudKit.Margin * .6f), new Vector2(600, 30));
            version.text = $"v{Application.version} • protocol {PrototypeHud.NetworkProtocolVersion}";

            Show(Page.Main);
        }

        private const int NicknameLimit = LobbyRules.MaxNameLength;

        /// <summary>The saved world: what it is and how to continue it, alone or as an online host that friends can rejoin.</summary>
        private void BuildContinueBlock(RectTransform main, in WorldSaveInfo saved, System.Collections.Generic.List<Button> buttons)
        {
            var caption = HudKit.Label(main, "SavedCaption", 16, HudKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            caption.text = "YOUR LAST WORLD";
            HudKit.Size(caption, height: 20);
            var summary = HudKit.Label(main, "SavedSummary", 19, HudKit.Ink, TextAnchor.MiddleLeft);
            summary.text = WorldSave.Describe(saved, WorldSave.NowUnix());
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudKit.Size(summary, height: 52);
            var row = HudKit.Node(main, "ContinueRow");
            HudKit.Size(row, height: 62);
            HudKit.Row(row, 12).childForceExpandWidth = true;
            var solo = HudKit.MakeButton(row, "ContinueSolo", "CONTINUE SOLO", new Color(.3f, .62f, .4f), 22);
            solo.onClick.AddListener(() => { if (WorldSave.Stage(RestoreMode.Solo)) hud.ConnectSolo(); else RefuseSavedWorld(); });
            var online = HudKit.MakeButton(row, "ContinueOnline", "CONTINUE ONLINE", new Color(.3f, .62f, .4f), 22);
            online.onClick.AddListener(() => { if (WorldSave.Stage(RestoreMode.Host)) hud.ConnectOnline(true); else RefuseSavedWorld(); });
            buttons.Add(solo);
            buttons.Add(online);
            var note = HudKit.Label(main, "SavedNote", 15, HudKit.TextMuted, TextAnchor.MiddleLeft);
            note.text = "Friends rejoin online with the new code and keep their seats. Starting a new world replaces this save.";
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudKit.Size(note, height: 40);
            Spacer(main, 4);
        }

        private void RefuseSavedWorld()
        {
            WorldSave.Discard();
            Debug.LogWarning("The saved world could not be loaded and was ignored.");
        }

        private Button BuildEraseButton(RectTransform settings, string name, string text, string doneText, System.Action erased)
        {
            var erase = HudKit.MakeButton(settings, name, text, new Color(.8f, .3f, .3f, .55f), 20);
            HudKit.Size(erase, height: 46);
            var label = erase.GetComponentInChildren<Text>();
            bool armed = false;
            erase.onClick.AddListener(() =>
            {
                if (!armed) { armed = true; label.text = "CLICK AGAIN TO ERASE"; return; }
                erased();
                label.text = doneText;
                erase.interactable = false;
            });
            return erase;
        }

        private RectTransform NewPage(Transform parent, Page page, string title, string subtitle)
        {
            var card = HudKit.Box(parent, page + "Page", HudKit.PanelStrong);
            HudKit.Anchor(card.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(620, 0));
            HudKit.Column(card.rectTransform, 14, new RectOffset(44, 44, 40, 40));
            var heading = HudKit.Label(card.transform, "Title", page == Page.Main ? 64 : 44, HudKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            heading.text = title;
            HudKit.Size(heading, height: page == Page.Main ? 76 : 56);
            if (subtitle != null)
            {
                var tagline = HudKit.Label(card.transform, "Tagline", 22, HudKit.TextMuted, TextAnchor.MiddleCenter);
                tagline.text = subtitle;
                tagline.horizontalOverflow = HorizontalWrapMode.Wrap;
                HudKit.Size(tagline, height: 34);
            }
            Spacer(card.transform, 10);
            pages[(int)page] = card.gameObject;
            return card.rectTransform;
        }

        private static Button AddButton(Transform parent, string name, string label, Color tint, int fontSize, float height, UnityEngine.Events.UnityAction onClick, bool dark = false)
        {
            var button = HudKit.MakeButton(parent, name, label, tint, fontSize);
            if (dark) button.GetComponentInChildren<Text>().color = new Color(.1f, .08f, .02f);
            HudKit.Size(button, height: height);
            button.onClick.AddListener(onClick);
            return button;
        }

        private static InputField BuildInput(Transform parent, string name, string placeholderText, string initial, int limit, int fontSize, InputField.CharacterValidation validation)
        {
            var background = HudKit.Box(parent, name, new Color(1, 1, 1, .1f), true, true);
            HudKit.Size(background, height: 64);
            var field = background.gameObject.AddComponent<InputField>();
            field.targetGraphic = background;
            field.characterLimit = limit;
            field.characterValidation = validation;
            field.lineType = InputField.LineType.SingleLine;
            var placeholder = HudKit.Label(background.transform, "Placeholder", fontSize, new Color(1, 1, 1, .35f), TextAnchor.MiddleCenter, FontStyle.Italic, false);
            HudKit.Stretch(placeholder.rectTransform, 8);
            placeholder.text = placeholderText;
            var text = HudKit.Label(background.transform, "Text", fontSize + 2, HudKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold, false);
            HudKit.Stretch(text.rectTransform, 8);
            text.supportRichText = false;
            field.placeholder = placeholder;
            field.textComponent = text;
            field.SetTextWithoutNotify(initial);
            return field;
        }

        private static void Spacer(Transform parent, float height) => HudKit.Size(HudKit.Node(parent, "Spacer"), height: height);

        private void OnCodeChanged(string value)
        {
            if (suppressCodeEvent) return;
            string upper = value.ToUpperInvariant();
            if (upper != value) { suppressCodeEvent = true; codeField.text = upper; suppressCodeEvent = false; }
            hud.JoinCode = upper;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Show(Page page)
        {
            if (current == Page.Settings && page != Page.Settings) settingsPanel.Unsubscribe();
            if (page == Page.Settings && current != Page.Settings) settingsPanel.Subscribe();
            current = page;
            for (int i = 0; i < pages.Length; i++) HudKit.SetActive(pages[i], i == (int)page);
        }

        public void SetVisible(bool visible)
        {
            if (visible && !root.activeSelf) Show(Page.Main);
            HudKit.SetActive(root, visible);
        }

        public void Refresh()
        {
            bool busy = hud.ConnectionAttempted;
            foreach (var button in actionButtons) button.interactable = !busy;
            joinButton.interactable = !busy && hud.JoinCode.Length >= 6;
            codeField.interactable = !busy;
            nicknameField.interactable = !busy;
            HudKit.SetActive(cancelButton.gameObject, hud.CanCancelConnection);
            bool showStatus = busy || hud.StatusIsError;
            HudKit.SetActive(statusText.gameObject, showStatus);
            if (showStatus)
            {
                HudKit.SetText(statusText, hud.Status);
                statusText.color = hud.StatusIsError ? HudKit.Danger : HudKit.Accent;
            }
            if (!codeField.isFocused && codeField.text != hud.JoinCode)
            {
                suppressCodeEvent = true;
                codeField.text = hud.JoinCode;
                suppressCodeEvent = false;
            }
        }
    }
}
