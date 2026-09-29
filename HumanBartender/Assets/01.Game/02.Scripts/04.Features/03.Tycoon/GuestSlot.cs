using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>손님 한 명이 자리에서 거치는 상태.</summary>
public enum EGuestState
{
    Empty,
    Coming,
    Sit,
    WaitinOrder,

    Leaving,
}

/// <summary>
/// 외형 슬롯 하나와 그 슬롯을 그리는 렌더러의 짝.
/// 이름 붙인 필드 아홉 개 대신 목록으로 두면, 슬롯이 늘어도 이 스크립트를 고칠 일이 없다.
/// </summary>
[System.Serializable]
public class GuestBodySlotRenderer
{
    public EGuestBodySlot slot;
    public SpriteRenderer renderer;
}

/// <summary>
/// 타이쿤(1부)에서 손님 한 명이 앉는 물리적 자리 하나.
/// slotType(Left/Right/Middle)은 대사(2부)의 DialogueCharacterManager/PlayCamera가 쓰는
/// ESlotType과 동일한 위치 개념을 공유한다.
///
/// 자리에 그림을 붙이는 방식은 손님에 따라 갈린다. 랜덤 손님은 partRenderers에 파츠 스프라이트를
/// 겹쳐 그리고, 단골(카메오)은 characterView가 표정·애니메이션까지 딸린 한 벌을 붙인다.
/// </summary>
public class GuestSlot : MonoBehaviour
{
    [SerializeField] ESlotType slotType;

    [Header("랜덤 손님 파츠 렌더러 (guest_bodies.json: bodies/outfits/eyes/hairs)")]
    [Tooltip("슬롯별 파츠 렌더러. 9슬롯 전부 연결해야 한다 — 빠진 슬롯은 그려지지 않는다. " +
             "겹쳐 그리는 순서는 EGuestBodySlot 값(0 body ~ 80 arm_accessory)을 따른다.")]
    [SerializeField] GuestBodySlotRenderer[] partRenderers;

    [Header("테스트용 임시 손님 오브젝트 (파츠 addressable 로딩 대신 단순 On/Off)")]
    [SerializeField] GameObject tempAppearanceObject;

    [Header("Character")]
    [Tooltip("단골(카메오)이 앉을 때 그림을 붙이는 곳. 이 좌석의 캐릭터 리그를 들고 있다.")]
    [SerializeField] GuestCharacterView characterView;

    [Header("말풍선 (Outside DynamicSpeechBubble 재사용, 위치는 슬롯 고정이라 트래커 없이 사용)")]
    [SerializeField] GameObject bubbleRoot;
    [SerializeField] DynamicSpeechBubble speechBubble;

    bool useTempAppearance;
    CancellationTokenSource bubbleCts;

    public ESlotType SlotType => slotType;
    public EGuestState CurrentState { get; private set; } = EGuestState.Empty;
    public Guest CurrentGuest { get; private set; }

    public bool IsEmpty => CurrentState == EGuestState.Empty;

    /// <summary>손님이 앉아 코스터를 기다리는 중인지(주문 대기로 넘어가기 전) 여부.</summary>
    public bool CanReceiveCoaster => CurrentGuest != null &&
        (CurrentState == EGuestState.Coming || CurrentState == EGuestState.Sit);

    /// <summary>
    /// 완성한 잔을 받을 수 있는 상태인지. 코스터가 놓여 주문 대기로 넘어갔고, 주문 대사까지 말한 뒤라야 받는다.
    /// 이미 잔을 받아 떠나는 중(Leaving)인 손님에게 한 잔 더 놓이는 것도 여기서 막힌다.
    /// </summary>
    public bool CanReceiveDrink => CurrentGuest != null &&
        CurrentState == EGuestState.WaitinOrder && CurrentGuest.hasOrdered;


    /// <summary>단골(카메오)의 그림을 붙이는 곳. 랜덤 손님에게는 쓰지 않는다.</summary>
    public GuestCharacterView CharacterView => characterView;

    /// <summary>GuestManager가 테스트 모드 여부를 전달한다. true면 파츠 addressable 로딩 대신 tempAppearanceObject를 On/Off한다.</summary>
    public void SetTempAppearanceMode(bool enabled)
    {
        useTempAppearance = enabled;
    }

    /// <summary>손님을 이 자리에 배정하고 입장 상태로 전환한다. 파츠 스프라이트는 로딩이 끝나는 대로 비동기로 적용된다.</summary>
    public void Seat(Guest guest)
    {
        CurrentGuest = guest;
        CurrentState = EGuestState.Coming;

        ApplyAppearanceAsync(guest).Forget();
    }

    /// <summary>
    /// guest.bodySprites 로딩(GuestManager에서 미리 시작됨)이 끝날 때까지 기다렸다가 파츠 스프라이트를 렌더러에 적용한다.
    ///
    /// 랜덤 손님만 대상이다. 단골(카메오)은 파츠를 조합하지 않고 2부 대화와 같은 캐릭터 체계로
    /// 붙이므로, 그쪽은 GuestManager가 DialogueCharacterManager에 맡긴다.
    /// </summary>
    async UniTaskVoid ApplyAppearanceAsync(Guest guest)
    {
        if (guest.appearance == null) return;

        if (useTempAppearance)
        {
            if (tempAppearanceObject != null) tempAppearanceObject.SetActive(true);
            return;
        }

        await UniTask.WaitUntil(() => guest.bodySprites != null, cancellationToken: this.GetCancellationTokenOnDestroy());

        if (CurrentGuest != guest) return; // 기다리는 동안 자리가 비워지거나 다른 손님으로 교체됨

        ApplySprites(guest.bodySprites);
    }

    /// <summary>
    /// 슬롯별 스프라이트를 렌더러에 붙인다.
    ///
    /// 배정되지 않은 선택 슬롯(겉옷·목걸이·팔 액세서리)에는 null이 들어간다. 비우는 것이 맞다 —
    /// 앞 손님이 걸치고 있던 겉옷이 남아 있으면 다음 손님이 그것을 입고 앉는다.
    /// </summary>
    void ApplySprites(GuestBodySprites sprites)
    {
        if (partRenderers == null) return;

        foreach (var entry in partRenderers)
        {
            if (entry == null || entry.renderer == null) continue;

            entry.renderer.sprite = sprites == null ? null : sprites.Get(entry.slot);
        }
    }

    /// <summary>
    /// 말풍선에 텍스트를 즉시 표시한다. durationSec이 0보다 크면 그 시간 뒤 자동으로 숨긴다.
    /// 슬롯이 고정 위치라 Outside처럼 매 프레임 화면 좌표를 추적할 필요는 없다.
    /// </summary>
    public void ShowBark(string text, float durationSec = 3f, NewTextTagDataSO tags = null,
        string cocktailName = null, bool typewriter = false)
    {
        bubbleCts?.Cancel();

        if (bubbleRoot != null) bubbleRoot.SetActive(true);
        if (typewriter && durationSec > 0f)
        {
            if (!DialogueTextCompiler.TryCompile(text, tags, DialoguePresentationSettings.Shared,
                    cocktailName, 0.05f, out var plan, out _)) typewriter = false;
            else
            {
                float estimated = 0f;
                foreach (var style in plan.SourceStyles) estimated += style.SpeedSeconds;
                foreach (var wait in plan.SourceWaits.Values) estimated += wait;
                if (estimated > durationSec)
                {
                    Debug.LogWarning($"[DialogueFX] Bark typing exceeds {durationSec:0.##}s display budget; showing instantly.");
                    typewriter = false;
                }
            }
        }
        if (typewriter)
            TypeBarkAsync(text, tags, cocktailName).Forget(Debug.LogException);
        else
            speechBubble.SetText(text, tags, cocktailName);

        if (durationSec > 0f)
        {
            var source = new CancellationTokenSource();
            bubbleCts = source;
            HideBarkAfterAsync(durationSec, source).Forget();
        }
    }

    async UniTask TypeBarkAsync(string text, NewTextTagDataSO tags, string cocktailName)
    {
        try
        {
            await speechBubble.TextPlayer.PlayAsync(
                new TypingData(text, speechBubble.nameLabel != null ? speechBubble.nameLabel.text : "",
                    Vector3.zero, speechBubble.nameLabel != null ? speechBubble.nameLabel.color : Color.white,
                    false), tags, 0.05f,
                this.GetCancellationTokenOnDestroy(), cocktailName);
        }
        catch (OperationCanceledException) { }
    }

    async UniTaskVoid HideBarkAfterAsync(float durationSec, CancellationTokenSource source)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(durationSec), cancellationToken: source.Token);
            if (ReferenceEquals(bubbleCts, source)) HideBark();
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(bubbleCts, source)) bubbleCts = null;
            source.Dispose();
        }
    }

    /// <summary>말풍선을 즉시 숨긴다.</summary>
    public void HideBark()
    {
        bubbleCts?.Cancel();
        if (speechBubble != null) speechBubble.GetComponent<DialogueTextPlayer>()?.Stop();
        if (bubbleRoot != null) bubbleRoot.SetActive(false);
    }

    /// <summary>손님이 자리에 앉는 연출이 끝났을 때 호출한다.</summary>
    public void MarkSit()
    {
        CurrentState = EGuestState.Sit;
    }

    /// <summary>주문 대기 상태로 전환한다.</summary>
    public void MarkWaitingOrder()
    {
        CurrentState = EGuestState.WaitinOrder;
    }

    /// <summary>응대가 끝나 손님이 떠나는 상태로 전환한다.</summary>
    public void MarkLeaving()
    {
        CurrentState = EGuestState.Leaving;
    }

    /// <summary>손님이 자리를 완전히 비웠을 때 호출한다. 로드된 파츠 스프라이트의 addressable 핸들을 반납하고 렌더러를 비운다.</summary>
    public void Clear()
    {
        CurrentGuest?.bodySprites?.Release();

        HideBark();

        if (tempAppearanceObject != null) tempAppearanceObject.SetActive(false);

        ApplySprites(null);

        CurrentGuest = null;
        CurrentState = EGuestState.Empty;
    }
}
