using UnityEditor;
using UnityEngine;

namespace AssetDeleteGuard
{
    internal static class UiText
    {
        private const string LanguageKey = "AssetDeleteGuard.Korean";
        internal static bool Korean
        {
            get { return EditorPrefs.GetBool(LanguageKey, Application.systemLanguage == SystemLanguage.Korean); }
            set { EditorPrefs.SetBool(LanguageKey, value); }
        }
        internal static string Get(string korean, string english) { return Korean ? korean : english; }
    }
}
