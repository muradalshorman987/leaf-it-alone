using System.Collections.Generic;
using FallenYard.Core;
using FallenYard.Leaves;
using FallenYard.Player;
using FallenYard.Progression;
using UnityEngine;

namespace FallenYard.Levels
{
    /// <summary>
    /// Sets the scene up for its <see cref="YardDefinition"/> before anything else starts:
    /// leaf count, mix and spawn zones on the <see cref="LeafField"/>, the goal on
    /// <see cref="YardProgress"/>, the yard id for saves, this yard's wind, and the player's
    /// start point. Raises <see cref="GameEvents.YardLoaded"/> on Start.
    /// One per yard scene; Fallen Yard > Build All Yards adds it.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public class YardLoader : MonoBehaviour
    {
        public static YardLoader Instance { get; private set; }

        public YardDefinition definition;

        [Tooltip("Build the greybox (ground, fence, props, compost bin) when the scene starts. " +
                 "Off for scenes made by Build All Yards, which already contain it.")]
        public bool buildGreyboxAtRuntime;

        [Tooltip("Move the player to the yard's start point on load.")]
        public bool placePlayer = true;

        public YardBuildResult Greybox { get; private set; }
        public bool Applied { get; private set; }

        readonly List<Object> _owned = new List<Object>();

        void Awake()
        {
            Instance = this;
            Apply();
        }

        void Start()
        {
            if (definition != null) GameEvents.RaiseYardLoaded(definition.yardId);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var o in _owned)
                if (o != null) DestroySafe(o);
            _owned.Clear();
        }

        /// <summary>Applies the definition to the scene. Safe to call once; later calls do nothing.</summary>
        public void Apply()
        {
            if (Applied) return;
            if (definition == null)
            {
                Debug.LogError("[YardLoader] No YardDefinition assigned.", this);
                return;
            }
            Applied = true;

            YardMaterialSet runtimeMats = null;
            if (buildGreyboxAtRuntime)
            {
                runtimeMats = YardMaterialSet.CreateRuntime(definition.palette);
                _owned.AddRange(runtimeMats.All());
                Greybox = YardGreybox.Build(definition, runtimeMats, transform);
            }

            ConfigureLeaves(FindFirstObjectByType<LeafField>(), runtimeMats);

            var progress = FindFirstObjectByType<YardProgress>();
            if (progress != null)
            {
                progress.yardId = definition.yardId;
                progress.completeAtFraction = definition.completeAtFraction;
                progress.completionBonus = definition.completionBonus;
            }

            var meta = FindFirstObjectByType<MetaProgressionService>();
            if (meta != null) meta.currentYardId = definition.yardId;

            var flow = FindFirstObjectByType<LevelFlow>();
            if (flow != null) flow.currentYardId = definition.yardId;

            var wind = FindFirstObjectByType<WindSystem>();
            if (wind != null)
            {
                wind.settings ??= new GustSettings();
                definition.weather.ApplyTo(wind.settings);
                wind.seed = definition.seed + 7;
            }

            if (placePlayer) PlacePlayer(FindFirstObjectByType<FirstPersonController>());
        }

        void ConfigureLeaves(LeafField field, YardMaterialSet runtimeMats)
        {
            if (field == null) return;
            field.transform.position = transform.position;
            field.yardSize = definition.size;
            field.leafCount = definition.leafCount;
            field.seed = definition.seed;
            field.SpawnSampler = new YardLeafSampler(definition);

            var types = new List<LeafTypeDefinition>();
            foreach (var entry in definition.leafMix)
            {
                if (entry == null || entry.weight <= 0f) continue;
                var t = ScriptableObject.CreateInstance<LeafTypeDefinition>();
                t.hideFlags = HideFlags.DontSave;
                t.ApplyPreset(entry.preset);
                t.spawnWeight = entry.weight;
                types.Add(t);
                _owned.Add(t);
            }
            if (types.Count > 0) field.leafTypes = types.ToArray();

            if (field.leafMaterials == null || field.leafMaterials.Length == 0)
            {
                if (runtimeMats == null)
                {
                    runtimeMats = YardMaterialSet.CreateRuntime(definition.palette);
                    _owned.AddRange(runtimeMats.All());
                }
                field.leafMaterials = runtimeMats.leaves;
            }
        }

        /// <summary>World position of a yard-local point (ground level).</summary>
        public Vector3 ToWorld(Vector2 local) => transform.position + new Vector3(local.x, 0f, local.y);

        void PlacePlayer(FirstPersonController player)
        {
            if (player == null) return;
            var cc = player.GetComponent<CharacterController>();
            bool wasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(ToWorld(definition.playerSpawn) + Vector3.up * 0.05f,
                Quaternion.Euler(0f, definition.playerYaw, 0f));
            if (cc != null) cc.enabled = wasEnabled;
        }

        static void DestroySafe(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
