using UnityEngine;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>Title and lobby screen shown until a voyage session is spawned.</summary>
    public sealed class MenuScreen
    {
        private readonly PrototypeHud hud;
        private readonly GameObject root;
        private readonly InputField codeField;
        private readonly Button createButton, joinButton, soloButton, cancelButton;
        private readonly Text statusText, versionText;
        private bool suppressCodeEvent;

        public MenuScreen(PrototypeHud hud, Transform canvas)
        {
            this.hud = hud;
            var screen = HudKit.Node(canvas, "MenuScreen");
            root = screen.gameObject;
            HudKit.Stretch(screen);
            var dim = HudKit.Box(screen, "Dim", new Color(.02f, .05f, .09f, .55f), false, true);
            HudKit.Stretch(dim.rectTransform);

            var card = HudKit.Box(screen, "Card", HudKit.PanelStrong);
            HudKit.Anchor(card.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(620, 0));
            HudKit.Column(card.rectTransform, 14, new RectOffset(44, 44, 40, 40));

            var title = HudKit.Label(card.transform, "Title", 64, HudKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.text = "SHIP MAYHAM";
            HudKit.Size(title, height: 76);
            var tagline = HudKit.Label(card.transform, "Tagline", 24, HudKit.TextMuted, TextAnchor.MiddleCenter);
            tagline.text = "Crew a chaotic ferry with up to four friends";
            HudKit.Size(tagline, height: 34);
            Spacer(card.transform, 14);

            createButton = HudKit.MakeButton(card.transform, "Create", "CREATE ONLINE VOYAGE", HudKit.Accent, 28);
            createButton.GetComponentInChildren<Text>().color = new Color(.1f, .08f, .02f);
            HudKit.Size(createButton, height: 70);
            createButton.onClick.AddListener(() => hud.ConnectOnline(true));

            var divider = HudKit.Label(card.transform, "Or", 20, HudKit.TextMuted, TextAnchor.MiddleCenter);
            divider.text = "or join a friend";
            HudKit.Size(divider, height: 30);

            var joinRow = HudKit.Node(card.transform, "JoinRow");
            HudKit.Size(joinRow, height: 64);
            HudKit.Row(joinRow, 12).childForceExpandHeight = true;
            codeField = BuildCodeField(joinRow);
            joinButton = HudKit.MakeButton(joinRow, "Join", "JOIN", new Color(.22f, .5f, .78f), 26);
            HudKit.Size(joinButton, width: 170);
            joinButton.onClick.AddListener(() => hud.ConnectOnline(false));

            Spacer(card.transform, 6);
            soloButton = HudKit.MakeButton(card.transform, "Solo", "PLAY SOLO", new Color(1, 1, 1, .14f), 24);
            HudKit.Size(soloButton, height: 56);
            soloButton.onClick.AddListener(hud.ConnectSolo);

            statusText = HudKit.Label(card.transform, "Status", 22, HudKit.Ink, TextAnchor.MiddleCenter);
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudKit.Size(statusText, height: 66);

            cancelButton = HudKit.MakeButton(card.transform, "Cancel", "CANCEL", new Color(.8f, .3f, .3f, .85f), 22);
            HudKit.Size(cancelButton, height: 48);
            cancelButton.onClick.AddListener(hud.LeaveSession);

            versionText = HudKit.Label(screen, "Version", 18, new Color(1, 1, 1, .5f), TextAnchor.LowerLeft);
            HudKit.Anchor(versionText.rectTransform, Vector2.zero, Vector2.zero, new Vector2(HudKit.Margin, HudKit.Margin * .6f), new Vector2(600, 30));
            versionText.text = $"v{Application.version} • protocol {PrototypeHud.NetworkProtocolVersion}";
        }

        private InputField BuildCodeField(Transform parent)
        {
            var background = HudKit.Box(parent, "Code", new Color(1, 1, 1, .1f), true, true);
            HudKit.Size(background, flexibleWidth: 1);
            var field = background.gameObject.AddComponent<InputField>();
            field.targetGraphic = background;
            field.characterLimit = 8;
            field.characterValidation = InputField.CharacterValidation.Alphanumeric;
            field.lineType = InputField.LineType.SingleLine;

            var placeholder = HudKit.Label(background.transform, "Placeholder", 28, new Color(1, 1, 1, .35f), TextAnchor.MiddleCenter, FontStyle.Italic, false);
            HudKit.Stretch(placeholder.rectTransform, 8);
            placeholder.text = "VOYAGE CODE";
            var text = HudKit.Label(background.transform, "Text", 32, HudKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold, false);
            HudKit.Stretch(text.rectTransform, 8);
            text.supportRichText = false;
            field.placeholder = placeholder;
            field.textComponent = text;
            field.onValueChanged.AddListener(value =>
            {
                if (suppressCodeEvent) return;
                string upper = value.ToUpperInvariant();
                if (upper != value) { suppressCodeEvent = true; field.text = upper; suppressCodeEvent = false; }
                hud.JoinCode = upper;
            });
            return field;
        }

        private static void Spacer(Transform parent, float height)
        {
            var node = HudKit.Node(parent, "Spacer");
            HudKit.Size(node, height: height);
        }

        public void SetVisible(bool visible) => HudKit.SetActive(root, visible);

        public void Refresh()
        {
            bool busy = hud.ConnectionAttempted;
            createButton.interactable = !busy;
            joinButton.interactable = !busy && hud.JoinCode.Length >= 6;
            soloButton.interactable = !busy;
            codeField.interactable = !busy;
            HudKit.SetActive(cancelButton.gameObject, hud.CanCancelConnection);
            HudKit.SetText(statusText, hud.Status);
            statusText.color = hud.StatusIsError ? HudKit.Danger : busy ? HudKit.Accent : HudKit.TextMuted;
            if (!codeField.isFocused && codeField.text != hud.JoinCode)
            {
                suppressCodeEvent = true;
                codeField.text = hud.JoinCode;
                suppressCodeEvent = false;
            }
        }
    }
}
