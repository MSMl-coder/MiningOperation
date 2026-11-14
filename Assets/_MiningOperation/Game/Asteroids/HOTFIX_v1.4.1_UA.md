# Mining Operation — Asteroid Field Studio Hotfix v1.4.1

Виправляє помилки, які з'являлися після зміни/втрати `Field Settings`, якщо в Asteroid Field Studio лишався попередній preview:

- `NullReferenceException` у `AsteroidFieldStudio.OnGUI` (v1.4:56).
- `NullReferenceException` у `AsteroidFieldStudio.DrawPreview` (v1.4:140).
- Повторні `Invalid GUILayout state` та `You are pushing more GUIClips than you are popping` після винятку.

## Встановлення

1. Закрий **Tools → Mining Operation → Asteroid Field Studio** в Unity.
2. Зроби копію `Assets/_MiningOperation/Game/Asteroids/Editor/AsteroidFieldStudio.cs`.
3. Замінюй **тільки `.cs`**, не видаляй його `.meta`.
4. Дочекайся компіляції, потім знову відкрий **Tools → Mining Operation → Asteroid Field Studio**.
5. Переконайся, що в **Field Settings** обраний `AsteroidFieldSettings`, а в нього налаштовані `Asteroid Generator Settings` та `Resource Catalog`.
6. Натисни **Preview Formation**. Зміни `Field Settings` на `None` — редактор має показати попередження, а не падати.
7. Признач налаштування назад і повторно натисни **Preview Formation**.

## Зміни коду

- Перед використанням `Field Settings` перевіряються сам asset, `asteroidSettings` та `catalog`.
- Якщо змінюються об'єкти `Field Settings`/`Field Root` або seed, застарілий preview скидається.
- `DrawPreview()` коректно обробляє порожній список і відсутні залежності.
- Для скролу та горизонтальної групи використовуються `EditorGUILayout.ScrollViewScope` і `EditorGUILayout.HorizontalScope` — `End...` гарантовано відпрацьовує за виходу з scope.
- Неочікувані винятки в `OnGUI` логуються без безкінечного повторення на кожному кадрі; `ExitGUIException` пропускається як штатна подія IMGUI.

## Сумісність

Змінено **лише Editor-вікно**. Не змінено runtime, формації, астероїди, ID, seed, матеріали, сцени, `.asset`, формат збережень. Повторної генерації полів **не потрібно**.

## Перевірка

Код і пакет пройшли статичні перевірки та ZIP-тест на цілісність. Компіляція та GUI-тест безпосередньо в Unity 6 поки що **не проводилися**.
