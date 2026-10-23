using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>
    /// The in-game menu opened with Esc: resume, settings, "I'm stuck" and leave. In a solo voyage it also pauses the game;
    /// online the voyage keeps running so nobody waits for one player.
    /// </summary>
    public sealed class PauseMenu
    {
        private readonly PrototypeHud hud;
        private readonly GameObject root, mainView, settingsView;
        private readonly Text title, stuckLabel, hint;
        private readonly Button resume, stuck;
        private readonly SettingsPanel settings;
        private bool wasOpen;
        private float stuckReadyAt;
        private int shownSeconds = -1;

        public PauseMenu(PrototypeHud hud, Transform parent)
        {
            this.hud = hud;
            var dim = HudKit.Box(parent, "PauseMenu", new Color(.02f, .05f, .09f, .6f), false, true);
            root = dim.gameObject;
            HudKit.Stretch(dim.rectTransform);

            var card = HudKit.Box(dim.transform, "Card", HudKit.PanelStrong, true, true);
            HudKit.Anchor(card.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(680, 0));
            HudKit.Column(card.rectTransform, 12, new RectOffset(44, 44, 34, 34));
            title = HudKit.Label(card.transform, "Title", 44, HudKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudKit.Size(title, height: 58);

            var main = HudKit.Node(card.transform, "Main");
            mainView = main.gameObject;
            HudKit.Column(main, 12, new RectOffset(0, 0, 0, 0), false);
            resume = Button(main, "Resume", "RESUME", HudKit.Accent, 66, () => hud.View.CloseMenu(true));
            resume.GetComponentInChildren<Text>().color = new Color(.1f, .08f, .02f);
            Button(main, "SettingsButton", "SETTINGS", new Color(.25f, .5f, .7f), 58, () => ShowSettings(true));
            stuck = Button(main, "Stuck", "I'M STUCK", new Color(1, 1, 1, .14f), 54, Unstick);
            stuckLabel = stuck.GetComponentInChildren<Text>();
            Button(main, "Leave", "LEAVE VOYAGE", new Color(.8f, .3f, .3f, .85f), 54, hud.LeaveSession);
            hint = HudKit.Label(main, "Hint", 18, HudKit.TextMuted, TextAnchor.MiddleCenter);
            HudKit.Size(hint, height: 28);

            var settingsNode = HudKit.Node(card.transform, "SettingsView");
            settingsView = settingsNode.gameObject;
            HudKit.Column(settingsNode, 12, new RectOffset(0, 0, 0, 0), false);
            settings = new SettingsPanel(settingsNode);
            Button(settingsNode, "Back", "BACK", new Color(1, 1, 1, .14f), 50, () => ShowSettings(false));
            root.SetActive(false);
        }

        private static Button Button(Transform parent, string name, string label, Color tint, float height, UnityEngine.Events.UnityAction onClick)
        {
            var button = HudKit.MakeButton(parent, name, label, tint, 26);
            HudKit.Size(button, height: height);
            button.onClick.AddListener(onClick);
            return button;
        }

        private void ShowSettings(bool show)
        {
            HudKit.SetActive(mainView, !show);
            HudKit.SetActive(settingsView, show);
            title.text = show ? "SETTINGS" : Title();
            if (show) settings.Subscribe(); else settings.Unsubscribe();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject((show ? settingsView.GetComponentInChildren<Button>(true) : resume).gameObject);
        }

        private string Title() => hud.Session != null && hud.Session.IsSoloVoyage ? "PAUSED" : "MENU";

        private void Unstick()
        {
            if (Time.unscaledTime < stuckReadyAt) return;
            stuckReadyAt = Time.unscaledTime + VoyageSimulation.UnstuckCooldown;
            hud.Session.UnstuckRpc();
            hud.View.CloseMenu(true);
        }

        public void Tick()
        {
            bool open = hud.View != null && hud.View.MenuOpen;
            HudKit.SetActive(root, open);
            if (!open)
            {
                if (wasOpen) { settings.Unsubscribe(); wasOpen = false; }
                return;
            }
            if (!wasOpen)
            {
                wasOpen = true;
                shownSeconds = -1;
                ShowSettings(false);
            }
            int remaining = Mathf.CeilToInt(Mathf.Max(0, stuckReadyAt - Time.unscaledTime));
            if (remaining != shownSeconds)
            {
                shownSeconds = remaining;
                stuckLabel.text = remaining > 0 ? $"I'M STUCK ({remaining}s)" : "I'M STUCK";
                stuck.interactable = remaining == 0;
            }
            HudKit.SetText(hint, hud.Session.IsSoloVoyage ? "The voyage is paused." : "The voyage keeps running.");
        }
    }
}
