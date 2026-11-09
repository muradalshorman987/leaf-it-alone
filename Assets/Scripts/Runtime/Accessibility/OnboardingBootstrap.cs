using FallenYard.Tutorial;
using FallenYard.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallenYard.Accessibility
{
    /// <summary>
    /// Adds WP11's components (options/captions UI, tutorial director, pile highlights) next to the
    /// scene's <see cref="UIManager"/> after every scene load, so no scene or yard builder needs
    /// wiring. A scene can opt out by adding the components itself (they are never doubled) or by
    /// having no UIManager.
    /// </summary>
    public static class OnboardingBootstrap
    {
        static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (!_hooked)
            {
                SceneManager.sceneLoaded += (_, __) => Install();
                _hooked = true;
            }
            Install();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _hooked = false;

        /// <summary>Adds any missing component to the UIManager's GameObject. Returns false if there is no UIManager.</summary>
        public static bool Install(UIManager ui = null)
        {
            if (ui == null) ui = UIManager.Instance != null ? UIManager.Instance : Object.FindFirstObjectByType<UIManager>();
            if (ui == null) return false;
            var go = ui.gameObject;
            if (Object.FindFirstObjectByType<TutorialDirector>() == null) go.AddComponent<TutorialDirector>();
            if (go.GetComponent<OnboardingUI>() == null) go.AddComponent<OnboardingUI>();
            if (Object.FindFirstObjectByType<PileHighlighter>() == null) go.AddComponent<PileHighlighter>();
            return true;
        }
    }
}
