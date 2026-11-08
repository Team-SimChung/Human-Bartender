using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>따르기에서 실제 사용하는 HUD를 프리팹 안에 한 번 저장한다.</summary>
public static class PourHudPrefabSetup
{
    public const string PrefabPath = GimmickUiPrefabSetup.Folder + "/03.Pour/NewPourGame.prefab";
    const string PlayPath = "Assets/01.Game/01.Scenes/Play.unity";

    [MenuItem("Tools/Crafting/Save Pour HUD In Prefab %&b")]
    public static void Run()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Attach(root);
            MoveBackgroundBehindGimmick(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    internal static void MoveBackgroundBehindGimmick(GameObject root)
    {
        var image = root.GetComponentsInChildren<Image>(true).SingleOrDefault(value =>
            value.enabled && string.Equals(value.name, "BackGround", StringComparison.OrdinalIgnoreCase) &&
            value.GetComponent<MinigameBackground>() == null);
        if (image == null) return;
        if (image.sprite == null)
        {
            Debug.LogWarning("[PourHUD] BackGround의 Sprite가 없어 배경 정렬을 변경하지 않았습니다.");
            return;
        }
        var manager = root.GetComponentInChildren<PourManager>(true);
        if (manager == null) throw new InvalidOperationException("[PourHUD] PourManager가 없습니다.");

        var corners = new Vector3[4];
        image.rectTransform.GetWorldCorners(corners);
        float width = Vector3.Distance(corners[0], corners[3]);
        float height = Vector3.Distance(corners[0], corners[1]);
        var sprite = image.sprite;
        var color = image.color;
        var background = image.gameObject;
        Undo.SetTransformParent(background.transform, root.transform, "Move pour background outside HUD");
        Undo.RecordObject(background.transform, "Keep pour background size");
        background.transform.position = manager.transform.position;
        background.transform.localRotation = Quaternion.identity;
        var parentScale = root.transform.lossyScale;
        background.transform.localScale = new Vector3(
            width / Mathf.Max(.0001f, sprite.bounds.size.x * Mathf.Abs(parentScale.x)),
            height / Mathf.Max(.0001f, sprite.bounds.size.y * Mathf.Abs(parentScale.y)), 1);

        var renderer = background.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(background);
        Undo.RecordObject(renderer, "Set pour background sorting");
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerID = 0;
        renderer.sortingOrder = -10;
        // 이미 쓰고 있는 셰이크 배경의 URP 머티리얼을 공유한다. 새 에셋은 만들지 않는다.
        var shake = AssetDatabase.LoadAssetAtPath<GameObject>(GimmickUiPrefabSetup.Folder + "/01.Shake/NewShakingMinigame.prefab");
        var source = shake != null ? shake.GetComponentInChildren<MinigameBackground>(true) : null;
        if (source != null) renderer.sharedMaterial = source.GetComponent<SpriteRenderer>().sharedMaterial;
        if (background.GetComponent<MinigameBackground>() == null) Undo.AddComponent<MinigameBackground>(background);

        // 원본 Image 설정은 보존하고 표시·입력만 끈다. 실행 시 HUD가 Overlay로 바뀌어도 배경은 월드에 남는다.
        Undo.RecordObject(image, "Disable pour background overlay");
        image.raycastTarget = false;
        image.enabled = false;
        EditorUtility.SetDirty(background);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(image);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[PourHUD] BackGround를 HUD 밖으로 옮기고 월드 배경(-10)과 카메라 크기 맞춤을 연결했습니다.");
    }

    internal static void Attach(GameObject root)
    {
        var manager = root.GetComponentInChildren<PourManager>(true);
        if (manager.GimmickHud != null) return;

        var scene = SceneManager.GetSceneByPath(PlayPath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(PlayPath, OpenSceneMode.Additive);
        try
        {
            var source = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<CraftGimmickHud>(true)).Single();
            var copy = Object.Instantiate(source.gameObject, root.transform, false);
            copy.name = "Pour HUD";
            copy.SetActive(true);
            var hud = copy.GetComponent<CraftGimmickHud>();
            var canvas = copy.GetComponent<Canvas>();

            // 프리팹 모드에서 월드의 병·잔과 UI를 같은 화면 크기로 보며 편집한다.
            // BeginCraft가 표시 방식을 화면 공간으로 바꾸며, Panel 이하 배치는 그대로 사용한다.
            float halfHeight = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Camera>(true))
                .First(camera => camera.CompareTag("MainCamera")).orthographicSize;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = null;
            canvas.sortingOrder = 20;
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(1280f, 720f);
            // 기존 프리팹의 최상위 좌표와 Pour Root가 서로 상쇄되어 있으므로 실제 조작 무대에 맞춘다.
            rect.localPosition = root.transform.InverseTransformPoint(manager.transform.position);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * (halfHeight * 2f / 720f);

            var data = new SerializedObject(hud);
            data.FindProperty("useScreenSpaceDuringCraft").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (string name in new[] { "root", "quantityRoot", "ingredientRoot", "tiltRoot", "timeRoot" })
                Read<GameObject>(hud, name).SetActive(true);
            Read<TMP_Text>(hud, "progressText").gameObject.SetActive(false);
            Read<GameObject>(hud, "okMark").SetActive(false);
            Read<Button>(hud, "nextButton").gameObject.SetActive(true);
            Read<TMP_Text>(hud, "targetValueText").text = "360 ml";
            Read<TMP_Text>(hud, "currentValueText").text = "0.00 ml";
            Read<TMP_Text>(hud, "subjectText").text = "병맥주";
            Read<TMP_Text>(hud, "timeText").text = "00:00";
            GimmickUiPrefabSetup.Set(manager, "gimmickHud", hud);

            foreach (var oldCanvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (oldCanvas == canvas) continue;
                oldCanvas.gameObject.SetActive(false);
                if (!oldCanvas.name.StartsWith("Legacy ")) oldCanvas.name = "Legacy " + oldCanvas.name;
            }
            Debug.Log("[PourHUD] 새 HUD를 NewPourGame/Pour HUD/Panel에 저장했습니다.");
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    static T Read<T>(Object target, string field) where T : Object =>
        (T)GimmickUiPrefabSetup.Read(target, field);
}
