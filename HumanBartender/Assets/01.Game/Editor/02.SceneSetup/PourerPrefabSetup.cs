using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>따르기 원본 프리팹에 저장된 푸어러와 피드백을 한 번 연결한다.</summary>
public static class PourerPrefabSetup
{
    const string SpritePath = "Assets/01.Game/03.Content/01.ArtResources/03.Crafting/04.Minigames/03.pour/pourer.png";
    const string FontPath = "Assets/01.Game/03.Content/05.Fonts/Bold_PFStardust_3_0_SDF.asset";

    [MenuItem("Tools/Crafting/Connect Pourer Breakaway")]
    public static void Run()
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(SpritePath).OfType<Sprite>().FirstOrDefault();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (sprite == null || font == null)
        {
            Debug.LogWarning("[Pourer] pourer 스프라이트 또는 PFStardust 폰트가 없어 연결하지 않았습니다.");
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(PourHudPrefabSetup.PrefabPath);
        try
        {
            var manager = root.GetComponentInChildren<PourManager>(true);
            if (manager == null) return;
            var existing = Read<PourerBreakaway>(manager, "pourer");
            if (existing != null)
            {
                bool changed = ConnectAudio(root, existing);
                changed |= PlaceBottleInFrontOfLiquid(manager, existing);
                if (!changed) return;
                PrefabUtility.SaveAsPrefabAsset(root, PourHudPrefabSetup.PrefabPath);
                Debug.Log("[Pourer] 필요한 효과음 연결과 병·푸어러의 액체 앞 정렬을 저장했습니다.");
                return;
            }
            var bottle = Read<BottleTiltController>(manager, "bottle");
            var body = Read<SpriteRenderer>(manager, "ingredientBottle");
            var hud = manager.GimmickHud;
            if (bottle == null || body == null || hud == null)
            {
                Debug.LogWarning("[Pourer] 병 또는 전용 HUD 연결이 없어 프리팹을 변경하지 않았습니다.");
                return;
            }

            // 구조는 프리팹에 저장하고, 실행 중에는 위치·표시 상태·유량만 바꾼다.
            var attached = NewSprite("AttachedPourer", body.transform.parent, sprite, body);
            var managerData = new SerializedObject(manager);
            float mouthY = managerData.FindProperty("bottleInteriorHalfExtents").vector2Value.y;
            // 연결 후 실행 중과 같은 부착 계산을 사용한다. 피벗 기준은 스프라이트에 저장한다.
            attached.gameObject.SetActive(false);
            var detached = NewSprite("DetachedPourer", bottle.BottleVisual.parent, sprite, body);
            detached.gameObject.SetActive(false);

            GameObject panel = Read<GameObject>(hud, "root");
            var feedback = new GameObject("PourerPopRoot", typeof(RectTransform), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(feedback, "Create pourer feedback");
            feedback.layer = LayerMask.NameToLayer("UI");
            feedback.transform.SetParent(panel.transform, false);
            var rect = (RectTransform)feedback.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.65f, 0.75f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(120f, 44f);
            var group = feedback.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var label = new GameObject("PourerPopText", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(label, "Create pourer feedback text");
            label.layer = feedback.layer;
            label.transform.SetParent(feedback.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = font;
            text.fontSize = 22f;
            text.enableAutoSizing = false;
            text.richText = false;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color32(255, 223, 138, 255);
            text.text = "펑!";
            feedback.SetActive(false);

            var effect = Undo.AddComponent<PourerBreakaway>(root);
            Set(effect, "bottle", bottle);
            Set(effect, "ingredientBottle", body);
            Set(effect, "attachedPourer", attached);
            Set(effect, "detachedPourer", detached);
            effect.BeginPour("gin", Vector3.up * mouthY);
            effect.ResetState();
            Set(effect, "feedbackRoot", group);
            Set(effect, "feedbackText", text);
            ConnectAudio(root, effect);
            PlaceBottleInFrontOfLiquid(manager, effect);
            Set(manager, "pourer", effect);
            // 기존 140도 제한에서는 웹빌드의 160도 분리에 도달할 수 없다.
            var tilt = new SerializedObject(bottle);
            tilt.FindProperty("maxTiltAngle").floatValue = Mathf.Max(bottle.MaxTiltAngle, 170f);
            tilt.ApplyModifiedProperties();
            EditorUtility.SetDirty(effect);
            EditorSceneManager.MarkSceneDirty(root.scene);
            PrefabUtility.SaveAsPrefabAsset(root, PourHudPrefabSetup.PrefabPath);
            Debug.Log("[Pourer] 따르기 원본 프리팹에 푸어러 분리·피드백·유량 배율을 연결했습니다.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static bool PlaceBottleInFrontOfLiquid(PourManager manager, PourerBreakaway effect)
    {
        var liquid = Read<SphLiquidRenderer>(manager, "liquidRenderer");
        var body = Read<SpriteRenderer>(manager, "ingredientBottle");
        if (liquid == null || body == null) return false;
        int liquidOrder = new SerializedObject(liquid).FindProperty("sortingOrder").intValue;
        var attached = Read<SpriteRenderer>(effect, "attachedPourer");
        bool changed = RaiseOrder(attached, liquidOrder + 1);
        changed |= RaiseOrder(body, attached != null ? attached.sortingOrder + 1 : liquidOrder + 1);
        changed |= RaiseOrder(Read<SpriteRenderer>(effect, "detachedPourer"), body.sortingOrder + 1);
        var silhouette = Read<BottleSilhouette>(manager, "bottleSilhouette");
        var filter = silhouette != null ? Read<MeshFilter>(silhouette, "silhouetteFilter") : null;
        if (filter != null) changed |= RaiseOrder(filter.GetComponent<MeshRenderer>(), body.sortingOrder);
        if (changed) EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        return changed;
    }

    static bool RaiseOrder(Renderer renderer, int minimum)
    {
        if (renderer == null || renderer.sortingOrder >= minimum) return false;
        Undo.RecordObject(renderer, "Keep pour bottle in front of liquid");
        renderer.sortingOrder = minimum;
        EditorUtility.SetDirty(renderer);
        return true;
    }

    static bool ConnectAudio(GameObject root, PourerBreakaway effect)
    {
        if (Read<AudioSource>(effect, "detachSource") != null) return false;
        var source = Undo.AddComponent<AudioSource>(root);
        Undo.RecordObject(source, "Configure pourer breakaway audio");
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        var output = Resources.Load<AudioOutputSettings>("AudioOutputSettings");
        if (output != null) output.Route(source, AudioBus.SoundEffects);
        else Debug.LogWarning("[Pourer] AudioOutputSettings가 없어 효과음 믹서 연결을 비워 두었습니다.");
        Set(effect, "detachSource", source);
        EditorUtility.SetDirty(source);
        EditorSceneManager.MarkSceneDirty(root.scene);
        return true;
    }

    static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, SpriteRenderer reference)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        Undo.RegisterCreatedObjectUndo(go, "Create pourer sprite");
        go.transform.SetParent(parent, false);
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = reference.sharedMaterial;
        renderer.sortingLayerID = reference.sortingLayerID;
        renderer.sortingOrder = reference.sortingOrder + 1;
        return renderer;
    }

    static T Read<T>(Object target, string field) where T : Object =>
        new SerializedObject(target).FindProperty(field).objectReferenceValue as T;

    static void Set(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }
}
