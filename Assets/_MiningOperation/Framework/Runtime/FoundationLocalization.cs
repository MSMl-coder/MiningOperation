using System;
using System.Collections.Generic;

namespace MiningOperation.Framework
{
    /// <summary>Minimal offline localization adapter for the Foundation menus.
    /// Replaced later by Unity Localization tables without changing screen consumers.</summary>
    public static class FoundationLocalization
    {
        private static readonly Dictionary<string, string> Ukrainian = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            {"FOUNDATION v0.4  /  SETTINGS", "FOUNDATION v0.6 / КЕРУВАННЯ · АУДІО · МОВА"},
            {"NEW GAME", "НОВА ГРА"}, {"LOAD GAME", "ЗАВАНТАЖИТИ ГРУ"},
            {"SETTINGS", "НАЛАШТУВАННЯ"}, {"EXIT", "ВИХІД"},
            {"Saves are stored in Application.persistentDataPath", "Збереження знаходяться у Application.persistentDataPath"},
            {"CREATE SESSION", "СТВОРИТИ СЕСІЮ"}, {"Session name", "Назва сесії"},
            {"CREATE AND ENTER", "СТВОРИТИ Й УВІЙТИ"}, {"BACK", "НАЗАД"},
            {"LOAD SESSION", "ЗАВАНТАЖИТИ СЕСІЮ"}, {"Reading slots...", "Читання слотів..."},
            {"No saves found", "Збережень не знайдено"},
            {"No active session. Return to menu and select NEW GAME or LOAD GAME.", "Немає активної сесії. Поверніться до меню й оберіть нову гру або завантаження."},
            {"RETURN TO MENU", "ДО ГОЛОВНОГО МЕНЮ"},
            {"FOUNDATION READY", "ОСНОВА ГОТОВА"},
            {"SAVE SESSION", "ЗБЕРЕГТИ СЕСІЮ"},
            {"SAVE & RETURN TO MENU", "ЗБЕРЕГТИ Й ПОВЕРНУТИСЯ"},
            {"RETURN WITHOUT SAVING", "ПОВЕРНУТИСЯ БЕЗ ЗБЕРЕЖЕННЯ"},
            {"GRAPHICS", "ГРАФІКА"}, {"AUDIO", "ЗВУК"}, {"GENERAL", "ЗАГАЛЬНІ"},
            {"CONTROLS", "КЕРУВАННЯ"}, {"Quality preset", "Якість графіки"},
            {"VSync", "Вертикальна синхронізація"},
            {"FPS limit (when VSync is off)", "Обмеження FPS (без VSync)"},
            {"Master", "Загальна гучність"}, {"Music", "Музика"}, {"SFX", "Ефекти"},
            {"UI", "Інтерфейс"}, {"Ambient", "Оточення"}, {"Voice", "Голос / радіо"},
            {"Language preference", "Мова інтерфейсу"},
            {"Autosave enabled*", "Автозбереження*"},
            {"Autosave interval, minutes*", "Інтервал автозбереження, хв*"},
            {"Notifications*", "Сповіщення*"},
            {"* Persisted preferences; gameplay integration is scheduled for later modules.", "* Параметри зберігаються; підключення до геймплею буде згодом."},
            {"APPLY AND SAVE", "ЗАСТОСУВАТИ Й ЗБЕРЕГТИ"},
            {"RESTORE DEFAULTS", "ВІДНОВИТИ СТАНДАРТНІ"}, {"CANCEL", "СКАСУВАТИ"},
            {"Preferences file: MiningOperation/Settings/settings.json", "Файл налаштувань: MiningOperation/Settings/settings.json"},
            {"TEST AUDIO (UI)", "ТЕСТ ЗВУКУ (UI)"},
            {"TEST ACTION", "ТЕСТОВА ДІЯ"},
            {"REBIND TEST ACTION", "ПРИЗНАЧИТИ КЛАВІШУ"},
            {"RESET TEST BINDING", "СКИНУТИ ПРИЗНАЧЕННЯ"},
            {"Press a key (Esc cancels)...", "Натисніть клавішу (Esc — скасувати)..."},
            {"Test action key:", "Клавіша тестової дії:"},
            {"Press assigned key to test input while playing.", "Натисніть призначену клавішу під час гри для перевірки."}
        };

        public static string Language => FoundationSettingsServices.Settings.Current.language;

        public static string Translate(string value)
        {
            if (Language == "uk" && value != null && Ukrainian.TryGetValue(value, out var translation))
                return translation;
            return value;
        }
    }
}
