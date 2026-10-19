using UnityEditor;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal static class StudioSerializedFields
    {
        // 공개 속성 이름을 Unity가 저장하는 비공개 필드 경로로 변환함.
        internal static SerializedProperty Find(SerializedObject data, string path)
        {
            string[] parts = path.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "Array" || parts[i].Length == 0)
                    continue;
                parts[i] = char.ToLowerInvariant(parts[i][0]) + parts[i].Substring(1);
            }

            return data.FindProperty(string.Join(".", parts));
        }
    }
}
