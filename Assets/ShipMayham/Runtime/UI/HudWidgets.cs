using UnityEngine;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>A row of "[key] label" hints. Slots are pooled; unused ones are hidden.</summary>
    public sealed class HintRow
    {
        private struct Slot { public GameObject Root; public Text Key, Label; }
        private readonly Slot[] slots;
        private readonly RectTransform root;
        private int signature = int.MinValue;

        public RectTransform Root => root;

        public HintRow(Transform parent, string name, int slotCount, int fontSize, TextAnchor alignment)
        {
            root = HudKit.Node(parent, name);
            HudKit.Row(root, 22, alignment);
            slots = new Slot[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                var item = HudKit.Node(root, "Hint" + i);
                HudKit.Row(item, 8);
                var key = HudKit.KeyCap(item, "Cap", fontSize, out _);
                var label = HudKit.Label(item, "Label", fontSize + 2, HudKit.Ink);
                slots[i] = new Slot { Root = item.gameObject, Key = key, Label = label };
            }
        }

        public void Set(bool gamepad, PromptHint a, PromptHint b = default, PromptHint c = default)
        {
            int sig = Sig(a, gamepad) * 31 + Sig(b, gamepad) * 7 + Sig(c, gamepad);
            if (sig == signature) return;
            signature = sig;
            Apply(0, a, gamepad); Apply(1, b, gamepad); Apply(2, c, gamepad);
        }

        private static int Sig(in PromptHint hint, bool gamepad) =>
            hint.IsEmpty ? 0 : ((int)hint.Control + 1) * 1009 + hint.Label.GetHashCode() + (gamepad ? 1 : 0);

        private void Apply(int index, in PromptHint hint, bool gamepad)
        {
            if (index >= slots.Length) return;
            bool visible = !hint.IsEmpty;
            HudKit.SetActive(slots[index].Root, visible);
            if (!visible) return;
            slots[index].Key.text = HudControls.Label(hint.Control, gamepad);
            slots[index].Label.text = hint.Label;
        }
    }

    /// <summary>Counter in the resource strip with a caption, an icon dot and a short-lived +/- delta.</summary>
    public sealed class ResourcePill
    {
        private readonly Text value, delta;
        private readonly RectTransform scaleRoot;
        private int last = int.MinValue;
        private float flash, deltaTime;

        public RectTransform Root { get; }

        public ResourcePill(Transform parent, string name, string caption, Color tint)
        {
            var bg = HudKit.Box(parent, name, HudKit.Panel);
            Root = bg.rectTransform;
            scaleRoot = Root;
            HudKit.Size(bg, width: 132, height: 74);
            HudKit.Column(Root, 0, new RectOffset(14, 14, 8, 8), false).childAlignment = TextAnchor.MiddleLeft;
            var title = HudKit.Label(Root, "Caption", 15, HudKit.TextMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.text = caption;
            HudKit.Size(title, height: 20);
            var line = HudKit.Node(Root, "Line");
            HudKit.Size(line, height: 38);
            HudKit.Row(line, 8);
            var dot = caption == "FOOD" ? HudKit.Box(line, "Icon", tint) : HudKit.Dot(line, "Icon", tint, 20);
            HudKit.Size(dot, 20, 20);
            value = HudKit.Label(line, "Value", 32, HudKit.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            delta = HudKit.Label(line, "Delta", 20, HudKit.Good, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        public void Set(int amount)
        {
            if (amount == last) return;
            if (last != int.MinValue)
            {
                int change = amount - last;
                delta.text = change > 0 ? "+" + change : change.ToString();
                delta.color = change > 0 ? HudKit.Good : HudKit.Danger;
                deltaTime = 2.2f;
                flash = 1f;
            }
            last = amount;
            value.text = amount.ToString();
        }

        public void Tick(float dt)
        {
            if (flash > 0) flash = Mathf.Max(0, flash - dt * 3f);
            scaleRoot.localScale = Vector3.one * (1f + .08f * flash);
            if (deltaTime > 0)
            {
                deltaTime -= dt;
                var color = delta.color;
                color.a = Mathf.Clamp01(deltaTime / .6f);
                delta.color = color;
                if (deltaTime <= 0) delta.text = string.Empty;
            }
        }
    }

    /// <summary>Stack of coloured warnings/tasks with a title and one or two lines of detail.</summary>
    public sealed class AlertList
    {
        private struct Row { public GameObject Root; public Image Bar; public Text Title, Body; public string TitleValue, BodyValue; }
        private readonly Row[] rows;
        private int count;

        public RectTransform Root { get; }

        public AlertList(Transform parent, string name, int capacity)
        {
            Root = HudKit.Node(parent, name);
            HudKit.Column(Root, 10, new RectOffset(0, 0, 0, 0), false).childAlignment = TextAnchor.UpperLeft;
            rows = new Row[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var bg = HudKit.Box(Root, "Alert" + i, HudKit.Panel);
                HudKit.Column(bg.rectTransform, 2, new RectOffset(26, 16, 10, 12), false);
                var bar = HudKit.Box(bg.transform, "Bar", HudKit.Info);
                bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(0, 1);
                bar.rectTransform.pivot = new Vector2(0, .5f);
                bar.rectTransform.offsetMin = new Vector2(10, 10); bar.rectTransform.offsetMax = new Vector2(16, -10);
                var title = HudKit.Label(bg.transform, "Title", 22, HudKit.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                var text = HudKit.Label(bg.transform, "Body", 19, HudKit.TextMuted);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                HudKit.Size(bg, width: 400);
                rows[i] = new Row { Root = bg.gameObject, Bar = bar, Title = title, Body = text };
                bg.gameObject.SetActive(false);
            }
        }

        public void Begin() => count = 0;

        public void Add(Color color, string title, string body)
        {
            if (count >= rows.Length) return;
            ref Row row = ref rows[count++];
            HudKit.SetActive(row.Root, true);
            row.Bar.color = color;
            if (row.TitleValue != title) { row.TitleValue = title; row.Title.text = title; row.Title.color = color; }
            if (row.BodyValue != body)
            {
                row.BodyValue = body; row.Body.text = body ?? string.Empty;
                HudKit.SetActive(row.Body.gameObject, !string.IsNullOrEmpty(body));
            }
        }

        public void End()
        {
            for (int i = count; i < rows.Length; i++) HudKit.SetActive(rows[i].Root, false);
        }
    }

    /// <summary>Crosshair dot and halo that tighten and tint when something interactive is targeted.</summary>
    public sealed class Crosshair
    {
        private readonly Image halo, dot;
        private float focus;

        public RectTransform Root { get; }

        public Crosshair(Transform parent)
        {
            Root = HudKit.Node(parent, "Crosshair");
            HudKit.Anchor(Root, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(64, 64));
            halo = HudKit.Dot(Root, "Halo", new Color(1, 1, 1, .16f), 34);
            dot = HudKit.Dot(Root, "Dot", new Color(1, 1, 1, .95f), 8);
            // Dark rim keeps the dot readable over bright sky and foam.
            var outline = dot.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, .6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        public void Tick(float dt, bool targeted, PromptTone tone)
        {
            focus = Mathf.MoveTowards(focus, targeted ? 1f : 0f, dt * 9f);
            Color accent = tone == PromptTone.Blocked ? HudKit.Danger : tone == PromptTone.Info ? HudKit.Info : HudKit.Accent;
            halo.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(34f, 46f, focus);
            halo.color = Color.Lerp(new Color(1, 1, 1, .16f), new Color(accent.r, accent.g, accent.b, .42f), focus);
            dot.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(8f, 11f, focus);
            dot.color = Color.Lerp(new Color(1, 1, 1, .95f), accent, focus);
        }
    }

    /// <summary>Card under the crosshair describing the focused object and the action available on it.</summary>
    public sealed class PromptCard
    {
        private readonly CanvasGroup group;
        private readonly Text title, action, detail, cost, key;
        private readonly Image keyBackground, costBackground;
        private readonly GameObject keyRoot, detailRoot, costRoot;
        private readonly HintRow hints;
        private InteractionPrompt shown;
        private bool shownGamepad;
        private float alpha;

        public PromptCard(Transform parent)
        {
            var bg = HudKit.Box(parent, "PromptCard", HudKit.PanelStrong);
            group = bg.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false; group.blocksRaycasts = false;
            var rect = bg.rectTransform;
            HudKit.Anchor(rect, new Vector2(.5f, .5f), new Vector2(.5f, 1f), new Vector2(0, -62), new Vector2(0, 0));
            HudKit.Column(rect, 6, new RectOffset(26, 26, 16, 18)).childAlignment = TextAnchor.UpperCenter;
            var fitter = rect.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vertical = rect.GetComponent<VerticalLayoutGroup>();
            vertical.childForceExpandWidth = false;
            vertical.childControlWidth = true;

            title = HudKit.Label(rect, "Title", 20, HudKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            var actionRow = HudKit.Node(rect, "ActionRow");
            HudKit.Row(actionRow, 14, TextAnchor.MiddleCenter);
            key = HudKit.KeyCap(actionRow, "Key", 26, out keyBackground);
            keyRoot = keyBackground.gameObject;
            action = HudKit.Label(actionRow, "Action", 32, HudKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            var detailNode = HudKit.Node(rect, "DetailRow");
            detailRoot = detailNode.gameObject;
            HudKit.Row(detailNode, 0, TextAnchor.MiddleCenter);
            detail = HudKit.Label(detailNode, "Detail", 21, HudKit.TextMuted, TextAnchor.MiddleCenter);
            var costNode = HudKit.Box(rect, "Cost", new Color(HudKit.Coin.r, HudKit.Coin.g, HudKit.Coin.b, .2f));
            costBackground = costNode;
            costRoot = costNode.gameObject;
            HudKit.Row(costNode.rectTransform, 8, TextAnchor.MiddleCenter).padding = new RectOffset(16, 16, 4, 4);
            HudKit.Dot(costNode.transform, "Coin", HudKit.Coin, 16).gameObject.AddComponent<LayoutElement>().preferredWidth = 16;
            cost = HudKit.Label(costNode.transform, "Value", 22, HudKit.Coin, TextAnchor.MiddleCenter, FontStyle.Bold);
            hints = new HintRow(rect, "Hints", 2, 17, TextAnchor.MiddleCenter);
            bg.gameObject.SetActive(false);
        }

        public void Tick(float dt, bool visible, in InteractionPrompt prompt, bool gamepad)
        {
            alpha = Mathf.MoveTowards(alpha, visible ? 1f : 0f, dt * 10f);
            group.alpha = alpha;
            HudKit.SetActive(group.gameObject, alpha > 0.001f);
            if (visible && (!prompt.Equals(shown) || gamepad != shownGamepad))
            {
                shown = prompt; shownGamepad = gamepad;
                Apply(prompt, gamepad);
            }
        }

        private void Apply(in InteractionPrompt p, bool gamepad)
        {
            HudKit.SetText(title, (p.Title ?? string.Empty).ToUpperInvariant());
            HudKit.SetActive(title.gameObject, !string.IsNullOrEmpty(p.Title));
            Color tone = p.Tone == PromptTone.Blocked ? HudKit.Danger : p.Tone == PromptTone.Info ? HudKit.TextMuted : HudKit.Ink;
            action.color = tone;
            HudKit.SetText(action, p.Action ?? string.Empty);
            bool hasKey = p.Key != HudControl.None;
            HudKit.SetActive(keyRoot, hasKey);
            if (hasKey) HudKit.SetText(key, HudControls.Label(p.Key, gamepad));
            HudKit.SetActive(detailRoot, !string.IsNullOrEmpty(p.Detail));
            HudKit.SetText(detail, p.Detail ?? string.Empty);
            HudKit.SetActive(costRoot, !string.IsNullOrEmpty(p.Cost));
            HudKit.SetText(cost, p.Cost ?? string.Empty);
            bool hasHints = !p.Hint1.IsEmpty || !p.Hint2.IsEmpty;
            HudKit.SetActive(hints.Root.gameObject, hasHints);
            if (hasHints) hints.Set(gamepad, p.Hint1, p.Hint2);
        }
    }

    /// <summary>Compact "LABEL  value" rows used by the voyage report.</summary>
    public sealed class StatRows
    {
        private struct Row { public GameObject Root; public Text Label, Value; }
        private readonly Row[] rows;
        private int count;

        public StatRows(Transform parent, int capacity)
        {
            rows = new Row[capacity];
            for (int i = 0; i < capacity; i++)
            {
                var node = HudKit.Node(parent, "Stat" + i);
                HudKit.Size(node, height: 30);
                HudKit.Row(node, 8);
                var label = HudKit.Label(node, "Label", 21, HudKit.TextMuted);
                HudKit.Size(label, flexibleWidth: 1);
                var value = HudKit.Label(node, "Value", 22, HudKit.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
                HudKit.Size(value, width: 120);
                rows[i] = new Row { Root = node.gameObject, Label = label, Value = value };
                node.gameObject.SetActive(false);
            }
        }

        public void Begin() => count = 0;

        public void Add(string label, string value, Color? color = null)
        {
            if (count >= rows.Length) return;
            ref Row row = ref rows[count++];
            HudKit.SetActive(row.Root, true);
            row.Label.text = label; row.Value.text = value;
            row.Value.color = color ?? HudKit.Ink;
        }

        public void End()
        {
            for (int i = count; i < rows.Length; i++) HudKit.SetActive(rows[i].Root, false);
        }
    }
}
