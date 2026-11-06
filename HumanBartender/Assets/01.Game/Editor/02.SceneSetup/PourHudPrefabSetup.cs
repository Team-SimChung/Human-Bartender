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

    [MenuItem("Tools/Crafting/Save Pour HUD In Prefab")]
    public static void Run()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Attach(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
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
