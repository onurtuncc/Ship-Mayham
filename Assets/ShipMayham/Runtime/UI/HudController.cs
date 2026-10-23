using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>
    /// Owns the screen-space canvas, switches between the lobby menu and the in-voyage HUD,
    /// and tracks whether the player is currently using a gamepad so hints show the right glyphs.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        private PrototypeHud hud;
        private Canvas canvas;
        private MenuScreen menu;
        private GameHud game;
        private LobbyScreen lobby;
        private bool gamepad;

        public bool UsingGamepad => gamepad;
        public Canvas Canvas => canvas;
        public GameHud Game => game;
        public LobbyScreen Lobby => lobby;
        public MenuScreen Menu => menu;

        public static HudController Create(PrototypeHud owner)
        {
            var go = new GameObject("HudCanvas", typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            go.transform.SetParent(owner.transform, false);
            var controller = go.AddComponent<HudController>();
            controller.Build(owner);
            return controller;
        }

        private void Build(PrototypeHud owner)
        {
            hud = owner;
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(HudKit.ReferenceWidth, HudKit.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            HudKit.EnsureEventSystem();
            menu = new MenuScreen(owner, transform);
            lobby = new LobbyScreen(owner, transform);
            game = new GameHud(owner, transform);
        }

        private void Update()
        {
            if (hud == null) return;
            canvas.enabled = !hud.IsReloading;
            if (!canvas.enabled) return;
            DetectInputDevice();
            bool inSession = hud.Session != null && hud.Session.IsSpawned;
            bool inLobby = inSession && hud.Session.InLobby;
            menu.SetVisible(!inSession);
            lobby.SetVisible(inLobby);
            game.SetVisible(inSession && !inLobby);
            if (!inSession) { menu.Refresh(); return; }
            if (inLobby) { lobby.Tick(); return; }
            if (Keyboard.current?.f1Key.wasPressedThisFrame == true) game.ToggleHelp();
            game.Tick(Time.unscaledDeltaTime, gamepad);
        }

        private void DetectInputDevice()
        {
            var pad = Gamepad.current;
            if (pad != null && pad.wasUpdatedThisFrame &&
                (pad.leftStick.ReadValue().sqrMagnitude > .25f || pad.rightStick.ReadValue().sqrMagnitude > .25f ||
                 pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.buttonWest.isPressed || pad.buttonNorth.isPressed ||
                 pad.rightTrigger.isPressed || pad.startButton.isPressed))
            {
                gamepad = true;
                return;
            }
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if ((keyboard != null && keyboard.wasUpdatedThisFrame && keyboard.anyKey.isPressed) ||
                (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 1f || mouse.leftButton.isPressed)))
                gamepad = false;
        }
    }
}
