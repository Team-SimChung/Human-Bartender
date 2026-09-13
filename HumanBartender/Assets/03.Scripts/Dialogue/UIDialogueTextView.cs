using Cysharp.Threading.Tasks;
using System;
using System.Data.SqlTypes;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>말풍선 타이핑에 필요한 데이터(대사 내용, 화자, 위치, 색상 등)를 담는 컨테이너.</summary>
[System.Serializable]
public class TypingData
{
    public string speaker = "";
    public Vector3 speakerPos;
    public string str;
    public Color32 nameColor;
    public bool isLunaSpeak = false;
    public EBubbleArrowType eBubbleArrowType = EBubbleArrowType.Center;
    public DialogueBubbleType bubbleType = DialogueBubbleType.Auto;
    public TypingData()
    {
    }

    public TypingData(
        string str, 
        string speaker, 
        Vector3 speakerPos, 
        Color32 nameColor, 
        bool isLunaSpeak,
        EBubbleArrowType eBubbleArrowType = EBubbleArrowType.Center,
        DialogueBubbleType bubbleType = DialogueBubbleType.Auto)
    {
        this.speaker = speaker;
        this.speakerPos = speakerPos;
        this.str = str;
        this.nameColor = nameColor;
        this.isLunaSpeak = isLunaSpeak;
        this.bubbleType = bubbleType;
    }
}
public enum DialogueBubbleType
{
    Auto,       // 기존처럼 화자가 루나인지로 결정
    Player,
    Customer,
    Narration
}

/// <summary>
/// 루나(플레이어)/손님 말풍선 두 개를 관리하며 텍스트 타이핑 연출을 담당한다.
/// 실제 태그 파싱/타이핑 로직은 DialogueTypingService를 공유해서 쓴다.
/// </summary>
public class UIDialogueTextView : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] TextTagDataSO textTagData;

    [Header("UI Components")]
    public DynamicSpeechBubble lunaSpeechBubble;
    public DynamicSpeechBubble customerSpeechBubble;
    [SerializeField] private DynamicSpeechBubble narrationSpeechBubble;
    public RectTransform canvasRect;

    [Header("Slot")]
    [SerializeField] Vector2 baseOffset;
    [SerializeField] Vector2 subOffset;



    private TypingData curTypingData;
    private CancellationTokenSource typingCts;

    [SerializeField] private float defaultTypingDelay = 0.05f;

    void Start()
    {
        curTypingData = new TypingData();
    }

    DynamicSpeechBubble targetBubble;


    /// <summary>
    /// 화자에 맞는 말풍선(루나/손님)을 선택하고, 손님 발화면 캐릭터 위치에 맞춰 말풍선 위치를 조정한 뒤 타이핑을 시작한다.
    /// </summary>
    public async UniTask StartType(TypingData data, string cocktailName = null)
    {
        var type = data.bubbleType;

        if (type == DialogueBubbleType.Auto)
        {
            type = data.isLunaSpeak
                ? DialogueBubbleType.Player
                : DialogueBubbleType.Customer;
        }

        targetBubble = type switch
        {
            DialogueBubbleType.Player => lunaSpeechBubble,
            DialogueBubbleType.Customer => customerSpeechBubble,
            DialogueBubbleType.Narration => narrationSpeechBubble,
            _ => customerSpeechBubble
        };

        if (type == DialogueBubbleType.Customer)
            SetBubblePosition(data.speakerPos);

        await TypeSentenceTMP(curTypingData, cocktailName);
    }

    /// <summary>캐릭터의 월드 좌표를 화면 좌표로 변환해 말풍선 위치를 캐릭터 머리 위(subOffset)로 맞춘다.</summary>
    public void SetBubblePosition(Vector3 characterTransform)
    {
        Vector2 screenPoint = Camera.main.WorldToScreenPoint(characterTransform + (Vector3)subOffset);

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out localPoint);

        localPoint.x = Mathf.RoundToInt(localPoint.x);
        localPoint.y = Mathf.RoundToInt(localPoint.y);
        targetBubble.bubble.localPosition = localPoint;
    }



    /// <summary>두 말풍선의 텍스트와 폰트 크기를 초기화하고 비활성화한다.</summary>
    private void ClearBubble(DynamicSpeechBubble bubble)
    {
        if (bubble == null) return;

        if (bubble.textLabel != null)
        {
            bubble.textLabel.enableAutoSizing = false;
            bubble.textLabel.text = "";
            bubble.textLabel.fontSize = bubble.baseFontSize;
        }

        bubble.gameObject.SetActive(false);
    }

    public void ClearText()
    {
        ClearBubble(lunaSpeechBubble);
        ClearBubble(customerSpeechBubble);
        ClearBubble(narrationSpeechBubble);
    }

    /// <summary>화면 클릭 시 타이핑을 중단(스킵)한다.</summary>
    public void OnScreenClick()
    {
        StopTyping();
    }

    /// <summary>타이핑 완료 후 현재 타이핑 데이터 참조를 정리한다.</summary>
    public void CompleteTyping()
    {
        curTypingData = null;
    }

    /// <summary>진행 중인 타이핑 코루틴을 취소한다.</summary>
    public void StopTyping()
    {
        if (typingCts != null)
        {
            typingCts.Cancel();
            typingCts.Dispose();
            typingCts = null;
        }
    }

    /// <summary>
    /// 말풍선에 한 글자씩 순차 표시(타이핑 효과)한다. 실제 태그 파싱/애니메이션은 DialogueTypingService에 위임한다.
    /// </summary>
    public async UniTask TypeSentenceTMP(TypingData data, string cocktailName = null)
    {
        if (!(data.str.Length > 0)) return;

        StopTyping();
        typingCts = new CancellationTokenSource();

        await DialogueTypingService.TypeSentenceTMP(data, targetBubble, textTagData, defaultTypingDelay, typingCts.Token, cocktailName);

        CompleteTyping();
    }

    /// <summary>표시 중인 글자 수(visibleCharCount)까지의 부분 문자열을 잘라 반환한다. (현재 미사용)</summary>
    string GetVisibleSubstring(string fullText, int visibleCharCount)
    {
        if (visibleCharCount <= 0) return "";
        if (visibleCharCount >= targetBubble.textLabel.textInfo.characterCount)
            return fullText;

        var charInfo = targetBubble.textLabel.textInfo.characterInfo[visibleCharCount - 1];
        int endIndex = charInfo.index + 1;

        if (endIndex < fullText.Length && fullText[endIndex] == '\n')
            endIndex++;

        return fullText.Substring(0, endIndex);
    }
}
