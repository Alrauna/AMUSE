---
alwaysApply: true
---

- Never commit, push, open/merge PRs, or delete/rewrite branches without explicit task authorization.
- Never commit private avatars, purchased/unredistributable shaders/assets, credentials, or Census Lab content.
- Census Lab privacy: never expose private names, paths, GUIDs, per-avatar/per-renderer rows, or fingerprint-like identifiers; only Tier 3 privacy-reviewed aggregates may leave the Lab.
- Never modify source meshes, materials, textures/importer settings, animation assets, prefabs, or scenes to make AMUSE succeed.
- Treat Unity assets and `.meta` files as one unit; never churn GUIDs.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or generated IDE state.
- Before any Unity MCP write or broad operation: verify the target instance's `Application.dataPath` exactly (normalized) matches the intended project; on mismatch or ambiguity, stop.
- Never blanket-catch failures into "unsupported"; programming/invariant failures remain defects.
- Never claim a test, build, or validation passed unless it was actually run and observed.
- `Packages/com.alrauna.amuse.research` must never ship in the product/VPM package.
