# HUD and interaction UI

The interface is a runtime-built uGUI canvas (`Assets/ShipMayham/Runtime/UI`). Nothing is serialized into the
scene: `PrototypeHud.Awake` creates a `HudController` in play mode, so scenes and prefabs are unchanged.
`PrototypeHud` keeps the networking/session flow only; it exposes `Status`, `JoinCode`, `ConnectionAttempted`,
`LeaveSession()` and similar members for the UI to read.

## Screens

| Screen | Class | Shown when |
| --- | --- | --- |
| Lobby | `MenuScreen` | No voyage session is spawned (create / join online voyage, play solo, cancel). |
| Voyage HUD | `GameHud` | Session spawned. |
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

`PrototypeView.FindTarget` still produces `FocusText` (kept for tests and logs) and now also a structured
`InteractionPrompt` (`View.Prompt`) built by the pure functions in `PromptCatalog`. A *targeted* prompt (object the
crosshair is on) is drawn as a card with title, key cap, action, detail line and an optional coin cost. Prompts
without a target (`Targeted == false`) are drawn as the bottom hint strip. A prompt with `HudControl.None` is
informational (nothing to press) and a `PromptTone.Blocked` prompt is drawn in red.

To add a new interactable: extend `PromptCatalog` (or build an `InteractionPrompt` where the target is found) and
set `Prompt` next to `FocusedTarget` in `PrototypeView`. Control glyphs live in `HudControls.Label`.

## Port panel

Only the sections usable at the player's position are shown: route and job buttons at the port office, job progress
while a job is running, paint choices at the paint dock. Buttons need a free cursor, so the panel reminds the player
to press `Esc` while the mouse look is captured.

## Testing

- `HudPromptTests` covers `PromptCatalog`, control glyphs and building the lobby canvas.
- `HudSceneTests` checks the lobby-to-HUD switch in the real scene. Run with `SHIPMAYHAM_CAPTURE=1` to render
  `Logs/HudLobby.png`, `Logs/HudGame.png` and `Logs/HudHelp.png` for visual review.
- `GameHud.CursorLockedOverride` lets captures show the aiming UI without a real pointer lock.
