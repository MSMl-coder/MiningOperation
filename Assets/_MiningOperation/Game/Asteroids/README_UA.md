# Mining Operation — Asteroid Generator v1.2 (Asteroid Field + Single Asteroid)

УВАГА: генерація полів/формацій описана в **README_FIELD_UA.md**. Ця частина документа описує базовий генератор одиничних астероїдів v1.1.

Автономний Unity 6-модуль для процедурних астероїдів. **Без DesertWorld Framework**. Код C# підтримує Unity 6; генерація Mesh не залежить від URP, але для відображення потрібен відповідний матеріал. **Геометричний алгоритм v2, JSON-схема v2.**

## 1. Встановлення та оновлення

1. Створіть резервну копію сцени й каталогу з ресурсами/налаштуваннями.
2. Вміст архівної папки `MiningOperation_Asteroids/` розмістіть у `Assets/MiningOperation/Asteroids/`.
3. **Оновлюючи v1.0, замінюйте існуючі `.cs`, не видаляючи їхні `.meta` у проєкті.** Це збереже Unity GUID посилань на скрипти. Нові `.meta` Unity створить сама.
4. В Unity: `Tools > Mining Operation > Asteroid Generator Studio`.
5. Для нового проєкту натисніть **Create Default Assets**: буде створено каталог матеріалів, settings і стандартний матеріал для URP (або Standard, якщо URP не встановлено).
6. Вкажіть settings і seed → **Generate Preview**. ЛКМ — обертання; колесо — масштаб.
7. **Create Asteroid In Scene** створює `AsteroidInstance`, `MeshFilter`, `MeshRenderer`, три LOD, `LODGroup` і опціональний `MeshCollider`.
8. Для тестів Unity: встановіть Test Framework та запустіть EditMode tests через `Window > General > Test Runner`.

## 2. Вимоги й концепція

- Клас I: Common + Industrial.
- Клас II: додатково Rare з шансом `rareChanceClassII` **для астероїда**.
- Клас III: Rare та Exotic із **незалежними ймовірностями** `rareChanceClassIII`, `exoticChanceClassIII`.
- Для появи спеціальних ресурсів мають існувати відповідні записи у Catalog, `maximumResources` має дозволяти достатньо позицій, а їхня максимально дозволена частка має бути > 0.
- Цінність **не впливає на геометрію, розмір або колір**. Глобальний вибір класів контролюється `classIIProbability` / `classIIIProbability`.
- Масовий склад **символічний**: немає фізичних покладів чи розподілу всередині меша.
- Видобуток лише зменшує `remainingMassKg`; геометрія не деформується. Після повного видобутку об'єкт автоматично не видаляється (політика світу/станції — зовнішня система).

## 3. Геометрія та обчислення

V2: детермінована ікосфера з випадковими осями, орієнтацією, макро-/мікрошумом, кратерами, інколи дволопатевими контурами та плоскими площинами розлому. Значення seed дає ту саму форму **за однакового алгоритму та збереженого profile**. Точна побітова тотожність між різними апаратними платформами не гарантується.

`diameterMeters` — **номінальний базовий діаметр**; не обов'язково максимальний габарит асиметричної форми. Розподіл діаметра — логарифмічно-рівномірний між заданими межами.

Три візуальні рівні LOD (`lod0Subdivisions`, `lod1Subdivisions`, `lod2Subdivisions`) не впливають на `volumeM3`. Об'єм для нових астероїдів вимірюється окремим замкненим Mesh із `volumeSubdivisions`. Чим він вищий, тим точніша оцінка об'єму й більша вартість створення астероїда.

Масові частки матеріалів нормалізовані до 1. Середня густина твердої речовини визначається гармонійним середнім, далі враховується пористість:

`bulk_density = (1 - porosity) / Σ(mass_fraction_i / material_density_i)`

`initialMassKg = volumeM3 * bulk_density`.

**Спрощення:** це ігрова модель узагальненої суміші, а не геологічне симулювання реальних порід/родовищ.

## 4. Програмне використання

### Генерація нового астероїда

```csharp
var data = AsteroidGeneration.Create(seed: 12345, settings: generatorSettings);
asteroidInstance.Settings = generatorSettings;
asteroidInstance.Apply(data);
```

### Стабільні ID для астероїдного поля

```csharp
// worldSeed + asteroidIndex => стабільний seed + унікальний ID в межах поля
AsteroidData data = AsteroidIdUtility.CreateFromField(7331L, 42, generatorSettings);
asteroidInstance.Apply(data);
```

Звичайний `Create(seed)` генерує ID `AST-XXXXXXXX`; **однаковий seed = однаковий ID**. Для поля з можливістю повторення seed використовуйте `CreateFromField` або `explicitId`.

### Видобуток

```csharp
Dictionary<string, double> yieldKg = asteroidInstance.ExtractResources(2500.0);
// Dictionary<resourceId, kilograms>. За невичерпного запасу сума = 2500 кг.
```

Потрібно передавати **кг**, а не тонни. Невалідні, від'ємні, NaN і нескінченні значення ігноруються. За нестачі запасу повертається реально видобута кількість.

### Збереження

```csharp
string json = AsteroidSaveUtility.CaptureJson(allAsteroidInstances);
// Збереження JSON на диск/у слот гри — відповідальність зовнішнього Save Manager.
AsteroidSaveUtility.RestoreExisting(json, allAsteroidInstances);
```

`RestoreExisting` відновлює **лише існуючі** об'єкти з відповідними ID. Він не зберігає позиції у світі й не створює астероїди з нуля. Власний менеджер поля відповідає за інстанціювання, позиції й збереження/потокове завантаження.

## 5. Сумісність з v1.0

- `AsteroidData`, `AsteroidSaveRecord`, `AsteroidInstance`, `AsteroidGeneratorSettings` і каталог зберегли назви та основні поля.
- Старі JSON із `schemaVersion=1` і `generatorVersion=1` читаються. Старі сцени з `AsteroidData.generatorVersion=1` теж відтворюються за допомогою збереженої реалізації `AsteroidGeometryLegacyV1`.
- **Важлива межа:** v1.0 НЕ зберігав параметри геометрії. Для точного відтворення мешів v1 потрібен початковий asset Generator Settings зі значеннями, які використовувалися для створення астероїда. Автоматично відновити втрачені параметри неможливо.
- Для v2 зберігається `geometryProfile` (форма), тому зміна поточних settings не змінить старі v2 меші.
- Для нових астероїдів `GeneratorVersion=2`, `schemaVersion=2`. Змінились алгоритм і ресурсна статистика: нова генерація навіть з тим самим seed не тотожна v1, але **старі дані не перераховуються** при завантаженні.
- Поля `rareChanceClassII`, `rareChanceClassIII`, `exoticChanceClassIII` змінили семантику: з ваг вибору v1 на справжні шанси v2.

## 6. Продуктивність

- Базова топологія ікосфери кешується. Кожен конкретний Mesh залишається окремим, бо його вершини різні.
- Вимірювання об'єму створює тимчасовий високоточний Mesh; тому **не викликайте `AsteroidGeneration.Create` для сотень астероїдів в одному кадрі**. Генеруйте невеликими порціями або зробіть пул даних/систему потокової генерації на рівні поля.
- `MeshCollider` дорого коштує для сотень об'єктів. Вимикайте `addMeshCollider` для далеких астероїдів та використовуйте `SetColliderEnabled(bool)` біля корабля/колекторів.
- В редакторі прев'ю має власні тимчасові ресурси; Unity Mesh об'єктів сцени позначаються `HideFlags.DontSave` і можуть відновлюватися після перезавантаження скриптів.
- LOD і колайдер не заміняють GPU instancing, batching та просторовий менеджер астероїдного поля.

## 7. Структура та межі модуля

`Runtime/` — дані, PRNG, геометрія v2 + legacy v1, облік матеріалів, інстанс, ID, JSON.

`Editor/` — прев'ю, Create Default Assets, Create Asteroid in Scene, Export Mesh Asset.

`Tests/Editor/` — EditMode NUnit тести детермінізму, класів, об'єму, збережень, видобутку, LOD і сумісності.

Не входять: Asteroid Field Manager, станція, колектори, економіка, інвентар, фізичне руйнування мешів, текстури/сканер/UI, scene/spawn persistence, повноцінний save slot manager.

## 8. Статус перевірки

Проведено вихідний аудит, перевірку структури файлів і статичні перевірки. **Unity Editor і C# compiler недоступні в середовищі збірки**, тому автоматична Unity-компіляція, actual EditMode tests, PlayMode та профілювання в Unity **не виконувались**. Після імпорту обов'язково запустіть Test Runner та перевірте Console. Результати аудиту — `AUDIT_UA.md`.
