using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>Shared look of the interface: palette, fonts, generated sprites and widget factories.</summary>
    public static class HudKit
    {
        public static readonly Color Panel = new Color(.055f, .09f, .13f, .82f);
        public static readonly Color PanelStrong = new Color(.04f, .07f, .1f, .94f);
        public static readonly Color PanelLight = new Color(1f, 1f, 1f, .08f);
        public static readonly Color Ink = new Color(.96f, .98f, 1f, 1f);
        public static readonly Color TextMuted = new Color(.67f, .75f, .82f, 1f);
        public static readonly Color Accent = new Color(1f, .78f, .28f, 1f);
        public static readonly Color Coin = new Color(1f, .82f, .2f, 1f);
        public static readonly Color Good = new Color(.4f, .84f, .55f, 1f);
        public static readonly Color Warning = new Color(1f, .6f, .22f, 1f);
        public static readonly Color Danger = new Color(.94f, .34f, .34f, 1f);
        public static readonly Color Info = new Color(.46f, .76f, 1f, 1f);
        public static readonly Color Food = new Color(1f, .55f, .2f, 1f);
        public static readonly Color Drink = new Color(.3f, .7f, 1f, 1f);

        public const float ReferenceWidth = 1920f, ReferenceHeight = 1080f;
        public const float Margin = 32f;

        private static Font font;
        private static Sprite rounded, circle;

        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>9-sliced rounded rectangle; use with Image.Type.Sliced.</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeSprite(64, 18, true);

        public static Sprite Circle => circle != null ? circle : circle = MakeSprite(32, 16, false);

        private static Sprite MakeSprite(int size, float radius, bool sliced)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            float half = size * .5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded box centred in the texture.
                float dx = Mathf.Abs(x + .5f - half) - (half - radius);
                float dy = Mathf.Abs(y + .5f - half) - (half - radius);
                float distance = Mathf.Sqrt(Mathf.Max(dx, 0) * Mathf.Max(dx, 0) + Mathf.Max(dy, 0) * Mathf.Max(dy, 0)) - radius;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(.5f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Box(Transform parent, string name, Color color, bool roundedCorners = true, bool raycast = false)
        {
            var image = Node(parent, name).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            if (roundedCorners) { image.sprite = Rounded; image.type = Image.Type.Sliced; }
            return image;
        }

        public static Image Dot(Transform parent, string name, Color color, float size)
        {
            var image = Box(parent, name, color, false);
            image.sprite = Circle;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }

        public static Text Label(Transform parent, string name, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal, bool shadow = true)
        {
            var text = Node(parent, name).gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            if (shadow)
            {
                var effect = text.gameObject.AddComponent<Shadow>();
                effect.effectColor = new Color(0, 0, 0, .55f);
                effect.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return text;
        }

        /// <summary>Vertical stack that sizes itself to its children.</summary>
        public static VerticalLayoutGroup Column(RectTransform rect, float spacing, RectOffset padding, bool fitHeight = true)
        {
            var group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing; group.padding = padding;
            group.childControlWidth = true; group.childControlHeight = true;
            group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            if (fitHeight) rect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return group;
        }

        public static HorizontalLayoutGroup Row(RectTransform rect, float spacing, TextAnchor alignment = TextAnchor.MiddleLeft, bool fitWidth = false)
        {
            var group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing; group.childAlignment = alignment;
            group.childControlWidth = true; group.childControlHeight = true;
            group.childForceExpandWidth = false; group.childForceExpandHeight = false;
            if (fitWidth) rect.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return group;
        }

        public static LayoutElement Size(Component component, float width = -1, float height = -1, float flexibleWidth = -1)
        {
            var element = component.gameObject.GetComponent<LayoutElement>() ?? component.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) element.preferredWidth = width;
            if (height >= 0) element.preferredHeight = height;
            if (flexibleWidth >= 0) element.flexibleWidth = flexibleWidth;
            return element;
        }

        public static Button MakeButton(Transform parent, string name, string label, Color tint, int fontSize = 24)
        {
            var image = Box(parent, name, tint, true, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1);
            colors.pressedColor = new Color(.78f, .78f, .78f, 1);
            colors.disabledColor = new Color(.5f, .5f, .5f, .55f);
            colors.fadeDuration = .06f;
            button.colors = colors;
            var text = Label(image.transform, "Label", fontSize, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform, 6);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.text = label;
            return button;
        }

        public static void SetLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null && text.text != label) text.text = label;
        }

        /// <summary>Small rounded key badge, e.g. [E].</summary>
        public static Text KeyCap(Transform parent, string name, int fontSize, out Image background)
        {
            background = Box(parent, name, new Color(1f, 1f, 1f, .94f));
            var row = Row(background.rectTransform, 0, TextAnchor.MiddleCenter);
            row.padding = new RectOffset(12, 12, 4, 4);
            background.gameObject.AddComponent<LayoutElement>().minWidth = fontSize * 1.9f;
            var text = Label(background.transform, "Key", fontSize, new Color(.07f, .1f, .14f), TextAnchor.MiddleCenter, FontStyle.Bold, false);
            return text;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
        }

        public static void SetText(Text text, string value)
        {
            if (text != null && text.text != value) text.text = value;
        }

        public static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
