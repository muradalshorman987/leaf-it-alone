using System;
using System.IO;
using UnityEngine;

namespace FallenYard.Save
{
    /// <summary>JSON save file in Application.persistentDataPath, written atomically.</summary>
    public static class SaveSystem
    {
        const string FileName = "fallenyard_save.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                    if (data != null) return data.Migrate();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FallenYard] Could not read save file, starting fresh. {e.Message}");
            }
            return new SaveData();
        }

        public static void Save(SaveData data)
        {
            if (data == null) return;
            try
            {
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FallenYard] Save failed: {e.Message}");
            }
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
