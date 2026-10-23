# HUD and interaction UI

The interface is a runtime-built uGUI canvas (`Assets/ShipMayham/Runtime/UI`). Nothing is serialized into the
scene: `PrototypeHud.Awake` creates a `HudController` in play mode, so scenes and prefabs are unchanged.
`PrototypeHud` keeps the networking/session flow only; it exposes `Status`, `JoinCode`, `ConnectionAttempted`,
`LeaveSession()` and similar members for the UI to read.

## Screens

| Screen | Class | Shown when |
| --- | --- | --- |
| Main menu | `MenuScreen` | No voyage session is spawned. Pages: main (nickname, host online, join online, play solo, settings, quit), join (code), settings (sound). Connection status and cancel sit under the card. |
| Lobby | `LobbyScreen` | Online voyage in the lobby phase: join code with copy, four slots with ready state, Ready (guests) or Start voyage (host, enabled when everyone else is ready). |
| Voyage HUD | `GameHud` | Session spawned and the voyage has started. |
| Controls | `HelpOverlay` | `F1` toggles; glyphs follow the last-used device (keyboard or gamepad). |

## Voyage HUD layout (1920x1080 reference, `CanvasScaler` match 0.5)

- **Top-left**: voyage card (voyage number, phase chip, route, hull bar, speed/distance or ticket progress), then
  the alert stack (leak, power, pirates, cannon, ship event, crane) and the end-of-voyage report.
- **Top-centre**: current goal; red/amber banner for host migration problems or a lost connection.
- **Top-right**: Coins, Reputation, Food and Drinks pills (value changes flash and show a `+/-` delta), session
  code and `LEAVE`, the minimap.
- **Centre**: crosshair (tightens and tints when something is targeted) and the prompt card.
- **Bottom-centre**: notices (fade out) and the contextual hint strip for helm, cannon, crane and carried passengers.
- **Bottom-left**: what the local crew member is carrying. **Bottom-right**: port panel.

The click-to-resume area of `PrototypeView` is the central 48% x 60% of the screen, so no interactive panel may
extend into it.

## Interaction prompts

`InteractionTargeting.Find` (`Runtime/Interaction`) decides what the crosshair is on and returns an
`InteractionTarget`: a typed kind and index (station, passenger, harbor action, salvage bundle) plus the
`InteractionPrompt` (`View.Target`, `View.Prompt`) built by the pure functions in `PromptCatalog`. The eye position
and look direction are plain inputs, so no camera is needed to call it. A *targeted* prompt (object the
crosshair is on) is drawn as a card with title, key cap, action, detail line and an optional coin cost. Prompts
without a target (`Targeted == false`) are drawn as the bottom hint strip. A prompt with `HudControl.None` is
informational (nothing to press) and a `PromptTone.Blocked` prompt is drawn in red.

To add a new interactable: extend `PromptCatalog` (or build an `InteractionPrompt` where the target is found) and
add the case to `InteractionTargeting` and the input dispatch in `PrototypeView.Update`. Control glyphs live in `HudControls.Label`.

## Settings and the in-game menu

`GameSettings` (`Runtime/Settings`) holds the player's preferences on the device: master, music and effects volume,
interface size (small, normal, large), camera shake (off, low, full), look speed, controller rumble and tips. Setters clamp,
save and raise `Changed`; storage failures only log a warning. `SettingsPanel` edits them and is shown on the main menu's
Settings page and inside the in-game menu. `M` still mutes everything; the old on-screen sound button is gone.

`Esc` (gamepad Start) opens `PauseMenu`: Resume, Settings, "I'm stuck" and Leave. In a solo voyage the menu pauses the game
(`Time.timeScale = 0`); online it only frees the cursor. Losing the pointer lock also opens it, which is how the menu is
reached in a browser where `Esc` is consumed by the browser. "I'm stuck" calls `UnstuckRpc`: the host puts the crew member
back on the deck spawn (or the quay boarding spot ashore), drops what they hold or man, and starts a 30 s cooldown.

`HudController` applies the interface size by changing the canvas reference resolution; `Haptics` pulses the gamepad on
hull damage, cannon fire and a lost ship unless rumble is off.

## Purser panel

Talking to the purser (Manifest station) opens `PurserPanel`: the Passengers button boards passengers or, after a trip,
starts the next voyage; each upgrade row shows its level and price. The panel frees the cursor (`View.PurserOpen`) and
closes with `Esc`, `E`, the gamepad `B` button, the Close button, or when the voyage starts sailing. Gamepad users
reach the buttons through UI navigation.

## Port panel

Only the sections usable at the player's position are shown: route and job buttons at the port office, job progress
while a job is running, paint choices at the paint dock. Buttons need a free cursor, so the panel reminds the player
to press `Esc` while the mouse look is captured.

## Testing

- `HudPromptTests` covers `PromptCatalog`, control glyphs and building the lobby canvas.
- `HudSceneTests` checks menu, lobby and voyage HUD switching in the real scene (the lobby is entered by setting
  `Session.LobbyBeforeVoyage` before `ConnectSolo`). Run with `SHIPMAYHAM_CAPTURE=1` to render `Logs/HudMenu.png`,
  `HudJoin.png`, `HudSettings.png`, `HudLobby.png`, `HudGame.png`, `HudPrompt.png`, `HudContext.png` and `HudHelp.png`.
- `GameHud.CursorLockedOverride` lets captures show the aiming UI without a real pointer lock.
