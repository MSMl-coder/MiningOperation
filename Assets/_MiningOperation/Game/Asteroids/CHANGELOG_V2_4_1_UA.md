# CHANGELOG v2.4.1

## Geometry
- Added `AsteroidMeshSharpener`.
- V5 asteroids now use creased normals after geology pass (`58°` crease threshold).
- `AsteroidGeologyV5` updated: deeper crater bowls, stronger rims, added inner lip + terrace variation.

## Materials / Surface
- `AsteroidStableMaterials` now prefers the packaged stable rock shader `Mining Operation/Asteroid Rock URP` for rocky asteroids when supported.
- Safe fallback to `Universal Render Pipeline/Lit` preserved.
- `AsteroidRockSurface` now configures both URP/Lit and custom rock shader properties.
- Stronger rock detail parameters and stronger normal influence.

## Texture import
- Rock map installer now sets `rockNormalStrength = 0.28`.
- Texture import raised to `maxTextureSize 2048`, `aniso 8`.

## User-facing goal
- Reduce the blurred / melted look.
- Improve close-up readability before further work on new content types.
