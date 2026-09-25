using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>
/// 2부 대본을 바 화면에 그리는 구현체.
///
/// 쓰는 부품은 공용 대화 시스템의 것 그대로다 — 말풍선(UIDialogueTextView)과
/// 좌석 인물(DialogueCharacterManager), 선택지(UIDialogueChoiceView).
///
/// 길거리처럼 좌석이 없는 화면이 신형 대본을 재생하게 되면 이 인터페이스의 다른 구현을 만들면 된다.
/// 실행기는 그대로 쓴다.
/// </summary>
public class BarStoryPresenter : MonoBehaviour, IStoryPresenter
{
    [Header("View")]
    [Tooltip("대사창. 공용 대화 시스템이 쓰는 것과 같은 것을 꽂는다.")]
    [SerializeField] UIDialogueTextView textView;

    [Tooltip("좌석에 인물을 세우는 곳. 1부 카메오와 같은 체계다.")]
    [SerializeField] DialogueCharacterManager characterManager;

    [Tooltip("선택지 UI. 공용 대화 시스템이 쓰는 것과 같은 것을 꽂는다.")]
    [SerializeField] UIDialogueChoiceView choiceView;

    [Header("Camera")]
    [Tooltip("자리 수에 따라 화면을 잡는 데 걸리는 시간(§10.1.1 camera_transition_sec). Day 0은 이동만, 이후 일차는 이동과 줌을 같은 시간 안에서 진행한다.")]
    [SerializeField] float cameraTransitionSec = 0.8f;

    [Header("Data")]
    [Tooltip("화면에 적을 이름과 이름 색을 찾는다.")]
    [SerializeField] NewCharacterDataSO characterData;

    /// <summary>표정을 따로 정하지 않은 등장에 쓰는 값.</summary>
    const string DefaultExpression = "default";

    /// <summary>화면을 잡는 두 가지 장치. 자리로 옮기는 것과 범위를 넓히고 좁히는 것이 따로 있다.</summary>
    [Inject] ISlotCamera slotCamera;
    [Inject] ICameraControlNew cameraZoom;

    /// <summary>
    /// 대사를 띄운다.
    ///
    /// 표정을 먼저 바꾸고 타이핑한다. 대사가 시작된 뒤에 바꾸면 이미 말하고 있는 얼굴이 뒤늦게 변한다
    /// (§10.1: 표정은 즉시 교체하며 크로스페이드하지 않는다).
    ///
    /// 루나는 초상을 세우지 않는다. 바 안의 루나는 1인칭이라 이름과 본문만 나온다(§10.1).
    /// </summary>
    public async UniTask ShowSayAsync(StorySayRequest request, CancellationToken token)
    {
        if (textView == null)
        {
            throw new InvalidOperationException("[BarStory] textView가 비어 있습니다.");
        }

        if (!request.IsPlayer && !string.IsNullOrEmpty(request.Expression) && characterManager != null)
            await characterManager.SetCharacterAsync(request.ActorId, request.Expression, ESlotType.None, token);

        ResolveSpeaker(request.ActorId, out string displayName, out Color32 nameColor);

        Vector3 speakerPos = characterManager != null && !request.IsPlayer
            ? characterManager.GetCharacterPosition(request.ActorId)
            : Vector3.zero;

        token.ThrowIfCancellationRequested();
        if (characterManager != null) characterManager.OnDialogueStart(request.ActorId);
        try
        {
            await textView.StartType(new TypingData(request.Body, displayName, speakerPos, nameColor, request.IsPlayer), token: token);
        }
        finally
        {
            if (characterManager != null) characterManager.OnDialogueEnd(request.ActorId);
        }
    }

    public async UniTask EnterAsync(string actorId, ESlotType slot, CancellationToken token)
    {
        if (characterManager == null) return;

        await characterManager.SetCharacterAsync(actorId, DefaultExpression, slot, token);
    }

    public void Exit(ESlotType slot)
    {
        if (characterManager != null) characterManager.ResetCharacter(slot);
    }

    /// <summary>
    /// 앉은 사람 수에 맞춰 화면을 잡는다(§10.1.1).
    ///
    /// 1명이면 그 자리로, 2명이면 두 자리의 중점으로 이동한다. Day 0은 이전 Play 씬의
    /// 960×540 화각을 유지하고 위치만 옮긴다. 이후 일차는 자리 수에 맞춰 줌도 함께 진행한다.
    ///
    /// 아무도 없으면 L·R을 담는 기본 프레임에 선다 — 2부 시작 화면이다. 셋 이상은 이미 실행기가
    /// 데이터 오류로 알린 뒤라 건들지 않는다.
    /// </summary>
    public async UniTask ApplyFramingAsync(IReadOnlyList<ESlotType> occupiedSlots, CancellationToken token)
    {
        if (slotCamera == null || cameraZoom == null || characterManager == null)
        {
            Debug.LogWarning("[BarStory] 카메라나 인물을 받지 못해 화면을 잡지 않습니다. " +
                             "InGameLifetimeScope에 BarStoryPresenter가 등록되어 있는지 보세요.");
            return;
        }

        int count = occupiedSlots?.Count ?? 0;

        if (count > 2) return;

        float targetX;
        ECameraZoomType targetZoom;

        // 2부 손님은 자리에서 움직이지 않는다. 인원이 바뀌면 카메라가 좌석을 따라간다.
        if (count == 1)
        {
            if (!TryGetSeatX(occupiedSlots[0], out float x)) return;
            targetX = x;
            targetZoom = ECameraZoomType.Sub;
        }
        else
        {
            // 아무도 없을 때(2부 시작)도 두 자리를 담는 프레임에 선다. 첫 손님이 들어오기 전에
            // 바 전체가 한 번 보여야 하고, 여기서 1부가 남긴 화면을 이어받으면 2부가 어디서
            // 시작하는지가 그날그날 달라진다.
            ESlotType left = count == 2 ? occupiedSlots[0] : ESlotType.Left;
            ESlotType right = count == 2 ? occupiedSlots[1] : ESlotType.Right;

            if (!TryGetSeatX(left, out float leftX)) return;
            if (!TryGetSeatX(right, out float rightX)) return;

            targetX = (leftX + rightX) * 0.5f;
            targetZoom = ECameraZoomType.Base;
        }

        if (GameStateManager.Instance.CurrentDay == 0)
            await slotCamera.MoveToXAsync(targetX, cameraTransitionSec, token);
        else
            await UniTask.WhenAll(
                cameraZoom.TransitionCameraZoomAsync(targetZoom, cameraTransitionSec, token: token),
                slotCamera.MoveToXAsync(targetX, cameraTransitionSec, token));

        // 목표점과 렌즈 보간은 Update에서 끝난다. CinemachineBrain이 LateUpdate에서
        // 실제 카메라에 반영한 다음 대사를 열어야 첫 말풍선 프레임과 카메라 위치가 일치한다.
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
    }

    /// <summary>자리가 서 있는 x를 인물 슬롯에서 읽는다. 슬롯이 없으면 세울 곳이 없다는 뜻이다.</summary>
    bool TryGetSeatX(ESlotType slot, out float x)
    {
        if (characterManager.TryGetSlotX(slot, out x)) return true;

        Debug.LogError($"[BarStory] '{slot}' 자리의 인물 슬롯이 씬에 없어 화면을 잡지 못했습니다.");
        return false;
    }

    /// <summary>
    /// 선택지를 띄우고 고를 때까지 기다린다.
    ///
    /// 칸을 그리는 일은 공용 선택지 뷰가 한다. 조건 판정은 이미 끝난 뒤라 무엇을 누를 수 있는지와
    /// 뭐라고 적을지만 넘긴다(§12.4).
    /// </summary>
    public async UniTask<int> ShowChoicesAsync(IReadOnlyList<StoryChoiceOption> options, CancellationToken token)
    {
        if (choiceView == null)
        {
            Debug.LogError("[BarStory] choiceView가 비어 있어 선택지를 띄우지 못했습니다.");
            return -1;
        }

        // 고를 수 없는 칸에는 문구 대신 잠긴 이유를 적는다 — 무엇을 골랐어야 하는지가 아니라
        // 왜 못 고르는지가 지금 필요한 정보다.
        var texts = new List<string>(options.Count);
        var selectable = new List<bool>(options.Count);

        foreach (var option in options)
        {
            texts.Add(option.IsSelectable ? option.Text : option.LockReason ?? option.Text);
            selectable.Add(option.IsSelectable);
        }

        var completion = new UniTaskCompletionSource<int>();

        choiceView.ShowChoice(texts, selectable, picked => completion.TrySetResult(picked));

        try
        {
            return await completion.Task.AttachExternalCancellation(token);
        }
        finally
        {
            if (choiceView != null) choiceView.CloseChoices();
        }
    }

    public void SkipTyping()
    {
        if (textView != null) textView.OnScreenClick();
    }

    public void Clear()
    {
        if (textView != null) textView.ClearText();
        if (choiceView != null) choiceView.CloseChoices();
        if (characterManager != null) characterManager.ResetCharacter();
    }

    /// <summary>characters.json에서 화면에 적을 이름과 색을 찾는다. 없으면 id를 그대로 쓴다.</summary>
    void ResolveSpeaker(string actorId, out string displayName, out Color32 nameColor)
    {
        displayName = actorId;
        nameColor = new Color32(255, 255, 255, 255);

        if (characterData?.characterData == null) return;

        foreach (var character in characterData.characterData)
        {
            if (character.Id != actorId) continue;

            if (!string.IsNullOrEmpty(character.Name.Ko)) displayName = character.Name.Ko;

            if (ColorUtility.TryParseHtmlString(character.NameColor, out Color parsed)) nameColor = parsed;

            return;
        }

        Debug.LogWarning($"[BarStory] characters.json에서 '{actorId}'를 찾지 못했습니다.");
    }
}
