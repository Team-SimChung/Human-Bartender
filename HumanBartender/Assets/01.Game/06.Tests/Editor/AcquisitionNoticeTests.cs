using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 재료·아이템 획득 알림은 실제로 받은 순간에만 뜬다(엔진 수정 사항 24·26).
/// 날짜·조건이 바뀌어 선반에 풀린 재료는 획득이 아니고, 거리 대본의 지급 행(기어 부품·엠블럼)은 아이템 획득 알림을 띄운다.
/// </summary>
public sealed class AcquisitionNoticeTests
{
    const string ProjectScopePrefab = "Assets/01.Game/03.Content/08.Prefabs/04.Shared/01.Bootstrap/ProjectLifeScope.prefab";

    // 3일차 조니 레시피 해금(wild_dog·bitters·sugar_cube의 unlock_when)이나 거리 대화의 flag 변화로 재료 알림이 뜨지 않는다.
    [TestCase("johnny_recipe_unlocked")]
    [TestCase("shiba_shop_met")]
    [TestCase("gang_wild_dog_received")]
    public void FlagChangesDoNotAnnounceIngredients(string flag)
    {
        var player = ScriptableObject.CreateInstance<PlayerDataSO>();
        try
        {
            var popups = new PopupSpy();
            using var audio = new SceneAudioController(null, player, popups);
            audio.Start();
            player.AddFlag(flag, true);
            Assert.That(popups.Types.Contains(PopupType.IngredientAcquired), Is.False, flag + " 변화로 재료 획득 알림이 뜨면 안 됩니다.");
        }
        finally { Object.DestroyImmediate(player); }
    }

    [TestCase("gear_part", "기어 부품")]
    [TestCase("machine_church_emblem", "기신교의 엠블럼")]
    public void StreetItemGrantsUseTheItemNotice(string id, string expectedName)
    {
        var view = LoadIngredientPopup();
        Assert.That(view.TryGetAcquiredItemName(id, out string name), Is.True, id + "는 재료가 아닌 획득 아이템이다.");
        Assert.That(name, Is.EqualTo(expectedName));
        Assert.That(view.TryGetAcquiredItemName("gin", out _), Is.False, "칵테일 재료는 재료 획득 알림을 그대로 쓴다.");
    }

    [Test]
    public void ItemNoticeShowsTheAcquiredItemWithoutAnIngredientIcon()
    {
        var view = Object.Instantiate(LoadIngredientPopup());
        try
        {
            using var serialized = new SerializedObject(view);
            var message = (TMP_Text)serialized.FindProperty("ingredientName").objectReferenceValue;
            var iconFrame = (GameObject)serialized.FindProperty("iconFrame").objectReferenceValue;
            Assert.That(serialized.FindProperty("ingredientIcon").objectReferenceValue, Is.Not.Null);
            Assert.That(view.TryPrepare("gear_part"), Is.True);
            Assert.That(message.text, Is.EqualTo("기어 부품 획득!"));
            Assert.That(iconFrame.activeSelf, Is.False);
        }
        finally { Object.DestroyImmediate(view.gameObject); }
    }

    [Test]
    public void SimultaneousNoticesStackInRequestOrderAndWaitForAvailableSpace()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var scope = (GameObject)PrefabUtility.InstantiatePrefab(LoadScope(), scene);
            var service = scope.GetComponentInChildren<PopupService>();
            using var serialized = new SerializedObject(service);
            var layout = (VerticalLayoutGroup)serialized.FindProperty("stack").objectReferenceValue;
            var rect = (RectTransform)layout.transform;
            var template = (UIPopupView)serialized.FindProperty("bindings.newObjective").objectReferenceValue;
            float height = template.Height;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(960, layout.padding.vertical + 2 * height + layout.spacing);

            Assert.That(service.Popup(PopupType.NewObjective, "첫째 목표").GetAwaiter().GetResult(), Is.True);
            Assert.That(service.Popup(PopupType.NewObjective, "둘째 목표").GetAwaiter().GetResult(), Is.True);
            Assert.That(service.Popup(PopupType.EventOccurred, "셋째 알림").GetAwaiter().GetResult(), Is.True);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            var first = ActiveRows(rect);
            Assert.That(first.Select(NoticeText), Is.EqualTo(new[] { "첫째 목표", "둘째 목표" }));
            Assert.That(first[0].anchoredPosition, Is.EqualTo(new Vector2(layout.padding.left, -layout.padding.top)));
            Assert.That(first[1].anchoredPosition.y, Is.EqualTo(first[0].anchoredPosition.y - height - layout.spacing));

            first[0].GetComponent<UIPopupView>().Hide();
            DOTween.Complete(first[0].GetComponent<CanvasGroup>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            Assert.That(ActiveRows(rect).Select(NoticeText), Is.EqualTo(new[] { "둘째 목표", "셋째 알림" }));

            Assert.That(service.Popup(PopupType.NewObjective, "넷째 목표").GetAwaiter().GetResult(), Is.True);
            Assert.That(rect.childCount, Is.EqualTo(3), "닫힌 목표 행을 다음 요청에 재사용합니다.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase("ingredientAcquired", "IMG_ingredient")]
    [TestCase("characterRegistered", "IMG_new_character")]
    [TestCase("affinityIncreased", "IMG_decrease")]
    [TestCase("affinityDecreased", "IMG_increase")]
    [TestCase("newObjective", "IMG_objective")]
    [TestCase("objectiveCompleted", "IMG_objective_complete")]
    [TestCase("eventOccurred", "IMG_new_event")]
    public void NoticeTemplatesUseTheLatestArt(string binding, string spriteName)
    {
        using var serialized = new SerializedObject(LoadScope().GetComponentInChildren<PopupService>());
        var view = (UIPopupView)serialized.FindProperty("bindings." + binding).objectReferenceValue;
        var image = view.GetComponent<Image>();
        Assert.That(image.sprite.name, Is.EqualTo(spriteName + "_0"));
        Assert.That(image.raycastTarget, Is.False);
        Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(view.gameObject.activeSelf, Is.False);
    }

    static RectTransform[] ActiveRows(RectTransform stack) => stack.Cast<RectTransform>().Where(rect => rect.gameObject.activeSelf).ToArray();
    static string NoticeText(RectTransform row) => row.GetComponentInChildren<TMP_Text>().text;

    static UIIngredientPopupView LoadIngredientPopup()
    {
        var view = LoadScope().GetComponentInChildren<UIIngredientPopupView>(true);
        Assert.That(view, Is.Not.Null, "ProjectLifeScope에 Popup_IngredientAcquired가 있어야 합니다.");
        return view;
    }

    static GameObject LoadScope()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectScopePrefab);
        Assert.That(prefab, Is.Not.Null, ProjectScopePrefab);
        return prefab;
    }

    sealed class PopupSpy : IPopupService
    {
        public readonly List<PopupType> Types = new();
        public UniTask<bool> Popup(PopupType type, string data)
        {
            Types.Add(type);
            return UniTask.FromResult(true);
        }
    }
}
