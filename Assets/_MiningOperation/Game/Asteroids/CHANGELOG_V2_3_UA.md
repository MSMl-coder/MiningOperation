# v2.3 — Unified Environment Generator

### Runtime
- Додано `MiningFieldContent` (Rock=0, Ice=1, Gas=2) на Spawn Point.
- Ice: незалежний Generator Settings і Resource Catalog, власний URP/Lit матеріал; на збереження додається лише `contentType=Ice` без зміни схеми.
- Ice: та сама система seed, ID, LOD, маси, видобутку та обертання, що й для Rock.
- Gas: детерміновані окремі хмари й постійні ID; окрема JSON-структура, `ExtractGas`.
- Gas: максимум один ParticleSystem renderer із 24–4000 billboard-puffs на Spawn Point, без GameObject на puff.
- При перемиканні Rock→Gas попередній snapshot не видаляється автоматично.

### Editor
- Один основний пункт меню Tools → Mining Operation → Asteroid Field Studio.
- Нове компактне двопанельне вікно: Location / Shape & Quality / Visuals, локації та створення зліва, попередній перегляд і Build в одному місці.
- Вбудована карта й 3D-зразок реального Mesh (не окреме вікно).
- Забрані окремі `AsteroidGeneratorStudio.cs` та `AsteroidField3DPreview.cs`.
- Спрощено Spawn Point Inspector.
- Генератор матеріалів у Editor створює reusable ice/gas textures і каталоги, а не динамічні runtime текстури.

### Безпека оновлення
- Нові поля не змінюють збережені rock asteroid records.
- Перегенерація існуючого rock/ice belt залишається підтверджуваною дією.
- Додані EditMode-тести для seed/ID/JSON та gas-only режиму.

### Незавершене
- Поточний газ — недорогий sprite volume, не реальна 3D raymarched nebula.
- Газовий Save Manager не підключено до Foundation автоматично.
- Немає гарантованих FPS чи результатів Unity/Metal тестування до імпорту.
