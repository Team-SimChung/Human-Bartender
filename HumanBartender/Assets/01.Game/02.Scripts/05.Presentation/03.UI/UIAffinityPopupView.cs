using TMPro;
using UnityEngine;

/// <summary>상승/하락 UI에서 공통 사용한다. 전달 문자열은 표시할 인물 이름이다.</summary>
public sealed class UIAffinityPopupView : UIPopupView
{
    [SerializeField] TMP_Text characterName;

    protected override bool Prepare(string data)
    {
        if (characterName == null || string.IsNullOrWhiteSpace(data)) return false;
        characterName.text = data;
        return true;
    }
}
