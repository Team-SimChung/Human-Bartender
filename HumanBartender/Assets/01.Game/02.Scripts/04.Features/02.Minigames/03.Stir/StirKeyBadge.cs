using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 잔 둘레 4방위에 놓이는 키 노드 하나. 현재 방위인지, 지금 눌러야 하는 방위인지를 테두리와 글자색으로 알린다.
///
/// 아트가 연결된 화면은 대기(C)·현재(A)·다음(B) 이미지를 쓴다.
/// 이전 프로토타입은 아트 참조를 비워 두면 기존 색/펄스 표시를 그대로 사용한다.
/// </summary>
public class StirKeyBadge : MonoBehaviour
{
    /// <summary>노드 상태. 프로토타입의 기본 / .position / .target에 대응한다.</summary>
    public enum EState
    {
        Idle,
        Current,    // 스푼이 가리키는 방위 (.position)
        Next,       // 지금 눌러야 하는 방위 (.target)
    }

    [SerializeField] Image frame;
    [SerializeField] TMP_Text letter;
    [SerializeField] TMP_Text alias;
    [Tooltip("노드 아래 작은 순번 표시(START / 04, 01 …). 상태에 따라 바뀌지 않는다.")]
    [SerializeField] TMP_Text stepOrder;

    [Header("Artwork (optional)")]
    [SerializeField] Sprite idleSprite;
    [SerializeField] Sprite currentSprite;
    [SerializeField] Sprite nextSprite;
    [Tooltip("원본 픽셀을 UI 단위로 환산한다. 잘린 여백이 다른 세 상태의 실물 크기를 맞춘다.")]
    [SerializeField] float artworkScale = 0.25f;

    [Header("Idle")]
    [SerializeField] Color idleFrame = new Color(0.67f, 0.79f, 0.79f, 0.30f);
    [SerializeField] Color idleText = new Color(0.93f, 0.97f, 0.96f, 0.58f);

    [Header("Current (.position)")]
    [SerializeField] Color currentFrame = new Color(0.41f, 0.96f, 0.88f);
    [SerializeField] Color currentText = new Color(0.41f, 0.96f, 0.88f);

    [Header("Next (.target)")]
    [SerializeField] Color nextFrame = new Color(0.87f, 1f, 0.42f);
    [SerializeField] Color nextText = new Color(0.87f, 1f, 0.42f);
    [Tooltip("펄스로 오갈 밝기 배수. 프로토타입 targetPulse(0.92~1.25)와 같은 폭이다.")]
    [SerializeField] float pulseMin = 0.92f;
    [SerializeField] float pulseMax = 1.25f;
    [SerializeField] float pulseSpeed = 2f;

    EState state = EState.Idle;

    public void SetLabels(string key, string arrow, string order)
    {
        if (letter != null) letter.text = key;
        if (alias != null) alias.text = arrow;
        if (stepOrder != null) stepOrder.text = order;
    }

    public void SetState(EState value)
    {
        state = value;

        Sprite sprite = state == EState.Current ? currentSprite :
            state == EState.Next ? nextSprite : idleSprite;
        if (frame != null && sprite != null)
        {
            frame.sprite = sprite;
            frame.rectTransform.sizeDelta = sprite.rect.size * artworkScale;
            frame.color = Color.white;
            Color textColor = state == EState.Current ? currentText :
                state == EState.Next ? nextText : idleText;
            if (letter != null) letter.color = textColor;
            if (alias != null) alias.color = textColor;
            return;
        }

        // Next는 Update에서 밝기가 계속 변하므로 여기서는 나머지 두 상태만 확정한다.
        if (state == EState.Idle) Apply(idleFrame, idleText, 1f);
        else if (state == EState.Current) Apply(currentFrame, currentText, 1f);
    }

    void Update()
    {
        if (state != EState.Next) return;
        if (nextSprite != null) return;

        float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        Apply(nextFrame, nextText, Mathf.Lerp(pulseMin, pulseMax, t));
    }

    void Apply(Color frameColor, Color textColor, float brightness)
    {
        if (frame != null) frame.color = Scale(frameColor, brightness);

        Color scaled = Scale(textColor, brightness);
        if (letter != null) letter.color = scaled;
        if (alias != null) alias.color = new Color(scaled.r, scaled.g, scaled.b, scaled.a * 0.7f);
    }

    /// <summary>알파는 두고 RGB만 밝기를 곱한다 — 알파까지 곱하면 펄스가 깜빡임처럼 보인다.</summary>
    static Color Scale(Color color, float brightness) => new Color(
        Mathf.Clamp01(color.r * brightness),
        Mathf.Clamp01(color.g * brightness),
        Mathf.Clamp01(color.b * brightness),
        color.a);
}
