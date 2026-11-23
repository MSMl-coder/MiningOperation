# Per-formation AsteroidFieldSettings (Editor authoring)

When you click **+ Create Spawn Point** in `Tools → Mining Operation → Asteroid Field Studio`, the new Spawn Point now **automatically owns its own persisted** `AsteroidFieldSettings.asset`.

## Workflow
1. Select a working Spawn Point, or provide a valid existing Field Settings template.
2. Select new location content type and press **+ Create Spawn Point**.
3. Editor creates a new unique GUID/Field ID and writes an independent clone of the source `AsteroidFieldSettings` to `Assets/_MiningOperation/AsteroidAuthoring/Formations/`.
4. The new Spawn Point references **the clone**; the source field retains its original settings reference and values.
5. Subsequent changes to the cloned Field Settings numeric properties do not affect other formations.

Generated example filename:
`MO_Rock_BrokenBelt_BELT-abcdef1234567890_FieldSettings.asset`

Only the **Field Settings** object is duplicated. Its referenced generator templates, resource catalogs, textures and materials remain shared by design. For per-location generator shape edits, the existing `MOSpawnGeology` local override system remains available. To change an authored material without affecting other fields, create/assign a unique Material asset separately.

**Special cases:**
- First-time setup (`CreateReadyAssets`) already creates a unique Field Settings file; the first Spawn Point reuses this newly created file to avoid unnecessary template duplication.
- Existing authored and baked Spawn Points and legacy conversions retain their current Field Settings references. No saved asteroid records, field IDs, geometry or mineral resources are migrated or regenerated.
- Changing an existing Spawn Point's `settings` by hand is still permitted. A shared reference can be intentionally assigned manually.
- The editor creates **new** assets during Spawn Point creation, not every time BUILD RECORDS / BAKE is pressed.

## Validation
Test with two new Rock Spawn Points: the object references and `AssetDatabase.GetAssetPath` must differ. Change a numeric field in one `.asset`, and confirm the other is unchanged. Confirm both retain resource and material references. Unity EditMode tests: `MOIndependentFieldSettingsTests`.

The code was reviewed statically but **has not been compiled/tested in Unity Editor** in this environment.
