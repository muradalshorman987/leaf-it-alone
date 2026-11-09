using UnityEngine.UIElements;

namespace FallenYard.UI
{
    /// <summary>Small helpers shared by the code-built UI Toolkit views.</summary>
    public static class UIViewUtil
    {
        public static T With<T>(this T element, string name, params string[] classes) where T : VisualElement
        {
            if (!string.IsNullOrEmpty(name)) element.name = name;
            foreach (var c in classes) element.AddToClassList(c);
            return element;
        }

        public static Label MakeLabel(string name, string text, params string[] classes) =>
            new Label(text).With(name, classes);

        public static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null) return;
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static bool IsVisible(VisualElement element) =>
            element != null && element.style.display.value != DisplayStyle.None;
    }
}
