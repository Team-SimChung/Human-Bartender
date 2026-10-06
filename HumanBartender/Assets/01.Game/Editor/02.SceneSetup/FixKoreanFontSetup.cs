// Editor/FixKoreanFontSetup.cs
// 메뉴: Tools > Tycoon > Fix Korean Font On Scene Text

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 현재 열려있는 씬의 TextMeshProUGUI(비활성 오브젝트 포함)에 NeoDunggeunmo SDF를 일괄 적용한다.
/// 기본 TMP 폰트가 한글을 지원하지 않는 기존 씬 텍스트를 수정할 때 사용한다.
/// 결과를 확인한 뒤 직접 Ctrl+S로 저장해야 한다.
/// </summary>
public static class FixKoreanFontSetup
{
    const string FontAssetPath = "Assets/01.Game/03.Content/05.Fonts/NeoDunggeunmo SDF.asset";

    [MenuItem("Tools/Tycoon/Fix Korean Font On Scene Text")]
    public static void Run()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (font == null)
        {
            Debug.LogError($"[FixKoreanFontSetup] {FontAssetPath}에서 폰트를 찾지 못했습니다.");
            return;
        }

        var texts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        int changed = 0;

        foreach (var text in texts)
        {
            // 프리팹 에셋 자체(씬에 없는 것)는 건드리지 않고, 현재 씬에 배치된 인스턴스만 대상으로 한다.
            if (!text.gameObject.scene.IsValid()) continue;
            if (text.font == font) continue;

            Undo.RecordObject(text, "Fix Korean Font");
            text.font = font;
            EditorUtility.SetDirty(text);
            changed++;
        }

        if (changed > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"[FixKoreanFontSetup] 완료. 텍스트 {changed}개에 폰트를 적용했습니다. Ctrl+S로 씬을 저장하세요.");
    }
}
