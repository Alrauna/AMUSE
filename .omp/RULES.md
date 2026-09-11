Never commit private avatars, purchased or unredistributable assets, credentials, or Census Lab content.
Never expose private Census names, paths, GUIDs, per-avatar/per-renderer rows, or fingerprint-like identifiers.
Never modify source meshes, materials, textures/import settings, animation assets, prefabs, or scenes to make AMUSE succeed.
Treat Unity assets and `.meta` files as one unit. Never churn GUIDs.
Before a Unity MCP write, broad operation, or reported validation, verify and pin the exact intended `Application.dataPath`.
Never blanket-catch programming or invariant failures into "unsupported."
`Packages/com.alrauna.amuse.research` must never ship in the product/VPM package.
Never record an absolute or machine-specific path in any document, comment, commit message, or test. Use a `<repo-root>`-relative path or an angle-bracket placeholder.
Never record host names, user account names, home-directory paths, ports, or Unity MCP instance names and hashes. Name the role of a machine or an instance, not its identity.
Every English text that a human reads uses ASD-STE100 Simplified Technical English: short active sentences, one idea per sentence, no semicolons, and no contractions.
Treat every local file as future committed content. The private-data rules bind scratch, plans, reports, and handoff notes.
Name machines and instances by role only. Write `the dev editor instance` or `the Census Lab editor instance`. Never write an instance name, hash, port, GUID, or test-job identifier. Record test results as observed counts.
Characterize private avatars by role. Never write avatar, renderer, material, animation clip, controller, or hierarchy names. Replace exact counts with ranges. Never write per-renderer or per-slot tables. A sanitized record opens with a privacy note that states the sanitization.
Date every status claim in a record. Never write an unqualified `today` or `currently`.
Before you finish document work, sweep the changed files for identifiers: an at sign joined to a hexadecimal hash, drive-letter paths, home-directory paths, four-digit ports, and private asset names learned in the session. Every hit is a defect. Fix the hits before you report.