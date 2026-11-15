Mining Operation — Environment Generator v2.3.1 | CS0103 hotfix

Проблема: AsteroidFieldStudio.cs(195,59) — змінна type поза областю видимості.
Виправлення: SerializedProperty type оголошено перед HorizontalScope;
EditorGUI.BeginChangeCheck перенесено перед редагованими властивостями.

Встановлення:
1. Вийди з Play Mode, закрий Asteroid Field Studio.
2. Замінити файл:
   Assets/_MiningOperation/Game/Asteroids/Editor/AsteroidFieldStudio.cs
3. Збережи старий AsteroidFieldStudio.cs.meta, не імпортуй новий .meta.
4. Дочекайся компіляції та відкрий Tools -> Mining Operation -> Asteroid Field Studio.

PATCH ZIP містить лише Editor/AsteroidFieldStudio.cs у папці MiningOperation_Asteroids.
FULL ZIP — повна v2.3 із виправленим Editor/AsteroidFieldStudio.cs.
Збереження, ресурси та конфігурації формацій не змінено.
Статична перевірка проведена; Unity 6 Editor недоступний для реальної компіляції.
