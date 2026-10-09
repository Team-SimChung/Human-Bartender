using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using System.Collections.Generic;

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
        var texts = new List<string>();
        var selectable = new List<bool>();
        foreach (var option in options)
        {
            bool allowed = option.Selectable ?? true;
            texts.Add(allowed ? option.Text?.Ko : option.LockReason?.Ko ?? option.Text?.Ko);
            selectable.Add(allowed);
        }
        choiceManager.ShowChoice(texts, selectable, index => onSelected(options[index]));
    }

    /// <summary>Step 하나의 대사를 타이핑 효과로 표시한다.</summary>
    public async UniTask ShowDialogueAsync(string actor, string text, string arg, CancellationToken token)
    {
        dialoguePanel.SetActive(true);
        typer.ClearText();

        await typer.StartType(new TypingData(
            text,
            ResolveName(actor),
            Vector2.zero,
            Color.white,
            actor == PLAYER_ID), token: token);
    }

    static string ResolveName(string id)
    {
        foreach (var actor in NewDataLoadManager.StoryCharacters ?? Array.Empty<NewCharacterData>())
            if (actor.Id == id) return actor.Name.Ko;
        return id;
    }

    public void SkipTyping()
    {
        if (typer != null) typer.OnScreenClick();
    }

    public void EndScene()
    {
        playMode = EActivationMode.None;
        if (typer != null) typer.ClearText();
        if (choiceManager != null) choiceManager.CloseChoices();
        HideDialogue();
    }
}
