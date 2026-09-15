using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 좌측 버튼을 눌러 열고 닫는 슬라이드 패널. panel의 anchoredPosition.x를 화면 밖(왼쪽, -width)과
/// 0 사이로 애니메이션한다. 제조 UI 등 실제 콘텐츠는 panel의 자식으로 추후 채워 넣으면 된다.
/// </summary>
public class LeftSlidePanel : MonoBehaviour
{
    [SerializeField] RectTransform panel;
    [SerializeField] Button toggleButton;
    [SerializeField] float duration = 0.3f;

    float closedX;
    float openX;
    bool isOpen;
    Coroutine anim;

    void Awake()
    {
        closedX = -panel.rect.width;
        openX = 0f;

        panel.anchoredPosition = new Vector2(closedX, panel.anchoredPosition.y);

        if (toggleButton != null)
            toggleButton.onClick.AddListener(Toggle);
    }

    /// <summary>열려있으면 닫고, 닫혀있으면 연다.</summary>
    public void Toggle()
    {
        SetOpen(!isOpen);
    }

    public void Open() => SetOpen(true);
    public void Close() => SetOpen(false);

    /// <summary>
    /// 패널을 여는 탭의 입력을 켜거나 끈다. 제조 준비·기믹 중에는 기존 패널을 먼저 닫아
    /// 준비 화면 위에 칵테일 목록을 다시 띄울 수 없게 한다.
    /// </summary>
    public void SetToggleInteractable(bool interactable)
    {
        if (!interactable) Close();
        if (toggleButton != null) toggleButton.interactable = interactable;
    }

    /// <summary>
    /// 패널이 열리고 닫힐 때 발생한다. 안쪽 화면의 뒤로 버튼을 거치지 않고 토글로 바로 닫는 길이 있어서,
    /// 패널이 닫혔다는 사실을 밖에서 알 방법이 이것뿐이다.
    /// </summary>
    public event System.Action<bool> OpenChanged;

    void SetOpen(bool open)
    {
        if (panel == null) return;

        float target = open ? openX : closedX;
        bool stateChanged = isOpen != open;
        bool positionChanged = !Mathf.Approximately(panel.anchoredPosition.x, target);

        if (!stateChanged && !positionChanged) return;

        isOpen = open;

        // 위치만 어긋난 경우도 화면상으로는 열림/닫힘이 일어나는 것이므로 구독자에게 알린다.
        OpenChanged?.Invoke(open);

        if (anim != null) StopCoroutine(anim);

        // 플레이 중 스크립트가 다시 로드되면 isOpen 같은 런타임 필드는 초기값으로 돌아가지만
        // RectTransform 위치는 열린 자리에 복원될 수 있다. 상태값만 보고 일찍 반환하면 그 뒤
        // Close()가 "이미 닫힘"으로 오판해 패널이 제조 준비 화면 위에 영구히 남는다.
        if (!positionChanged)
        {
            anim = null;
            return;
        }

        // 비활성 오브젝트에서는 코루틴을 시작할 수 없다. 다음에 켰을 때 이전 위치가 남지 않도록
        // 요청한 위치로 즉시 맞춘다.
        if (!isActiveAndEnabled)
        {
            panel.anchoredPosition = new Vector2(target, panel.anchoredPosition.y);
            anim = null;
            return;
        }

        anim = StartCoroutine(AnimateX(target));
    }

    /// <summary>panel의 anchoredPosition.x를 duration초 동안 ease-in-out으로 target까지 보간한다.</summary>
    IEnumerator AnimateX(float target)
    {
        float start = panel.anchoredPosition.x;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            float x = Mathf.Lerp(start, target, k);

            panel.anchoredPosition = new Vector2(x, panel.anchoredPosition.y);
            yield return null;
        }

        panel.anchoredPosition = new Vector2(target, panel.anchoredPosition.y);
        anim = null;
    }
}
