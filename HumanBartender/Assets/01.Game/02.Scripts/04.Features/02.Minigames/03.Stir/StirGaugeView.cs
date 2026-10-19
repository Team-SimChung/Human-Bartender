using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하단 가로 진행 게이지. 판정 하나가 끝날 때마다 왼쪽부터 칸 하나가 채워진다 —
/// 성공은 푸른색, 실패는 적색이라 지나온 시도의 성패가 한눈에 남는다.
///
/// 표시 칸 수는 실제 판정 목표에 맞춘다. 아트가 연결되면 성공/실패 이미지를 사용한다.
/// </summary>
public class StirGaugeView : MonoBehaviour
{
    [Tooltip("왼쪽에서 오른쪽 순서.")]
    [SerializeField] Image[] segments;
    [SerializeField] Sprite successSprite;
    [SerializeField] Sprite failSprite;
    [SerializeField] Vector2 padding = new Vector2(4f, 3f);
    [SerializeField, Min(0f)] float gap = 2f;

    [Header("Next Segment")]
    [SerializeField] StirGaugeOutline nextOutline;
    int nextSegment = -1;
    int configuredCount;

    [Header("Color")]
    [SerializeField] Color idleColor = new Color(1f, 1f, 1f, 0.025f);
    [SerializeField] Color successColor = new Color(0.25f, 0.72f, 1f);
    [SerializeField] Color failColor = new Color(1f, 0.39f, 0.47f);

    void Awake() => ResetAll();

    public void Configure(int count)
    {
        // 이전 프로토타입처럼 색만 쓰는 화면은 기존 배치를 유지한다.
        if (successSprite == null || failSprite == null) return;
        count = Mathf.Max(1, count);
        int previousCount = segments?.Length ?? 0;
        if (previousCount < count)
        {
            System.Array.Resize(ref segments, count);
            for (int i = previousCount; i < count; i++)
            {
                var go = new GameObject($"Segment {i}", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                segments[i] = go.GetComponent<Image>();
                segments[i].raycastTarget = false;
            }
        }

        configuredCount = count;
        for (int i = 0; i < segments.Length; i++)
        {
            Image segment = segments[i];
            if (segment == null) continue;
            segment.gameObject.SetActive(i < count);
            if (i >= count) continue;
            segment.sprite = successSprite;
            segment.type = Image.Type.Simple;
        }
        RefreshLayout();
        ResetAll();
    }

    /// <summary>창 크기 변경 시 기존 판정 결과를 유지한 채 칸의 위치와 크기만 조정한다.</summary>
    public void RefreshLayout()
    {
        if (successSprite == null || failSprite == null || segments == null) return;
        int count = configuredCount > 0 ? configuredCount : segments.Length;
        if (count == 0) return;
        RectTransform area = (RectTransform)transform;
        float width = Mathf.Max(0f, (area.rect.width - padding.x * 2f - gap * (count - 1)) / count);
        for (int i = 0; i < count; i++)
        {
            Image segment = segments[i];
            if (segment == null) continue;
            RectTransform rect = segment.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(padding.x + i * (width + gap), 0f);
            rect.sizeDelta = new Vector2(width, Mathf.Max(0f, area.rect.height - padding.y * 2f));
            rect.localRotation = Quaternion.identity;
        }
    }

    public void ResetAll()
    {
        SetNextSegment(-1);
        if (segments == null) return;

        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            if (successSprite != null) segments[i].sprite = successSprite;
            segments[i].color = idleColor;
        }
    }

    /// <summary>index번째 칸을 결과 색으로 채운다. 칸 수보다 판정이 많으면 넘치는 건 그냥 무시한다.</summary>
    public void SetResult(int index, bool success)
    {
        if (segments == null || index < 0 || index >= segments.Length) return;
        if (segments[index] == null) return;

        Sprite resultSprite = success ? successSprite : failSprite;
        if (resultSprite != null)
        {
            segments[index].sprite = resultSprite;
            segments[index].color = Color.white;
        }
        else segments[index].color = success ? successColor : failColor;
    }

    /// <summary>다음 판정 칸을 강조한다. -1 또는 범위 밖이면 강조를 끈다.</summary>
    public void SetNextSegment(int index)
    {
        bool visible = segments != null && index >= 0 && index < segments.Length
            && segments[index] != null;
        if (!visible)
        {
            if (nextOutline != null) nextOutline.gameObject.SetActive(false);
            nextSegment = -1;
            return;
        }

        if (nextOutline == null)
        {
            var go = new GameObject("Next Segment Outline", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(StirGaugeOutline));
            go.layer = gameObject.layer;
            nextOutline = go.GetComponent<StirGaugeOutline>();
            nextOutline.color = new Color(0.25f, 0.72f, 1f, 0.95f);
            nextOutline.raycastTarget = false;
        }

        if (nextSegment == index && nextOutline.gameObject.activeSelf) return;
        nextSegment = index;
        RectTransform rect = nextOutline.rectTransform;
        rect.SetParent(segments[index].transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.SetAsLastSibling();
        nextOutline.gameObject.SetActive(true);
    }
}
