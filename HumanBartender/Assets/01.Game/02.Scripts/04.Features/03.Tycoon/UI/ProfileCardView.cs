using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>씬에 배치된 인물 카드 하나. 등록·선택 상태만 바꾸며 진행 데이터를 수정하지 않는다.</summary>
public sealed class ProfileCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("인물 · 기존 그림")]
    [SerializeField, Tooltip("characters.csv의 인물 ID입니다.")] string characterId;
    [SerializeField, Tooltip("이 카드와 상세창의 초상 표시 방식입니다. 다른 방식의 연결은 유지됩니다. 실행 중 변경 시 화면을 다시 열면 반영됩니다.")]
    ProfilePortraitView.Mode portraitMode;
    [SerializeField, Tooltip("단일 이미지 방식에서 사용할 초상입니다. 비어 있으면 —로 표시합니다.")] Sprite singlePortrait;
    [SerializeField, Tooltip("파츠형에서 사용할 기존 초상 연결입니다. 단일 이미지로 바꿔도 지우지 않습니다.")]
    ProfilePortraitView.Layer[] portraitLayers;
    [SerializeField] ProfilePortraitView portrait;
    [Header("카드 연결")]
    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] Image frame;
    [SerializeField] GameObject unknownGroup;
    [SerializeField] GameObject captionGroup;
    [SerializeField] TMP_Text nameText;
    [SerializeField] Image[] affinitySegments;
    [Header("Profile 원본 리소스")]
    [SerializeField] Sprite unmetBackground;
    [SerializeField] Sprite normalBackground;
    [SerializeField] Sprite selectedBackground;
    [SerializeField] Sprite unmetFrame;
    [SerializeField] Sprite normalFrame;
    [SerializeField] Sprite selectedFrame;
    [SerializeField] Sprite filledAffinity;
    [SerializeField] Sprite emptyAffinity;
    [SerializeField, Tooltip("선택 이미지 바깥 발광 여백. 카드 배치 크기는 유지합니다.")] Vector2 selectedPadding;

    bool registered;
    bool selected;
    bool hovered;
    bool focused;
    public string CharacterId => characterId;
    public ProfilePortraitView.Mode PortraitMode => portraitMode;
    public Sprite SinglePortrait => singlePortrait;
    public ProfilePortraitView.Layer[] PortraitLayers => portraitLayers;
    public Button SelectionControl => button;
    public event Action<ProfileCardView> Clicked;

    void OnEnable() { if (button != null) button.onClick.AddListener(Click); }
    void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(Click);
        hovered = focused = false;
    }

    void Click()
    {
        if (registered) Clicked?.Invoke(this);
    }

    public void Show(string label, bool met, int stage)
    {
        registered = met;
        button.interactable = met;
        nameText.text = met ? label : "";
        unknownGroup.SetActive(!met);
        captionGroup.SetActive(met);
        portrait.gameObject.SetActive(met);
        if (met) portrait.Show(portraitMode, singlePortrait, portraitLayers);
        for (int i = 0; i < affinitySegments.Length; i++)
            affinitySegments[i].sprite = i < stage ? filledAffinity : emptyAffinity;
        ApplyState();
    }

    public void Select(bool value) { selected = value; ApplyState(); }
    public void OnPointerEnter(PointerEventData data) { hovered = true; ApplyState(); }
    public void OnPointerExit(PointerEventData data) { hovered = false; ApplyState(); }
    public void OnSelect(BaseEventData data) { focused = true; ApplyState(); }
    public void OnDeselect(BaseEventData data) { focused = false; ApplyState(); }

    void ApplyState()
    {
        bool highlight = registered && (selected || hovered || focused);
        background.sprite = !registered ? unmetBackground : highlight ? selectedBackground : normalBackground;
        frame.sprite = !registered ? unmetFrame : highlight ? selectedFrame : normalFrame;
        var padding = highlight ? selectedPadding : Vector2.zero;
        background.rectTransform.sizeDelta = frame.rectTransform.sizeDelta = padding;
    }
}
