# Project engineering standards

This Unity game is intended for production. All code changes must be production-ready, reliable, and designed so the game can be extended without unnecessary rewrites.

## Architecture and implementation

- Follow the architecture, naming, and style already established in the relevant part of the project. Inspect neighboring code and project settings before making structural changes.
- Keep responsibilities focused and dependencies explicit. Prefer cohesive components and clear boundaries over monolithic classes, hidden global state, or unnecessary coupling.
- Design extension points around real gameplay needs. Avoid speculative frameworks and abstractions that make current behavior harder to understand.
- Preserve existing behavior unless the requested change requires otherwise. Keep changes scoped, and account for callers, serialized Unity references, scenes, prefabs, and asset compatibility when changing APIs or component behavior.
- Handle invalid state and failure paths deliberately. Validate external/configurable inputs, guard Unity object lifetimes and nullable references where appropriate, and provide useful diagnostics without silently swallowing errors.
- Respect Unity lifecycle and serialization rules. Avoid allocations and expensive work in hot paths; clean up event subscriptions, coroutines, and other owned resources reliably.
- Keep gameplay logic testable where practical, and update or add focused tests when behavior changes and the project has an appropriate test setup.
- Update relevant documentation or configuration when behavior, setup, or operational requirements change.

## Verification and completion

- Review the final diff for correctness, maintainability, unintended scope, and compatibility with the project's Unity version and packages.
- Run relevant available validation, such as compilation, focused tests, or build checks, when feasible. Report checks that could not be run and any remaining limitations accurately.
- Do not claim code is error-free without evidence. Resolve issues found during implementation and leave the code in a state suitable for production review and deployment.
