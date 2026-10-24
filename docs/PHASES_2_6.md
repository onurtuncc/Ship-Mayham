# GDD implementation branch — phases 2–6

Branch: codex/gdd-phases-2-6. Base: 441fef9. This isolated worktree does not merge into main.
The numbering follows the six-row user-facing plan, not the older numbered GDD sections.
Phase 1 onboarding belongs to another ongoing work and is not changed here.

## Phase 2 — navigation and workload

Three seeded shoals leave the centre corridor and both ports clear. Crossing quickly costs hull and speed;
below 1 m/s it is safe. Rocks appear on the minimap within 60 m, with a close-range warning.
Storm Passage adds a five-second directional rogue-wave warning: face within 30 degrees to avoid damage.
State, seed and timers replicate and migrate. Wave/event gaps and pirate fire rate use connected-crew pressure
(4/3/2/1: 1/.8/.6/.45), without changing passenger counts or economy. Active event deadlines are not reset.
NavigationPhaseTests covers layout, impact cooldown, safe speed, directional avoidance, scaling and migration.
Human balance and audio/visual feel still require player testing.

Phase 2 validation: 6/6 focused tests passed (Logs/Phase2.xml).

## Phase 3 — living passengers and service comedy

Patient, impatient and nervous passengers have different waiting/fear responses. At most two take short
walks into the aisle and return; blocked walkers time out safely. Pickup, servicing and rescue interrupt the routine.
R / D-pad down while looking at a passenger reassures them, with an anti-spam cooldown.
Spilled drinks appear on deck, lower nearby satisfaction and can be mopped with three E/A interactions.
The typed target, replicated state and migration include the new tasks; old seat roots and interaction IDs stay fixed.
The animation upgrade adds Walking and Standing states using existing Mixamo clips.
Phase 2+3 validation: 12/12 focused tests passed (Logs/Phase3.xml), exit 0.

## Phase 4 — engineering decisions

The electrical fire warns for six seconds before causing damage. Isolating it with R / D-pad down at POWER
stops fire damage and disables service counters. R at KITS equips/returns a red extinguisher; E at the fire
uses one of four shared charges. Two sprays extinguish it. Restore the power breakers afterwards.
At REPAIR, R cycles A/B/C valves; a pressure clue identifies the hot pipe. E applies the selected valve:
correct selection stops the leak without a kit; incorrect selection costs 2 hull. Existing manual and kit repair remain.
Isolation intentionally does not stop an unrelated hull leak.
Phases 2–4: 17/17 focused tests passed, exit 0 (Logs/Phase4-Final.xml).

## Phase 5 — alternative resolutions and sea opportunity

An inspected unpaid passenger can accept a cleaning job with R / D-pad down when a spill exists.
They walk to the spill and work for six seconds; the debt is settled without adding 40 ticket coins.
Manual payment or pickup interrupts unfinished work. A bounded task timeout prevents blocked NPC jobs.
The plug locker on deck supplies two temporary plugs: carry one to REPAIR to pause leak damage for 20 seconds.
The leak still needs a valve, manual patch or quick kit. Returned/disconnected plugs conserve inventory.
A sea chest appears off the direct route: approach within 15 m, slow below 1 m/s and E at the port-side recovery rope.
Its 30-coin bonus pays once on successful arrival, never on pickup. All task/countdown state migrates.
Phases 2–5: 22/22 focused tests passed, exit 0 (Logs/Phase5.xml).

## Phase 6 — equipment, spending and physical passages

At the purser, GEAR / PASSAGE offers a service tray (90 coins: two matching orders) or workshop
(90: +2 patch kits once per voyage, three-kit repair bench). Buy each once, then refit owned gear free.
The purchased decorative pennant (60) can be shown/hidden free; it never changes ship performance.
Choices must be made at port with empty hands, before boarding. Existing engine/seats/comfort remain under UPGRADES.
The direct practice passage remains available. Safe western and risky eastern passages require two ordered
physical gate visits before docking, paying +12/+25 respectively on successful arrival.
The western gates avoid the shoals; eastern gates run close to them. These are navigation paths, not new ports/maps.
The minimap and named world markers guide the next gate.
Career v2 stores purchased equipment/pennant and reads v1 saves without losing the wallet.
Migration snapshot 16 / network protocol 20 includes all new state: use matching builds for all peers.
Any future integration with the other development branches must reconcile/rebump protocol versions together.
PreparationPhaseTests covers inventory conservation, repeat purchases, kit-grant farming, course gates and save compatibility.
Phases 2–6 plus career checks: 35/35 focused tests passed (Logs/Phase6-Focused.xml).

## Integration and presentation notes

New gameplay remains in focused host-owned VoyageSimulation partials; presentation does not authorize actions.
RPC commands validate sender slot, phase, range, station use, jumping and held items.
All new simulation state replicates and survives host migration, including in-progress timers and NPC jobs.
The scene's existing IDs, station anchors and seat roots are unchanged.
PrototypeNavigationView and PrototypeDeckLifeView generate named primitive presentation roots at runtime;
these are deliberately temporary visualizations, not final art. Replace the visual creation in these presenters
without moving authoritative points (FirePoint, PlugLocker, RescueRope, CourseGate and ShoalPosition).
The passenger controller reuses the project's existing walking/idle clips; no new asset downloads are required.

The full regression run caught a MaterialPropertyBlock constructed during MonoBehaviour initialization;
it is now created in Start, and a real-scene RPC integration test covers the new presenters.
Legacy event/combat timing tests now explicitly use four connected crew for baseline cadence.
Old-snapshot rejection tests assert the JSON version really changed before checking rejection.
Human four-person playtesting is still required for balance, route readability, comedy and controller feel.
