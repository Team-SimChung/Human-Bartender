using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class ServeFallbackTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Field(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);

    static CraftedDrink Drink(string id) =>
        new(new CraftSession(id), null, new NewCocktailData { Id = id, Name = new LocalizedText { Ko = id }, Color = "#ffffff" });

    // Mirrors Canvas_Serve/BG: drink (Image + Animator + ServeScreen) followed by FallbackDrinkImage and the grade.
    sealed class Fixture : IDisposable
    {
        public readonly GameObject bg;
        public readonly ServeScreen screen;
        public readonly Image animated;
        public readonly Image still;
        public readonly TMP_Text grade;
        public readonly Sprite stillArt;
        readonly Texture2D texture;
        readonly CocktailRecipeVisualCatalog catalog;
        readonly AnimationClip clip;

        public Fixture(bool connectStill = true)
        {
            bg = new GameObject("BG", typeof(RectTransform));
            var drink = new GameObject("drink", typeof(RectTransform), typeof(Image), typeof(Animator));
            drink.SetActive(false);
            drink.transform.SetParent(bg.transform, false);
            screen = drink.AddComponent<ServeScreen>();
            animated = drink.GetComponent<Image>();
            still = new GameObject("FallbackDrinkImage", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            still.transform.SetParent(bg.transform, false);
            still.rectTransform.anchoredPosition = new Vector2(0, 10);
            still.gameObject.SetActive(false);
            grade = new GameObject("Grade", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            grade.transform.SetParent(bg.transform, false);
            Set(screen, "animator", drink.GetComponent<Animator>());
            Set(screen, "Grade", grade);

            texture = new Texture2D(1, 1);
            stillArt = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            catalog = ScriptableObject.CreateInstance<CocktailRecipeVisualCatalog>();
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("cocktailSprites");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("id").stringValue = CampaignStoryRules.JohnnyCocktailId;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("sprite").objectReferenceValue = stillArt;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (connectStill)
            {
                Set(screen, "fallbackDrinkImage", still);
                Set(screen, "animatedDrinkImage", animated);
                Set(screen, "visuals", catalog);
            }

            clip = new AnimationClip { name = "Serve test" };
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
            var field = typeof(ServeScreen).GetField("_animations", Private);
            var animations = (IList)Activator.CreateInstance(field.FieldType);
            foreach (string id in new[] { "gin_tonic", "cosmopolitan" })
            {
                var entry = Activator.CreateInstance(field.FieldType.GetGenericArguments()[0]);
                entry.GetType().GetField("id").SetValue(entry, id);
                entry.GetType().GetField("clip").SetValue(entry, clip);
                animations.Add(entry);
            }
            field.SetValue(screen, animations);
            drink.SetActive(true);
        }

        public void Dispose()
        {
            Object.DestroyImmediate(bg);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(stillArt);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void CocktailWithAClipPlaysTheAnimationAndKeepsTheStillHidden()
    {
        using var f = new Fixture();
        Assert.That(f.screen.TryPlay(Drink("gin_tonic")), Is.True);
        Assert.That(f.still.gameObject.activeSelf, Is.False);
        Assert.That(f.animated.enabled, Is.True);
        Assert.That(f.grade.gameObject.activeSelf, Is.False, "클립이 끝나기 전에는 등급을 숨긴다.");
    }

    [Test]
    public void CocktailWithoutAClipShowsItsFinishedArtLikeTheWebFallback()
    {
        using var f = new Fixture();
        // Edit Mode has no tween clock; the still finishes immediately and reveals the grade.
        Assert.That(f.screen.TryPlay(Drink(CampaignStoryRules.JohnnyCocktailId)), Is.True);
        Assert.That(f.animated.enabled, Is.False, "코스모폴리탄 애니메이션 Image를 숨긴다.");
        Assert.That(f.still.gameObject.activeSelf && f.still.enabled, Is.True);
        Assert.That(f.still.sprite, Is.SameAs(f.stillArt));
        Assert.That(f.still.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0, 10 - 60)));
        Assert.That(f.still.rectTransform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(f.still.color.a, Is.EqualTo(1f));
        Assert.That(f.grade.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void ResetAndTheNextClipRestoreTheStillToItsSavedPlace()
    {
        using var f = new Fixture();
        f.screen.TryPlay(Drink(CampaignStoryRules.JohnnyCocktailId));
        f.screen.Reset();
        Assert.That(f.still.gameObject.activeSelf, Is.False);
        Assert.That(f.still.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0, 10)));
        Assert.That(f.still.rectTransform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(f.animated.enabled, Is.True);

        f.screen.TryPlay(Drink(CampaignStoryRules.JohnnyCocktailId));
        Assert.That(f.screen.TryPlay(Drink("gin_tonic")), Is.True);
        Assert.That(f.still.gameObject.activeSelf, Is.False);
        Assert.That(f.still.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0, 10)), "연속 재생에도 자리가 밀리지 않는다.");
    }

    [Test]
    public void MissingCatalogArtWarnsAndStillRevealsTheGrade()
    {
        using var f = new Fixture();
        LogAssert.Expect(LogType.Warning, "[ServeScreen] 'red_wine'의 완성 그림이 카탈로그에 없어 그림 없이 진행합니다.");
        Assert.That(f.screen.TryPlay(Drink("red_wine")), Is.True);
        Assert.That(f.still.enabled, Is.False);
        Assert.That(f.grade.gameObject.activeSelf, Is.True);
    }

    [Test]
    public void WithoutTheStillSetupTheOldCosmopolitanClipIsUsedWithAnError()
    {
        using var f = new Fixture(connectStill: false);
        LogAssert.Expect(LogType.Error, new Regex(@"^\[ServeScreen\] 'godfather' 전용 클립이 없고"));
        Assert.That(f.screen.TryPlay(Drink("godfather")), Is.True);
        Assert.That(f.still.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void PlaySceneHasServeFallbackDrink()
    {
        var previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene(ServeFallbackSetup.ScenePath, OpenSceneMode.Single);
            var screen = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<ServeScreen>(true)).Single();
            var still = (Image)Field(screen, "fallbackDrinkImage");
            Assert.That(still != null, Is.True, "Setup Serve Fallback Drink 메뉴 실행 후 Play 씬을 저장해 주세요.");
            Assert.That(still.name, Is.EqualTo(ServeFallbackSetup.ImageName));
            Assert.That(still.transform.parent, Is.SameAs(screen.transform.parent));
            Assert.That(still.transform.GetSiblingIndex(), Is.EqualTo(screen.transform.GetSiblingIndex() + 1), "drink 바로 뒤(위에 그림)");
            Assert.That(still.gameObject.activeSelf, Is.False);
            Assert.That(still.raycastTarget, Is.False);
            Assert.That(still.preserveAspect, Is.True);
            Assert.That(still.rectTransform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(still.rectTransform.sizeDelta, Is.EqualTo((Vector2)ServeFallbackSetup.Size));
            Assert.That(Field(screen, "animatedDrinkImage"), Is.SameAs(screen.GetComponent<Image>()));
            Assert.That((Object)Field(screen, "visuals") != null, Is.True);
        }
        finally
        {
            if (previous.Any(s => !string.IsNullOrEmpty(s.path))) EditorSceneManager.RestoreSceneManagerSetup(previous);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
