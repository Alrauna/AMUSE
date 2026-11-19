# Atlasing migration characterization

Privacy note: This record contains public synthetic fixture results only. It contains no private asset observations, machine identifiers, or vendor source excerpts.

Date: 2026-11-17.
Status on 2026-11-17: Native prefab characterization and current preset characterization completed. These results support ordinary field initializers for new settings. They do not prove migration of atlas fields that do not exist yet. No product code changed.

## Question

Can Unity distinguish a missing default-true field from an explicitly stored false without a new migration marker?

The controlling design is `docs/superpowers/specs/2026-11-16-merge-ready-atlasing-design.md`. Gate G0.5 is in `docs/superpowers/plans/2026-11-16-0-2-0-atlasing-implementation-plan.md`.

## Method

The dev editor used Unity 2022.3.22f1. Its exact asset directory matched the repository before the operations. No operation targeted the private editor.

The spike used the real `AmuseAvatarOptimizer` component. Its existing `_alphaSeparatorEnabled` field has a true initializer. Its Reset method also restores true. This field served as a proxy for a future default-true atlas field.

A new public synthetic GameObject was created in an isolated preview scene. The preview scene also contained a Camera and a Directional Light. The fixture component stored two false choices and a custom coverage value of 37. The existing cutout backfill marker was true. This kept the old cutout migration separate from the experiment.

Unity saved only the fixture GameObject to a disposable native prefab. The spike removed the serialized `_alphaSeparatorEnabled` entry with a focused text edit. It retained `_allowDepthTestChange: 0` and the custom numeric setting. This modeled an older native payload without changing the component source.

The spike then imported the prefab synchronously and loaded it through `PrefabUtility.LoadPrefabContents`. It exercised native save and unload cycles, the first edit, Undo, editor Reset through `Unsupported.SmartReset`, and Undo of Reset. It did not substitute JSON overwrite for native prefab loading.

The preset checks invoked the real current parser. They used the existing public schema-five fixture. They did not implement or simulate a future schema migration.

## Observed results

All 14 native-fixture assertions completed on 2026-11-17.

| Scenario | Observed result |
| --- | --- |
| Input setup | The default-true field was absent. A different explicit false remained present. |
| Native load | The missing field became true. The stored false and custom numeric setting survived. |
| Save and reload | Unity saved the initialized true field. Both choices survived reload. |
| First edit | Changing the initialized true field to false survived save and native reload. |
| Undo of another edit | Changing that false to true and undoing restored false. |
| Editor Reset | Reset restored both true defaults and the default coverage value of 25. |
| Undo of Reset | Both false choices and the custom coverage value of 37 returned. |

Five current-parser scenarios completed on 2026-11-17.

| Input | Observed result |
| --- | --- |
| Valid schema five | Accepted. |
| Explicit false feature | Accepted with false preserved. |
| Missing existing required feature | Refused as MissingField with no preset. |
| Future schema version | Refused as UnknownSchemaVersion with no preset. |
| New atlas key in schema five | Refused as UnknownField with no preset. |

The first parser batch stopped at its third scenario because the spike expected the wrong refusal name. It expected MissingKey instead of the actual MissingField. The first two scenarios had completed. The corrected batch ran the remaining three scenarios. No product assertion or parser behavior changed.

The first native execution request was blocked before execution because its cleanup called `AssetDatabase.DeleteAsset`. The retry explicitly allowed deletion of the newly created fixture folder. It did not delete a pre-existing asset.

## Cleanup and Console

Cleanup completed on 2026-11-17. The spike unloaded its prefab contents. It cleared Undo records only for its own component. It closed its preview scene. It deleted the disposable folder through the AssetDatabase. A filesystem check confirmed that neither the folder nor its metadata remained.

No product script, source asset, existing scene, import setting, or preset file changed. No temporary script file was created. The fixture and its metadata were disposable as one unit.

The Console query returned two error rows about asynchronous readback. This spike made no readback request. The Console was not cleared. No clean-Console claim follows from this run.

## Engineering recommendation

Recommendation on 2026-11-17: Give each new serialized atlas field its agreed initializer and include that value in Reset. Do not add an atlas migration marker, nullable field scheme, or generic absence detector without evidence that the actual new representation needs it. The native proxy distinguishes missing from explicit false without any of those mechanisms.

Preset files need a separate bounded version branch. The current closed schema refuses new keys and missing old keys. Preserve that behavior for schema five. A future parser can accept a valid schema-five record, retain all existing values, and add the agreed atlas defaults. A new schema must preserve explicitly stored choices. Do not make old required keys optional to implement that migration.

This recommendation does not change the existing cutout backfill. That migration copies earlier user values rather than merely applying a new field default.

## Limits and remaining acceptance

As of 2026-11-17, G0.5 has supporting evidence but is not fully closed.

- The atlas component fields and new preset schema do not exist yet. Their exact consumer tests remain required.
- The native proxy covered a plain prefab, not prefab variants, scene serialization, or a script/domain reload.
- Reset and Undo ran through editor APIs. This was not a visual inspector interaction test.
- No Unity Test Runner suite ran. These are disposable characterization assertions, not RED/GREEN evidence for a feature.
- No new settings snapshot or migration implementation is authorized by these results.

The simplest proposed representation now has native evidence. Keep the remaining acceptance checks tied to the actual new fields and their consumers rather than build a migration framework first.
