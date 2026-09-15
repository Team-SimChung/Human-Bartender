using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public enum EBubbleArrowType
{
    Left,
    Center,
    Right
}

[RequireComponent(typeof(RectTransform))]
/// <summary>
/// 텍스트 타이핑에 맞춰 말풍선 크기를 동적으로 조절하는 컴포넌트.
/// GrowPerCharacter 모드: 글자가 늘어날수록 박스가 확장.
/// PreExpand 모드: 시작부터 최종 크기로 펼치고 안에서 타이핑만 진행.
/// PrepareForText()로 문장 준비, UpdateForVisible()을 매 타이핑 스텝마다 호출한다.
/// </summary>
public class DynamicSpeechBubble : MonoBehaviour
{
    public enum BubbleSizeMode
    {
        GrowPerCharacter, // 한 글자씩 박스가 늘어남 (기존 방식)
        PreExpand,         // 시작부터 최종 크기로 펼쳐 두고 그 안에서 타이핑만

        Fixed // 인스펙터에서 변경없음
    }

    [Header("Mode")]
    public BubbleSizeMode sizeMode = BubbleSizeMode.GrowPerCharacter;

    [Header("References")]
    public TMP_Text textLabel;
    public TMP_Text nameLabel;
    public RectTransform bubble;
    public GameObject centerArrow;
    public GameObject leftArrow;
    public GameObject rightArrow;


    [Header("Size Limits")]
    public Vector2 minSize = new Vector2(80, 40);
    public Vector2 maxSize = new Vector2(400, 240);

    [Header("Padding")]
    public float paddingLeft = 24f;
    public float paddingRight = 24f;
    public float paddingTop = 16f;
    public float paddingBottom = 16f;

    [Header("Font")]
    public float baseFontSize = 28f;
    public float minFontSize = 14f;

    [Header("Layout")]
    [Tooltip("측정 너비에 더하는 안전 마진(px). 보통 1~3이면 충분.")]
    public float widthSafetyMargin = 2f;

    // ── 한 문장 단위로 미리 계산되는 캐시 ────────────────────────────
    string _fullText = "";
    float _targetTextW;     // 최종(가장 긴 줄) 텍스트 너비 (패딩 제외)
    float _targetTextH;     // 최종 텍스트 높이 (패딩 제외)
    int _visibleCount;    // 전체 보이는 글자 수
    int _firstLineEnd;    // 첫 줄이 끝나는 '보이는 글자' 인덱스
    bool _overflow;        // 최대 높이 초과 → 오토사이즈 모드
    bool _ready;

    // 최종 너비 레이아웃에서 캡처한 글자별 정보
    float[] _charRight;      // 글자 오른쪽 x (라벨 로컬)
    int[] _charLine;       // 글자가 속한 줄 번호
    bool[] _isSpace;        // 공백/개행 여부 (단어 경계 판정)
    float _originX;        // 첫 글자 왼쪽 x
    float _ascender0;      // 0번 줄 위쪽 y
    float[] _lineDescender;  // 줄별 아래쪽 y

    float PaddingH => paddingLeft + paddingRight;
    float PaddingV => paddingTop + paddingBottom;
    float MaxTextW => maxSize.x - PaddingH;
    float MaxTextH => maxSize.y - PaddingV;
    float MinTextW => minSize.x - PaddingH;

    public int TotalVisibleCharacters => _visibleCount;

    void OnEnable()
    {
        paddingLeft = textLabel.rectTransform.offsetMin.x;
        paddingRight = -textLabel.rectTransform.offsetMax.x;
        paddingTop = -textLabel.rectTransform.offsetMax.y;
        paddingBottom = textLabel.rectTransform.offsetMin.y;
    }

    // \n 으로 나눈 줄 중 가장 긴 줄의 자연 너비
    float MeasureLongestLine(string text)
    {
        float longest = 0f;
        if (!string.IsNullOrEmpty(text))
        {
            foreach (var line in text.Split('\n'))
            {
                if (line.Length == 0) continue;
                float w = textLabel.GetPreferredValues(line, Mathf.Infinity, 0f).x;
                if (w > longest) longest = w;
            }
        }
        if (longest <= 0f) longest = MinTextW;
        return longest;
    }

    /// <summary>현재 sizeMode 로 준비.</summary>
    public void PrepareForText(string fullClean)
    {
        PrepareForText(fullClean, sizeMode);
    }

    /// <summary>
    /// 타이핑 시작 전에 한 번 호출. fullClean = 색상 태그까지 적용되고
    /// 타이핑 딜레이 태그(&lt;123&gt;)는 제거된 최종 문자열.
    /// mode 로 이번 문장의 크기 동작을 지정한다.
    /// </summary>
    public void PrepareForText(string fullClean, BubbleSizeMode mode)
    {
        sizeMode = mode;
        _ready = false;
        _fullText = fullClean ?? "";

        if (sizeMode == BubbleSizeMode.Fixed)
        {
            // 위쪽 고정 → 높이가 늘어나면 아래로 확장
            bubble.pivot = new Vector2(0.5f, 1f);

            textLabel.margin = Vector4.zero;
            textLabel.enableAutoSizing = false;
            textLabel.fontSize = baseFontSize;

            // 현재 말풍선 너비는 유지
            float width = bubble.rect.width;
            float textWidth = Mathf.Max(1f, width - PaddingH);

            Vector2 preferred = textLabel.GetPreferredValues(
                _fullText, textWidth, Mathf.Infinity);

            float height = Mathf.Max(
                minSize.y,
                preferred.y + PaddingV);

            bubble.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, height);

            textLabel.text = _fullText;
            textLabel.maxVisibleCharacters = int.MaxValue;

            LayoutRebuilder.ForceRebuildLayoutImmediate(bubble);
            textLabel.ForceMeshUpdate(true, true);

            _visibleCount = textLabel.textInfo.characterCount;
            textLabel.maxVisibleCharacters = 0;

            _ready = true;
            return;
        }

        textLabel.margin = Vector4.zero;
        textLabel.enableAutoSizing = false;
        textLabel.fontSize = baseFontSize;
        textLabel.text = _fullText;

        // 1) 최종 너비 결정 (가장 긴 줄 + 일관된 마진)
        float longest = MeasureLongestLine(_fullText);
        _targetTextW = Mathf.Clamp(Mathf.Ceil(longest) + widthSafetyMargin, MinTextW, MaxTextW);

        // 2) 최종 너비/넉넉한 높이로 한 번 레이아웃 → 글자 최종 위치 캡처
        //    (여기서 잡힌 줄바꿈 위치가 곧 '최종' 위치이므로 타이핑 중 흔들리지 않음)
        bubble.sizeDelta = new Vector2(_targetTextW + PaddingH, maxSize.y);
        textLabel.maxVisibleCharacters = int.MaxValue;
        LayoutRebuilder.ForceRebuildLayoutImmediate(textLabel.rectTransform);
        Canvas.ForceUpdateCanvases();
        textLabel.ForceMeshUpdate(true, true);

        var info = textLabel.textInfo;
        _visibleCount = Mathf.Max(0, info.characterCount);

        int n = Mathf.Max(1, _visibleCount);
        _charRight = new float[n];
        _charLine = new int[n];
        _isSpace = new bool[n];

        _originX = (_visibleCount > 0) ? info.characterInfo[0].bottomLeft.x : 0f;
        _firstLineEnd = _visibleCount;

        for (int c = 0; c < _visibleCount; c++)
        {
            var ci = info.characterInfo[c];
            _charRight[c] = ci.topRight.x;
            _charLine[c] = ci.lineNumber;
            char ch = ci.character;
            _isSpace[c] = (ch == ' ' || ch == '\n' || ch == '\t');

            // 처음으로 1번 줄에 들어간 글자 = 첫 줄의 끝
            if (_firstLineEnd == _visibleCount && _charLine[c] > 0)
                _firstLineEnd = c;
        }

        int lineCount = Mathf.Max(1, info.lineCount);
        _ascender0 = (info.lineCount > 0) ? info.lineInfo[0].ascender : 0f;
        _lineDescender = new float[lineCount];
        for (int l = 0; l < info.lineCount; l++)
            _lineDescender[l] = info.lineInfo[l].descender;

        // 3) 최종 높이 / 최대 높이 초과 검사
        float fullH = (info.lineCount > 0) ? (_ascender0 - _lineDescender[info.lineCount - 1]) : 0f;
        _targetTextH = Mathf.Clamp(fullH, minSize.y - PaddingV, MaxTextH);
        _overflow = fullH > MaxTextH;
        if (_overflow)
        {
            textLabel.enableAutoSizing = true;
            textLabel.fontSizeMin = minFontSize;
            textLabel.fontSizeMax = baseFontSize;
        }

        // 4) 시작 크기 설정
        textLabel.maxVisibleCharacters = 0;
        _ready = true;

        if (sizeMode == BubbleSizeMode.PreExpand)
        {
            // 시작부터 최종 크기로 펼쳐 둔다. 이후 타이핑 동안 크기 변화 없음.
            UpdateForVisible(0);
        }
        else
        {
            // 최소 크기에서 시작해서 한 글자씩 확장.
            ApplySize(MinTextW, minSize.y - PaddingV);
        }
    }

    // visibleCount 가 포함된 단어의 끝(배타적, '보이는 글자' 기준)
    int WordEndVisible(int visibleCount)
    {
        int last = Mathf.Clamp(visibleCount - 1, 0, _visibleCount - 1);
        int i = last;
        if (!_isSpace[i])
            while (i + 1 < _visibleCount && !_isSpace[i + 1]) i++;
        return i + 1;
    }

    /// <summary>타이핑 매 스텝마다 호출 (visibleCount = 현재 보이는 글자 수).</summary>
    public void UpdateForVisible(int visibleCount)
    {
        if (!_ready) return;
        if (sizeMode == BubbleSizeMode.Fixed) return;

        visibleCount = Mathf.Clamp(visibleCount, 0, _visibleCount);

        // ── PreExpand: 항상 최종 크기로 고정 ──────────────────────────
        if (sizeMode == BubbleSizeMode.PreExpand)
        {
            textLabel.margin = Vector4.zero;
            if (_overflow) ApplySize(MaxTextW, MaxTextH);
            else ApplySize(_targetTextW, _targetTextH);
            return;
        }

        // ── GrowPerCharacter: 한 글자씩 확장 (기존 방식) ──────────────
        if (_overflow) { ApplySize(MaxTextW, MaxTextH); return; }
        if (visibleCount == 0) { ApplySize(MinTextW, minSize.y - PaddingV); return; }

        float widthW;
        if (visibleCount <= _firstLineEnd)
        {
            // 1. 순수하게 '현재 글자'의 우측 좌표를 가져온다.
            int idx = Mathf.Clamp(visibleCount - 1, 0, _visibleCount - 1);
            float charRightW = (_charRight[idx] - _originX) + widthSafetyMargin;

            // 2. 첫째 줄 진행률에 맞춰 최종 너비까지 여백을 늘려준다.
            float progress = _firstLineEnd > 0 ? (float)visibleCount / _firstLineEnd : 1f;
            float toward = Mathf.Lerp(MinTextW, _targetTextW, progress);

            // 3. 둘 중 더 큰 값.
            widthW = Mathf.Max(charRightW, toward);
        }
        else
        {
            // 4. 둘째 줄부터는 최종 너비로 고정.
            widthW = _targetTextW;
        }

        widthW = Mathf.Clamp(widthW, MinTextW, _targetTextW);

        // ── 높이 ── (최종 레이아웃의 줄 번호 기준 = 안정적)
        int lastLine = _charLine[Mathf.Clamp(visibleCount - 1, 0, _visibleCount - 1)];
        lastLine = Mathf.Clamp(lastLine, 0, _lineDescender.Length - 1);
        float heightH = _ascender0 - _lineDescender[lastLine];
        heightH = Mathf.Clamp(heightH, minSize.y - PaddingV, MaxTextH);

        ApplySize(widthW, heightH);
    }

    void ApplySize(float textW, float textH)
    {
        Vector2 newSize = new Vector2(
            Mathf.Clamp(textW + PaddingH, minSize.x, maxSize.x),
            Mathf.Clamp(textH + PaddingV, minSize.y, maxSize.y));

        // 박스가 최종 너비보다 좁을 때, 라벨 우측 마진을 음수로 줘서
        // 라벨의 줄바꿈 기준 너비는 최종 너비로 유지 → 캡처한 글자 위치가 안 흔들림.
        float deficitW = _targetTextW - textW;
        if (deficitW > 0) textLabel.margin = new Vector4(0, 0, -deficitW, 0);
        else textLabel.margin = Vector4.zero;

        if (bubble.sizeDelta == newSize) return;

        bubble.sizeDelta = newSize;
        LayoutRebuilder.ForceRebuildLayoutImmediate(bubble);
        LayoutRebuilder.ForceRebuildLayoutImmediate(textLabel.rectTransform);
        Canvas.ForceUpdateCanvases();
        textLabel.ForceMeshUpdate(true, true);
    }

    // ── 호환용 / 즉시 전체 표시 ───────────────────────────────────
    public void ResetToMinSize()
    {
        textLabel.enableAutoSizing = false;
        textLabel.fontSize = baseFontSize;
        bubble.sizeDelta = minSize;
        LayoutRebuilder.ForceRebuildLayoutImmediate(bubble);
        textLabel.ForceMeshUpdate();
    }

    /// <summary>타이핑 없이 전체 텍스트를 한 번에 표시.</summary>
    public void SetText(string content)
    {
        PrepareForText(content);
        textLabel.maxVisibleCharacters = int.MaxValue;
        UpdateForVisible(_visibleCount);
    }
}