using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ScrollRect 의 높이를 Content 크기에 맞춰 늘렸다가, maxHeight 를 넘으면
/// 그 높이로 고정하고 스크롤을 켠다. (내용이 짧으면 스크롤 없이 딱 맞게 줄어듦)
///
/// 배치: ScrollRect 가 붙은 오브젝트에 함께 부착.
///       해당 오브젝트에는 LayoutElement 가 자동 추가되어 높이가 제어된다.
///       (부모가 VerticalLayoutGroup 이면 preferredHeight 가 그대로 반영됨)
/// </summary>
[RequireComponent(typeof(ScrollRect))]
[RequireComponent(typeof(LayoutElement))]
public class ScrollRectAutoSizer : MonoBehaviour
{
    [SerializeField] private float maxHeight = 360f;  // 이 높이를 넘으면 스크롤
    [SerializeField] private float minHeight = 0f;     // 최소 높이(선택)
    [SerializeField] private bool disableScrollWhenFits = true; // 다 보이면 스크롤 끄기

    private ScrollRect scroll;
    private LayoutElement layoutElement;
    private RectTransform content;
    private float lastApplied = -1f;

    private void Awake()
    {
        scroll = GetComponent<ScrollRect>();
        layoutElement = GetComponent<LayoutElement>();
        content = scroll.content;
    }

    private void LateUpdate()
    {
        if (content == null) return;

        float contentHeight = LayoutUtility.GetPreferredHeight(content);
        float target = Mathf.Clamp(contentHeight, minHeight, maxHeight);

        // 변화가 있을 때만 적용 (레이아웃 thrash 방지)
        if (!Mathf.Approximately(target, lastApplied))
        {
            layoutElement.preferredHeight = target;
            lastApplied = target;
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        // 내용이 다 들어가면 스크롤 비활성 (선택)
        if (disableScrollWhenFits)
        {
            bool needsScroll = contentHeight > maxHeight + 0.5f;
            scroll.vertical = needsScroll;
            if (!needsScroll && scroll.verticalNormalizedPosition != 1f)
                scroll.verticalNormalizedPosition = 1f; // 위로 고정
        }
    }
}
