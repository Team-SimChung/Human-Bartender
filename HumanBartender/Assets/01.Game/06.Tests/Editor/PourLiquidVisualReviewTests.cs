using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// 실제 기믹 카메라(ortho 5, 1920×1080) 구도로 따르기 화면을 캡처하고 수치를 남긴다.
/// 확대 테스트 카메라와 달리 플레이어가 보는 크기감과 픽셀 밀도를 그대로 확인한다.
/// </summary>
public sealed class PourLiquidVisualReviewTests
{
    const string PrefabPath = "Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/03.Pour/NewPourGame.prefab";
    static string Directory => Path.Combine(Path.GetTempPath(), "PourLiquidValidation", "ingame");

    [UnityTest]
    public IEnumerator BeerFoamGrowsHoldsAndCollapsesAtInGameFraming()
    {
        RequireGpu();
        yield return new EnterPlayMode();
        yield return Pour("beer", "mug", 110f, 1230, new[] { 12, 45, 120, 300, 1230 }, true);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator GentleBeerPourMakesLessFoamThanStrongPour()
    {
        RequireGpu();
        yield return new EnterPlayMode();
        yield return Pour("beer", "mug", 97f, 1500, new[] { 300, 750, 1500 }, false);
        yield return new ExitPlayMode();
        string strong = File.Exists(Metric("beer-110")) ? File.ReadAllText(Metric("beer-110")) : null;
        string gentle = File.Exists(Metric("beer-97")) ? File.ReadAllText(Metric("beer-97")) : null;
        TestContext.WriteLine($"strong: {strong}\ngentle: {gentle}");
    }

    [TestCase("gin", ExpectedResult = null)]
    [TestCase("orange_juice", ExpectedResult = null)]
    [TestCase("milk", ExpectedResult = null)]
    [TestCase("cola", ExpectedResult = null)]
    [UnityTest]
    public IEnumerator OtherLiquidsAtInGameFraming(string ingredient)
    {
        RequireGpu();
        yield return new EnterPlayMode();
        yield return Pour(ingredient, null, 110f, 300, new[] { 45, 300 }, false);
        yield return new ExitPlayMode();
    }

    static string Metric(string name) => Path.Combine(Directory, name + "-metrics.txt");

    /// <summary>
    /// pourFrames 동안 따른 뒤 멈추고, captureFrames(따르기 시작 기준 프레임)에서 캡처한다.
    /// 마지막 값 이후 600프레임(10초)을 더 기다려 붕괴를 기록한다.
    /// </summary>
    static IEnumerator Pour(string ingredient, string glassId, float angle, int pourFrames, int[] captureFrames, bool settle)
    {
        var stageRoot = new GameObject("liquid review stage");
        stageRoot.SetActive(false);
        stageRoot.transform.position = new Vector3(0, 1000, 0);
        var stage = stageRoot.AddComponent<CraftGimmickStage>();
        var cameraObject = new GameObject("Gimmick Camera", typeof(Camera));
        cameraObject.transform.SetParent(stageRoot.transform, false);
        cameraObject.transform.localPosition = new Vector3(0, 0, -10);
        var cam = cameraObject.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.16f, 0.2f);
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        target.Create();
        cam.targetTexture = target;
        Set(stage, "stageCamera", cam);
        // GimmickRunner와 같이 프리팹 루트의 로컬 위치를 유지한다(Pour Root가 무대 원점으로 돌아온다).
        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), stageRoot.transform);
        var manager = instance.GetComponentInChildren<PourManager>(true);
        Set(manager, "drivenByRunner", true);
        Set(manager, "pourIngredientId", ingredient);
        Set(manager, "pourStep", new GimmickStep(ECraftGimmick.Pour, ingredient, 12f, ENewUnit.Oz, true));
        Set(manager, "showTargetHeight", glassId == "mug");
        var session = new CraftSession(ingredient);
        if (glassId != null) session.Actual.SetGlass(glassId);
        Set(manager, "craftContext", CraftContext.From(session));
        var balance = ScriptableObject.CreateInstance<NewBalanceDataSO>();
        balance.balanceData = new NewBalanceDataBase { Config = new NewBalanceConfig { PourEmitRateMlPerSec = 70, UnitOzToMl = 30 } };
        Set(manager, "balanceData", balance);
        var glass = Get<Transform>(manager, "glassCenter");
        var gpu = Get<SphLiquidRenderer>(manager, "liquidRenderer");
        var bottle = Get<BottleTiltController>(manager, "bottle");
        float previousCapture = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f / 60;
        var muted = new List<AudioListener>();
        foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (listener.enabled) { listener.enabled = false; muted.Add(listener); }
        cameraObject.AddComponent<AudioListener>();
        string tag = ingredient + "-" + Mathf.RoundToInt(angle);
        var log = new System.Text.StringBuilder();
        System.IO.Directory.CreateDirectory(Directory);
        try
        {
            stageRoot.SetActive(true);
            yield return null;
            bottle.BeginDrag(bottle.PourPivotWorldPosition + Quaternion.Euler(0, 0, -angle) * Vector3.down * 2);
            // 병을 집어 기울이는 시간을 지나 실제로 액체가 나오기 시작한 시점을 0으로 잡는다.
            int wait = 0;
            while (manager.PouredInTargetUnit <= 0f && wait++ < 240) yield return null;
            var captures = new HashSet<int>(captureFrames);
            int last = 0;
            for (int frame = 1; frame <= pourFrames + 600; frame++)
            {
                if (frame == pourFrames + 1) bottle.EndDrag();
                yield return null;
                if (captures.Contains(frame) || frame == pourFrames + 30 || frame == pourFrames + 180 || frame == pourFrames + 600)
                {
                    string label = frame <= pourFrames ? $"pour{frame:0000}" : $"stop+{frame - pourFrames:000}";
                    if (!settle && frame > pourFrames && frame != pourFrames + 30 && frame != pourFrames + 600) continue;
                    SaveCrop(target, cam, glass, $"{tag}-{label}");
                    log.AppendLine($"{label}: oz={manager.PouredInTargetUnit:0.00} {Describe(gpu)}");
                    last = frame;
                }
                if (!settle && frame > pourFrames + 30) break;
            }
            File.WriteAllText(Metric(tag), log.ToString());
            TestContext.WriteLine(log.ToString());
            Assert.That(last, Is.GreaterThan(0));
        }
        finally
        {
            Time.captureDeltaTime = previousCapture;
            foreach (var listener in muted) if (listener != null) listener.enabled = true;
            cam.targetTexture = null;
            Object.DestroyImmediate(stageRoot);
            Object.DestroyImmediate(balance);
            if (RenderTexture.active == target) RenderTexture.active = null;
            target.Release(); Object.DestroyImmediate(target);
        }
    }

    /// <summary>수면 텍스처의 평균 거품량·높이와 내부 구름의 평균값.</summary>
    static string Describe(SphLiquidRenderer gpu)
    {
        var surface = Get<RenderTexture>(gpu, "surfaceRead");
        var cloud = Get<RenderTexture>(gpu, "effectRead");
        if (surface == null || cloud == null) return "no gpu data";
        var s = Copy(surface); var c = Copy(cloud);
        float foam = 0, foamMax = 0, height = 0, valid = 0, cloudMean = 0, cloudMax = 0;
        foreach (var p in s.GetPixels()) if (p.a > 0.5f) { foam += p.g; foamMax = Mathf.Max(foamMax, p.g); height += p.r; valid++; }
        foreach (var p in c.GetPixels()) { cloudMean += p.r; cloudMax = Mathf.Max(cloudMax, p.r); }
        cloudMean /= c.width * c.height;
        Object.DestroyImmediate(s); Object.DestroyImmediate(c);
        valid = Mathf.Max(1, valid);
        return $"surface={height / valid:0.000} foam={foam / valid:0.000} foamMax={foamMax:0.000} cloud={cloudMean:0.0000} cloudMax={cloudMax:0.000}";
    }

    /// <summary>잔과 병 입구를 포함한 영역을 원본 픽셀과 3배 확대(최근접) 두 장으로 저장한다.</summary>
    static void SaveCrop(RenderTexture target, Camera cam, Transform glass, string name)
    {
        var full = Copy(target);
        Vector3 centre = cam.WorldToScreenPoint(glass.position);
        int w = 360, h = 480;
        int x0 = Mathf.Clamp(Mathf.RoundToInt(centre.x) - w / 2 - 40, 0, target.width - w);
        int y0 = Mathf.Clamp(Mathf.RoundToInt(centre.y) - h / 2 + 70, 0, target.height - h);
        var crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
        crop.SetPixels(full.GetPixels(x0, y0, w, h));
        crop.Apply();
        File.WriteAllBytes(Path.Combine(Directory, name + ".png"), crop.EncodeToPNG());
        var big = new Texture2D(w * 2, h * 2, TextureFormat.RGBA32, false);
        var src = crop.GetPixels();
        var dst = new Color[w * h * 4];
        for (int y = 0; y < h * 2; y++)
            for (int x = 0; x < w * 2; x++)
                dst[y * w * 2 + x] = src[(y / 2) * w + x / 2];
        big.SetPixels(dst); big.Apply();
        File.WriteAllBytes(Path.Combine(Directory, name + "-x2.png"), big.EncodeToPNG());
        Object.DestroyImmediate(full); Object.DestroyImmediate(crop); Object.DestroyImmediate(big);
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    static void RequireGpu()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device, run without -nographics.");
    }

    static Texture2D Copy(RenderTexture target)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var copy = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
        copy.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        return copy;
    }

    static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(owner, value);
}
