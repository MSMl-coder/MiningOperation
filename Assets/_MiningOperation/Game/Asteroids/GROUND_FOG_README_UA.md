# Ground Fog v0.3 — Fast Surface (URP)

**Чому змінили:** попередній режим `VolumetricCloudlets` виконував багатокроковий raymarching у кожній із багатьох хмар, що перекривались. На користувацькій сцені спостерігали ~21 FPS; показник належить старому алгоритму, не є виміром v0.3.

## Новий стандартний режим
У `Tools → Mining Operation → Asteroid Field Studio → VISUALS`, для Gas обери:
- `Rendering mode = GroundFog`
- `Fog rendering quality = FastSurface` (типове значення навіть для старих сцен)
- `Base world Y = 0`
- `Height = 120`
- `Surface grid resolution = 96` (спробуй 48 для великих полів)
- `Cloud top height variation = 0.8`
- `Cloud shape irregularity = 0.85`
- `Cloud gaps / empty pockets = 0.55`
- `Opacity = 0.1–0.2`

**Для вже наявної сцени:** якщо немає матеріалу, у вкладці VISUALS натисни `INSTALL FAST FOG MATERIAL (keep existing layout)`; створиться власний `MO_GroundFogFastURP_v03.mat` і повторно збереться **лише візуальна сітка**, без перегенерації газових записів. `Build Cloud Visuals` для цього не потрібна.

## Технічна схема
- **Один** згенерований heightfield-меш для всього поля замість 12×13 та більше об'ємів;
- CPU-згенеровані під час authoring/Start карта висот і альфа-густина зі збережених газових центрів; жодного перерахунку меша в кожному кадрі;
- один простий URP шейдер `MiningOperation/GroundFogFastURP`, один текстурний шум на піксель, без циклу raymarching;
- одна прозора поверхня із розмитими краями, нерівною висотою і просвітами; світло від до 4 джерел приблизне;
- відстань між камерою та поверхнею контролюється звичайним depth test; туман є **візуальною імітацією**, а не фізичним об'ємним середовищем.

`Fog rendering quality = VolumetricCloudlets` повертає попередній важкий режим, якщо потрібно порівняти зображення. Від `Gas / Volumetric` він незалежний.

**Обмеження:** поверхня найкраще читається з камери над полем; збоку або знизу вона не відтворює глибокий 3D-туман. У межах одного Spawn Point — один Draw MeshRenderer (не гарантується як одна GPU draw call для всіх сцен). Прозорість усе одно потребує blend. Необхідно перевірити результат і GPU frame time у Unity 6/Metal до merge.

## Як виміряти прискорення
1. Збережи сцену, обери той самий ракурс і роздільність Game View.
2. В Unity Profiler порівняй час **GPU Frame** та FPS при вимкненому fog, з FastSurface і з VolumetricCloudlets.
3. Перевір сцени з 1, 5, 13 кластерами та рух камери; за потреби зменшуй Surface grid resolution до 48.
4. Якщо помилки імпорту/шейдера — скопіюй текст Console. Рушій у цьому середовищі не запускався.

---

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

## Ground Fog v0.2 — cloudlet formation

A single saved Gas cluster is now a **group of deterministic subclouds**, rather than one enormous smooth ellipsoid. The generated subclouds are purely visual; the gas JSON, IDs and saved cloud centers do not change.

In **VISUALS** choose `Rendering mode = GroundFog` and tune:

| Property | Suggested starting value | Function |
| --- | ---: | --- |
| Small clouds per cluster | 12 | Subdivides one gas cluster into many overlapping cloudlets |
| Cloud top height variation | 0.8 | Different billow heights; no uniform ceiling |
| Cloudlet spread | 0.8 | Width of the subcloud grouping |
| Cloud shape irregularity | 0.85 | Distorts boundaries, emphasizing structure |
| Cloud gaps / empty pockets | 0.55 | Higher values remove uniform grey fill and create openings |
| Soft upper fade | 0.65 | Makes the top of every cloudlet softer |
| Raymarch samples | 12–16 | Quality/performance tradeoff |
| Opacity | 0.1–0.2 | Start modestly; overlapping cloudlets accumulate opacity |

The renderer builds **one combined mesh** for each Gas Spawn Point; it does not create additional GameObjects or change saved gas records.

**GPU cost:** cloudlets increase overdraw, even though they share a mesh; a dozen gas clusters with 12 cloudlets each can be expensive at high resolution. For larger fields start with 4–8 cloudlets/cluster, 8–12 raymarch samples, and profile GPU time in Unity.

**Color:** for the dark Falling Frontier look, darken the per-spawn Gas colors. The currently pale gas palette may still read as grey in a brightly lit scene. Use restrained local lighting. Changes to existing Gas/Volumetric mode remain isolated.

**Validation:** the additional deterministic-cloudlet tests have not been run in Unity in this environment. Check compilation and Game View before merging.

## Notes
- This first version renders **flattened individual cloud clusters**, not an infinitely tiled full-map blanket. Increase each cloud radius and cluster count if you want more coverage; adding clouds via BUILD changes the procedural cloud layout.
- The fog is anchored to world Y, so it still appears around Y = 0 when the Gas Spawn Point is at Y = -3371.
- It only affects the chosen Gas Spawn Point. To create fog without changing an existing gas region, create a **separate Gas Spawn Point**, assign new field ID/seed and switch its rendering to GroundFog.
- Lighting from local Point/Spot Lights is a radial approximation. Actual Spot cone attenuation, physical scattering, volumetric shadows and transparent-depth occlusion are not implemented.
- This has **not** been compiled or tested inside Unity Editor / Metal. Run EditMode tests; if shader compilation logs errors, please share their full text.
