using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>칵테일 하나를 표시하고, 호버와 클릭을 목록 컨트롤러에 전달한다.</summary>
public sealed class CocktailRecipeRow : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image cocktailIcon;
    [SerializeField] private TextMeshProUGUI cocktailNameText;
    [SerializeField] private TextMeshProUGUI methodAndPriceText;
    [SerializeField] private Color lockedTextColor = new Color(0.47f, 0.53f, 0.60f, 1f);

    private CocktailRecipeBrowser browser;
    private string cocktailId;
    private bool unlocked;
    private Color nameColor;
    private Color detailColor;
    private Color iconColor;

    private void Awake()
    {
        if (cocktailNameText != null) nameColor = cocktailNameText.color;
        if (methodAndPriceText != null) detailColor = methodAndPriceText.color;
        if (cocktailIcon != null) iconColor = cocktailIcon.color;
    }

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    public void Bind(NewCocktailData cocktail, bool isUnlocked, Sprite icon, CocktailRecipeBrowser owner)
    {
        if (button == null || cocktailIcon == null || cocktailNameText == null || methodAndPriceText == null)
        {
            Debug.LogError("[CocktailRecipeRow] 프리팹의 Button, Image, TMP 참조를 연결해 주세요.", this);
            return;
        }

        browser = owner;
        cocktailId = cocktail.Id;
        unlocked = isUnlocked;

        cocktailNameText.text = cocktail.Name.Ko;
        methodAndPriceText.text = cocktail.Mix.ToString().ToUpperInvariant() + " · " + cocktail.Price + " G";
        cocktailNameText.color = unlocked ? nameColor : lockedTextColor;
        methodAndPriceText.color = unlocked ? detailColor : lockedTextColor;
        cocktailIcon.sprite = icon;
        cocktailIcon.enabled = icon != null;
        cocktailIcon.preserveAspect = true;
        cocktailIcon.color = unlocked ? iconColor : new Color(iconColor.r, iconColor.g, iconColor.b, iconColor.a * 0.45f);
        button.interactable = unlocked;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (unlocked && browser != null) browser.ShowPreview(cocktailId);
    }

    private void HandleClick()
    {
        if (unlocked && browser != null) browser.TryStart(cocktailId);
    }
}
