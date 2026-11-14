# v1.4.2 — Authoring UX

## Змінені файли

- `Editor/AsteroidFieldStudio.cs`: studio відновлено як орієнтований на сцену редактор, quick start, chooser, Preview Top/Side, статистика, stale detection, взаємодія із маркерами, одна кнопка Build.
- `Editor/AsteroidFieldSpawnPointEditor.cs`: тематичний Inspector, count-mode dependent parameters, side warnings, Scene radius handle, явне підтвердження руйнівних дій.
- `Editor/AsteroidGeneratorStudio.cs`: quick tuning, outdated preview guard, layout scopes, характеристики й міні-діаграми складу.
- `Runtime/Fields/AsteroidFieldSpawnPoint.cs`: одне необов'язкове серіалізоване поле підпису Editor-побудови.
- `Tests/Editor/MiningOperation.Asteroids.Tests.asmdef`: посилання на editor asmdef для UX тестів.

## Нові файли

- `Editor/AsteroidAuthoringWorkflow.cs`: complete asset set, spawn point, legacy conversion, signature/stale tracking, non-blocking rebuild/cancel.
- `Tests/Editor/AsteroidAuthoringUXTests.cs`: 3 регресійні тести.
- `README_UX_v1.4.2_UA.md`: сценарії роботи й перевірки.

## Не змінено

- Алгоритми геометрії, композиції, volume/density, layout, LOD, шейдери.
- Формат snapshot/JSON, seed/ID ресурсу та існуючі runtime API.

## Відомі обмеження

- EditMode-тести та компіляція Unity 6 не виконані в цьому середовищі.
- Старі поля не мають `lastBuildSignature`; до першої побудови їхній статус untracked.
- Прев'ю — редакторна 2D проекція розміщення, а не GPU-відтворення всього поясу.
- Налаштування рендерингу залишаються в окремому Field Settings asset.
