# Зміни v1.4 відносно v1.3

- `AsteroidGeneration.Create` отримав необов'язковий `diameterOverride` без зміни старих викликів.
- `AsteroidFieldLayoutOptions` містить незалежний набір параметрів планування; старий `AsteroidFieldLayout.Plan(settings,seed,id)` працює як раніше.
- `AsteroidFieldSpawnPoint` — автономна точка спавну з власним Field ID/Seed/Formation/CountMode/Size/Radius.
- Нова редакторська кнопка `Force Rebuild This Field`, і меню `Create Field Spawn Point`.
- `AsteroidFieldController` використовує параметри Spawn Point, плановий діаметр і збережену формацію. Очищає старі дочірні GameObject перед runtime streaming.
- `AsteroidFieldLayout.LayoutVersion`: 1→2; зчитування старих v1 записів підтримується.
- 5 LOD у `AsteroidInstance` та підтримка shader cross-fade, включно зі ShadowCaster.
- Triplanar текстура, генератор Rock Detail Texture, команди підвищення якості матеріалів і LOD.
- `AsteroidSelectionController` з ray–sphere picking постійних записів, 3D Preview, HUD та кільцем.
- `AsteroidTestEnvironmentSetup`: безпечніша стартова позиція камери й автоматичне додавання selection controller.
- `AsteroidFieldDiagnostics`: підрахунок для всіх полів сцени.
- Змінено регресійні тести під 5 LOD і додано `AsteroidV14IntegrationTests`.

**Дані Foundation та інші ігрові модулі не змінено.**
