# Mining Operation — Environment Generator v2.6.0

Ця версія адаптує генератор під **retro sci-fi asteroid fields** та під **гру, обмежену по глибині Z**.

## Основні цілі версії
- додати кероване **обмеження генерації по осі Z**;
- зробити астероїди **більш читабельними, чистими та добре освітленими**;
- додати локальний **retro sci-fi art direction** для кожного Spawn Point;
- дати швидкий спосіб налаштувати **простий космічний фон** без зайвого шуму.

## Де шукати нові параметри
### LOCATION
- `2D gameplay depth constraint`
  - `Z constraint mode`
  - `Z center`
  - `Half range on Z` / `Plane jitter`
  - `Flatten rotations to plane`

### VISUALS
- `Art direction — this spawn`
  - `Shape style`
  - `Noise suppression`
  - `Crater readability`
  - `Silhouette simplification`
  - `Apply Retro Sci-Fi Preset`

### VISUALS → Shared assets / installation
- `Setup retro sci-fi background & lights`

## Рекомендований швидкий workflow
1. Вибери потрібний Spawn Point.
2. У **VISUALS** натисни `Apply Retro Sci-Fi Preset`.
3. У **LOCATION** перевір `2D gameplay depth constraint`.
4. Натисни `Refresh Preview`.
5. Якщо все ок — `BUILD / REBUILD`.
