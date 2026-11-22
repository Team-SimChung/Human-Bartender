using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>버튼이나 토글의 마우스·키보드 포커스 피드백. 확정 소리는 각 화면이 재생한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Selectable))]
public sealed class UISelectionAudio : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    ISoundEffectPlayer sound;
    string hoverKey;
    Selectable selectable;
    int lastFrame = -1;

    public void Configure(ISoundEffectPlayer manager, string key)
    {
        sound = manager;
        hoverKey = key;
        selectable = GetComponent<Selectable>();
        lastFrame = -1;
    }

    internal static void ConfigureMenu(GameObject menu, ISoundEffectPlayer manager)
    {
        if (menu == null) return;
        string key = DialoguePresentationSettings.Shared?.ChoiceHoverSoundKey;
        foreach (var feedback in menu.GetComponentsInChildren<UISelectionAudio>(true))
            feedback.Configure(manager, key);
    }

    public void OnPointerEnter(PointerEventData eventData) => PlayHover();
    public void OnSelect(BaseEventData eventData) => PlayHover();

    void PlayHover()
    {
        if (selectable == null || !selectable.IsActive() || !selectable.IsInteractable() ||
            sound == null || string.IsNullOrWhiteSpace(hoverKey) || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        sound.PlaySE(hoverKey);
    }

    void OnDisable() => lastFrame = -1;
}
