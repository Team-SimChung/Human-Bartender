using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>웹 소지금 알림의 원본 이미지와 레이아웃을 공용 프리팹에 저장한다.</summary>
public static class CurrencyNoticeSetup
{
    const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab";
    const string ArtPath = "Assets/01.Game/03.Content/01.ArtResources/05.UI/01.Shared/01.MainScreen/IMG_money.png";
    const string FontPath = "Assets/01.Game/03.Content/05.Fonts/Regular_PFStardust_3_0_SDF.asset";

    [MenuItem("Tools/Human Bartender/UI/Apply web currency notice")]
    public static void Apply()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var panel = root.GetComponentInChildren<UICashPanel>(true);
            var data = new SerializedObject(panel);
            var frame = (RectTransform)data.FindProperty("noticeRoot").objectReferenceValue;
            if (frame == null)
                frame = panel.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "CashLine");
            frame.SetParent(panel.transform, false);
            frame.name = "CurrencyNotice";
            for (int index = frame.childCount - 1; index >= 0; index--)
                Object.DestroyImmediate(frame.GetChild(index).gameObject);
            var oldBackground = frame.GetComponent<Image>();
            if (oldBackground != null) Object.DestroyImmediate(oldBackground);
            frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.one;
            frame.anchoredPosition = new Vector2(-23, -17);
            frame.sizeDelta = new Vector2(234, 234f * 108 / 542);
            frame.localScale = Vector3.one * .5f;
            var group = frame.GetComponent<CanvasGroup>();
            if (group == null) group = frame.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = group.blocksRaycasts = false;

            var art = Child("Art", frame).gameObject.AddComponent<Image>();
            art.sprite = AssetDatabase.LoadAllAssetsAtPath(ArtPath).OfType<Sprite>()
                .OrderByDescending(s => s.rect.width * s.rect.height).First();
            art.raycastTarget = false;
            art.preserveAspect = true;
            var rect = art.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var amount = Label("Amount", frame, new Vector2(-18, 0), new Vector2(148, frame.sizeDelta.y), 31);
            amount.verticalAlignment = VerticalAlignmentOptions.Middle;
            amount.characterSpacing = -.5f;
            var delta = Label("Delta", frame, new Vector2(-18, -52), new Vector2(216, 36), 28);
            delta.verticalAlignment = VerticalAlignmentOptions.Top;
            data.FindProperty("amountText").objectReferenceValue = amount;
            data.FindProperty("deltaText").objectReferenceValue = delta;
            data.FindProperty("noticeRoot").objectReferenceValue = frame;
            data.FindProperty("noticeGroup").objectReferenceValue = group;
            data.ApplyModifiedPropertiesWithoutUndo();
            frame.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("WEB_CURRENCY_NOTICE_APPLIED");
    }

    static RectTransform Child(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = 5;
        rect.SetParent(parent, false);
        return rect;
    }

    static TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        var rect = Child(name, parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.TopRight;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

}
