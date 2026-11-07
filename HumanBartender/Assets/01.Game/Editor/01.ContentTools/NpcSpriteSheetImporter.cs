using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;

/// <summary>기존 Sprite 식별자를 보존하며 슬라이스 정의만 갱신한다. 실제 재임포트 시점은 호출자가 결정한다.</summary>
internal static class NpcSpriteSheetImporter
{
    internal static void Apply(TextureImporter importer, IReadOnlyList<SpriteRect> definitions)
    {
        if (importer == null) throw new ArgumentNullException(nameof(importer));
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new InvalidOperationException("NPC 스프라이트 데이터 제공자를 찾을 수 없습니다: " + importer.assetPath);
        provider.InitSpriteEditorDataProvider();
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names == null) throw new InvalidOperationException("NPC 스프라이트의 이름·식별자 연결을 찾을 수 없습니다: " + importer.assetPath);

        var existing = new Dictionary<string, SpriteRect>(StringComparer.Ordinal);
        foreach (var rect in provider.GetSpriteRects()) existing.Add(rect.name, rect);
        var pairs = new Dictionary<string, SpriteNameFileIdPair>(StringComparer.Ordinal);
        foreach (var pair in names.GetNameFileIdPairs()) pairs.Add(pair.name, pair);

        var updated = new SpriteRect[definitions.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.name) || !seen.Add(definition.name))
                throw new InvalidOperationException("NPC 스프라이트 이름이 비어 있거나 중복되었습니다.");
            if (!existing.TryGetValue(definition.name, out var rect))
            {
                var id = pairs.TryGetValue(definition.name, out var previousPair) ? previousPair.GetFileGUID() : GUID.Generate();
                rect = new SpriteRect { name = definition.name, spriteID = id };
            }
            // 기존 식별자와 테두리·추가 데이터는 보존한다. 정의 파일이 소유한 영역과 피벗만 갱신한다.
            rect.rect = definition.rect;
            rect.alignment = definition.alignment;
            rect.pivot = definition.pivot;
            updated[i] = rect;
            if (!pairs.ContainsKey(rect.name)) pairs.Add(rect.name, new SpriteNameFileIdPair(rect.name, rect.spriteID));
        }
        provider.SetSpriteRects(updated);
        names.SetNameFileIdPairs(pairs.Values);
        provider.Apply();
    }
}
