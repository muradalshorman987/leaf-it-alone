using System;
using FallenYard.Core;
using FallenYard.Progression;
using FallenYard.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallenYard.Levels
{
    /// <summary>
    /// Moves the player between yards. Each yard is its own scene with its own managers, so
    /// travelling is: save, flag "skip the title", load the yard's scene. Listens for
    /// <see cref="GameEvents.YardTravelRequested"/> from the UI. Put one next to the GameManager.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class LevelFlow : MonoBehaviour
    {
        public static LevelFlow Instance { get; private set; }

        public YardCatalog catalog;

        [Tooltip("Yard in this scene. Set by the scene's YardLoader.")]
        public string currentYardId;

        /// <summary>(yard id, scene name) just before the scene load starts.</summary>
        public event Action<string, string> TravelStarted;

        /// <summary>Loads a scene by name. Tests replace it so nothing is actually loaded.</summary>
        public Func<string, bool> sceneLoader = DefaultLoadScene;

        static SaveData Data => GameManager.Instance != null ? GameManager.Instance.Data : null;

        public YardDefinition CurrentYard => catalog != null ? catalog.Get(currentYardId) : null;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable() => GameEvents.YardTravelRequested += HandleTravelRequested;
        void OnDisable() => GameEvents.YardTravelRequested -= HandleTravelRequested;

        void HandleTravelRequested(string yardId) => TryTravel(yardId);

        public YardDefinition GetYard(string yardId) => catalog != null ? catalog.Get(yardId) : null;

        public YardStatus GetStatus(string yardId) => YardFlow.Status(Data, catalog, yardId);

        /// <summary>Where "Continue" should go from the title menu.</summary>
        public string ContinueTarget => YardFlow.ContinueTarget(Data, catalog);

        /// <summary>The next yard in town after this one, with its status.</summary>
        public (YardDefinition yard, YardStatus status) NextYard => YardFlow.Next(Data, catalog, currentYardId);

        /// <summary>
        /// Loads <paramref name="yardId"/> if it's playable and not the yard we're in.
        /// Returns true if a load started.
        /// </summary>
        public bool TryTravel(string yardId)
        {
            if (string.IsNullOrEmpty(yardId) || yardId == currentYardId) return false;
            var data = Data;
            if (!YardFlow.IsPlayable(data, catalog, yardId))
            {
                Debug.LogWarning($"[LevelFlow] '{yardId}' isn't playable yet.", this);
                return false;
            }
            string scene = YardFlow.SceneFor(catalog, yardId);
            if (scene == null) return false;

            if (data != null) data.lastYardId = yardId;
            if (GameManager.Instance != null) GameManager.Instance.Save();
            GameState.SkipTitleOnNextLoad = true;
            TravelStarted?.Invoke(yardId, scene);
            if (sceneLoader != null && sceneLoader(scene)) return true;

            GameState.SkipTitleOnNextLoad = false;
            return false;
        }

        /// <summary>Title "Continue": travels if the save points at another yard. False means "play here".</summary>
        public bool TryContinueElsewhere()
        {
            string target = ContinueTarget;
            return !string.IsNullOrEmpty(target) && target != currentYardId && TryTravel(target);
        }

        static bool DefaultLoadScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[LevelFlow] Scene '{sceneName}' isn't in Build Settings. Run Fallen Yard > Build All Yards.");
                return false;
            }
            SceneManager.LoadScene(sceneName);
            return true;
        }
    }
}
