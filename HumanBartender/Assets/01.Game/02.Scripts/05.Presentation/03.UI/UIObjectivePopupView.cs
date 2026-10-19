using TMPro;
using UnityEngine;

/// <summary>새 목표와 목표 완료 팝업에 전달받은 목표 종류를 표시한다.</summary>
public sealed class UIObjectivePopupView : UIPopupView
{
    [SerializeField] TMP_Text objectiveType;

    protected override bool Prepare(string data)
    {
        if (objectiveType == null || string.IsNullOrWhiteSpace(data)) return false;
        objectiveType.text = data;
        return true;
    }
}
