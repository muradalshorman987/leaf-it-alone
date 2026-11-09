using System;
using System.IO;
using UnityEngine;

namespace FallenYard.Save
{
    /// <summary>Where the data returned by the last <see cref="SaveSystem.Load"/> came from.</summary>
    public enum SaveLoadSource
    {
        /// <summary>No usable file: a new game.</summary>
        Fresh,
        /// <summary>The main save file.</summary>
        Main,
        /// <summary>The main file was missing or corrupt, so the previous save (.bak) was used.</summary>
        Backup,
    }

    /// <summary>
    /// JSON save file in Application.persistentDataPath.
    ///
    /// - Writes go to a .tmp file first, then replace the real file, so a crash mid-write
    ///   never leaves a half-written save.
    /// - The previous save is kept as .bak and used if the main file is corrupt.
    /// - Older versions are migrated on load (see <see cref="SaveMigrator"/>), and the
    ///   original file is copied to .v{N}.bak first so a bad migration can be undone.
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "fallenyard_save.json";

        /// <summary>
        /// Folder the save file lives in. Null means Application.persistentDataPath.
        /// Tests point this at a temp folder so they never touch a real save.
        /// </summary>
        public static string DirectoryOverride { get; set; }

        /// <summary>Folder for save and settings files.</summary>
        public static string Folder =>
            string.IsNullOrEmpty(DirectoryOverride) ? Application.persistentDataPath : DirectoryOverride;

        public static string FilePath => Path.Combine(Folder, FileName);

        /// <summary>The previous successful save.</summary>
        public static string BackupPath => FilePath + ".bak";

        /// <summary>Copy of a file as it was before migrating from (or loading a file written by) another version.</summary>
        public static string VersionBackupPath(int version) => FilePath + ".v" + version + ".bak";

        public static SaveLoadSource LastLoadSource { get; private set; } = SaveLoadSource.Fresh;

        /// <summary>Version of the file on disk before migration, or -1 for a fresh game.</summary>
        public static int LastLoadedFileVersion { get; private set; } = -1;

        /// <summary>Raised after every successful write, with the data that was written. Used by UI ("Saved") toasts.</summary>
        public static event Action<SaveData> Saved;

        public static SaveData Load()
        {
            if (TryLoadFile(FilePath, true, out var data, out int fileVersion))
            {
                LastLoadSource = SaveLoadSource.Main;
                LastLoadedFileVersion = fileVersion;
                return data;
            }
            if (TryLoadFile(BackupPath, false, out data, out fileVersion))
            {
                Debug.LogWarning("[FallenYard] Save file was missing or unreadable, restored the previous save.");
                LastLoadSource = SaveLoadSource.Backup;
                LastLoadedFileVersion = fileVersion;
                return data;
            }
            LastLoadSource = SaveLoadSource.Fresh;
            LastLoadedFileVersion = -1;
            return new SaveData();
        }

        /// <summary>
        /// Parses save JSON and migrates it to the current version. Returns null for empty or
        /// corrupt input. Exposed for tests and for importing saves.
        /// </summary>
        public static SaveData FromJson(string json, out int fileVersion)
        {
            fileVersion = -1;
            if (string.IsNullOrWhiteSpace(json)) return null;
            // Read the version on its own: SaveData's initializer would report CurrentVersion
            // for a file that has no version field at all.
            var probe = JsonUtility.FromJson<VersionProbe>(json);
            var data = JsonUtility.FromJson<SaveData>(json);
            if (probe == null || data == null) return null;
            fileVersion = probe.version;
            data.version = probe.version;
            SaveMigrator.Migrate(data);
            return data;
        }

        public static string ToJson(SaveData data) => JsonUtility.ToJson(data, true);

        public static void Save(SaveData data)
        {
            if (data == null) return;
            SaveRegistry.CaptureAll(data);
            try
            {
                Directory.CreateDirectory(Folder);
                // Never let an older build overwrite a newer file's version number.
                data.version = Math.Max(data.version, SaveData.CurrentVersion);
                data.savedAtUtcTicks = DateTime.UtcNow.Ticks;

                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, ToJson(data));
                if (File.Exists(FilePath))
                {
                    // Only keep files that still parse as the backup, so one bad write can't destroy both.
                    if (IsReadable(FilePath)) File.Copy(FilePath, BackupPath, true);
                    File.Delete(FilePath);
                }
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[FallenYard] Save failed: {e.Message}");
                return;
            }
            Saved?.Invoke(data);
        }

        /// <summary>Deletes the save, its backup and any version backups. Settings are kept.</summary>
        public static void Delete()
        {
            TryDelete(FilePath);
            TryDelete(BackupPath);
            TryDelete(FilePath + ".tmp");
            for (int v = 0; v <= SaveData.CurrentVersion + 8; v++) TryDelete(VersionBackupPath(v));
        }

        static bool TryLoadFile(string path, bool isMain, out SaveData data, out int fileVersion)
        {
            data = null;
            fileVersion = -1;
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path);
                data = FromJson(json, out fileVersion);
                if (data == null) return false;

                if (isMain && fileVersion != SaveData.CurrentVersion)
                {
                    string versionBackup = VersionBackupPath(fileVersion);
                    if (!File.Exists(versionBackup)) File.WriteAllText(versionBackup, json);
                    if (fileVersion > SaveData.CurrentVersion)
                        Debug.LogWarning($"[FallenYard] Save is from a newer build (v{fileVersion}, this build reads v{SaveData.CurrentVersion}). A copy was kept at {versionBackup}.");
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FallenYard] Could not read {Path.GetFileName(path)}: {e.Message}");
                data = null;
                return false;
            }
        }

        static bool IsReadable(string path)
        {
            try
            {
                return FromJson(File.ReadAllText(path), out _) != null;
            }
            catch
            {
                return false;
            }
        }

        static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FallenYard] Could not delete {Path.GetFileName(path)}: {e.Message}");
            }
        }

        [Serializable]
        internal class VersionProbe
        {
            public int version;
        }

        /// <summary>Resets static state before each play session (domain reload may be off).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Saved = null;
            LastLoadSource = SaveLoadSource.Fresh;
            LastLoadedFileVersion = -1;
        }
    }
}
