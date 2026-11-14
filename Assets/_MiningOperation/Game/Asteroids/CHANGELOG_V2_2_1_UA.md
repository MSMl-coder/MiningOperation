# Asteroid Generator v2.2.1 — зміни

1. `Runtime/AsteroidRockSurface.cs`: більше немає runtime-генерації 256px текстур; спільні імпортовані 1K maps; нормалі 0.18.
2. `Runtime/AsteroidSurfaceUV.cs`: прибраний множник UV 3.5, доданий seed-based поворот UV, UV seam/pole fix.
3. `Textures/*`: два оригінальні 1K PNG для URP/Lit.
4. `Editor/AsteroidRockTextureGenerator.cs`: інсталятор PNG + configure importer, **без переходу на експериментальний шейдер**.
5. `Runtime/AsteroidStableMaterials.cs`: URP/Lit default, shared map references, слабке заповнення тіней, відсутність матеріалу на кожен asteroid.
6. `Runtime/AsteroidGeologyV5.cs`, `AsteroidGeometryGenerator.cs`, `AsteroidGeneration.cs`, `AsteroidSaveUtility.cs`: новий geometryVersion=5, старі V1–V4 збережені.
7. `AsteroidSpin.cs` + рендерери/Field Settings: сильніше обертання, роздільні множники mineable/decorative, синхронний far/near час.
8. `AsteroidFieldStudio`, `AsteroidAuthoringWorkflow`: одна кнопка Install Non-Tiling Rock Surface, нові ready assets одразу отримують карти.
9. `Tests/Editor`: перевірки single-wrap, відтворення V4, часової стабільності обертання; тест замкненості враховує UV-дублікати.
