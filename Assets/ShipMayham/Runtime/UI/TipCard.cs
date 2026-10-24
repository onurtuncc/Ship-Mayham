using UnityEngine;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>
    /// Shows first-time tips one at a time. Reads the local crew member's situation, asks <see cref="TipLogic"/> what applies,
    /// shows it for a few seconds and remembers it in <see cref="TipStore"/>. Never changes the voyage.
    /// </summary>
    public sealed class TipCard
    {
        public const float ShowSeconds = 9f, FadeSeconds = .6f, CheckInterval = .25f, GapSeconds = 2.5f;

        private readonly GameObject root;
        private readonly CanvasGroup group;
        private readonly Text body;
        private float nextCheck, shownAt = float.NegativeInfinity, hiddenAt = float.NegativeInfinity;
        private TipId? current;

        public TipCard(Transform parent)
        {
            var box = HudKit.Box(parent, "Tip", HudKit.PanelStrong);
            root = box.gameObject;
            HudKit.Anchor(box.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(HudKit.Margin, 150), new Vector2(520, 0));
            HudKit.Column(box.rectTransform, 6, new RectOffset(22, 22, 14, 16));
            group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var caption = HudKit.Label(box.transform, "Caption", 15, HudKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            caption.text = "TIP";
            body = HudKit.Label(box.transform, "Body", 21, HudKit.Ink, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.supportRichText = true;
            root.SetActive(false);
        }

        public TipId? Current => current;
        public bool Visible => root.activeSelf;

        /// <summary>Advances the card. <paramref name="context"/> is only evaluated at <see cref="CheckInterval"/>.</summary>
        public void Tick(float now, System.Func<TipContext> context, bool gamepad, bool blocked)
        {
            if (current.HasValue)
            {
                float age = now - shownAt;
                bool expired = age >= ShowSeconds || !GameSettings.Tips;
                if (expired) { Hide(now); return; }
                group.alpha = Mathf.Clamp01(Mathf.Min(age / FadeSeconds, (ShowSeconds - age) / FadeSeconds));
                if (blocked) return;
            }
            if (!GameSettings.Tips || blocked || now < nextCheck) return;
            nextCheck = now + CheckInterval;

            var ctx = context();
            var pick = TipLogic.Pick(ctx, TipStore.SeenMask);
            if (!pick.HasValue) return;
            if (current.HasValue)
            {
                if (!(TipLogic.IsUrgent(pick.Value) && !TipLogic.IsUrgent(current.Value))) return;
            }
            else if (now - hiddenAt < GapSeconds && !TipLogic.IsUrgent(pick.Value)) return;
            Show(pick.Value, now, gamepad);
        }

        /// <summary>Hides the card without marking anything (leaving the voyage).</summary>
        public void Reset()
        {
            current = null;
            hiddenAt = float.NegativeInfinity;
            root.SetActive(false);
        }

        private void Show(TipId id, float now, bool gamepad)
        {
            current = id;
            shownAt = now;
            body.text = TipLogic.Render(TipLogic.Template(id), gamepad);
            group.alpha = 0;
            root.SetActive(true);
            TipStore.MarkSeen(id);
        }

        private void Hide(float now)
        {
            current = null;
            hiddenAt = now;
            root.SetActive(false);
        }
    }
}
