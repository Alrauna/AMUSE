# Title Subtext Design

Date: 2026-10-05. Base: `main` at `f319186`, branch `feature/optimizer-ui-rework`.

Investigation: `docs/superpowers/investigations/2026-10-05-title-subtext-investigation.md`.

## 1. Overview

The header gains one subtext line under the "AMUSE" title. The line
reads "Alrauna's Material Understanding and Simplification Engine". The
owner picked the "and" wording on 2026-10-05. The right-aligned version
label, the "Report a bug" button, and the placement help box keep their
behavior. No component state changes and no build behavior changes.

## 2. Draw order and style

`DrawHeader` draws, in order:

1. The 26-pixel title row: "AMUSE" centered, version right-aligned.
   Unchanged.
2. The new subtext line: the full product name, centered, in a style
   derived from `EditorStyles.centeredGreyMiniLabel` with `wordWrap` on.
3. The "Report a bug" button. Unchanged.
4. The 4-pixel space. Unchanged.

The subtext is a proper product name. It is not a sentence, so the
plain-English sentence rules do not rewrite it. A narrow inspector wraps
the line instead of clipping it.

## 3. Source of the text

One internal constant on the editor class holds the string:

```csharp
internal const string ProductSubtext =
    "Alrauna's Material Understanding and Simplification Engine";
```

No package metadata changes. `package.json` keeps
`"displayName": "AMUSE"`. The version label keeps reading the installed
package metadata.

## 4. Test strategy

Compile, the full EditMode suite, and a smoke check on the dev editor
instance: open the component inspector, see the subtext under the title,
and narrow the inspector to see the line wrap. No new test draws the
inspector. An assertion on the constant would pin source text, which the
repository conventions reject.

## 5. Documentation

None. The README does not describe the header.
