using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

public sealed class DialoguePresentationRegressionTests
{
    const string Art = "Assets/01.Game/03.Content/01.ArtResources/01.Characters/01.Bar/";
    const string DialoguePrefab = "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/Dialogue TextBox.prefab";
    const string BarkPrefab = "Assets/01.Game/03.Content/08.Prefabs/03.UI/03.Outside/OutsideDialogueTextBox.prefab";
    static CsvContentCatalog Catalog() => CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));

    [TestCase(DialoguePrefab)]
    [TestCase(BarkPrefab)]
    public void TextUsesPositiveInsetsAndKeepsKoreanWordsTogether(string path)
    {
        var canvas = new GameObject("dialogue layout test", typeof(RectTransform), typeof(Canvas));
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), canvas.transform);
        try
        {
            var bubble = instance.GetComponent<DynamicSpeechBubble>();
            bubble.sizeMode = DynamicSpeechBubble.BubbleSizeMode.PreExpand;
            bubble.maxSize.x = Mathf.Min(bubble.maxSize.x, 260f);
            bubble.SetText("오늘은 좋은 술을 마시고 싶어요. 편안한 밤이네요.");
            var rect = bubble.textLabel.rectTransform;
            Assert.That(rect.offsetMin.x, Is.GreaterThanOrEqualTo(10));
            Assert.That(rect.offsetMin.y, Is.GreaterThanOrEqualTo(10));
            Assert.That(-rect.offsetMax.x, Is.GreaterThanOrEqualTo(10));
            Assert.That(-rect.offsetMax.y, Is.GreaterThanOrEqualTo(10));
            Assert.That(bubble.textLabel.textInfo.lineCount, Is.GreaterThan(1));
            foreach (string word in new[] { "오늘은", "좋은", "술을", "마시고", "싶어요.", "편안한", "밤이네요." })
            {
                int start = bubble.textLabel.text.IndexOf(word, StringComparison.Ordinal);
                var chars = bubble.textLabel.textInfo.characterInfo;
                Assert.That(chars[start].lineNumber, Is.EqualTo(chars[start + word.Length - 1].lineNumber), word);
            }
            instance.SetActive(false);
            instance.SetActive(true);
            bubble.SetText("첫 줄입니다.\n직접 넣은 줄바꿈도 유지합니다.");
            Assert.That(rect.offsetMin.y, Is.GreaterThan(0), "reactivation must not replace the bottom inset");
            Assert.That(bubble.textLabel.textInfo.lineCount, Is.GreaterThanOrEqualTo(2));
        }
        finally { UnityEngine.Object.DestroyImmediate(canvas); }
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public void ActualSpeakerPlaybackKeepsSmallerSkyBlueNamesAwayFromFrameAndBody(bool playerLine, bool multiline)
    {
        var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Play.unity", OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var view = roots.SelectMany(r => r.GetComponentsInChildren<UIDialogueTextView>(true)).Single();
            view.lunaSpeechBubble.gameObject.SetActive(false); view.customerSpeechBubble.gameObject.SetActive(false);
            var bubble = playerLine ? view.lunaSpeechBubble : view.customerSpeechBubble;
            string text = multiline ? "다른 생각을 하고 있었나 보군. 내가 설명한 건 기억하지?\n천천히 생각해도 좋으니 한 번 더 확인해 보게." : "다른 생각을 하고 있었나 보군. 내가 설명한 건 기억하지?";
            var data = new TypingData(text, playerLine ? "루나" : "크리스", Vector3.zero, new Color32(139, 94, 47, 255), playerLine);
            bubble.TextPlayer.PlayAsync(data, null, 0, wait: (_, __) => UniTask.CompletedTask).GetAwaiter().GetResult();
            bubble.GetComponent<DialogueInputPrompt>().SetWaiting(true);
            var name = bubble.nameLabel; name.ForceMeshUpdate(true, true); bubble.textLabel.ForceMeshUpdate(true, true);
            Assert.That((Color32)name.color, Is.EqualTo(new Color32(143, 220, 255, 255)), "authored brown must not overwrite the common sky-blue presentation");
            Assert.That(name.enableAutoSizing, Is.False); Assert.That(name.fontSize, Is.EqualTo(16));
            var namePoints = GlyphPoints(name, bubble.bubble).ToArray(); var bodyPoints = GlyphPoints(bubble.textLabel, bubble.bubble).ToArray();
            Assert.That(bubble.bubble.rect.yMax - namePoints.Max(p => p.y), Is.GreaterThanOrEqualTo(20), "actual glyphs need air below the top frame edge");
            Assert.That(namePoints.Min(p => p.y) - bodyPoints.Max(p => p.y), Is.GreaterThanOrEqualTo(12), "name and body must be separate rows");
            Assert.That(bodyPoints.Min(p => p.y) - bubble.bubble.rect.yMin, Is.GreaterThanOrEqualTo(20));
            var frame = bubble.transform.Find("Frame").GetComponent<UnityEngine.UI.Image>();
            Assert.That(frame.raycastTarget, Is.False); Assert.That(bubble.GetComponent<UnityEngine.UI.Image>().color.a, Is.Zero);
            Assert.That(frame.GetComponent<DialogueFrameMesh>(), Is.Not.Null, "corner and tail artwork needs independent scaling");
            Assert.That(frame.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            foreach (var guest in roots.SelectMany(r => r.GetComponentsInChildren<GuestSlot>(true))) guest.gameObject.SetActive(false);
            var manager = roots.SelectMany(r => r.GetComponentsInChildren<DialogueCharacterManager>(true)).Single();
            var rigs = Field<SlotCharacterPart[]>(manager, "slotParts");
            foreach (var rig in rigs)
            {
                rig.slot.transform.parent.gameObject.SetActive(true);
                rig.slot.SetActive(rig.type == ESlotType.Right);
                if (rig.type != ESlotType.Right) continue;
                rig.ApplyPortrait(null);
                foreach (var part in rig.parts)
                {
                    string key = part.partName switch { EAnimationPart.Eyes => "eyes", EAnimationPart.Eyeblows => "eyebrows", EAnimationPart.Upper_Face => "upper_face", EAnimationPart.Lower_Face => "lower_face", EAnimationPart.Body => "body", _ => null };
                    if (key == null) { part.SetInactive(); continue; }
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/01.Game/03.Content/07.Animations/01.Characters/02.Chris/01.Clips/01.Idle/chris_" + key + "_default_Loop.anim");
                    part.ApplySprite(null); clip.SampleAnimation(part.Renderer.gameObject, 0); part.Renderer.color = Color.white;
                }
            }
            foreach (var tray in roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Where(c => c.name == "Coaster Tray Canvas"))
                tray.gameObject.SetActive(false);
            SavePreview(scene, "spacing-" + (playerLine ? "player" : multiline ? "multiline" : "chris"), new Vector3(rigs.Average(r => r.slot.transform.position.x), 0, 0));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    static IEnumerable<Vector3> GlyphPoints(TMP_Text label, RectTransform root)
    {
        foreach (var character in label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(c => c.isVisible))
        {
            yield return root.InverseTransformPoint(label.transform.TransformPoint(character.topRight));
            yield return root.InverseTransformPoint(label.transform.TransformPoint(character.bottomLeft));
        }
    }

    [TestCase("08.Shiba/Shiba_Idle.png")]
    [TestCase("07.Tom/Idle/Tom_Idle.png")]
    public void PortraitBottomStaysOnTheSeatInsteadOfBelowTheCounter(string relativePath)
    {
        var root = new GameObject("seat");
        var child = new GameObject("portrait", typeof(SpriteRenderer));
        child.transform.SetParent(root.transform);
        try
        {
            root.transform.position = new Vector3(2, -1.37f, 0);
            var renderer = child.GetComponent<SpriteRenderer>();
            var rig = new SlotCharacterPart { portaitSpriteRenderer = renderer };
            rig.ApplyPortrait(AssetDatabase.LoadAllAssetsAtPath(Art + relativePath).OfType<Sprite>().Single());
            Assert.That(renderer.bounds.min.y, Is.EqualTo(root.transform.position.y).Within(.001f));
            Assert.That(renderer.bounds.center.x, Is.EqualTo(root.transform.position.x).Within(.001f));
            Assert.That(renderer.bounds.max.y, Is.GreaterThan(root.transform.position.y + 1.4f));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void EveryGuestPartAndBothTomExpressionsResolveToRegisteredSprites()
    {
        var catalog = Catalog();
        var data = catalog.Read<NewGuestBodyDataBase>("guest_bodies");
        foreach (var slot in NewGuestBodyDataBase.AllSlots)
            foreach (var part in data.GetParts(slot) ?? Array.Empty<NewGuestBodyPartData>())
                Assert.That(SpriteAtAddress(part.Sprite), Is.Not.Null, part.Id);
        var expressions = catalog.Read<Dictionary<string, Dictionary<string, NewExpressionEntry>>>("expressions");
        foreach (var expression in new[] { "default", "serious" })
            Assert.That(SpriteAtAddress(expressions["tom"][expression].Sprite), Is.Not.Null, "tom:" + expression);
        foreach (var exclusion in data.Exclusions)
        {
            Assert.That(data.TryGetPart(exclusion.A, out _), Is.True, exclusion.A);
            Assert.That(data.TryGetPart(exclusion.B, out _), Is.True, exclusion.B);
        }
        var builder = new GuestAppearanceBuilder(data, 1);
        foreach (string gender in new[] { "m", "f" })
        {
            var first = builder.Pick(gender, "", null);
            var second = builder.Pick(gender, "", new[] { first });
            Assert.That(first.SameAs(second), Is.False, "seated guests must not repeat the same appearance");
        }
    }

    [TestCase("m")]
    [TestCase("f")]
    public void CroppedPartsStayTogetherAndBarksExpandBelowTheFace(string gender)
    {
        var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Play.unity", OpenSceneMode.Additive);
        var sprites = new GuestBodySprites();
        try
        {
            var roots = scene.GetRootGameObjects();
            var manager = roots.SelectMany(r => r.GetComponentsInChildren<GuestManager>(true)).Single();
            Assert.That(Field<bool>(manager, "useTempAppearance"), Is.False, "production scene must use the appearance table");
            var data = Catalog().Read<NewGuestBodyDataBase>("guest_bodies");
            var appearance = new GuestAppearanceBuilder(data, 1).BuildDefault(gender);
            foreach (var pair in appearance.Parts)
                sprites.Set(pair.Key, Addressables.ResourceManager.CreateCompletedOperation(SpriteAtAddress(pair.Value.Sprite), null));
            foreach (var slot in roots.SelectMany(r => r.GetComponentsInChildren<GuestSlot>(true)))
            {
                slot.transform.parent.gameObject.SetActive(true);
                typeof(GuestSlot).GetMethod("ApplySprites", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(slot, new object[] { sprites });
                var parts = Field<GuestBodySlotRenderer[]>(slot, "partRenderers");
                var body = parts.Single(p => p.slot == EGuestBodySlot.Body).renderer;
                var eyes = parts.Single(p => p.slot == EGuestBodySlot.Eyes).renderer;
                var mouth = parts.Single(p => p.slot == EGuestBodySlot.Mouth).renderer;
                Assert.That(body.gameObject.activeInHierarchy, Is.True);
                Assert.That(body.bounds.min.y, Is.EqualTo(slot.transform.position.y).Within(.001f));
                Assert.That(eyes.bounds.center.y, Is.GreaterThan(mouth.bounds.center.y));
                Assert.That(eyes.bounds.center.y, Is.GreaterThan(body.bounds.center.y));
                Assert.That(Mathf.Abs(eyes.bounds.center.x - body.bounds.center.x), Is.LessThan(.5f));
                var bubble = Field<DynamicSpeechBubble>(slot, "speechBubble");
                Field<GameObject>(slot, "bubbleRoot").SetActive(true);
                bubble.SetText("안녕하세요. 오늘은 시원한 술 한 잔 부탁해요.");
                Assert.That(bubble.bubble.pivot.y, Is.EqualTo(1), "bark must expand downwards");
                var corners = new Vector3[4];
                bubble.bubble.GetWorldCorners(corners);
                Assert.That(corners.Max(c => c.y), Is.LessThan(mouth.bounds.min.y - .2f), "bark must not cover the face");
            }
            SavePreview(scene, "guests-" + gender, Vector3.zero);
        }
        finally
        {
            sprites.Release();
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void DayOnePortraitsUseTheRealStorySeats()
    {
        var scene = EditorSceneManager.OpenScene("Assets/01.Game/01.Scenes/Play.unity", OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var manager = roots.SelectMany(r => r.GetComponentsInChildren<DialogueCharacterManager>(true)).Single();
            var rigs = Field<SlotCharacterPart[]>(manager, "slotParts");
            foreach (var root in roots)
                foreach (var guest in root.GetComponentsInChildren<GuestSlot>(true)) guest.gameObject.SetActive(false);
            foreach (var rig in rigs)
            {
                rig.slot.transform.parent.gameObject.SetActive(true);
                var sprite = SpriteAtAddress(rig.type == ESlotType.Left ? "shiba_default" : "tom_default[Tom_Idle_0]");
                rig.ApplyPortrait(sprite);
                Assert.That(rig.portaitSpriteRenderer.bounds.min.y,
                    Is.EqualTo(rig.slot.transform.position.y).Within(.001f));
            }
            var view = roots.SelectMany(r => r.GetComponentsInChildren<UIDialogueTextView>(true)).Single();
            view.lunaSpeechBubble.gameObject.SetActive(false);
            view.customerSpeechBubble.SetText("안녕하세요. 오늘도 편안하게 한 잔 하고 갈게요.\n이렇게 줄을 바꾸어도 여백은 유지됩니다.");
            SavePreview(scene, "day1-portraits", new Vector3(rigs.Average(r => r.slot.transform.position.x), 0, 0));
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    static void SavePreview(UnityEngine.SceneManagement.Scene scene, string name, Vector3 center)
    {
        string directory = Environment.GetEnvironmentVariable("HUMAN_BARTENDER_PREVIEW_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        var roots = scene.GetRootGameObjects();
        var sceneCamera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
        var cameraObject = new GameObject("Dialogue review camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.CopyFrom(sceneCamera);
        camera.enabled = false;
        camera.transform.SetPositionAndRotation(new Vector3(center.x, center.y, sceneCamera.transform.position.z), sceneCamera.transform.rotation);
        camera.rect = new Rect(0, 0, 1, 1);
        camera.aspect = 16f / 9f;
        camera.orthographicSize = 2.7f;
        var canvases = roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true))
            .Where(c => c.isActiveAndEnabled && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var previous = canvases.Select(c => (c.worldCamera, c.planeDistance)).ToArray();
        var render = new RenderTexture(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var active = RenderTexture.active;
        try
        {
            camera.targetTexture = render;
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + .1f;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = active;
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceOverlay;
                canvases[i].worldCamera = previous[i].worldCamera;
                canvases[i].planeDistance = previous[i].planeDistance;
            }
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(render);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    [UnityTest]
    [Explicit("Run alone: loads the shipped portraits and guest table through the real Play Mode Addressables pipeline.")]
    public IEnumerator PortraitsAndGuestPartsActuallyLoadInPlayMode()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        yield return LoadInPlayMode();
        yield return new ExitPlayMode();
    }

    static IEnumerator LoadInPlayMode()
    {
        var data = Catalog().Read<NewGuestBodyDataBase>("guest_bodies");
        var keys = NewGuestBodyDataBase.AllSlots.SelectMany(s => data.GetParts(s) ?? Array.Empty<NewGuestBodyPartData>())
            .Select(p => p.Sprite).Concat(new[] { "shiba_default", "tom_default[Tom_Idle_0]" }).ToArray();
        foreach (string key in keys)
        {
            UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<Sprite>? handle = null;
            yield return ResourceLoader.TryLoadAsync<Sprite>(key, default).ToCoroutine(value => handle = value);
            Assert.That(handle.HasValue, Is.True, "actual Addressables load: " + key);
            Assert.That(handle.Value.Result, Is.Not.Null, key);
            ResourceLoader.ReleaseHandle(ref handle);
        }
    }

    static T Field<T>(object owner, string name) => (T)owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    static Sprite SpriteAtAddress(string key)
    {
        int bracket = key.IndexOf('[');
        string address = bracket < 0 ? key : key.Substring(0, bracket);
        string name = bracket < 0 ? null : key.Substring(bracket + 1, key.Length - bracket - 2);
        var entries = AddressableAssetSettingsDefaultObject.Settings.groups.Where(g => g != null)
            .SelectMany(g => g.entries).Where(e => e.address == address).ToArray();
        Assert.That(entries.Length, Is.EqualTo(1), "unique address: " + address);
        return AssetDatabase.LoadAllAssetsAtPath(entries[0].AssetPath).OfType<Sprite>()
            .Single(s => name == null || s.name == name);
    }
}
