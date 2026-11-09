using System;
using System.IO;
using FallenYard.Save;
using UnityEngine;

namespace FallenYard.Settings
{
    /// <summary>
    /// Reads and writes <see cref="GameSettings"/> as JSON next to the save file
    /// (same folder, so tests that set SaveSystem.DirectoryOverride stay isolated).
    /// </summary>
    public static class SettingsStore
    {
        const string FileName = "fallenyard_settings.json";

        public static string FilePath => Path.Combine(SaveSystem.Folder, FileName);

        public static bool Exists => File.Exists(FilePath);

        /// <summary>Loads settings, or returns defaults if the file is missing or unreadable. Always sanitized.</summary>
        public static GameSettings Load(int qualityLevelCount = int.MaxValue)
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = JsonUtility.FromJson<GameSettings>(File.ReadAllText(FilePath));
                    if (s != null) return s.Sanitize(qualityLevelCount);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FallenYard] Could not read settings, using defaults. {e.Message}");
            }
            return new GameSettings().Sanitize(qualityLevelCount);
        }

        public static void Save(GameSettings settings)
        {
            if (settings == null) return;
            try
            {
                Directory.CreateDirectory(SaveSystem.Folder);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(settings, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FallenYard] Saving settings failed: {e.Message}");
            }
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
