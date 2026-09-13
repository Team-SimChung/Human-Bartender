using Cysharp.Threading.Tasks;
using System;
using System.Threading;

/// <summary>
/// DialogueRunner가 대화 진행 상태에 따라 호출하는 화면 표시 인터페이스.
/// 실제 UI 연출(텍스트 타이핑, 선택지, 트리거 실행 등)은 구현체에서 처리한다.
/// </summary>
public interface IDialoguePresenter
{
    /// <summary>대사 하나를 화면에 표시(타이핑 포함)한다.</summary>
    UniTask ShowDialogueAsync(DialogueData dialogue, CancellationToken token);
    /// <summary>선택지 목록을 표시하고, 선택 시 onSelected 콜백을 호출한다.</summary>
    void ShowChoices(ChoiceData[] choices, Action<ChoiceData> onSelected);
    /// <summary>타이핑 중인 텍스트를 즉시 완성한다.</summary>
    void SkipTyping();
    /// <summary>대사창을 숨긴다.</summary>
    void HideDialogue();
    /// <summary>시스템 연출을 위해 대사창을 숨기는 등 시스템 액션 상태로 전환한다.</summary>
    void ShowSystemAction();
    /// <summary>씬 종료 처리.</summary>
    void EndScene();
    EActivationMode GetPlayMode();

    // [신규] Step 기반 대사 출력 (4개 인자)
    UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken ct);

    // [신규] 아웃사이드 선택지 출력
    void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> onSelected);
}