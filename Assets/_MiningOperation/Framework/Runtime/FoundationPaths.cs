using System.IO;
using UnityEngine;

namespace MiningOperation.Framework
{
    /// <summary>Storage paths are strictly isolated from DesertWorld saves and preferences.</summary>
    public static class FoundationPaths
    {
        public const string FolderName = "MiningOperation";
        public static string Root => Path.Combine(Application.persistentDataPath, FolderName);
        public static string SaveDirectory => Path.Combine(Root, "Saves");
        public static string SettingsDirectory => Path.Combine(Root, "Settings");
        public static string SettingsFile => Path.Combine(SettingsDirectory, "settings.json");
        public static string BindingFile => Path.Combine(SettingsDirectory, "bindings.json");
        public static string LogsDirectory => Path.Combine(Root, "Logs");
    }
}
