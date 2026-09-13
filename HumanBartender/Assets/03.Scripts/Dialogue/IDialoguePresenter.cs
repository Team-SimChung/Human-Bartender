using Cysharp.Threading.Tasks;
using System;
using System.Threading;

/// <summary>
/// DialogueRunner가 실외 대사 스텝을 진행하며 호출하는 화면 표시 인터페이스.
/// 실제 UI 연출(텍스트 타이핑, 선택지)은 구현체에서 처리한다.
/// </summary>
public interface IDialoguePresenter
{
    /// <summary>타이핑 중인 텍스트를 즉시 완성한다.</summary>
    void SkipTyping();
    /// <summary>대사창을 숨긴다.</summary>
    void HideDialogue();
    /// <summary>씬 종료 처리.</summary>
    void EndScene();
    EActivationMode GetPlayMode();

    /// <summary>Step 하나의 대사를 표시한다.</summary>
    UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken ct);

    /// <summary>실외 선택지를 표시하고, 선택 시 onSelected 콜백을 호출한다.</summary>
    void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> onSelected);
}