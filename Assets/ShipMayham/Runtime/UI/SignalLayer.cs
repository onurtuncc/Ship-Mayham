using UnityEngine;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>
    /// Draws what the crew signals: ping markers that follow their spot in the world (pinned to the screen edge when it is out of
    /// view) and emote bubbles above the head of whoever emoted. Purely presentation.
    /// </summary>
    public sealed class SignalLayer
    {
        private const int MaxPings = 8;
        private const float EdgeMargin = 70f, FadeSeconds = .8f, PopSeconds = .15f, TotalBubbleHeight = 2.4f;

        private sealed class Marker
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Body, Tip;
            public Text Label;
            public CanvasGroup Group;
            public SignalMessage Message;
            public float Born;
            public bool Active;
        }

        private readonly RectTransform layer;
        private readonly Marker[] pings = new Marker[MaxPings];
        private readonly Marker[] bubbles = new Marker[VoyageSimulation.MaxCrew];

        public SignalLayer(Transform parent)
        {
            layer = HudKit.Node(parent, "SignalLayer");
            HudKit.Stretch(layer);
            for (int i = 0; i < pings.Length; i++) pings[i] = Build("Ping" + i, 24, 22);
            for (int i = 0; i < bubbles.Length; i++) bubbles[i] = Build("Bubble" + i, 30, 26);
        }

        private Marker Build(string name, int fontSize, int tipSize)
        {
            var root = HudKit.Node(layer, name);
            root.pivot = new Vector2(.5f, 0);
            var marker = new Marker { Root = root.gameObject, Rect = root };
            marker.Group = root.gameObject.AddComponent<CanvasGroup>();
            marker.Group.blocksRaycasts = false; marker.Group.interactable = false;
            var tip = HudKit.Dot(root, "Tip", Color.white, tipSize * .6f);
            tip.rectTransform.anchorMin = tip.rectTransform.anchorMax = new Vector2(.5f, 0);
            tip.rectTransform.pivot = new Vector2(.5f, 1);
            tip.rectTransform.anchoredPosition = new Vector2(0, -4);
            var body = HudKit.Box(root, "Body", Color.white);
            body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(.5f, 0);
            body.rectTransform.pivot = new Vector2(.5f, 0);
            HudKit.Row(body.rectTransform, 0, TextAnchor.MiddleCenter, true).padding = new RectOffset(18, 18, 8, 10);
            body.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var label = HudKit.Label(body.transform, "Text", fontSize, new Color(.07f, .1f, .14f), TextAnchor.MiddleCenter, FontStyle.Bold, false);
            marker.Body = body; marker.Tip = tip; marker.Label = label;
            root.gameObject.SetActive(false);
            return marker;
        }

        /// <summary>Shows a newly received signal. Pings take a free marker (or the oldest); an emote replaces its sender's bubble.</summary>
        public void Add(SignalMessage message)
        {
            Marker target;
            if (Signals.IsEmote(message.Kind))
            {
                if (message.Slot < 0 || message.Slot >= bubbles.Length) return;
                target = bubbles[message.Slot];
            }
            else
            {
                target = pings[0];
                foreach (var ping in pings)
                {
                    if (!ping.Active) { target = ping; break; }
                    if (ping.Born < target.Born) target = ping;
                }
            }
            target.Message = message;
            target.Born = Time.unscaledTime;
            target.Active = true;
            Color color = Signals.ColorFor(message.Slot);
            target.Body.color = color;
            target.Tip.color = color;
            target.Label.text = Signals.IsEmote(message.Kind) ? Signals.Text(message.Kind) : string.IsNullOrEmpty(message.Label) ? "PING" : message.Label.ToUpperInvariant();
            target.Root.SetActive(true);
        }

        public void Tick(PrototypeHud hud)
        {
            var view = hud.View;
            var camera = view != null ? view.Camera : null;
            if (camera == null) return;
            int local = hud.Session.LocalCrewIndex;
            foreach (var ping in pings) Place(ping, hud, camera, local, Signals.PingSeconds, edgePinned: true);
            foreach (var bubble in bubbles) Place(bubble, hud, camera, local, Signals.BubbleSeconds, edgePinned: false);
        }

        private void Place(Marker marker, PrototypeHud hud, Camera camera, int local, float lifetime, bool edgePinned)
        {
            if (!marker.Active) return;
            float age = Time.unscaledTime - marker.Born;
            if (age >= lifetime) { marker.Active = false; marker.Root.SetActive(false); return; }

            var message = marker.Message;
            bool isBubble = Signals.IsEmote(message.Kind);
            Vector3 world;
            Vector2 screen;
            bool visible = true;
            if (isBubble && message.Slot == local)
            {
                // Your own head is not in view: show your emote just above the crosshair.
                screen = new Vector2(Screen.width * .5f, Screen.height * .5f + Screen.height * .14f);
                world = Vector3.zero;
            }
            else
            {
                world = isBubble
                    ? hud.View.CrewRoots[message.Slot].position + Vector3.up * TotalBubbleHeight
                    : (message.Attached ? hud.Session.ShipRoot.TransformPoint(message.Point) : message.Point) + Vector3.up * (.6f + Mathf.Sin(age * 5f) * .12f);
                Vector3 projected = camera.WorldToScreenPoint(world);
                screen = projected;
                var center = new Vector2(Screen.width, Screen.height) * .5f;
                bool onScreen = projected.z > 0 && projected.x > EdgeMargin && projected.x < Screen.width - EdgeMargin && projected.y > EdgeMargin && projected.y < Screen.height - EdgeMargin;
                if (!onScreen)
                {
                    if (!edgePinned) visible = false;
                    else
                    {
                        // Behind the camera the projection mirrors; flip it so the marker sits on the side the spot really is.
                        if (projected.z < 0) screen = center - (screen - center);
                        screen.x = Mathf.Clamp(screen.x, EdgeMargin, Screen.width - EdgeMargin);
                        screen.y = Mathf.Clamp(screen.y, EdgeMargin, Screen.height - EdgeMargin);
                    }
                }
            }

            HudKit.SetActive(marker.Root, visible);
            if (!visible) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out Vector2 local2);
            marker.Rect.anchoredPosition = local2;
            float pop = Mathf.Clamp01(age / PopSeconds);
            marker.Rect.localScale = Vector3.one * Mathf.Lerp(.4f, 1f, 1f - (1f - pop) * (1f - pop));
            marker.Group.alpha = Mathf.Clamp01((lifetime - age) / FadeSeconds);
        }
    }

    /// <summary>The four-way emote wheel shown while the signal key is held.</summary>
    public sealed class SignalWheel
    {
        private static readonly (SignalKind kind, Vector2 offset)[] Choices =
        {
            (SignalKind.Help, new Vector2(0, 150)), (SignalKind.Thanks, new Vector2(230, 0)),
            (SignalKind.WatchOut, new Vector2(0, -150)), (SignalKind.OverHere, new Vector2(-230, 0)),
        };

        private readonly GameObject root;
        private readonly Image[] wedges = new Image[Choices.Length];
        private readonly Text[] labels = new Text[Choices.Length];
        private SignalKind? shown = (SignalKind)255;

        public SignalWheel(Transform parent)
        {
            var node = HudKit.Node(parent, "SignalWheel");
            root = node.gameObject;
            HudKit.Anchor(node, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(10, 10));
            var hub = HudKit.Dot(node, "Hub", new Color(1, 1, 1, .85f), 18);
            for (int i = 0; i < Choices.Length; i++)
            {
                var wedge = HudKit.Box(node, "Wedge" + Choices[i].kind, HudKit.PanelStrong);
                HudKit.Anchor(wedge.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Choices[i].offset, new Vector2(230, 74));
                var label = HudKit.Label(wedge.transform, "Text", 28, HudKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                HudKit.Stretch(label.rectTransform);
                label.text = Signals.Text(Choices[i].kind);
                wedges[i] = wedge; labels[i] = label;
            }
            var hint = HudKit.Label(node, "Hint", 22, HudKit.Ink, TextAnchor.MiddleCenter);
            HudKit.Anchor(hint.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -250), new Vector2(500, 30));
            hint.text = "Move to choose, release to send";
            root.SetActive(false);
        }

        public void Tick(PrototypeView view)
        {
            bool open = view != null && view.WheelOpen;
            HudKit.SetActive(root, open);
            if (!open) { shown = (SignalKind)255; return; }
            var selection = view.WheelSelection;
            if (selection == shown) return;
            shown = selection;
            for (int i = 0; i < Choices.Length; i++)
            {
                bool chosen = selection == Choices[i].kind;
                wedges[i].color = chosen ? Signals.ColorFor(0) : HudKit.PanelStrong;
                labels[i].color = chosen ? new Color(.07f, .1f, .14f) : HudKit.Ink;
                wedges[i].rectTransform.localScale = Vector3.one * (chosen ? 1.12f : 1f);
            }
        }
    }
}
