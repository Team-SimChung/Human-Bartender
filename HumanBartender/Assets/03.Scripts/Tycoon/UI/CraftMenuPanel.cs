using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 좌측 Craft Panel 내부에서 "메뉴(제조하기 버튼)" 뷰, "제조할 음료 선택" 목록 뷰, "음료 설명" 상세 뷰를 전환한다.
/// 목록은 cocktailData를 이름 기준으로 정렬해 첫 글자별로 구분선을 넣어 보여준다(레퍼런스 이미지의 알파벳 목록 참고).
/// 목록에서 항목을 고르면 상세 뷰로 이름/아이콘/설명/태그(잔/제조법/키워드)를 보여주고, 상세 뷰의 우측 하단
/// 버튼을 누르면 CraftStarted 이벤트가 발생한다 — 실제 제조 미니게임 연결은 추후 작업.
/// </summary>
public class CraftMenuPanel : MonoBehaviour
{
    [Header("Views")]
    [SerializeField] GameObject menuView;
    [SerializeField] GameObject cocktailListView;
    [SerializeField] GameObject detailView;

    [Header("Menu View")]
    [SerializeField] Button craftButton;

    [Header("Cocktail List View")]
    [SerializeField] Button backButton;
    [SerializeField] RectTransform listContent;

    [Header("Detail View")]
    [SerializeField] Button detailBackButton;
    [SerializeField] TextMeshProUGUI detailTitleText;
    [SerializeField] Image detailIconImage;
    [SerializeField] TextMeshProUGUI detailDescriptionText;
    [SerializeField] RectTransform detailTagsContent;
    [SerializeField] Button startCraftButton;

    [Header("Data")]
    [SerializeField] NewCocktailDataSO cocktailData;

    [Header("Font")]
    [SerializeField] TMP_FontAsset font; // 한글 미지원 기본 폰트 대신 NeoDunggeunmo SDF를 써야 한다.

    [Header("Row Style")]
    [SerializeField] float rowHeight = 40f;
    [SerializeField] float headerHeight = 24f;
    [SerializeField] Color headerColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField] Color rowColor = new Color(1f, 1f, 1f, 0.03f);
    [SerializeField] Color tagColor = new Color(1f, 1f, 1f, 0.1f);

    /// <summary>상세 뷰의 제조 시작 버튼을 눌렀을 때 선택된 칵테일 id와 함께 발생한다. 제조 로직 연결은 구독하는 쪽에서 처리한다.</summary>
    public event System.Action<string> CraftStarted;

    /// <summary>
    /// 제조 흐름 안에 들어갔는지가 바뀔 때 발생한다. 칵테일 목록을 열면 true, 고르지 않고 되돌아가면 false다.
    /// 제조를 시작한 뒤에는 이 패널이 닫혀 있으므로 여기서 false가 나가지 않는다 — 제조가 끝났다는 소식은
    /// 제조 쪽(CraftFlowController)이 알린다. 바 운영 타이머를 세우는 쪽이 이 신호를 쓴다.
    /// </summary>
    public event System.Action<bool> CraftFlowActiveChanged;

    string selectedCocktailId;

    const int HangulBase = 0xAC00;
    const int HangulLast = 0xD7A3;
    static readonly char[] Choseong =
    {
        'ㄱ', 'ㄲ', 'ㄴ', 'ㄷ', 'ㄸ', 'ㄹ', 'ㅁ', 'ㅂ', 'ㅃ', 'ㅅ',
        'ㅆ', 'ㅇ', 'ㅈ', 'ㅉ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ',
    };

    bool isPopulated;

    void Awake()
    {
        craftButton.onClick.AddListener(OpenCocktailList);
        backButton.onClick.AddListener(CloseCocktailList);
        detailBackButton.onClick.AddListener(CloseDetail);
        startCraftButton.onClick.AddListener(() => CraftStarted?.Invoke(selectedCocktailId));

        ShowMenu();
    }

    /// <summary>
    /// 제조하기·제조 시작 버튼을 누를 수 있는지 정한다.
    ///
    /// 막히는 동안에도 버튼이 멀쩡히 눌리면 눌렀는데 아무 일도 일어나지 않는 화면이 된다.
    ///
    /// 안쪽의 제조 시작 버튼만 끄지 않고 메뉴를 여는 버튼까지 끈다. 칵테일 메뉴에 들어가는 순간
    /// 바 운영 시간이 멈추기 때문에(craft_pause_start), 어차피 만들지 못할 때 메뉴만 열어 두면
    /// 손님 시간이 멈춘 채로 아무 일도 일어나지 않는다.
    /// </summary>
    public void SetCraftEnabled(bool enabled)
    {
        if (craftButton != null) craftButton.interactable = enabled;
        if (startCraftButton != null) startCraftButton.interactable = enabled;
    }

    /// <summary>
    /// 처음 화면(제조하기 버튼)으로 되돌린다.
    ///
    /// 칵테일을 고르고 제조로 넘어가면 이 패널은 상세 뷰를 띄운 채로 닫힌다. 그대로 두면 다음에
    /// 열었을 때 지난번에 보던 칵테일 설명이 그대로 남아 있어, 방금 만든 것을 또 고르는 화면처럼 보인다.
    /// </summary>
    public void ResetToMenu()
    {
        selectedCocktailId = null;
        ShowMenu();
    }

    void ShowMenu()
    {
        menuView.SetActive(true);
        cocktailListView.SetActive(false);
        detailView.SetActive(false);
    }

    void OpenCocktailList()
    {
        if (!isPopulated)
        {
            Populate();
            isPopulated = true;
        }

        menuView.SetActive(false);
        cocktailListView.SetActive(true);

        CraftFlowActiveChanged?.Invoke(true);
        detailView.SetActive(false);
    }

    void CloseCocktailList()
    {
        ShowMenu();

        // 고르지 않고 되돌아왔으므로 제조 흐름에서 빠져나온 것이다. 여기서 알리지 않으면
        // 목록만 열어 봤다가 닫은 뒤로 바 운영 시간이 영영 멈춰 있게 된다.
        CraftFlowActiveChanged?.Invoke(false);
    }

    /// <summary>목록에서 항목을 선택했을 때 상세 뷰로 전환하고 이름/아이콘/설명/태그를 채운다.</summary>
    void ShowDetail(NewCocktailData cocktail)
    {
        selectedCocktailId = cocktail.Id;

        detailTitleText.text = cocktail.Name.Ko;
        detailDescriptionText.text = cocktail.Flavor.Ko;
        detailIconImage.color = ColorUtility.TryParseHtmlString(cocktail.Color, out Color color) ? color : Color.white;

        PopulateTags(cocktail);

        cocktailListView.SetActive(false);
        detailView.SetActive(true);
    }

    /// <summary>상세 뷰의 뒤로가기. 메뉴가 아니라 목록 뷰로 돌아간다.</summary>
    void CloseDetail()
    {
        detailView.SetActive(false);
        cocktailListView.SetActive(true);
    }

    /// <summary>잔(Glass), 제조법(Mix), 키워드(Tags)를 태그 칩으로 나열한다. 매번 기존 칩을 지우고 새로 만든다.</summary>
    void PopulateTags(NewCocktailData cocktail)
    {
        for (int i = detailTagsContent.childCount - 1; i >= 0; i--)
            Destroy(detailTagsContent.GetChild(i).gameObject);

        if (!string.IsNullOrEmpty(cocktail.Glass))
            CreateTagChip(cocktail.Glass);

        string mixLabel = GetMixLabel(cocktail.Mix);
        if (mixLabel != null)
            CreateTagChip(mixLabel);

        if (cocktail.Tags == null) return;

        foreach (var tag in cocktail.Tags)
            if (!string.IsNullOrEmpty(tag.Ko))
                CreateTagChip(tag.Ko);
    }

    static string GetMixLabel(ENewMixMethod mix) => mix switch
    {
        ENewMixMethod.Build => "빌드",
        ENewMixMethod.Shake => "쉐이크",
        ENewMixMethod.Stir => "스터(믹싱글라스)",
        _ => null,
    };

    /// <summary>텍스트 폭에 맞춰 너비를 계산하는 태그 칩 하나를 detailTagsContent 아래에 만든다.</summary>
    void CreateTagChip(string label)
    {
        var go = new GameObject("Tag", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(detailTagsContent, false);

        go.GetComponent<Image>().color = tagColor;

        var text = CreateLabel(go.transform, label, 13f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.85f), 0f);
        text.alignment = TextAlignmentOptions.Center;

        Vector2 preferred = text.GetPreferredValues();
        var layout = go.GetComponent<LayoutElement>();
        layout.preferredWidth = preferred.x + 20f;
        layout.preferredHeight = 26f;
        layout.minHeight = 26f;
    }

    /// <summary>cocktailData를 이름(Ko) 순으로 정렬해 첫 글자가 바뀔 때마다 구분 행을, 그 아래에 항목 행을 만든다.</summary>
    void Populate()
    {
        var sorted = cocktailData.cocktailData
            .OrderBy(c => c.Name.Ko, System.StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string currentLetter = null;
        foreach (var cocktail in sorted)
        {
            string letter = GetGroupKey(cocktail.Name.Ko);

            if (letter != currentLetter)
            {
                currentLetter = letter;
                CreateHeaderRow(letter);
            }

            CreateCocktailRow(cocktail);
        }
    }

    /// <summary>
    /// 이름 첫 글자로 구분 그룹 키를 만든다. 한글 음절(가~힣)이면 초성만 분리해 반환한다
    /// (예: "깔루아밀크" -> "ㄲ"). 한글이 아니면 첫 글자를 대문자로 반환한다.
    /// </summary>
    static string GetGroupKey(string name)
    {
        if (string.IsNullOrEmpty(name)) return "?";

        char c = name[0];
        if (c >= HangulBase && c <= HangulLast)
        {
            int choseongIndex = (c - HangulBase) / (21 * 28);
            return Choseong[choseongIndex].ToString();
        }

        return c.ToString().ToUpperInvariant();
    }

    void CreateHeaderRow(string letter)
    {
        var go = new GameObject($"Header {letter}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(listContent, false);

        go.GetComponent<Image>().color = headerColor;

        var layout = go.GetComponent<LayoutElement>();
        layout.preferredHeight = headerHeight;
        layout.minHeight = headerHeight;

        var text = CreateLabel(go.transform, letter, 14f, FontStyles.Bold, new Color(1f, 1f, 1f, 0.6f), 12f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
    }

    void CreateCocktailRow(NewCocktailData cocktail)
    {
        var go = new GameObject(cocktail.Id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(listContent, false);

        go.GetComponent<Image>().color = rowColor;

        var layout = go.GetComponent<LayoutElement>();
        layout.preferredHeight = rowHeight;
        layout.minHeight = rowHeight;

        CreateIcon(go.transform, cocktail.Color);

        var text = CreateLabel(go.transform, cocktail.Name.Ko, 16f, FontStyles.Normal, Color.white, 40f);
        text.alignment = TextAlignmentOptions.MidlineLeft;

        go.GetComponent<Button>().onClick.AddListener(() => ShowDetail(cocktail));
    }

    /// <summary>cocktail.color(hex)로 채운 작은 정사각형 아이콘 placeholder. 파싱 실패 시 흰색.</summary>
    void CreateIcon(Transform parent, string colorHex)
    {
        const float size = 20f;

        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = new Vector2(10f, 0f);

        if (!ColorUtility.TryParseHtmlString(colorHex, out Color color))
            color = Color.white;

        go.GetComponent<Image>().color = color;
    }

    TextMeshProUGUI CreateLabel(Transform parent, string content, float fontSize, FontStyles style, Color color, float leftOffset)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(leftOffset, 0f);
        rect.offsetMax = Vector2.zero;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        if (font != null) text.font = font;

        return text;
    }
}
