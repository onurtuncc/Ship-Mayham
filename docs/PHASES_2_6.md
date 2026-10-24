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
