#if USE_LOCALIZATION
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace NKStudio
{
    internal static partial class LocalizationGUI
    {
        private static bool TryGetLocales(out List<string> locales)
        {
            if (LocalizationSettings.HasSettings)
            {
                // 로케일 목록을 가져옴
                var locals = LocalizationEditorSettings.GetLocales();

                if (locals.Count > 0)
                {
                    string[] localNames = new string[locals.Count];

                    for (int i = 0; i < locals.Count; i++)
                        localNames[i] = locals[i].ToString();

                    locales = localNames.ToList();
                    return true;
                }
            }

            locales = null;
            return false;
        }

        /// <summary>
        /// 현재 선택된 로컬 인덱스를 반환합니다.
        /// </summary>
        /// <returns>현재 로컬 인덱스</returns>
        private static int CurrentLocalIndex
        {
            get
            {
                Locale currentLocal = LocalizationSettings.SelectedLocale;
                ReadOnlyCollection<Locale> locals = LocalizationEditorSettings.GetLocales();

                for (int i = 0; i < locals.Count; i++)
                {
                    if (currentLocal == locals[i])
                        return i;
                }

                return 0;
            }
        }
    }
}
#endif