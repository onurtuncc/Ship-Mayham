using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>
    /// The settings rows (volumes, interface size, camera shake, look speed, rumble, tips) bound to <see cref="GameSettings"/>.
    /// Used by the main menu and by the in-game menu; it follows the stored values, including after a reset.
    /// </summary>
    public sealed class SettingsPanel
    {
        private readonly Slider master, music, sfx, look;
        private readonly Text masterValue, musicValue, sfxValue, lookValue;
        private readonly Button soundButton, rumbleButton, tipsButton;
        private readonly Segmented uiSize, shake;
        private bool refreshing;

        public RectTransform Root { get; }

        public SettingsPanel(Transform parent)
        {
            Root = HudKit.Node(parent, "Settings");
            HudKit.Column(Root, 10, new RectOffset(0, 0, 0, 0), false);

            soundButton = ToggleRow("ALL SOUND", ToggleSound);
            master = SliderRow("MASTER VOLUME", 0, 1, v => GameSettings.MasterVolume = v, out masterValue);
            music = SliderRow("MUSIC", 0, 1, v => GameSettings.MusicVolume = v, out musicValue);
            sfx = SliderRow("SOUND EFFECTS", 0, 1, v => GameSettings.SfxVolume = v, out sfxValue);
            uiSize = SegmentedRow("INTERFACE SIZE", new[] { "SMALL", "NORMAL", "LARGE" }, i => GameSettings.UiSize = (UiSize)i);
            shake = SegmentedRow("CAMERA SHAKE", new[] { "OFF", "LOW", "FULL" }, i => GameSettings.CameraShake = (ShakeLevel)i);
            look = SliderRow("LOOK SPEED", GameSettings.MinLookSpeed, GameSettings.MaxLookSpeed, v => GameSettings.LookSpeed = v, out lookValue);
            rumbleButton = ToggleRow("CONTROLLER RUMBLE", () => GameSettings.Rumble = !GameSettings.Rumble);
            tipsButton = ToggleRow("TIPS", () => GameSettings.Tips = !GameSettings.Tips);

            var resetTips = HudKit.MakeButton(Root, "ResetTips", "SHOW ALL TIPS AGAIN", new Color(1, 1, 1, .12f), 20);
            HudKit.Size(resetTips, height: 46);
            resetTips.onClick.AddListener(TipStore.ResetAll);

            var reset = HudKit.MakeButton(Root, "Reset", "RESET TO DEFAULTS", new Color(1, 1, 1, .12f), 20);
            HudKit.Size(reset, height: 46);
            reset.onClick.AddListener(GameSettings.ResetToDefaults);
            Refresh();
        }

        public void Subscribe() { GameSettings.Changed += Refresh; Refresh(); }
        public void Unsubscribe() => GameSettings.Changed -= Refresh;

        private static void ToggleSound() => FeedbackSettings.Muted = !FeedbackSettings.Muted;

        public void Refresh()
        {
            refreshing = true;
            SetSlider(master, masterValue, GameSettings.MasterVolume, true);
            SetSlider(music, musicValue, GameSettings.MusicVolume, true);
            SetSlider(sfx, sfxValue, GameSettings.SfxVolume, true);
            SetSlider(look, lookValue, GameSettings.LookSpeed, false);
            uiSize.Select((int)GameSettings.UiSize);
            shake.Select((int)GameSettings.CameraShake);
            HudKit.SetLabel(soundButton, FeedbackSettings.Muted ? "OFF" : "ON");
            HudKit.SetLabel(rumbleButton, GameSettings.Rumble ? "ON" : "OFF");
            HudKit.SetLabel(tipsButton, GameSettings.Tips ? "ON" : "OFF");
            refreshing = false;
        }

        private static void SetSlider(Slider slider, Text label, float value, bool percent)
        {
            slider.SetValueWithoutNotify(value);
            label.text = percent ? Mathf.RoundToInt(value * 100) + "%" : value.ToString("0.0") + "x";
        }

        private Slider SliderRow(string caption, float min, float max, UnityAction<float> onChange, out Text valueText)
        {
            var row = Row(caption);
            var slider = HudKit.MakeSlider(row, caption, min, max, min, value => { if (!refreshing) onChange(value); });
            HudKit.Size(slider, width: 230, height: 36);
            valueText = HudKit.Label(row, "Value", 20, HudKit.TextMuted, TextAnchor.MiddleRight);
            HudKit.Size(valueText, width: 64);
            return slider;
        }

        private Button ToggleRow(string caption, UnityAction onClick)
        {
            var row = Row(caption);
            var button = HudKit.MakeButton(row, caption, string.Empty, new Color(.25f, .5f, .7f), 20);
            HudKit.Size(button, width: 300, height: 40);
            button.onClick.AddListener(onClick);
            return button;
        }

        private Segmented SegmentedRow(string caption, string[] labels, UnityAction<int> onChange)
        {
            var row = Row(caption);
            return new Segmented(row, labels, 300, 40, index => { if (!refreshing) onChange(index); });
        }

        private RectTransform Row(string caption)
        {
            var row = HudKit.Node(Root, caption + "Row");
            HudKit.Size(row, height: 44);
            HudKit.Row(row, 12);
            var label = HudKit.Label(row, "Caption", 20, HudKit.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            label.text = caption;
            HudKit.Size(label, flexibleWidth: 1);
            return row;
        }

        /// <summary>A row of buttons of which exactly one is highlighted.</summary>
        private sealed class Segmented
        {
            private static readonly Color Off = new Color(1, 1, 1, .12f), On = new Color(.25f, .5f, .7f);
            private readonly Button[] buttons;

            public Segmented(Transform parent, string[] labels, float width, float height, UnityAction<int> onChange)
            {
                var holder = HudKit.Node(parent, "Segments");
                HudKit.Size(holder, width: width, height: height);
                HudKit.Row(holder, 6).childForceExpandWidth = true;
                buttons = new Button[labels.Length];
                for (int i = 0; i < labels.Length; i++)
                {
                    int index = i;
                    buttons[i] = HudKit.MakeButton(holder, "Segment" + i, labels[i], Off, 17);
                    buttons[i].onClick.AddListener(() => onChange(index));
                }
            }

            public void Select(int index)
            {
                for (int i = 0; i < buttons.Length; i++) buttons[i].GetComponent<Image>().color = i == index ? On : Off;
            }
        }
    }
}
