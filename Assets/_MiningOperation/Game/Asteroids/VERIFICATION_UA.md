# Mining Operation Asteroid Field Generator v1.2 — перевірка пакета

## Перевірено в контейнері

- У комплекті наявні всі вихідні C# файли Asteroid Generator v1.1 і нового `Runtime/Fields`.
- Типи формацій відповідають Editor-інструменту та тестам: CrescentBelt, BrokenBelt, BrokenRing, ClusterGroups, Stream, SphericalCloud.
- Структура assembly definitions збережена: Runtime, Editor та Tests/EditMode.
- Генератор поля застосовує spatial hash для обмеження перевірок перетинів при розміщенні багатьох астероїдів.
- `BuildStep` створює повні дані астероїдів поступово, `StreamAround` керує кількістю GameObject, `ExtractResourcesById` працює без мешів, `CaptureJson`/`LoadJson` зберігають стан поля.
- Перевірено баланс фігурних дужок у C# файлах; архів перевірено за CRC.

## Не перевірено

- Реальна компіляція у Unity 6 / URP (Unity Editor у цьому контейнері відсутній).
- Фактичний результат Unity Test Runner / NUnit.
- Профілювання CPU, GPU, RAM і GC на цільовій платформі.
- Гарантована геометрична відсутність перетинів за фактичними сітками Mesh (перевіряється консервативна оболонка).

## Рекомендована перевірка у Unity

1. Імпортуйте модуль і переконайтесь, що Console не показує C# помилок.
2. Відкрийте Test Runner > EditMode і запустіть AsteroidGeneratorTests та AsteroidFieldTests.
3. У Field Studio перевірте шість типів формацій у Preview.
4. Створіть поле на 100–150 астероїдів, призначте Viewer і перевірте Mesh/LOD під час переміщення камерою.
5. Запустіть ExtractResourcesById; збережіть JSON і відновіть його, перевіривши залишкову масу.
6. Профілюйте поле зі 100, 500 і 1000 записів і поступово збільшуйте maxActiveAsteroids.
