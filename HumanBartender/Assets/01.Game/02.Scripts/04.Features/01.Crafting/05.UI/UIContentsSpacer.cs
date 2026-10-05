using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.UI;

/// <summary>LayoutGroup 내에서 빈 간격(Spacer)의 크기를 코드로 조절하기 위한 래퍼.</summary>
public class UIContentsSpacer : MonoBehaviour
{
    [SerializeField] LayoutElement element;

    /// <summary>스페이서의 최소 너비를 지정한다.</summary>
    public void SetElementWidth(float v)
    {
        element.minWidth = v;
    }

    /// <summary>스페이서의 최소 높이를 지정한다.</summary>
    public void SetElementHeight(float v)
    {
        element.minHeight = v;
    }
}
