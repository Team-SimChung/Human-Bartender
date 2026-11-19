using System.Linq;
using System.Text.RegularExpressions;
using HumanBartender.CutsceneStudio;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>첫 테라스 뒤 유나 회상과 마지막 유나 회상이 호출 형식과 관계없이 각자의 원고를 순서대로 재생하는지 확인한다.</summary>
public sealed class TerraceMemoryLookupTests
{
    const string CatalogPath = "Assets/01.Game/03.Content/02.Cutscenes/Studio/CutsceneStudioCatalog.asset";

    static string Plain(string text) => Regex.Replace(text ?? "", "<[^>]+>", "").Trim();

    static StudioSequence LoadSequence(string resourceKey)
    {
        var entry = AddressableAssetSettingsDefaultObject.Settings.groups.Where(group => group != null)
            .SelectMany(group => group.entries).SingleOrDefault(value => value.address == resourceKey);
        Assert.That(entry, Is.Not.Null, "Addressables에 등록되지 않은 컷씬 주소: " + resourceKey);
        var sequence = AssetDatabase.LoadAssetAtPath<StudioSequence>(entry.AssetPath);
        Assert.That(sequence, Is.Not.Null, resourceKey);
        return sequence;
    }

    // 실제 재생(WorldInteractionController.PlayStudioAsync)과 같은 순서로 찾는다: 카탈로그 → CSV 기억 대본.
    static string[] ResolveLines(StudioCatalog catalog, CsvStoryTestContent content, string id, string memoryKey)
    {
        var entry = catalog.Find(id, memoryKey);
        if (entry != null)
            return StudioEvaluation.Clips(LoadSequence(entry.ResourceKey))
                .Where(clip => clip.Asset.Kind == StudioKind.Dialogue).Select(clip => Plain(clip.Asset.Text)).ToArray();
        Assert.That(CsvStoryCutsceneProjection.TryGetSource(id, memoryKey, out var source), Is.True,
            "카탈로그와 CSV 대체 경로 어디에도 없는 회상 호출은 재생 중 예외가 납니다: " + id + " / " + memoryKey);
        return content.GetCutsceneSteps(source).Select(step => Plain(step.Text?.Ko)).ToArray();
    }

    // 엔진 CSV의 기존 호출(terrace-memory)과 웹 최신 CSV의 호출(tl_yuna_memory_d0 / d3 + terraceMemory3)을 모두 확인한다.
    [TestCase("terrace-memory", null, "terraceMemory")]
    [TestCase("terrace-memory", "terraceMemory3", "terraceMemory3")]
    [TestCase("tl_yuna_memory_d0", null, "terraceMemory")]
    [TestCase("tl_yuna_memory_d3", "terraceMemory3", "terraceMemory3")]
    public void EachMemoryCallPlaysItsOwnManuscriptInOrder(string id, string memoryKey, string manuscript)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<StudioCatalog>(CatalogPath);
        var content = new CsvStoryTestContent();
        var expected = content.GetCutsceneSteps(manuscript).Select(step => Plain(step.Text?.Ko)).ToArray();
        Assert.That(expected, Is.Not.Empty);
        CollectionAssert.AreEqual(expected, ResolveLines(catalog, content, id, memoryKey));
    }

    [Test]
    public void FirstAndLastMemoriesResolveToDifferentPlayback()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<StudioCatalog>(CatalogPath);
        var content = new CsvStoryTestContent();
        CollectionAssert.AreNotEqual(ResolveLines(catalog, content, "terrace-memory", null),
            ResolveLines(catalog, content, "terrace-memory", "terraceMemory3"));
        CollectionAssert.AreNotEqual(ResolveLines(catalog, content, "tl_yuna_memory_d0", null),
            ResolveLines(catalog, content, "tl_yuna_memory_d3", "terraceMemory3"));
        Assert.That(catalog.Find("tl_yuna_memory_d0")?.ResourceKey, Is.EqualTo("cutscene-studio/tl_yuna_memory_d0"));
        Assert.That(catalog.Find("tl_yuna_memory_d3", "terraceMemory3")?.ResourceKey, Is.EqualTo("cutscene-studio/tl_yuna_memory_d3"));
    }

    // 첫 회상만 연결된 상태에서 마지막 회상 호출(memoryKey)이 그 연결을 빌려 쓰지 않아야 한다.
    [Test]
    public void KeyedMemoryNeverFallsBackToTheUnkeyedEntry()
    {
        var catalog = ScriptableObject.CreateInstance<StudioCatalog>();
        try
        {
            catalog.Sequences.Add(new StudioCatalogEntry { Id = "terrace-memory", ResourceKey = "first" });
            Assert.That(catalog.Find("terrace-memory")?.ResourceKey, Is.EqualTo("first"));
            Assert.That(catalog.Find("terrace-memory", "terraceMemory3"), Is.Null);
            catalog.Sequences.Add(new StudioCatalogEntry { Id = "terrace-memory", MemoryKey = "terraceMemory3", ResourceKey = "last" });
            Assert.That(catalog.Find("terrace-memory", "terraceMemory3")?.ResourceKey, Is.EqualTo("last"));
            Assert.That(catalog.Find("terrace-memory")?.ResourceKey, Is.EqualTo("first"));
        }
        finally { Object.DestroyImmediate(catalog); }
    }

    // 현재 CSV의 실제 호출을 그대로 따라간다. 웹 CSV가 반입되어 호출 이름이 바뀌어도 같은 기준으로 확인한다.
    [TestCase("night0", "terraceMemory")]
    [TestCase("ending", "terraceMemory3")]
    public void HomeNightCallPlaysTheMatchingMemory(string scene, string manuscript)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<StudioCatalog>(CatalogPath);
        var content = new CsvStoryTestContent();
        var calls = content.GetSceneSteps(scene).Where(step => !string.IsNullOrEmpty(step.CutsceneAfter)).ToArray();
        Assert.That(calls.Length, Is.EqualTo(1), scene);
        var expected = content.GetCutsceneSteps(manuscript).Select(step => Plain(step.Text?.Ko)).ToArray();
        CollectionAssert.AreEqual(expected, ResolveLines(catalog, content, calls[0].CutsceneAfter, calls[0].MemoryKey));
    }
}
