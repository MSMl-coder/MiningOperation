# Mining Operation — Asteroid Field Generator v1.2.1 (hotfix)

## Виправлено

**MissingComponentException: There is no 'MeshRenderer' attached** під час
`AsteroidFieldController.Spawn()` → `AsteroidInstance.Restore()` → `RebuildGeometry()`.

**Першопричина:** у v1.2 використовувався оператор C# `??` для `UnityEngine.Object`
(`GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>()`).
Unity використовує власну семантику порівняння з `null`, тож оператор `??`
не є безпечним способом перевірити відсутні або знищені компоненти.

Зміни:
1. `AsteroidInstance` — додано `RequireComponent(MeshFilter, MeshRenderer)`.
2. Створення `MeshFilter`, `MeshRenderer` та `LODGroup` — через `if (component == null)`.
3. `AsteroidGeneratorStudio` — пошук резервного шейдера теж через Unity-сумісну перевірку `== null`.
4. Додано три EditMode регресійні тести: створення меша, повторна побудова LOD,
   завантаження астероїда через `StreamAround()`.

## Встановлення поверх існуючого проєкту

З огляду на твій шлях зі stack trace, **не створюй другу копію** в
`Assets/MiningOperation/` — твоя робоча папка:

`Assets/_MiningOperation/Game/Asteroids/`

1. Зроби коміт або резервну копію проєкту.
2. З ZIP скопіюй **вміст** папки `MiningOperation_Asteroids/`
   до `Assets/_MiningOperation/Game/Asteroids/`, замінюючи однойменні `.cs`.
3. Збережи існуючі `.meta` файли в Unity-проєкті: вони містять GUID посилань.
4. Дочекайся перекомпіляції Unity.
5. Відкрий Window → General → Test Runner → EditMode та запусти
   `AsteroidComponentRegressionTests`.
6. Запусти сцену з `AsteroidFieldController` і перевір streaming.

Для мінімального виправлення stack trace достатньо замінити
`Runtime/AsteroidInstance.cs`. Інші файли додають захист Editor і тести.

Дані астероїдів, класів, seed, ID, ресурсного складу та JSON **не змінено**.
Скидання/повторна генерація поля не потрібні.

## Перевірка

Зроблено статичний аудит початкового ZIP v1.2 і вихідних `.cs`, перевірено
відсутність небезпечних `GetComponent<T>() ??` в оновленому пакеті.
Запуск Unity 6 та виконання EditMode тестів у цьому середовищі недоступні.
