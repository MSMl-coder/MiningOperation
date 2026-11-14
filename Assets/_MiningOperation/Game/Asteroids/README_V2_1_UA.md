# Mining Operation — Asteroid Generator v2.1 (Cleanup & Rendering)

Це **повний модуль**, призначений для оновлення v2.0 у Unity 6 URP.

## Що виправлено

- Видимі далекі астероїди й декоративні уламки відбираються по просторових клітинках: camera frustum + distance culling. Це не відсікає й не видаляє записи видобувних астероїдів.
- Значно менше декоративних об'єктів за замовчуванням у **нових** Field Settings: 1600 rock fragments, 0 micro fragments, 8 варіантів mesh. Старі .asset **не змінюються автоматично**.
- **Balanced Rendering** для вже створеного Field Settings: 1600 каменів, 0 мікроуламків, 12 далеких мешів, 24 детальні астероїди. Змінює тільки спільні параметри відображення; збережений Snapshot не перебудовується.
- Сцена більше не генерує планету; інструмент Repair вимикає відомі старі освітлювальні й Volume-об'єкти та прибирає лише раніше створений `MO Distant Planet (background)`.
- Небо: виправлено HLSL тип `fixed4` → `half4`; якщо material/shader не підтримується, у Game Camera використовується **суцільний синій фон**, а не маджента.
- Керування світлом: один керований key, один fill із обмеженою інтенсивністю; far rock shader отримав мінімальне ambient освітлення, щоб не ставати чорним за відсутності light probes.
- Вікно `Asteroid Field Studio` — єдиний пункт меню. Додаткові редактори доступні як кнопки в ньому. Застарілі окремі Editor-утиліти прибрані з нового FULL пакета.
- `Frame Real Asteroid` ставить тестову Game Camera поряд із реально збереженим астероїдом і призначає її Viewer для відповідного контролера. У HUD тепер видно distance to nearest та near detail range.

## Перехід із v2.0

1. **Зробіть резервну копію проєкту/гілку Git.** Не робіть upgrade в Play Mode.
2. Розпакуйте `MiningOperation_Asteroids_v2.1_PATCH.zip` поверх `Assets/_MiningOperation/Game/Asteroids/`. Усередині ZIP є коренева папка `MiningOperation_Asteroids`; переносіть **її вміст**, а не саму папку, до існуючої папки гри.
3. **Не видаляйте `.meta`** файли скриптів, які продовжують використовуватися.
4. Оскільки розпакування не видаляє старі файли, у Studio натисніть **Remove Old Editor Tools...**. Це без підтвердження нічого не видаляє. Воно прибере тільки:
    - `Editor/AsteroidTestEnvironmentSetup.cs`
    - `Editor/AsteroidGraphicsPreset.cs`
    Ці файли більше не мають залежностей усередині поточного модуля.
5. Відкрийте **Tools → Mining Operation → Asteroid Field Studio**.
6. Натисніть **Repair Sky + Lighting / Remove Old Visuals**. Команда не зачіпає Spawn Points, Resource Catalog, Snapshot та інші об'єкти гри. Вона вимкне відомі старі visual GameObjects і видалить `MO Distant Planet (background)`; перед зміною зробіть копію сцени.
7. Виберіть Spawn Point. За бажання натисніть **Balanced Rendering**, щоб оптимізувати **його спільний** Field Settings. Інші поля з тим самим asset також отримають цей preset.
8. Натисніть **Frame Real Asteroid**, щоб розмістити камеру близько до астероїда. Якщо Snapshot не створений, спочатку виконайте Build Field.
9. Запустіть Play Mode. Перевірте Game View та HUD: `Detailed GameObjects`, `Far GPU instances`, `Decorative fragments`, `Nearest`, `detailed range`.

## Що більше не потрібно

У новому FULL пакеті **немає** двох застарілих Editor-утиліт та `MO_DistantPlanetURP.shader`. Старий шейдер/матеріал планети, якщо вони лишилися у проєкті після оновлення, можна видаляти вручну **лише після перевірки посилань**. Очищення не виконується автоматично, оскільки може вплинути на власні матеріали користувача.

Розділ `AsteroidGeometryLegacyV1` залишено свідомо — для сумісності старих збережених астероїдів. Не видаляйте його. `AsteroidGeneratorStudio`, `AsteroidField3DPreview`, `AsteroidFieldSpawnPointEditor` залишені робочими, але не займають окремі головні пункти меню.

## Налаштування якості

- **Balanced**: 1600 декоративних, 0 micro, до 24 детальних (рекомендовано для старту).
- **Dense**: піднімайте декор до 3000–5000 лише після профілювання.
- Менший `detailedDistanceMeters` знижує CPU/GPU витрати на унікальні меші, але камера може бачити тільки far-layer.
- `Nearest > detailed range` у HUD означає, що `Detailed GameObjects = 0` може бути **нормальною** поведінкою, а не поломкою.
- `Camera.main` повинна мати тег `MainCamera`, інакше в Inspector у контролері явно призначте `Viewer`.

## Межі перевірки

Здійснено структурні перевірки C#, JSON asmdef, повноти архівів і посилань усередині модуля. Компіляція шейдерів URP, перевірка Unity Test Runner, performance profiling і поведінка у конкретній сцені **не підтверджені без запуску Unity 6**. Збереження гри інтегруються окремо з Foundation Framework; цей пакет не переписує Foundation.
