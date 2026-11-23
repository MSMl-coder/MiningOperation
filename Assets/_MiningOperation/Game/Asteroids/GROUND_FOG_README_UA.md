# Mining Operation — Ground Fog URP v0.1 (experimental)

Branch: `feature/ground-fog-urp-rebased-20261123`

## Purpose
An **optional** low, flattened visual gas mode built on the existing gas cloud records and GPU raymarching pipeline. This does **not** replace the working Volumetric gas presentation or modify asteroid snapshots, field IDs, mineral resources, gas JSON schema, or the project's global URP renderer.

## Updated files
- `AsteroidFieldSpawnPoint.cs`: new `MOGasPresentation.GroundFog = 2`, world-space floor, height, lateral scale, softness, 4 local Light references.
- `AsteroidFieldSettings.cs` and `MOSpawnOverrides.cs`: independent fog material slots.
- `MOGasFieldRenderer.cs`: routes the new option through a separate mesh and shader, retaining SoftParticles and Volumetric.
- `MOGroundFogMeshBuilder.cs`: creates a single mesh of flattened ellipsoids whose centers are at world-space `floorY + height/2`.
- `MO_GroundFogURP.shader`: depth-clipped raymarching with noise, vertical fade and simple local light scattering. **Approximate** lighting, not physical volumetric shadows.
- `MOVisualAssetInstaller.cs`: generates `MO_GroundFogURP_v01.mat` when installing gas assets; existing assets are not overwritten.
- `AsteroidFieldStudio.cs`: controls available in VISUALS.
- `MOGroundFogTests.cs`: edit-mode tests.

## Setup
1. Save your scene and commit your current working state before using this branch.
2. In URP Renderer Data, **disable the old MO_HeightFog Full Screen Pass** if you added it. The new mode needs no Renderer Feature.
3. Open `Tools → Mining Operation → Asteroid Field Studio`.
4. Select an existing **Gas Spawn Point**. Its existing gas record layout is retained.
5. Under **VISUALS → Rendering mode**, choose **GroundFog**.
6. If the material was not created yet, use the existing **Install / Repair Ice & Gas Assets** action (the install method now creates the new material). You can also use BUILD CLOUD VISUALS when it installs missing shared materials, but that command **rebuilds the gas cloud layout**: do not use it if preservation of your existing layout matters.
7. Set **Base world Y = 0**, **Height = 120**, **Horizontal radius multiplier = 1.5**, **Soft upper fade = 0.7**.
8. Set **Raymarch samples = 12–16**, **Opacity = 0.15–0.25**, **3D noise scale ≈ 1**, **Density Variation ≈ 0.6**, **Gentle drift ≈ 0.05–0.15**.
9. Optional: drag up to four Point Lights from the Hierarchy into **Local fog lights**. Light scattering is sampled when visuals are rebuilt; after moving a light, rebuild visuals to update its position.
10. The Field Studio queues a safe `RebuildVisuals()` when these visual settings are edited, without rebuilding saved cloud records. Save your scene and test in Game view.

## Notes
- This first version renders **flattened individual cloud clusters**, not an infinitely tiled full-map blanket. Increase each cloud radius and cluster count if you want more coverage; adding clouds via BUILD changes the procedural cloud layout.
- The fog is anchored to world Y, so it still appears around Y = 0 when the Gas Spawn Point is at Y = -3371.
- It only affects the chosen Gas Spawn Point. To create fog without changing an existing gas region, create a **separate Gas Spawn Point**, assign new field ID/seed and switch its rendering to GroundFog.
- Lighting from local Point/Spot Lights is a radial approximation. Actual Spot cone attenuation, physical scattering, volumetric shadows and transparent-depth occlusion are not implemented.
- This has **not** been compiled or tested inside Unity Editor / Metal. Run EditMode tests; if shader compilation logs errors, please share their full text.
