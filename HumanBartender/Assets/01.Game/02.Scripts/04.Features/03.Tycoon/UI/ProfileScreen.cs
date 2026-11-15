using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

/// <summary>기존 인물·수첩 데이터의 읽기 전용 열람 화면. UI 구조는 Play 씬에 저장한다.</summary>
public sealed class ProfileScreen : MonoBehaviour
{
    [Serializable]
    struct InformationSection
    {
        public ENewDossierKind kind;
        public TMP_Text text;
    }

    [Header("기존 데이터")]
    [SerializeField] NewCharacterDataSO characters;
    [SerializeField] NewDossierDataSO dossier;
    [SerializeField] PlayPhaseController playPhase;
    [Inject] IPlayerDataReader player;
    [Inject] IConditionUtil conditions;
    [Header("목록")]
    [SerializeField] GameObject root;
    [SerializeField] CanvasGroup rootGroup;
    [SerializeField] CanvasGroup listGroup;
    [SerializeField] ScrollRect listScroll;
    [SerializeField] Button closeButton;
    [SerializeField] ProfileCardView[] cards;
    [Header("상세")]
    [SerializeField] GameObject detailRoot;
    [SerializeField] Button detailCloseButton;
    [SerializeField] ScrollRect detailScroll;
    [SerializeField] ProfilePortraitView detailPortrait;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text englishNameText;
    [SerializeField] InformationSection[] sections;
    [SerializeField] Image[] affinitySegments;
    [SerializeField] Sprite filledAffinity;
    [SerializeField] Sprite emptyAffinity;
    [Header("표시 규칙")]
    [Tooltip("웹 Profile의 2·3단계 호감도 기준. 설명 공개는 dossier의 min_affinity/when을 사용합니다.")]
    [SerializeField] Vector2Int affinityThresholds = new(10, 30);
    [SerializeField] string missingText = "—";
    [SerializeField] string lockedText = "???";
    [SerializeField, Min(0)] float fadeDuration = .15f;

    ProfileCardView selectedCard;
    EPlayPhase openedPhase;
    ELanguage language;
    float previousTimeScale;
    Tween fade;
    public bool IsOpen { get; private set; }
    public bool IsDetailOpen => IsOpen && detailRoot.activeSelf;
    public bool CanOpen => isActiveAndEnabled && player != null && conditions != null &&
        characters != null && characters.characterData != null && dossier != null && dossier.dossierData != null &&
        playPhase != null && (playPhase.CanReceiveInput(EPlayPhase.Tycoon) || playPhase.CanReceiveInput(EPlayPhase.Dialogue));
    public event Action OpenChanged;

    void Awake() { if (root != null) root.SetActive(false); }
    void OnEnable()
    {
        if (closeButton != null) closeButton.onClick.AddListener(CloseFromButton);
        if (detailCloseButton != null) detailCloseButton.onClick.AddListener(BackFromButton);
        if (cards != null) foreach (var card in cards) if (card != null) card.Clicked += OpenDetail;
    }
    void OnDisable()
    {
        Close();
        if (closeButton != null) closeButton.onClick.RemoveListener(CloseFromButton);
        if (detailCloseButton != null) detailCloseButton.onClick.RemoveListener(BackFromButton);
        if (cards != null) foreach (var card in cards) if (card != null) card.Clicked -= OpenDetail;
    }
    void Update()
    {
        if (!IsOpen) return;
        if (!CanOpen || !playPhase.CanReceiveInput(openedPhase)) { Close(); return; }
        if (language != GameStateManager.Instance.Language) Refresh();
    }

    public void Open()
    {
        if (IsOpen || !CanOpen || UIDisplayOptions.IsOptionOpen) return;
        openedPhase = playPhase.CurrentPhase;
        previousTimeScale = Time.timeScale;
        IsOpen = true;
        Time.timeScale = 0;
        detailRoot.SetActive(false);
        listGroup.interactable = listGroup.blocksRaycasts = true;
        selectedCard = null;
        root.SetActive(true);
        Refresh();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(listScroll.content);
        listScroll.StopMovement();
        listScroll.verticalNormalizedPosition = 1;
        fade?.Kill();
        rootGroup.alpha = 0;
        fade = rootGroup.DOFade(1, fadeDuration).SetUpdate(true);
        EventSystem.current?.SetSelectedGameObject(closeButton.gameObject);
        OpenChanged?.Invoke();
    }

    internal void Refresh()
    {
        language = GameStateManager.Instance.Language;
        foreach (var card in cards)
        {
            bool known = TryCharacter(card.CharacterId, out var character);
            bool met = known && player.CheckFlag(card.CharacterId + "_met");
            card.Show(known ? Text(character.Name, language, missingText) : missingText, met,
                Stage(character, met));
            card.Select(card == selectedCard);
        }
        if (selectedCard != null && IsDetailOpen)
        {
            if (!player.CheckFlag(selectedCard.CharacterId + "_met")) Back();
            else RenderDetail(selectedCard);
        }
    }

    int Stage(NewCharacterData character, bool met) => !met ? 0 : !character.Affinity ? 1 :
        player.GetCurCharacterAffinityValue(character.Id) >= affinityThresholds.y ? 3 :
        player.GetCurCharacterAffinityValue(character.Id) >= affinityThresholds.x ? 2 : 1;

    internal bool TryCharacter(string id, out NewCharacterData value)
    {
        foreach (var character in characters.characterData)
            if (character.Id == id) { value = character; return true; }
        value = default;
        return false;
    }

    public void OpenDetail(ProfileCardView card)
    {
        if (!IsOpen || card == null || UIDisplayOptions.IsOptionOpen ||
            !TryCharacter(card.CharacterId, out _) || !player.CheckFlag(card.CharacterId + "_met")) return;
        selectedCard = card;
        foreach (var item in cards) item.Select(item == card);
        listScroll.StopMovement();
        listGroup.interactable = listGroup.blocksRaycasts = false;
        detailRoot.SetActive(true);
        RenderDetail(card);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(detailScroll.content);
        detailScroll.StopMovement();
        detailScroll.verticalNormalizedPosition = 1;
        EventSystem.current?.SetSelectedGameObject(detailCloseButton.gameObject);
    }

    void RenderDetail(ProfileCardView card)
    {
        TryCharacter(card.CharacterId, out var character);
        nameText.text = Text(character.Name, language, missingText);
        englishNameText.text = string.IsNullOrWhiteSpace(character.Name.En) ? missingText : character.Name.En.ToUpperInvariant();
        detailPortrait.Show(card.PortraitMode, card.SinglePortrait, card.PortraitLayers);
        int stage = Stage(character, true);
        for (int i = 0; i < affinitySegments.Length; i++)
            affinitySegments[i].sprite = i < stage ? filledAffinity : emptyAffinity;
        foreach (var section in sections)
            section.text.text = SectionText(dossier.dossierData, character.Id, section.kind,
                player.GetCurCharacterAffinityValue(character.Id), conditions, language, missingText, lockedText);
    }

    // 조건이 거짓인 사건 원고는 숨기고, 호감도만 부족한 원고는 잠금으로 표시한다.
    internal static string SectionText(NewDossierData[] rows, string id, ENewDossierKind kind,
        int affinity, IConditionUtil conditions, ELanguage language, string missing, string locked)
    {
        var visible = new List<string>();
        bool hasLocked = false;
        foreach (var row in rows)
        {
            if (row.CharacterId != id || row.Kind != kind || !conditions.CheckRequired(row.When)) continue;
            if (affinity < row.MinAffinity) { hasLocked = true; continue; }
            string text = Text(row.Text, language, null);
            if (!string.IsNullOrWhiteSpace(text)) visible.Add(text);
        }
        if (hasLocked) visible.Add(locked);
        return visible.Count == 0 ? missing : string.Join("\n\n", visible);
    }

    internal static string Text(LocalizedText text, ELanguage language, string missing) =>
        language == ELanguage.En && !string.IsNullOrWhiteSpace(text.En) ? text.En :
        !string.IsNullOrWhiteSpace(text.Ko) ? text.Ko : missing;

    void CloseFromButton() { if (!UIDisplayOptions.IsOptionOpen) Close(); }
    void BackFromButton() { if (!UIDisplayOptions.IsOptionOpen) Back(); }
    public void Back()
    {
        if (!IsOpen) return;
        if (!IsDetailOpen) { Close(); return; }
        detailScroll.StopMovement();
        detailRoot.SetActive(false);
        listGroup.interactable = listGroup.blocksRaycasts = true;
        EventSystem.current?.SetSelectedGameObject(selectedCard != null ? selectedCard.SelectionControl.gameObject : closeButton.gameObject);
    }

    public void Close()
    {
        fade?.Kill();
        fade = null;
        if (!IsOpen) return;
        IsOpen = false;
        Time.timeScale = previousTimeScale;
        listScroll.StopMovement();
        detailScroll.StopMovement();
        detailRoot.SetActive(false);
        root.SetActive(false);
        EventSystem.current?.SetSelectedGameObject(null);
        OpenChanged?.Invoke();
    }
}
