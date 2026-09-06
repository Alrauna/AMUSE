# VPM packaging research - GitHub-only distribution

Date: 2026-09-06. Branch: `investigate/vpm-packaging` at `d84c72b` base. Method: three
parallel read-only research passes (spec, real-world survey, repo mapping) over official
docs, live listing JSONs, and this repository's workflows. No production code changed.

Labels: `[SOURCE]` is a fact read in a fetched file or URL. `[MEASURED]` is a fact from a
run. `[INFERENCE]` is a conclusion. `[DECISION NEEDED]` is a choice the controller makes.

## 1. Question and answer in one line

> What does it take to install com.alrauna.amuse from a VPM repository link in VCC and
> ALCOM, hosted purely on GitHub?

The repository was created from the official `vrchat-community/template-package`, which
already ships the complete VPM listing pipeline. The work is configuration and metadata,
not new infrastructure. `[SOURCE]`

## 2. The dormant pipeline this repo already has `[SOURCE]`

`.github/workflows/build-listing.yml` exists on `main`:

- Triggers: `workflow_dispatch`, `workflow_run` after "Build Release", and every release
  event (published/edited/deleted/...).
- Checks out `vrchat-community/package-list-action`, runs
  `build.cmd BuildRepoListing --current-package-name ${{ vars.PACKAGE_NAME }}`, and
  deploys the generated `Website/` to GitHub Pages with the official
  `configure-pages` / `upload-pages-artifact` / `deploy-pages` actions.
- The generated listing accumulates one version entry per GitHub release of the package,
  each carrying `zipSHA256` and the release-asset download URL.

`.github/workflows/release.yml` (verified in S13) produces the correctly rooted package
zip + unitypackage + version tag + release assets on manual dispatch.

## 3. Why nothing has run `[SOURCE]` `[MEASURED]`

Two repository-settings gaps, both discovered earlier for the release path:

1. Actions variable `vars.PACKAGE_NAME` is unset (verified via `gh variable list`).
   Both `release.yml` (config job) and `build-listing.yml` (listing job) gate on it.
   A dispatch today silently skips both.
2. GitHub Pages is not enabled for the repository. The listing deploy requires Pages
   with Source = "GitHub Actions".

## 4. The listing that will result `[SOURCE]`

Listing URL: `https://alrauna.github.io/AMUSE/index.json` (project Pages). Format
(VCC docs + fetched template-package listing; no formal JSON Schema exists - the
de facto machine schema is `vrc-get-vpm`'s structs, which ALCOM uses):

```json
{
  "name": "...", "author": "...", "url": ".../index.json", "id": "...",
  "packages": {
    "com.alrauna.amuse": {
      "versions": {
        "0.1.0": {
          "name": "com.alrauna.amuse", "displayName": "AMUSE",
          "version": "0.1.0", "unity": "2022.3", "description": "...",
          "author": { "name": "Alrauna", "email": "...", "url": "..." },
          "vpmDependencies": { "nadena.dev.ndmf": ">=1.14.4 <2.0.0-a" },
          "zipSHA256": "<computed by the action>",
          "url": "https://github.com/Alrauna/AMUSE/releases/download/0.1.0/com.alrauna.amuse-0.1.0.zip"
        }
      }
    }
  }
}
```

VCC-required version-entry fields: `name`, `version`, `displayName`, `url`,
`author.name`, `author.email`. AMUSE's `package.json` already carries all of them.
`[SOURCE]`

## 5. Real-world precedent (all fetched 2026-09-06) `[SOURCE]`

| Project | Listing URL | Hosting | Automation |
|---|---|---|---|
| VRChat template-package | `<owner>.github.io/<repo>/index.json` | Pages via Actions | release events + workflow_run |
| z3y | `z3y.github.io/vpm-package-listing/index.json` | Pages via Actions | push of `source.json` + dispatch, forked list-action |
| xtlcdn (JLChnToZ) | `xtlcdn.github.io/vpm/index.json` | Pages via Actions | official template listing repo, 13 `githubRepos` |
| Razgriz | `vpm.razgriz.one/index.json` | Pages, custom domain | template listing, `source.json` |
| anatawa12, bd_ | custom domains | non-Pages front ends for scale/caching | cross-repo workflows |

GitHub-only hosting is the norm, not a compromise. Custom hosts exist only for scale.

## 6. Gap table - what actually remains `[MEASURED]`

| Item | Status | Needed change |
|---|---|---|
| `vars.PACKAGE_NAME` | unset | Set Actions variable to `com.alrauna.amuse` (repo settings; needs controller action or authorization) |
| GitHub Pages | disabled | Enable, Source = GitHub Actions (repo settings) |
| First release | none exists | The listing builds from releases; the S13 stop gate (controller test + explicit go) still applies |
| `package.json` metadata | `license` field missing (repo LICENSE is MIT); `keywords`, `changelogUrl`, `documentationUrl` recommended by template | Small manifest edit |
| End-user install docs | README documents developer VPM CLI setup only | Add VCC/ALCOM install section: add NDMF repo (`https://vpm.nadena.dev/vpm.json`), then AMUSE listing URL; Add-to-VCC button (`vcc://vpm/addRepo?url=...`) |
| Cross-repo dependency UX | NDMF lives only in `dev.nadena.vpm`; no repo can declare another repo | Documented two-step onboarding (above); VCC/ALCOM flag the missing dependency otherwise `[SOURCE]` |

## 7. Decisions this research leaves open `[DECISION NEEDED]`

See the grilling round that follows this document. Summary: proceed with the dormant
official pipeline; who applies the two settings changes; release sequencing vs the
controller's own test; single-package now vs multi-package listing shape; prerelease
listing policy; keeping the unitypackage artifact; listing identity defaults vs custom;
manifest metadata; end-user docs shape; validation plan in VCC and ALCOM.

## 8. Privacy statement

No Census Lab data, no private names, and no machine-specific paths appear in this
document. All external URLs are public documentation or public listings fetched during
research.

## 9. Sources

- `https://vcc.docs.vrchat.com/vpm/repos`, `/vpm/packages`, `/guides/create-listing`
- `https://github.com/vrchat-community/package-list-action`, `.../template-package`
  (README, build-listing.yml, fetched `index.json`)
- `https://github.com/vrc-get/vrc-get` (`vrc-get-vpm` repository/manifest structs)
- Fetched listings: `vrchat-community.github.io/template-package/index.json`,
  `z3y.github.io/vpm-package-listing/index.json`, `xtlcdn.github.io/vpm/index.json`,
  `vpm.nadena.dev/vpm.json`, `vpm.anatawa12.com/vpm.json`
- This repository: `.github/workflows/{release,build-listing,pr}.yml`,
  `Packages/com.alrauna.amuse/package.json`, `README.md`, `LICENSE`
