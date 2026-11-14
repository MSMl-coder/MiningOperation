# Mining Operation — Asteroid Authoring UX v1.4.2

Оновлення інструментів Unity Editor для поточного Asteroid Generator v1.4.1.
Це не зміна математичного ядра чи збережень, а покращення робочого процесу.

## Швидкий старт (нова сцена)

1. Розпакуй модуль у `Assets/_MiningOperation/Game/Asteroids/` (без додаткового вкладеного каталогу). Не видаляй існуючі `.meta`, `.asset` і сцени.
2. Зачекай перекомпіляції в Unity 6.
3. Відкрий **Tools → Mining Operation → Asteroid Field Studio**.
4. Натисни **Create Ready Setup**. Unity створить Resource Catalog, Asteroid Generator Settings, Asteroid Field Settings, базові матеріали та один порожній GameObject зі Spawn Point. Файли assets: `Assets/_MiningOperation/AsteroidAuthoring/`.
5. Розмісти `Belt Spawn Point` у Scene View, обертай для орієнтації формації. Радіус можна змінювати прямо хендлом у Scene View.
6. В Inspector налаштуй тип формації, кількість, розміри та відстань між астероїдами.
7. У Field Studio перевір **Layout Preview**: Top/Side, клас/діаметр, клік на маркер, statistics.
8. Натисни **Build Field in Scene**. Після генерації збережи сцену. Тільки цей крок змінює збережені asteroid records.

## Кілька формацій

У Field Studio натисни **Add Spawn Point** для кожного нового поясу. Це створить окремий ID і seed; кожна точка має свої overrides. У списку **Spawn Point** перемикайся між полями без пошуку об'єктів у Hierarchy. Натисни **Select in Hierarchy** / **Frame in Scene** для швидкої навігації.

Кнопка `Create Ready Setup` завжди створює **новий** комплект assets. Для другого/третього поясу краще використовувати **Add Spawn Point**, який повторно застосує існуючі спільні assets.

## Точки спавну: Inspector

Замість 30+ полів суцільним списком налаштування поділено на:

- Identity & Dependencies — settings, Field ID, seed, independent overrides.
- Formation & Count — режим Fixed / Seeded Range / Density, конкретні поля для вибраного режиму.
- Asteroid sizes — межі діаметрів і зміщення до менших каменів.
- Placement & spacing — технологічні параметри в додатковому згортаному блоці.
- Live Field Status — Target / Stored / Outdated з підказками.
- Actions — Preview, Build/Rebuild, Frame; небезпечне Clear заховано в Advanced.

**Build / Rebuild** тепер виконується через `EditorApplication.update`: контролюється прогрес, можна скасувати, Inspector не утримує Unity у синхронному `while` циклі. Скасування не повинно замінювати раніше завершений snapshot.

## Layout Preview

- **Live preview** автоматично перераховує лише невеликі поля (≤350), з паузою після зміни параметрів. Великі формації оновлюються вручну кнопкою Refresh Preview.
- Вид **Top (X/Z)** / **Side (X/Y)**, масштаб, розфарбування класом або діаметром.
- Натискання маркера показує index, seed, номінальний діаметр та локальні координати.
- Статистика заповнення, приблизні класи I–III, min/max діаметр, попередження про недостатнє місце.
- Прев’ю НЕ змінює persistent field records, не обнуляє ресурси та не створює Mesh GameObjects.
- Колір класів у preview прогнозується за тим самим seed/RNG порядком; джерело істини — згенеровані runtime AsteroidData.

## Asteroid Generator Studio (один астероїд)

З'явився блок Quick Tuning: мінімальний/максимальний діаметр, shape irregularity, crater intensity, surface roughness, class probabilities та LOD0 subdivisions. Прев’ю має reset view, обертання й масштабування. Зміни налаштувань позначають старе прев’ю як **OUTDATED**; створення об'єкта і Export Mesh застарілого прев’ю блокуються до регенерації.

## Виявлення змін та старі сцени

- В `AsteroidFieldSpawnPoint` додано лише **необов'язкове** поле `lastBuildSignature` — метадані для редактора. Воно зберігається в сцені, але НЕ входить у JSON збереження поля.
- Під час нової побудови записується контрольний підпис generation-relevant параметрів (field ID / seed, layout, geometry, material catalog, resource probabilities). Зміни світла/матеріалів/дистанції стрімінгу НЕ помічаються як потреба перебудови resource records.
- Поля, згенеровані раніше і без підпису, показуються як **legacy-untracked**. Це не означає втрату даних: повторно генерувати потрібно лише якщо хочеш застосувати нові параметри.
- Для сцен без Spawn Point вибери старий Field Controller в Hierarchy й натисни у Field Studio **Use Selected Legacy Field (Preserve Snapshot)**. Це додає point-контрол без перебудови.
- Зберігаються ID, seed, класи, записи матеріалів, формацій і runtime JSON. У Core генерації/LOD/шейдерах змін немає.

## Сумісність і перевірка

Пакет створено на основі повного `MiningOperation_AsteroidGenerator_v1.4.1_GUI_Fix_FULL.zip`, а не на основі загальної схеми з пам'яті.

Статичні перевірки: цілісність ZIP, перевірка структури C# та asmdef, звірка змінених/незмінених файлів, збалансованість дужок у вихідному коді. Додано **3 EditMode-тести** UX (`AsteroidAuthoringUXTests`). Компіляція та запуск тестів у Unity 6 у цьому середовищі недоступні — їх необхідно провести у редакторі.

### Тестовий сценарій

1. Створи Ready Setup і три Spawn Point.
2. На кожному вибери іншу формацію й кількість. Перевір Top/Side preview.
3. Згенеруй кожен belt; статус має стати UP TO DATE.
4. Зміни радіус чи seed одного поля: лише його статус має перейти в OUTDATED, але збережений count/records ще не повинні змінитися.
5. Натисни Rebuild, перевір попередження, початок прогресу й завершення.
6. Перевір Cancel на іншому полі: попередній snapshot повинен зберегтися.
7. Збережи/відкрий сцену: підпис зберігається, статус не повинен безпричинно змінюватися.

**Не належить до цього оновлення:** новий skybox/lighting/rock shader, переробка фізики, справжня компіляційна перевірка в Unity, повноцінна інтеграція зі збереженнями Foundation.
