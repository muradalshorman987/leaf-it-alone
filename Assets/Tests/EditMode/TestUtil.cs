using System;
using System.IO;
using System.Reflection;
using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Save;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FallenYard.Tests
{
    /// <summary>
    /// Helpers for EditMode tests. In EditMode Unity does not call Awake/Update on
    /// MonoBehaviours, so tests call them by reflection and set the static Instance
    /// properties the runtime code relies on.
    /// </summary>
    public static class TestUtil
    {
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static void Call(object target, string method)
        {
            var m = target.GetType().GetMethod(method, AnyInstance, null, Type.EmptyTypes, null);
            if (m == null) throw new MissingMethodException(target.GetType().Name, method);
            m.Invoke(target, null);
        }

        public static void SetStaticInstance<T>(T value) where T : class
        {
            var p = typeof(T).GetProperty("Instance", AnyStatic);
            if (p == null) throw new MissingMemberException(typeof(T).Name, "Instance");
            p.SetValue(null, value);
        }

        /// <summary>Points the save system at a fresh temp folder and returns it.</summary>
        public static string UseTempSaveFolder()
        {
            string dir = Path.Combine(Path.GetTempPath(), "FallenYardTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            SaveSystem.DirectoryOverride = dir;
            return dir;
        }

        public static void ReleaseTempSaveFolder(string dir)
        {
            SaveSystem.DirectoryOverride = null;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        /// <summary>Creates a GameManager backed by the temp save folder (call UseTempSaveFolder first).</summary>
        public static GameManager CreateGameManager()
        {
            if (string.IsNullOrEmpty(SaveSystem.DirectoryOverride))
                throw new InvalidOperationException("Call TestUtil.UseTempSaveFolder() before CreateGameManager().");
            var gm = new GameObject("GameManager (test)").AddComponent<GameManager>();
            gm.autosaveInterval = 0f;
            Call(gm, "Awake");
            return gm;
        }

        /// <summary>Creates and spawns a LeafField at the origin and registers it as LeafField.Instance.</summary>
        public static LeafField CreateLeafField(Vector2 yardSize, float cellSize, int leafCount, int seed = 1)
        {
            var field = new GameObject("LeafField (test)").AddComponent<LeafField>();
            field.yardSize = yardSize;
            field.cellSize = cellSize;
            field.leafCount = leafCount;
            field.spawnOnStart = false;
            Call(field, "Awake");
            field.Spawn(seed);
            return field;
        }

        public static void DestroyLeafField(LeafField field)
        {
            if (field == null) return;
            // OnDestroy doesn't run in EditMode; call it so the field frees its NativeArrays.
            Call(field, "OnDestroy");
            if (field.leafMesh != null) Object.DestroyImmediate(field.leafMesh);
            Object.DestroyImmediate(field.gameObject);
            SetStaticInstance<LeafField>(null);
        }

        /// <summary>
        /// Destroys a singleton-style component and clears its static Instance
        /// (OnDestroy does not run for components whose Awake we invoked by hand).
        /// </summary>
        public static void DestroySingleton<T>(T c) where T : Component
        {
            if (c != null) Object.DestroyImmediate(c.gameObject);
            SetStaticInstance<T>(null);
        }

        public static void Destroy(Component c)
        {
            if (c != null) Object.DestroyImmediate(c.gameObject);
        }
    }
}
