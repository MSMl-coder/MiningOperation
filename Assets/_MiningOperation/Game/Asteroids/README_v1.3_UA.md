# Mining Operation — Asteroid Field + Graphics v1.3 (Unity 6 URP)

## Що саме змінено

**Проблема v1.2:** `spawnDistanceMeters = 13 000 м`, а типовий пояс має радіус близько 8500 м. Отже, поле могло створити до 120 GameObject одразу, кожний з окремими трьома Mesh/LOD + дочірніми об'єктами. Це неприйнятно як основний шлях візуалізації великих формацій.

**Рішення v1.3:**

1. Усі астероїди залишаються у `AsteroidFieldSnapshot` з постійним ID/seed/ресурсами/залишками, **без GameObject** для кожного запису.
2. Для віддалених каменів: до 12 спільних low-poly мешів із різними силуетами, матриці положення/масштабу для всіх видимих екземплярів, `Graphics.RenderMeshInstanced` з обмеженням 511 екземплярів на виклик. Окремих GameObject, MeshRenderer, MeshCollider та власних мешів для віддалених каменів **немає**.
3. Поблизу (`detailedDistanceMeters`, типово 1800 м): створюється унікальна детальна геометрія. Одночасно максимум `maxDetailedAsteroids` (типово 28) і вже існуючий `maxActiveAsteroids`. Створення дозовано параметром `instantiatePerFrame`.
4. На віддалі заради продуктивності видимий силует апроксимується одним із спільних мешів. **Перехід між far і near не є ідентичним** — можливий короткий візуальний pop. Дані, об'єм, маса та ресурси при переході незмінні.
5. Поверхня: процедурний URP шейдер без UV-текстур, 3D noise, розломи, мікрорельєф, матовий камінь, vertex-color варіативність, підтримка світла й тіней від головного джерела.
6. Унікальні меші тепер включають `Color32[]` геологічної варіативності; колір **не залежить від класу ресурсу**.
7. Старі v1/v2 записи, JSON і системи видобутку не змінюються; нові поля і Scene Settings не вимагають перегенерації.

## Оновлення існуючого проєкту

1. Зробіть commit / резервну копію Unity-проєкту.
2. Вміст `MiningOperation_Asteroids/Runtime/`, `Editor/`, `Tests/`, `Shaders/` скопіюйте до поточної папки модуля, у вашому випадку `Assets/_MiningOperation/Game/Asteroids/`.
3. **Не видаляйте з Unity існуючі `.meta` файли.** У нової версії ті самі шляхи `.cs` + нові файли, тому посилання на існуючі компоненти збережуться.
4. Зачекайте імпорту ShaderLab і компіляції C#.
5. Відкрийте `Tools → Mining Operation → Create Rock Materials` (створює матеріали в `Assets/_MiningOperation/AsteroidTestAssets/`).
6. У вашому `AsteroidFieldSettings` призначте `AsteroidRock_URP` у `Asteroid Material`, `AsteroidFar_URP` у `Distant Material`. Самі матеріали зі зміненим шейдером потребують URP.
7. Виберіть `useGpuFarField=true`, `detailedDistanceMeters=1800`, `maxDetailedAsteroids=28`, `distantMeshVariants=12`, `distantSubdivisions=1`, `distantDrawDistanceMeters=24000`, `meshColliders=false`. Стара дистанція `spawnDistanceMeters=13000` може залишатися, бо в оптимізованому режимі для унікальних мешів використовується **менше** зі старої і нової дистанцій.
8. Запустіть існуючу сцену в Play Mode. Коли є `Snapshot`, стан буде відновлено, а застарілі тимчасові GameObject прибрано в Play Mode. У сцені Edit Mode вони залишаються, доки ви не приберете їх через Field Studio або вручну.

### Оновлення тестової сцени Foundation

1. Відкрийте **існуючу** сцену `02_EmptyGame` у Unity Editor (саме її завантажує `FoundationBootstrap.Host.OpenEmptyGameAsync()`).
2. Меню `Tools → Mining Operation → Setup Current Scene as Asteroid Test`.
3. Інструмент залишає робочими Foundation Bootstrap, меню та життєвий цикл сесії, але вимикає **лише** EmptyGame `FoundationScreen` і його `UIDocument`. Змінює положення камери, додає освітлення, free camera, HUD та FieldController. Якщо поле ще не сформовано, генерує постійні записи в Editor; **не зобов'язує генерувати фізичні 3D-об'єкти при запуску**.
4. Збережіть сцену (Cmd/Ctrl+S) і натисніть Play. З меню **NEW GAME → CREATE AND ENTER** перехід залишається на `02_EmptyGame`.
5. Для камерного керування в режимі Legacy/Both: WASD, Q/E, RMB+mouse, Shift. Якщо проект має тільки New Input System — справа внизу є кнопки-напрямки (OnGUI); вони працюють без пакета Input System.
6. Зверху ліворуч тестова панель: Total, Detailed GameObjects, Far GPU instances, FPS, Classes.

**Увага:** цей Editor-інструмент працює з поточною сценою, тому перед запуском бажано зробити її резервну копію. Він **не** автоматизує збереження астероїдів у Foundation `.dwsave` — `CaptureJson()` і `LoadJson()` залишаються ручною інтеграцією.

## Оптимізація

| Параметр | Стартове значення | Що регулює |
|---|---:|---|
| `detailedDistanceMeters` | 1800 м | Радіус унікальних мешів і окремих GameObject |
| `maxDetailedAsteroids` | 28 | Ліміт складних астероїдів |
| `distantDrawDistanceMeters` | 24000 м | Межа рендеру GPU-екземплярів |
| `distantMeshVariants` | 12 | Кількість спільних low-poly силуетів |
| `distantSubdivisions` | 1 | Топологія far-мешів |
| `instantiatePerFrame` | 1 | Скільки деталізованих астероїдів матеріалізувати за тік |
| `streamingTickSeconds` | 0.25 с | Частота перевірки відстаней і матриць |
| `meshColliders` | false | MeshCollider лише для деталізованих астероїдів, якщо включено |

`GPU instancing` потребує його підтримки відеокартою і Material. Runtime створює свою копію distant Material з `enableInstancing=true`; активам не змінює режим. Якщо GPU instancing не підтримується, рендериться лише ближня зона й виводиться попередження. Навіть за активного instancing Unity малює **за кількома draw calls** (на variant/batch), а не обов'язково одним. Кількість draw calls і реальну швидкодію треба виміряти Unity Profiler/Frame Debugger.

Детальні астероїди залишаються унікальними CPU-мешами; далеко вони використовують **набір повторюваних силуетів** із індивідуальними координатами, розмірами й поворотами. Це компроміс між різноманітністю й продуктивністю. Для повністю точного силуету на всіх відстанях потрібен інший механізм — кеш унікальних LOD або GPU-генерація за ID.

## Безпека даних / сумісність

- `AsteroidData`, `AsteroidSaveRecord`, `AsteroidFieldSnapshot`, `AsteroidFieldSaveUtility.SchemaVersion` та `AsteroidGeometryGenerator` як основний генератор форми — без змін структур збереження.
- Класи I/II/III залишаються лише правилами рідкісності, розмір і колір окремо.
- Формації та старий редактор Asteroid Field Studio збережено.
- Коли астероїд не має GameObject, ресурси видобуваються через `ExtractResourcesById` за збереженим записом.
- Збереження поля виконується `CaptureJson()`; інтеграцію з Foundation Save Manager потрібно реалізувати окремо.

## Перевірки й обмеження

- Додано EditMode-тести на сталість vertex-color, відсутність GameObject для віддалених астероїдів, незмінність JSON після перемикання графічного режиму.
- Запустити: `Window → General → Test Runner → EditMode` і тести `MiningOperation.Asteroids.Tests`.
- Збірка та шейдери пройшли статичну перевірку файлів і архіву, але **Unity 6 в цьому середовищі не запускався**. Отже компіляція ShaderLab, реальний Frame Debugger, Play Mode та тести на GPU мають бути перевірені в проєкті.
- Для полів розміром десятки-сотні кілометрів у майбутньому знадобиться Floating Origin. Для центральної тестової формації координати прийнятні.
