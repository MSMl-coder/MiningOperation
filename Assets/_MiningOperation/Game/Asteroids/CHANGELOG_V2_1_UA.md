# v2.1 — Cleanup & Rendering

## Runtime
- Новий `AsteroidVisibilityGrid`: просторові комірки з консервативними bounds; лише віддалені/видимі комірки потрапляють до списку GPU instances.
- `AsteroidFieldFarRenderer` та `AsteroidDebrisRenderer` тепер використовують видимі комірки; micro fragments не генеруються за замовчуванням у новому профілі.
- Зменшено subdivision декоративних GPU мешів до 1 для зниження навантаження.
- Діагностика пояснює дистанцію до найближчого астероїда та near spawn threshold.
- Мінімальний ambient у far shader; полагоджено тип повернення небесного HLSL-шейдера.
- Проста безпечна заміна на solid-color background, якщо шейдер не підтримується.

## Editor
- Одне головне меню `Tools/Mining Operation/Asteroid Field Studio`; інші вікна доступні кнопками.
- `Repair Sky + Lighting / Remove Old Visuals` очищає тільки автоматично згенероване старе освітлення/планету; не чіпає геологію та ресурси.
- `Balanced Rendering` змінює лише графічні параметри shared Field Settings із підтвердженням.
- `Frame Real Asteroid` переносить тестову камеру до записаного астероїда й налаштовує Viewer.
- Команда opt-in для видалення 2 застарілих Editor-скриптів; самі скрипти не входять до нового FULL.
- Прибрана генерація planet і зайвих Color Grading Volume з нової сцени.

## Сумісність
- Геометричні версії, Resource Catalog, Field Snapshot і збережені ID **не змінюються**.
- Старі `.asset` не модифікуються автоматично; переналаштування спільних presets можливе тільки вручну.
- Legacy геометрія v1 залишається як compatibility code.
