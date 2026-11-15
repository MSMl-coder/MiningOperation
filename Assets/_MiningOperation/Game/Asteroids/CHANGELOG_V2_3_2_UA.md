# v2.3.2 — Unified Editor / Gas State Stabilization

- **FIX:** Gas plan totals now equal the configured gas mass; weighted randomization only allocates shares among cloud records.
- **SAVE:** Optional `authoringSignature` field identifies whether saved gas deposits match the current layout settings. Legacy JSON is preserved.
- **UX:** Gas appearance parameters moved to VISUALS; gas shape tab no longer exposes unrelated rock geology/LOD controls.
- **UX:** Rock/Ice/Gas display only relevant visual fields; explicit “Apply Appearance” does not destroy gas progress.
- **UX:** Preview invalidation compares current settings to last preview. Gas 2D preview reflects configured colors.
- **RENDERING:** Gas particle alpha responds to remaining resources; visual updates coalesced into one refresh every 0.5 seconds.
- **RUNTIME:** Old gas visuals automatically hide when Spawn Point switches back to Rock/Ice.
- **ASSETS:** Ice/Gas material installer is idempotent and preserves compatible user materials and import settings.
- **VALIDATION:** Rejects nonfinite or negative requested gas mass; warns about empty persistent Gas ID.
- **TESTS:** Six EditMode regression tests added.

No changes to asteroid resource classes, asteroid shape/LOD serialization or mining field persistence schema. Unity Editor verification still required.
