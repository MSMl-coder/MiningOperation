# Mining Operation — Asteroid Generator v2.2

## Мета релізу

Виправити підміну силуету астероїда під час переходу між far/near, покращити геометричні кратери й поверхню та додати детерміноване повільне обертання **без скидання ID, ресурсів і стану видобутку**.

**Цей пакет призначений для Unity 6 URP.** Перевірено цілісність архівів, структуру C# та залежності файлів. У цьому середовищі немає Unity Editor: компіляція URP та проходження EditMode-тестів не підтверджені.

## Оновлення з v2.1.1

1. Зроби Git commit / резервну копію всього `Assets/_MiningOperation/Game/Asteroids/` разом із `.meta`.
2. Вийди з Play Mode та закрий Field Studio.
3. Розпакуй PATCH: перенеси **вміст** каталогу `MiningOperation_Asteroids/` поверх `Assets/_MiningOperation/Game/Asteroids/`. Не створюй дубльовану папку Asteroids/Asteroids.
4. Замінюй `.cs`, але **не видаляй існуючі `.meta`**. Для чотирьох нових скриптів Unity сам згенерує `.meta`.
5. Переконайся, що активний URP Render Pipeline Asset (Project Settings → Graphics і Quality).
6. Відкрий **Tools → Mining Operation → Asteroid Field Studio**. У блоці `V2.2 / SHAPE CONTINUITY + ROCK + MOTION` увімкни `Same shape from far to near`, `Slow asteroid rotation`, натисни `Use Safe URP Rock Surface`.
7. Запусти сцену **без Force Rebuild**, щоб перевірити на існуючому полі перехід far→near і обертання. Для тесту нових **геометричних** кратерів зроби окремий backup тестового поля і тоді явно перебудуй поле (ця дія скине видобуток цього поля).
8. У Test Runner → EditMode запусти `AsteroidV22ContinuityTests`, потім усі `Asteroids` тести. При проблемах надішли **першу** помилку Console зі шляхом скрипта та рядком.

## Що змінюється

### 1. Перехід far / near

- **Колишня проблема:** far renderer вибирав один із 12 чужих мешів. При наближенні створювався унікальний астероїд з іншим силуетом.
- **Тепер:** кожний віддалений видобувний астероїд має *свій* спрощений меш, побудований із збережених seed, generatorVersion, geometryProfile і diameter. При першому створенні GameObject він **тимчасово використовує той самий Mesh (sharedMesh)** — перемикання far→near не змінює силует.
- Ближче до камери меш поступово оновлюється до деталізованішого із тієї самої аналітичної форми. Одне оновлення за streaming tick і без одночасної генерації після нового спавну.
- Примітка: між різними рівнями деталізації **можливе слабке локальне перемикання вершин**. Це не mesh-decimator із суворим обмеженням screen-space error і не дезеринг по кожному об'єкту.
- Віддалені **декоративні уламки** продовжують використовувати GPU-instancing. Унікальні far меші видобувних астероїдів використовують **окремі виклики Graphics.RenderMesh**. Це точніше, але може збільшити draw calls. Для цього є `Max Far Render Draws`.

### 2. Кратери та поверхня

- Нові астероїди генеруються з версією геометрії `4` (`AsteroidGeologyV4.cs`). З'явилися виразніші великі кратери з обідком і локальні тріщинні западини.
- Всі рівні геометрії базуються на одному seed/profile; незмінні базові напрямки ікосфери мають однакові координати на різних subdivision.
- Новий безпечний `URP/Lit` матеріал із процедурною текстурою породи, normal map і низькою smoothness. Один набір текстур використовується всіма астероїдами — без створення окремої текстури на кожен об'єкт.
- Текстурування зараз **сферичне UV**, не triplanar: на полюсах і UV-швах можливі артефакти. Це усвідомлений компроміс для усунення рожевих шейдерів.

### 3. Обертання

- Стабільні вісь і швидкість з seed та розміру астероїда, без MonoBehaviour.Update на кожному камені.
- Ближні: обертання кожен кадр. Далекі: matrix rotation кожен кадр; декоративні: оновлення при streaming tick.
- `simulationSeconds` записується разом з `AsteroidFieldSnapshot`. Старі saves без поля отримають 0. Відновлення обертання після завантаження відтворюване в межах часу симуляції цього поля.

## Параметри старту (Field Settings)

| Параметр | Пропоноване значення | Коментар |
|---|---:|---|
| Same shape from far to near | true | увімкнено за замовчуванням у v2.2 |
| Far mesh subdivisions | 2 | 162 базові вершини + UV |
| Maximum near subdivisions | 4 | до 2 562 базових вершин + UV |
| Far meshes per frame | 2 | гарантує поступове заповнення поля |
| Far build time budget | 2 ms | м'який бюджет: один меш може перевищити його |
| Max far asteroid draw calls | 300 | точніші силуети мають GPU/CPU ціну |
| Slow asteroid rotation | true | ввімкнено |
| Rotation speed multiplier | 1.0 | 0 зупиняє обертання |
| Allow Experimental Rock Shaders | false | зберегти для уникнення magenta |

**Важливо:** 2ms — не жорстка гарантія часу кадру. Виклики створення `UnityEngine.Mesh` залишаються синхронними в головному потоці, хоча й дозуються. Job System/Burst в цій версії не реалізовано.

## Сумісність

- `AsteroidSaveUtility.SchemaVersion = 2` — без змін.
- `AsteroidFieldSaveUtility.SchemaVersion = 1` — без змін (додано необов'язковий час симуляції).
- Старі астероїди з `generatorVersion` 1, 2 або 3 використовують свою попередню геометрію; їх ID, об'єм, маса й ресурси не змінюються.
- **Нові кратери V4** з'являться тільки на **новостворених** астероїдах. Для старого поля `Force Rebuild` не виконувати без резервної копії даних видобутку.
- Нові Editor-функції не перебудовують поле автоматично.

## Що перевіряти в Unity

1. Вибери великий камінь, повільно наближай камеру: far→near має бути без зміни чужого силуету. При наближенні локальна сітка стає детальнішою.
2. Після віддалення і повернення перевір ту саму початкову орієнтацію та безперервне обертання.
3. Відкрий Frame Debugger/Profiler: `Graphics.RenderMesh` для видобувних far-каменів може створювати більше draw calls ніж у v2.1.1.
4. Перевір матеріал у **Game View**, не лише Scene View: колір каменю, normal map і відсутність мадженти.
5. Перезапусти Unity, завантаж збережене поле й перевір збіг ID, класу, запасу маси та фази обертання.

## Файли v2.2

- Нові: `AsteroidSpin.cs`, `AsteroidGeologyV4.cs`, `AsteroidSurfaceUV.cs`, `AsteroidRockSurface.cs`, `AsteroidV22ContinuityTests.cs`.
- Оновлені: `AsteroidGeneration`, `AsteroidGeometryGenerator`, `AsteroidSaveUtility`, `AsteroidStableMaterials`, `AsteroidInstance`, `AsteroidFieldController`, `AsteroidFieldFarRenderer`, `AsteroidDebrisRenderer`, `AsteroidFieldSaveUtility`, `AsteroidFieldSettings`, `AsteroidFieldDiagnostics`, `AsteroidFieldStudio`.
