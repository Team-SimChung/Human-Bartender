using UnityEngine.UIElements;

namespace NKStudio
{
    internal static class Unity6CaptionGUI
    {
        internal static void RemoveCaption(VisualElement element)
        {
            if (element  == null )
                return;
            
            var caption = element.Q<VisualElement>("ToolbarProductCaption");

            if (caption != null)
                element.Remove(caption);
        }
    }
}