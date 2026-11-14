# Mining Operation — Asteroid Generator v2.2

- [x] Детермінована унікальна low-LOD геометрія для кожного видобувного астероїда замість чужих far mesh variants.
- [x] Near/far handoff з одним і тим самим `Mesh` без стартової підміни силуету.
- [x] Покрокове підвищення subdivisions до максимуму налаштувань у ближній зоні.
- [x] Дозоване створення far мешів за кадр і верхня межа far draw calls.
- [x] Новий `generatorVersion = 4`: помітніші геометричні кратери та дрібні сколи для нових астероїдів.
- [x] Безпечний URP/Lit із загальними процедурними rock albedo/normal maps і UV.
- [x] Повільне обертання near/far/decorative за seed, діаметром і часом симуляції.
- [x] Час обертання зберігається в snapshot JSON, попередній JSON формат зберігається.
- [x] Налаштування v2.2 у наявному Field Studio, без нового дубльованого editor-вікна.
- [x] Чотири нові EditMode тести; старі тести скориговані до generatorVersion 4.

Не входить у v2.2: Burst/Jobs, перевірений triplanar custom shader, GPU-instanced унікальні mineable меші, 100% безшовний cross-fade між subdivisions, промислове профілювання в Unity.
