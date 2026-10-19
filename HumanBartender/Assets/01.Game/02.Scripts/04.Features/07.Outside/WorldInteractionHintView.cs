using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>InteractionHint 프리팹의 안내 문구와 클릭 동작만 바꿈.</summary>
public sealed class WorldInteractionHintView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image keyIcon;
    [Tooltip("비워 두면 E 아이콘을 숨기고 문구에 Esc를 표시합니다.")]
    [SerializeField] private Sprite escapeKeyIcon;
    [SerializeField] private string interactionText = "상호작용";
    [SerializeField] private string revealText = "펼치기";
    [SerializeField] private string advanceText = "다음";
    [SerializeField] private string exitText = "나가기";
    [SerializeField] private string selectionText = "선택";
    private Sprite interactKeyIcon;
    private Action advance;
    private Action close;
    private bool exits;

    internal Button Button => button;
    internal TMP_Text Label => label;
    internal Image KeyIcon => keyIcon;
    public bool IsConfigured => button != null && label != null && keyIcon != null;

    public void Initialize(Action onAdvance, Action onClose)
    {
        advance = onAdvance;
        close = onClose;
        interactKeyIcon = keyIcon.sprite;
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        if (exits) close?.Invoke();
        else advance?.Invoke();
    }

    public void ShowInteraction(string actionLabel)
        => Show(string.IsNullOrWhiteSpace(actionLabel) ? interactionText : actionLabel, false);

    public void ShowDialogue(bool waiting) => Show(waiting ? advanceText : revealText, false);
    public void ShowExit() => Show(exitText, true);
    public void ShowSelection() => Show(selectionText, false);

    private void Show(string text, bool exit)
    {
        exits = exit;
        var icon = exit ? escapeKeyIcon : interactKeyIcon;
        keyIcon.sprite = icon;
        keyIcon.enabled = icon != null;
        // 상호작용·진행은 프리팹의 E 아이콘을 쓰고 종료는 Esc로 구분함.
        label.text = icon != null ? text : (exit ? "Esc  " : "E  ") + text;
        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }
}
