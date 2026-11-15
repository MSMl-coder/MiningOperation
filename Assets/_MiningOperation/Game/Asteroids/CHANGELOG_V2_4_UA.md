# Mining Operation Environment Generator v2.4 — Gas Rendering

- Нове: `MOGasPresentation` (`SoftParticles`, `Volumetric`) у `AsteroidFieldSpawnPoint`, зі старим значенням за замовчуванням для зворотної сумісності.
- Нове: `MOGasVolumeMeshBuilder.cs` створює один спільний mesh до 20 газових хмар, дані центра/радіуса/щільності в атрибутах вершин.
- Нове: `MO_GasVolumeURP.shader` обчислює процедурну 3D-щільність із 6–24 кроками, нерівномірним силуетом, повільним рухом і псевдооб’ємним освітленням.
- Runtime: `MOGasFieldRenderer` перемикає рендерери без перебудови seed/JSON; автоматично повертається до particles при відсутньому/несумісному volume shader.
- Runtime: distance culling для газового поля з урахуванням реальних центрів і радіусів хмар.
- Editor: ті самі 3 вкладки й одне вікно; параметри нового рендерера у VISUALS, без нових дубльованих редакторів.
- Editor: `MOVisualAssetInstaller` необов’язково встановлює новий матеріал, не перезаписуючи користувацькі.
- Тести: 5 EditMode перевірок структури, детермінізму, атрибутів, сумісності та незмінності збережень.
- Не змінено: Rock/Ice rendering; gas cloud snapshot schemaVersion 2; gas залишився виключно середовищем, без видобутку.
- Відомі обмеження: не фізично точна атмосфера, немає об’ємних тіней чи повної інтеграції з scene depth; raymarching треба тестувати на Metal і профілювати GPU.
