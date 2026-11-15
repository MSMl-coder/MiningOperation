# Mining Operation — Environment Generator v2.6.1 (Snapshot Build Hotfix)

## Виправлення

Після оновлення до v2.6.0 генератор почав створювати `layoutVersion = 4` (обмеження поля по Z), але в `AsteroidFieldSaveUtility.Validate` лишилася перевірка тільки версій 1–3. Тому `BuildStep()` завершувався помилкою `Invalid or unsupported field snapshot`.

Тепер валідатор приймає версії 1–4 й відхиляє майбутні невідомі версії з конкретним повідомленням. Жодних схем JSON, seed, ID, ресурсів або параметрів поля не змінено.

## Встановлення PATCH v2.6.0 → v2.6.1

1. Вийди з Play Mode, зроби коміт Git або резервну копію.
2. Відкрий архів `MiningOperation_EnvironmentGenerator_v2.6.1_HOTFIX_PATCH.zip`.
3. Із папки `MiningOperation_Asteroids/` скопіюй `Runtime/Fields/AsteroidFieldSaveUtility.cs` до `Assets/_MiningOperation/Game/Asteroids/Runtime/Fields/`, замінивши старий файл; **збережи його `.meta`**.
4. Необов'язково скопіюй `Tests/Editor/MOV261SnapshotLayoutTests.cs` до відповідної папки тестів.
5. Дочекайся перекомпіляції Unity та ще раз натисни **Build / Rebuild** у Field Studio.

**Збережені поля не потрібно видаляти**, а окремі існуючі астероїди не потрібно перегенеровувати. Якщо Build раніше не завершився, повторний Build уже створить snapshot.

## Перевірка

Додано EditMode-тести: layout v1, v2, v3, v4 проходять JSON round-trip; невідома версія v5 та Gas snapshot відхиляються. Архів пройшов перевірку цілісності. Фактичне виконання тестів у Unity 6/Metal потребує локального запуску редактора.
