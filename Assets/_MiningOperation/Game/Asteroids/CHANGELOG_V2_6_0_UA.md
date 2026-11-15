# Mining Operation v2.6.0 — retro sci-fi + 2D depth constraints

## Що додано

### 1) Обмеження генерації по осі Z для кожного Spawn Point
Тепер кожен `AsteroidFieldSpawnPoint` має власний блок **2D / depth constraint**:
- `Free3D` — без обмежень;
- `PlanarSlab` — генерація стискається в заданий діапазон по `Z`;
- `Strict2DPlane` — астероїди прив’язуються майже до однієї площини по `Z` з невеликим jitter.

Додатково можна ввімкнути:
- `Flatten rotations to plane` — обмежує нахили, щоб поле краще читалось у 2D-подібному геймплеї.

### 2) Retro sci-fi art direction для Spawn Point
У вкладці **VISUALS** з’явився блок **Art direction — this spawn**:
- `Default`
- `RetroSciFi`
- `RetroMonolith`
- `RetroCratered`
- `RetroStationReady`

Також додані локальні параметри:
- `Noise suppression`
- `Crater readability`
- `Silhouette simplification`

Ці параметри впливають на фінальну геометрію астероїдів під час генерації/перебудови поля.

### 3) Готовий пресет для Mining Operation
Кнопка **Apply Retro Sci-Fi Preset** автоматично:
- вмикає `PlanarSlab` для Z,
- робить форми чистішими та читабельнішими,
- зменшує зайвий поверхневий шум,
- залишає астероїди добре освітленими та придатними для стилю retro sci-fi.

### 4) Кнопка для простого ретро-фону та освітлення
У **VISUALS → Shared assets / installation** додано:
- `Setup retro sci-fi background & lights`

Вона налаштовує простий синій космічний фон без зайвого шуму, відключає планету та ставить більш читабельне освітлення.

---

## Які файли змінені
- `Runtime/Fields/MOSpawnOverrides.cs`
- `Runtime/Fields/AsteroidFieldSpawnPoint.cs`
- `Runtime/Fields/AsteroidFieldLayout.cs`
- `Runtime/Fields/AsteroidFieldComposer.cs`
- `Editor/AsteroidAuthoringWorkflow.cs`
- `Editor/AsteroidFieldStudio.cs`
- `Editor/AsteroidV2SceneSetup.cs`

---

## Важливо
- це оновлення **не змінює каталог ресурсів**;
- це оновлення **не змінює майнінговий сейв автоматично**;
- після зміни геометрії / art direction / Z constraint потрібно зробити **BUILD / REBUILD** для конкретного Spawn Point.
