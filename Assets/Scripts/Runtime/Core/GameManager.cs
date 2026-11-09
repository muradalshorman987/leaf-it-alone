using System;
using FallenYard.Save;
using UnityEngine;

namespace FallenYard.Core
{
    /// <summary>
    /// Owns the save data, the coin wallet and autosaving.
    /// Put exactly one in the scene (the test-yard builder does this for you).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Tooltip("Seconds between autosaves. 0 disables autosave.")]
        public float autosaveInterval = 30f;

        public SaveData Data { get; private set; }
        public int Coins => Data.coins;

        public event Action<int> OnCoinsChanged;
        public event Action<string> OnYardCompleted;

        float _nextAutosave;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Data = SaveSystem.Load();
            _nextAutosave = Time.unscaledTime + autosaveInterval;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (autosaveInterval > 0f && Time.unscaledTime >= _nextAutosave)
            {
                Save();
                _nextAutosave = Time.unscaledTime + autosaveInterval;
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        void OnApplicationQuit() => Save();

        public void Save() => SaveSystem.Save(Data);

        public void AddCoins(int amount)
        {
            if (amount == 0) return;
            Data.coins = Mathf.Max(0, Data.coins + amount);
            OnCoinsChanged?.Invoke(Data.coins);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Data.coins < amount) return false;
            Data.coins -= amount;
            OnCoinsChanged?.Invoke(Data.coins);
            return true;
        }

        public void RecordLeavesBagged(int count) => Data.totalLeavesBagged += count;

        public bool IsYardCompleted(string yardId) => Data.completedYards.Contains(yardId);

        public void MarkYardCompleted(string yardId)
        {
            if (string.IsNullOrEmpty(yardId) || Data.completedYards.Contains(yardId)) return;
            Data.completedYards.Add(yardId);
            Save();
            OnYardCompleted?.Invoke(yardId);
        }

        [ContextMenu("Delete Save File")]
        public void ResetSave()
        {
            SaveSystem.Delete();
            Data = new SaveData();
            OnCoinsChanged?.Invoke(Data.coins);
        }
    }
}
