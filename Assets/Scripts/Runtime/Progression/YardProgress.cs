using System;
using FallenYard.Core;
using FallenYard.Leaves;
using UnityEngine;

namespace FallenYard.Progression
{
    /// <summary>Tracks how clean the current yard is and rewards finishing it.</summary>
    public class YardProgress : MonoBehaviour
    {
        public string yardId = "yard_01";
        [Range(0.5f, 1f)] public float completeAtFraction = 0.95f;
        public int completionBonus = 250;

        /// <summary>0..1, fraction of leaves bagged.</summary>
        public float CleanFraction => LeafField.Instance != null ? LeafField.Instance.CleanFraction : 0f;
        public bool IsComplete { get; private set; }

        public event Action OnYardCleaned;

        void Update()
        {
            if (IsComplete || LeafField.Instance == null || LeafField.Instance.TotalLeaves == 0) return;
            if (CleanFraction >= completeAtFraction)
            {
                IsComplete = true;
                if (GameManager.Instance != null)
                {
                    bool firstTime = !GameManager.Instance.IsYardCompleted(yardId);
                    if (firstTime) GameManager.Instance.AddCoins(completionBonus);
                    GameManager.Instance.MarkYardCompleted(yardId);
                }
                OnYardCleaned?.Invoke();
            }
        }
    }
}
