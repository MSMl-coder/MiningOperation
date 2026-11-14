> **v1.2.1 hotfix:** виправлено MissingComponentException під час потокового створення астероїдів. Інструкція: [HOTFIX_v1.2.1_UA.md](HOTFIX_v1.2.1_UA.md).

# Mining Operation — Asteroid Field Generator v1.2 (Unity 6)

Цей архів включає **всю попередню систему окремих астероїдів v1.1** і новий підмодуль `Runtime/Fields/` для створення повноцінних астероїдних формацій у стилі EVE Online. Не залежить від DesertWorld Framework.

## Призначення

- Кожне поле має `Field ID`, 64-бітний `fieldSeed`, тип формації та повний набір астероїдів.
- Кожен астероїд має власні ID, seed, положення, поворот, форму, клас I–III, об'єм, масу, перелік ресурсів і залишок запасів.
- **Клас I–III визначає ймовірності доступності рідкісних ресурсів, не розмір астероїда.**
- Ресурсний склад символічний: внутрішньої 3D-сітки родовищ немає.
- Дані поля створюються один раз, а потім зберігаються. При повторному відкритті поля астероїди не «перетасовуються».
- Меші створюються поступово в радіусі `spawnDistanceMeters` від `viewer` з обмеженням кількості активних об'єктів; повні ресурсні записи існують навіть коли мешів немає.

## Шість формацій

| Тип (Inspector) | Опис |
|---|---|
| `CrescentBelt` | Півколо/дуга з нерівномірними позиціями — основний референс EVE |
| `BrokenBelt` | Три окремі ділянки однієї дуги |
| `BrokenRing` | Кільце з чотирьох розірваних сегментів |
| `ClusterGroups` | Кілька щільних об'ємних груп |
| `Stream` | Видовжений уламковий потік із бічними відгалуженнями |
| `SphericalCloud` | Вільне тривимірне розсіяне поле |

Ці формації **натхнені візуальним принципом EVE Online**, але не відтворюють конкретні внутрішні алгоритми, пропорції чи локації EVE.

## Встановлення

1. Зробіть резервну копію Unity-проєкту.
2. Скопіюйте каталог `MiningOperation_Asteroids/` з ZIP у `Assets/MiningOperation/Asteroids/` (вміст каталогу — Runtime, Editor, Tests і документація). При оновленні v1.1 замініть `.cs`, але не видаляйте чинні `.meta` файли.
3. В Unity відкрийте `Tools > Mining Operation > Asteroid Generator Studio` → `Create Default Assets`. Створяться `AsteroidGeneratorSettings` і `AsteroidResourceCatalog`.
4. Відкрийте `Tools > Mining Operation > Asteroid Field Studio` → `Create Field Settings Asset`.
5. В Inspector об'єкта `AsteroidFieldSettings` призначте створений `AsteroidGeneratorSettings` у **Asteroid Settings** і матеріал у **Asteroid Material**. Налаштуйте `formation`, `asteroidCount`, `radiusMeters`, `thicknessMeters`, `minimumSurfaceGapMeters`.
6. У **Asteroid Field Studio** виберіть `Field Settings`, встановіть seed, натисніть **Preview Formation**. Побачите 2D-проєкцію та прогноз класів.
7. Натисніть **Create Field Root in Scene**. Для нового поля автоматично створюється унікальний `FieldId`. Потім **Generate Persistent Field**. Генератор створює повні записи поступово, з прогресом і кнопкою скасування.
8. Виділіть GameObject `AsteroidField` у Hierarchy: у Scene View з'являться кольорові Gizmos усіх астероїдів. `Select Field and Frame Scene` масштабує Scene View під формацію.
9. На компоненті `AsteroidFieldController` задайте **Viewer** (корабель чи камеру). Без Viewer використовується `Camera.main` — камера повинна мати тег `MainCamera`. Після входу в Play Mode поруч з Viewer астероїди створюватимуться поступово.

> Важливо: для поясу з радіусом 25 км і `spawnDistanceMeters = 13 км` камера у центрі поля не бачитиме астероїдів. Перемістіть камеру ближче до дуги або збільште `spawnDistanceMeters`. Початковий пресет використовує радіус 8,5 км.

## Параметри

- `asteroidCount`: загальна кількість запланованих астероїдів (1–2000).
- `radiusMeters`: радіус дуги/кільця або протяжність хмари.
- `thicknessMeters`: товщина пояса, ширина груп чи потоку, товщина в вертикальному напрямку (залежно від формації).
- `arcDegrees`: кут відкритої дуги.
- `clusterCount`, `clusterRadiusFraction`: кількість і розмір груп.
- `minimumSurfaceGapMeters`: приблизний мінімальний проміжок між оболонками.
- `placementAttempts`: спроби підібрати місце для кожного астероїда; якщо поле переповнене, планувальник **пропускає** об'єкти, а не допускає перетини.
- `generatePerFrame`: скільки **повних ресурсно-фізичних записів** обчислювати за кадр у Play Mode.
- `instantiatePerFrame`: скільки мешів створювати за кадр біля Viewer.
- `maxActiveAsteroids`: максимум астероїдів зі створеними GameObject.
- `meshColliders`: додавати MeshCollider активним астероїдам; для великих полів краще вимкнути.

## Збереження та відновлення

У `AsteroidFieldController`:

```csharp
string fieldJson = fieldController.CaptureJson();
// Передайте fieldJson у власний менеджер слотів збереження.

fieldController.LoadJson(fieldJson);

// Майнінг за постійним ID навіть коли астероїд не відображається:
Dictionary<string, double> collectedKg =
    fieldController.ExtractResourcesById(asteroidId, 1250.0);
```

`CaptureJson()` і `LoadJson()` працюють з JSON у пам'яті: **вони самі не записують файл на диск**. `LoadJson` відновлює розміщення та ресурси, але не позицію самого Transform кореня поля. Transform треба зберігати разом зі сценою або власним World Save.

При перегенерації того самого поля через `Generate Persistent Field` старі записи виснаження будуть замінені. Стан усіх астероїдів, створений під час первинної генерації, **зберігає свої значення** навіть після зміни глобального каталогу чи налаштувань для майбутніх полів.

## Структура

```text
MiningOperation_Asteroids/
├── Runtime/
│   ├── AsteroidGeneration.cs
│   ├── AsteroidGeometryGenerator.cs
│   ├── AsteroidCompositionGenerator.cs
│   ├── AsteroidInstance.cs
│   ├── ... інші файли v1.1
│   └── Fields/
│       ├── AsteroidFieldSettings.cs
│       ├── AsteroidFieldLayout.cs
│       ├── AsteroidFieldController.cs
│       ├── AsteroidFieldMember.cs
│       └── AsteroidFieldSaveUtility.cs
├── Editor/
│   ├── AsteroidGeneratorStudio.cs
│   └── AsteroidFieldStudio.cs
└── Tests/Editor/
    ├── AsteroidGeneratorTests.cs
    └── AsteroidFieldTests.cs
```

## Важливі обмеження

- **Unity 6 не запускався в цьому середовищі.** Статична перевірка вихідного коду й ZIP не замінює перевірки компіляції, EditMode-тестів і профілювання у вашому проєкті.
- Всі меші генеруються на CPU. Jobs/Burst, GPU instancing, імпостори та HLOD — майбутня оптимізація.
- Перевірка проміжків використовує консервативну оцінку оболонки; вона не є точною Mesh-vs-Mesh collision detection.
- Кожен меш отримує три власні LOD; для тисяч одночасно видимих астероїдів будуть потрібні інші засоби рендерингу.
- Для просторів набагато більших за кілька десятків кілометрів рекомендується система Floating Origin або локальні координати відносно поля.
- Модуль не містить ігрових колекторів, респавну, економіки або центрального Save Manager — їх можна під'єднати пізніше за постійними ID.

## Тести

Запуск: `Window > General > Test Runner > EditMode`. Перевіряються детермінізм усіх 6 формацій, унікальність ID між полями, відсутність номінальних перетинів, JSON round-trip, перевірка дубльованих ID і видобуток без активного меша. Для оцінки продуктивності потрібен профайлер Unity з цільовою кількістю астероїдів.
