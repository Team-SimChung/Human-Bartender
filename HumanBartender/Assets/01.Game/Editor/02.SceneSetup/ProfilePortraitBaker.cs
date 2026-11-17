using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>선택한 카드의 기존 캐릭터 파츠 초상 연결만 갱신한다. 표시 방식·단일 이미지·씬 저장은 건드리지 않는다.</summary>
public static class ProfilePortraitBaker
{
    const string Menu = "Tools/Human Bartender/Profile/Refresh selected part portraits";
    static readonly string[] PartOrder = { "body", "lower_face", "upper_face", "eyes", "eyebrows", "extra", "etc" };

    [MenuItem(Menu)]
    public static void RefreshSelectedPortraits()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[Profile] Play 모드를 끄고 실행하세요."); return; }
        var cards = SelectedCards();
        if (cards.Length == 0) { Debug.LogWarning("[Profile] 초상을 갱신할 카드를 선택하세요."); return; }
        var catalog = CsvDataReader.LoadDirectory(Path.Combine(Application.streamingAssetsPath, "csv"));
        var expressions = catalog.Read<Dictionary<string, Dictionary<string, NewExpressionEntry>>>("character_anim");
        var bindings = CsvContentCatalog.ParseRows(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "unity/resource_bindings.csv")), "resource_bindings");
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Refresh Profile Portraits");
        int count = 0;
        try
        {
            foreach (var card in cards)
            {
                if (string.IsNullOrWhiteSpace(card.CharacterId)) continue;
                if (!TryReadPortrait(card.CharacterId, expressions, Resolve, out var layers))
                {
                    Debug.LogWarning("[Profile] 초상 리소스가 완성되지 않아 기존 연결을 유지합니다: " + card.CharacterId, card);
                    continue;
                }
                if (card.PortraitLayers != null && card.PortraitLayers.SequenceEqual(layers)) continue;
                Undo.RecordObject(card, "Refresh Profile Portrait");
                using var data = new SerializedObject(card);
                var values = data.FindProperty("portraitLayers"); values.arraySize = layers.Length;
                for (int i = 0; i < layers.Length; i++)
                {
                    values.GetArrayElementAtIndex(i).FindPropertyRelative("sprite").objectReferenceValue = layers[i].sprite;
                    values.GetArrayElementAtIndex(i).FindPropertyRelative("frame").rectValue = layers[i].frame;
                }
                data.ApplyModifiedProperties();
                if (PrefabUtility.IsPartOfPrefabInstance(card)) PrefabUtility.RecordPrefabInstancePropertyModifications(card);
                EditorUtility.SetDirty(card);
                if (card.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(card.gameObject.scene);
                count++;
            }
        }
        finally { Undo.CollapseUndoOperations(undoGroup); }
        Debug.Log($"[Profile] 선택한 카드 {cards.Length}개 중 파츠 초상 연결 {count}개를 갱신했습니다. 표시 방식과 단일 이미지는 유지했습니다. 저장은 직접 해주세요.");

        Object Resolve(string key, Type type)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var binding = bindings.FirstOrDefault(row => row["asset_type"] == type.Name && row["source_key"] == key);
            if (binding != null) key = binding["unity_key"];
            string sub = null;
            int bracket = key.IndexOf('[');
            if (bracket >= 0) { sub = key.Substring(bracket + 1).TrimEnd(']'); key = key.Substring(0, bracket); }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var address = settings == null ? null : settings.groups.Where(group => group != null)
                .SelectMany(group => group.entries).FirstOrDefault(entry => entry.address == key);
            if (address == null) return null;
            return AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(address.guid))
                .FirstOrDefault(asset => type.IsInstanceOfType(asset) && (sub == null || asset.name == sub));
        }
    }

    [MenuItem(Menu, true)]
    static bool CanRefresh() => !EditorApplication.isPlayingOrWillChangePlaymode && SelectedCards().Length > 0;

    static ProfileCardView[] SelectedCards() => Selection.gameObjects
        .SelectMany(go => go.GetComponentsInChildren<ProfileCardView>(true)
            .Concat(new[] { go.GetComponentInParent<ProfileCardView>(true) }))
        .Where(card => card != null).Distinct().ToArray();

    internal static bool TryReadPortrait(string id, Dictionary<string, Dictionary<string, NewExpressionEntry>> expressions,
        Func<string, Type, Object> resolve, out ProfilePortraitView.Layer[] layers)
    {
        layers = Array.Empty<ProfilePortraitView.Layer>();
        if (!expressions.TryGetValue(id, out var variants) || !variants.TryGetValue("default", out var entry)) return false;
        var sprites = new List<Sprite>();
        if (entry.Mode == ENewExpressionMode.Sprite)
        {
            var sprite = resolve(entry.Sprite, typeof(Sprite)) as Sprite;
            if (sprite == null) return false;
            sprites.Add(sprite);
        }
        else if (entry.Parts != null)
        {
            foreach (string part in PartOrder)
            {
                if (!entry.Parts.TryGetValue(part, out var animation)) continue;
                var clip = resolve(CharacterLoader.LoopResourceKey(animation.Clip), typeof(AnimationClip)) as AnimationClip;
                var sprite = clip == null ? null : AnimationUtility.GetObjectReferenceCurveBindings(clip)
                    .Where(binding => binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
                    .SelectMany(binding => AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    .Select(frame => frame.value as Sprite).FirstOrDefault(value => value != null);
                if (sprite == null) return false;
                sprites.Add(sprite);
            }
        }
        if (sprites.Count == 0) return false;
        var points = sprites.SelectMany(sprite => sprite.vertices.Select(vertex => vertex * sprite.pixelsPerUnit + sprite.pivot)).ToArray();
        if (points.Length == 0) return false;
        var min = new Vector2(points.Min(point => point.x), points.Min(point => point.y));
        var max = new Vector2(points.Max(point => point.x), points.Max(point => point.y));
        var size = max - min;
        if (size.x <= 0 || size.y <= 0) return false;
        layers = sprites.Select(sprite => new ProfilePortraitView.Layer { sprite = sprite,
            frame = new Rect(-min.x / size.x, -min.y / size.y, sprite.rect.width / size.x, sprite.rect.height / size.y) }).ToArray();
        return true;
    }
}
