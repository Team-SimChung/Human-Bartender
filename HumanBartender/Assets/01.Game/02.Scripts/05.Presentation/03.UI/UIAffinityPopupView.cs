using TMPro;
using UnityEngine;

/// <summary>상승/하락 UI에서 공통 사용한다. 전달 문자열은 표시할 인물 이름이다.</summary>
public sealed class UIAffinityPopupView : UIPopupView
{
    [SerializeField] TMP_Text characterName;
    [Tooltip("인물 이름 뒤에 붙는 문구입니다. 예: 호감도 상승!")]
    [SerializeField] string changeText;

    protected override bool Prepare(string data)
    {
        if (characterName == null || string.IsNullOrWhiteSpace(data)) return false;
        characterName.text = data + " " + changeText;
        return true;
    }
}
