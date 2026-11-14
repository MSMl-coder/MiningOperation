# v1.3 — GPU field rendering, URP rock material, Foundation test scene setup

**New**
- Runtime/Fields/AsteroidFieldFarRenderer.cs: shared low-poly variant meshes, Graphics.RenderMeshInstanced, no per-rock GameObject at far distance.
- Runtime/Fields/AsteroidFieldDiagnostics.cs: Play Mode performance/field counters.
- Runtime/Fields/AsteroidTestFlightCamera.cs: minimal test camera controls.
- Editor/AsteroidTestEnvironmentSetup.cs: converts CURRENT 02_EmptyGame scene without deleting Foundation runtime; generates resource records in Editor.
- Shaders/AsteroidRockURP.shader: 3D noise coloration, fine bump, lighting and near-rock shadows.
- Shaders/AsteroidFarURP.shader: cheap instanced variant with vertex colors.
- Tests/Editor/AsteroidRenderingV13Tests.cs: generation & streaming regressions.

**Changed**
- AsteroidFieldSettings.cs: additive rendering options; existing settings and saves remain valid.
- AsteroidFieldController.cs: near-only GameObjects (default 1800 m, 28 max); far instanced; old children cleared on Play start in optimized mode.
- AsteroidGeometryGenerator.cs: deterministic rock-tone vertex colors, physics mesh skips visual-only work, v1 legacy meshes get white vertex colors.
- AsteroidGeneration.cs: faster volume mesh without vertex-color noise.

**Unchanged**
- AsteroidFieldSnapshot, save schema, ID/seed generation, class composition, field layout algorithms, extraction-by-ID, existing Editor windows.
