# v2.1.1 — Hotfix: Pink Rocks + Streaming Hitch

### Нові файли
- `Runtime/AsteroidStableMaterials.cs` — стабільний спільний матеріал URP/Lit для near/far/debris.
- `Tests/Editor/AsteroidV211StableRenderingTests.cs` — регресійні тести матеріалу й налаштувань.

### Оновлено
- `Runtime/AsteroidInstance.cs` — `RestoreStreamed()`, швидка однорівнева геометрія, синхронізація маси без перегенерації.
- `Runtime/Fields/AsteroidFieldController.cs` — обмежений LRU-кеш, стабільний near-матеріал, швидкий шлях створення.
- `Runtime/Fields/AsteroidFieldSettings.cs` — явно керовані режими швидкого streaming та experimental shaders.
- `Runtime/Fields/AsteroidFieldFarRenderer.cs` — безпечний shader fallback.
- `Runtime/Fields/AsteroidDebrisRenderer.cs` — безпечний shader fallback.
- `Runtime/Fields/AsteroidSelectionController.cs` — безпечне відображення 3D-прев'ю.
- `Runtime/Fields/AsteroidFieldDiagnostics.cs` — лічильник кешованих астероїдів.
- `Editor/AsteroidFieldStudio.cs` та `Editor/AsteroidV2SceneSetup.cs` — кнопка стабілізації без rebuild.
- `Tests/Editor/AsteroidComponentRegressionTests.cs` — враховує новий single-mesh streaming.

**Не змінено:** збереження, генерацію формацій, правила ресурсів, класи I–III, розрахунок об'єму/маси, формат LOD в ручному режимі.
