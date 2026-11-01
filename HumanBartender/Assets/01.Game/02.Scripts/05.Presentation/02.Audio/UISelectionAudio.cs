using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>선택지의 마우스·키보드 포커스 피드백. 확정 소리는 선택지를 소유한 뷰가 재생한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UISelectionAudio : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    ISoundEffectPlayer sound;
    string hoverKey;
    Button button;
    int lastFrame = -1;

    public void Configure(ISoundEffectPlayer manager, string key)
    {
        sound = manager;
        hoverKey = key;
        button = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData) => PlayHover();
    public void OnSelect(BaseEventData eventData) => PlayHover();

    void PlayHover()
    {
        if (button == null || !button.IsActive() || !button.IsInteractable() ||
            sound == null || string.IsNullOrWhiteSpace(hoverKey) || lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        sound.PlaySE(hoverKey);
    }

    void OnDisable() => lastFrame = -1;
}
