# Verification — v1.3

- Архів цілісний: перевірити `unzip -t` (виконується перед видачею користувачеві).
- Збережено наявні назви файлів, класи, namespace й `.asmdef` з v1.2.1; нові поля `AsteroidFieldSettings` адитивні.
- Не змінюється `AsteroidFieldSnapshot` JSON, `AsteroidData` або моделі ресурсів.
- Вбудовані EditMode-тести додані; вони **не виконані** в цьому середовищі.
- C# та ShaderLab перевірені лише статично; відсутній Unity Editor / Unity Package URP для реальної компіляції.
- Unity runtime GPU-instancing, Frame Debugger, перевірка переходів LOD, сцени Foundation та керування камерою потребують Play Mode в локальному проєкті.
