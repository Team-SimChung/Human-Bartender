using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
    public void ItemNoticeCanSwapTitleAndHideTheIcon()
    {
        var serialized = new SerializedObject(LoadIngredientPopup());
        foreach (string field in new[] { "title", "ingredientName", "iconFrame", "ingredientIcon" })
            Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, "연결되지 않은 참조: " + field);
    }

    static UIIngredientPopupView LoadIngredientPopup()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectScopePrefab);
        Assert.That(prefab, Is.Not.Null, ProjectScopePrefab);
        var view = prefab.GetComponentInChildren<UIIngredientPopupView>(true);
        Assert.That(view, Is.Not.Null, "ProjectLifeScope에 Popup_IngredientAcquired가 있어야 합니다.");
        return view;
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
