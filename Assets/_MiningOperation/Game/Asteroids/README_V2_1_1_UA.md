# Mining Operation — Asteroids v2.1.1 (Stable Streaming Hotfix)

## Що виправлено

- **Рожеві астероїди**: near / far / debris / 3D selection preview за замовчуванням отримують **стандартний Universal Render Pipeline/Lit**, а не власні експериментальні шейдери. Матеріал створюється один раз на рендерер/поле, а не для кожного каменя. Потребує активного URP Pipeline Asset у Unity 6.
- **Підвисання при появі ближніх астероїдів**: streaming створює **один procedural mesh**, а не п'ять мешів LOD в одному кадрі. Для Editor/manual asteroid як і раніше доступні старі 5 LOD.
- **Повторні підвисання під час повернення**: кеш останніх 12 деактивованих астероїдів повторно використовує GameObject і геометрію. Кеш має обмежений розмір; прогрес видобутку залишається в `AsteroidFieldSnapshot`.
- **Панель діагностики**: доданий лічильник `Cached`.
- **Editor**: кнопка `Fix Pink Rocks / Smooth Streaming (v2.1.1)` у `Asteroid Field Studio`. Налаштовує спільний Field Settings без перебудови збережених asteroid records.

## Встановлення

1. Зроби копію проєкту або Git commit. Вийди з Play Mode.
2. **PATCH**: відкрий ZIP, скопіюй *вміст* вкладеної папки `MiningOperation_Asteroids/` до `Assets/_MiningOperation/Game/Asteroids/` з перезаписом відповідних `.cs`.
3. **Не видаляй наявні `.meta`, `.asset`, сцени та матеріали.**
4. Дочекайся компіляції. Відкрий `Tools → Mining Operation → Asteroid Field Studio`.
5. Вибери точку спавну зі своїм Field Settings. Натисни `Fix Pink Rocks / Smooth Streaming (v2.1.1)` → **Apply stable preset**.
6. Збережи проєкт і запусти тестову сцену знову. Регенерація belt **не потрібна**.
7. Перевір Console, `Cached` у HUD, частоту та тривалість підвисань у **Unity Profiler → CPU Usage** (Timeline). На першому запуску Shader/URP теж може робити компіляцію.

## Початкові налаштування

| Setting | Значення | Навіщо |
|---|---:|---|
| `allowExperimentalRockShaders` | `false` | безпечний URP Lit замість кастомних Near/Far shader variants |
| `fastStreamedAsteroids` | `true` | один меш на ближній астероїд |
| `streamedMeshSubdivisions` | `3` | компроміс якості і CPU; для ще меншого навантаження — `2` |
| `cachedStreamedAsteroids` | `12` | повторно використовувати останні меші |
| `instantiatePerFrame` | `1` | не створювати кілька дорогих об’єктів за один streaming tick |
| `meshColliders` | `false` | вибір ЛКМ продовжує працювати по ID та геометричним bounds |

## Обмеження й сумісність

- Формат JSON, ID, seed, класи, матеріали в ресурсному складі та інші дані астероїдів **не змінюються**.
- Поточний hotfix зменшує складність **візуального near-streaming**, але не розрахунку геометрії для маси та об'єму під час генерації нових записів.
- Для streamed астероїдів зараз використовуємо один меш. П'ять LOD лишаються для одиничних/manual астероїдів; це компроміс для усунення стрибків CPU. Перемикання near ↔ GPU-instanced far може бути помітним.
- `URP/Lit` стабільніший, але **не відтворює triplanar/cavity/vertex-color detail** власного шейдера. `allowExperimentalRockShaders=true` дозволяє повернути їх для діагностики.
- `Shader.isSupported` не гарантує коректності всіх shader variants. Якщо рожевий колір залишиться, відкрий `Console` та перевір Graphics/Quality → URP Pipeline Asset, а також надішли **першу помилку компіляції shader**.
- Кеш зменшує повторні сплески, але створення першого меша все ще виконується в головному потоці. Для повністю безперервного завантаження знадобиться профілювання та Job System/Burst-пайплайн генерації даних меша.
- Статично перевірено структуру та архів; **Unity 6 Editor/Metal/URP компіляція та реальне FPS профілювання тут недоступні**.
