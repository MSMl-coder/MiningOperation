# Mining Operation — Asteroid Generator v1.4

**Повний модуль для Unity 6 (URP).** Це оновлення v1.3, а не окремий проєкт Unity. Встановлюйте поверх поточної папки модуля, зберігаючи наявні `.meta`.

## Що змінилося

1. **Розміри.** Генератор використовує мінімальний/максимальний діаметр і `smallAsteroidBias` (розподіл із перевагою малих об'єктів). Діаметр з плану розташування тепер точно передається в `AsteroidGeneration.Create` — усунено стару розбіжність між розміщенням і справжнім розміром каменя.
2. **Різні seed.** У кожного астероїда є детермінований seed та власний ID від комбінації `Field ID + Field Seed + Index`.
3. **Кілька точок спавну.** `AsteroidFieldSpawnPoint` додається на пустий GameObject і автоматично отримує `AsteroidFieldController`. Кожна точка має власну формацію, seed, розміри та кількість. Повернення/поворот точки змінює розміщення всього поясу у світових координатах.
4. **П'ять LOD.** Для ближніх GameObject згенеровано 5 рівнів (попередньо 3). `LODGroup.CrossFade` з дезерингом у кам'яному URP-шейдері. Перехід до GPU-instanced далекого рендера поки що може бути помітним — це окрема система від LODGroup.
5. **Графіка.** Triplanar rock texture (генерується без сторонніх ассетів), процедурна неоднорідність кольору, покращений 5-LOD пресет; біля камери немає вимоги малювати поверхню low-poly.
6. **Регенерація.** Кнопка `Force Rebuild This Field` в Inspector кожної точки явно будує новий snapshot з поточними параметрами. Вона перезаписує лише дані конкретної точки (а разом з ними — її прогрес видобутку!).
7. **Кількість.** `Fixed`, `SeededRange`, `DensityPerSquareKm`. Режим вибирається на кожній точці окремо. Нова кількість застосовується при регенерації; це **не** безперервне додавання/видалення під час Play Mode.
8. **Вибір.** Клік ЛКМ на астероїді (в тому числі далекому без GameObject) показує фокус-панель угорі: 3D RenderTexture-прев'ю з кільцем, ID, клас, діаметр, об'єм, початкову/залишкову масу, відомі матеріали. `Esc` або `×` закривають фокус. Для далеких екземплярів використовується вибір за наближеною сферою, а не точний MeshCollider.

## Оновлення існуючого проєкту

1. Зробіть резервну копію проєкту / git commit. **Не видаляйте попередні `.meta`** файли й наявні `.asset` (Resource Catalog, Generator Settings, Field Settings).
2. Розпакуйте архів. **Вміст** `MiningOperation_Asteroids/` скопіюйте поверх `Assets/_MiningOperation/Game/Asteroids/` у вашому Unity-проєкті. Не створюйте зайву вкладену папку.
3. Дочекайтеся перекомпіляції. Unity сам створить `.meta` для **нових** файлів.
4. Виконайте `Tools > Mining Operation > Create Rock Materials`, потім `Tools > Mining Operation > Upgrade Rock Materials v1.4`. Остання команда створює `AsteroidRockDetail512.asset` і призначає текстуру кам'яному матеріалу.
5. Виконайте `Tools > Mining Operation > Apply High Quality Asteroid LOD Preset`. Команда змінює відповідні Generator Settings на 5 / 4 / 3 / 2 / 1 і Far Field Settings на 20 варіантів / subdivision 2. На слабкіших ПК зменшіть ці значення вручну.
6. Відкрийте вашу `02_EmptyGame`; виконайте `Tools > Mining Operation > Setup Current Scene as Asteroid Test`, щоб увімкнути камеру вибору астероїдів. Ця дія не видаляє Foundation Framework.
7. `Tools > Mining Operation > Create Field Spawn Point` — з'явиться **порожній** GameObject із `AsteroidFieldSpawnPoint` та `AsteroidFieldController`. Встановіть на ньому Field Settings. Створіть стільки точок, скільки потрібно. Розставте їх позиціями/поворотами Transform.
8. На кожному `AsteroidFieldSpawnPoint` встановіть `Formation`, `Count Mode`, `Count`, `Radius`, `Thickness`, `Min/Max Diameter`, `Small Asteroid Bias` тощо. Натисніть **Force Rebuild This Field** на кожній точці. Збережіть сцену.
9. За потреби вимкніть старий `AsteroidFieldController`-root, щоб уникнути подвоєного пояса. Якщо старе поле вже розроблялося — не видаляйте його, доки не збережете дані.
10. Запустіть Play Mode і перевірте тестову панель, поле, натискання ЛКМ та перемикання LOD.

## Приклад

```
02_EmptyGame
├── Foundation Systems (не чіпати)
├── Main Camera
│   ├── AsteroidTestFlightCamera
│   └── AsteroidSelectionController
├── Mining Test Diagnostics
└── Asteroid Spawn Points
    ├── Belt_Alpha        AsteroidFieldSpawnPoint + AsteroidFieldController
    ├── Belt_Beta         AsteroidFieldSpawnPoint + AsteroidFieldController
    └── Anomaly_Gamma     AsteroidFieldSpawnPoint + AsteroidFieldController
```

Зауваження: пункт меню створює точку **на корені сцени**, у позиції центру Scene View. Ви можете самі створити пустий parent `Asteroid Spawn Points` і перемістити точки під нього.

### Приклад початкового профілю

| Параметр | Belt Alpha | Belt Beta | Anomaly Gamma |
|---|---:|---:|---:|
| Формація | CrescentBelt | BrokenRing | ClusterGroups |
| Count Mode | Fixed | SeededRange | DensityPerSquareKm |
| Count | 140 | 80–200 | 0.8 / км² |
| Radius | 8 000 м | 11 000 м | 5 000 м |
| Diameter | 8–500 м | 12–320 м | 20–650 м |
| Small Bias | 0.72 | 0.64 | 0.52 |

## Чому раніше зміна формації не працювала

Раніше в сцені зберігався готовий `AsteroidFieldSnapshot` з координатами, і сам факт зміни `Field Settings` не перебудовував його. Генератор та редактор були розділені. **Тепер точки спавну мають однозначну дію `Force Rebuild This Field`.** Якщо у HUD `Total asteroids: 0`, переконайтеся, що призначений Field Settings, натисніть Rebuild і збережіть сцену.

## LOD і продуктивність

- `lod0Subdivisions` ... `lod4Subdivisions` — щільність 5 окремих мешів; LOD0 високодетальний. Перемикання виконується за розміром на екрані (`0.55 / 0.31 / 0.16 / 0.065 / 0.001`) із cross-fade.
- `detailedDistanceMeters`, `maxDetailedAsteroids` — кількість та дистанція ближніх унікальних GameObject.
- `distantMeshVariants`, `distantSubdivisions` — якість GPU-instanced силуетів; вони **не** мають власного GameObject.
- `distantDrawDistanceMeters` — дальність показу далеких інстансів.
- Якщо потрібно більше детальних астероїдів на екрані — змінюйте `maxDetailedAsteroids` поступово, спостерігаючи CPU/GPU у Profiler. 5 LOD × багато великих каменів потребують часу для генерації CPU.

## Збереження та сумісність

- **Збережено** формат `AsteroidFieldSaveUtility.SchemaVersion = 1` і дані астероїдів v2.
- `AsteroidFieldLayout.LayoutVersion = 2` відображає нову процедуру розміщення та діаметрів. Збережені `layoutVersion=1` як і раніше читаються. Стан ресурсу в збереженому snapshot залишається авторитетним.
- **Force Rebuild** руйнує попередній прогрес видобутку **тільки вибраного поля**. Проста заміна файлів модуля не вимагає перебудови старих полів.
- Модуль не інтегрує JSON автоматично у `FoundationSessionService`: обов'язок вашого Save Manager — викликати `CaptureJson()` / `LoadJson()` для кожного Field ID.
- Поки немає фізичної деформації астероїдів під час видобутку, як і погоджено раніше.

## Обмеження перевірки

Структуру C# і ZIP перевірено статично, але цей пакет **не скомпільовано і не профільовано в Unity 6** у середовищі підготовки. Запустіть Test Runner в Unity: `Window > General > Test Runner > EditMode` і перевірте матеріали URP. Якщо з'явиться compile error, важливий повний текст, шлях і номер рядка.

Зверніть увагу: preview controller використовує **Layer 31** виключно для RenderTexture. Якщо його вже використовує ваш проєкт, потрібно замінити PreviewLayer і culling mask.
