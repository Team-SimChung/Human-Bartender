using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 범용 텍스트 표시 래퍼(uGUI Text 기반). 텍스트/폰트 크기/색상을 코드로 설정하는 공용 유틸리티 컴포넌트.
/// 주석 처리된 TMP 필드는 인코딩이 깨져 있으나(원래 한글 주석으로 추정) TMP 전환 시도의 흔적으로 보임.
/// </summary>
public class UIContentsText : MonoBehaviour
{
    //public TextMeshProUGUI contextTMPText;      // �̸� �ؽ�Ʈ
    public Text contextText;

    public int defaultFont = 25;

    public void Start()
    {
        SetFontSize(defaultFont);
    }

    /// <summary>표시 텍스트를 설정한다.</summary>
    public void SetText(string text)
    {
        contextText.text = text;
        //contextTMPText.text = text;
    }

    /// <summary>폰트 크기를 설정한다.</summary>
    public void SetFontSize(int size)
    {
        contextText.fontSize = size;
        //contextTMPText.fontSize = size;
    }

    /// <summary>텍스트 색상을 설정한다.</summary>
    public void SetFontColor(Color color)
    {
        contextText.color = color;
        //contextTMPText.color = color;

    }
}
