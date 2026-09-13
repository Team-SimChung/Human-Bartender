using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 실외(길거리 등) 씬에서 사용하는 IDialoguePresenter 구현체.
/// </summary>
public class OutsideDialoguePresenter : MonoBehaviour, IDialoguePresenter
{
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private UIDialogueTextView typer;
    [SerializeField] private UIDialogueChoiceView choiceManager;
    public EActivationMode playMode = EActivationMode.Interact;
    private const string PLAYER_ID = "luna";
    
    public EActivationMode GetPlayMode()
    {
        return playMode;
    }
    public void HideDialogue()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    /// <summary>아웃사이드 전용 선택지 UI 표시</summary>
    public void ShowOutsideChoices(NewStreetOptionData[] options, Action<NewStreetOptionData> onSelected)
    {
        choiceManager.ShowOutsideChoice(options, onSelected);
    }

    /// <summary>Step 하나의 대사를 타이핑 효과로 표시한다.</summary>
    public async UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken token)
    {
        dialoguePanel.SetActive(true);
        typer.ClearText();

        await typer.StartType(new TypingData(
            text,
            actor,
            Vector2.zero,
            Color.white,
            actor == PLAYER_ID));
    }

    public void SkipTyping()
    {
        typer.OnScreenClick();
    }

    public void EndScene()
    {
        playMode = EActivationMode.None;
        typer.ClearText();
        HideDialogue();
    }
}