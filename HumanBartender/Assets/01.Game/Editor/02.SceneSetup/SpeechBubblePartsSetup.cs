using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>파츠 오브젝트는 편집 시 한 번 구성한다. 런타임에는 생성/제거 없이 크기와 메쉬만 갱신한다.</summary>
public static class SpeechBubblePartsSetup
{
    public const string OutsidePrefabPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/OutsideDialogueTextBox.prefab";
    public const string PlayPrefabPath = "Assets/01.Game/03.Content/08.Prefabs/03.UI/02.Dialogue/PlayDialogueTextBox.prefab";
    public const string PlayArtPath = "Assets/01.Game/03.Content/01.ArtResources/05.UI/01.Shared/01.MainScreen/";
    const string ArtPath = "Assets/01.Game/03.Content/01.ArtResources/05.UI/06.Ect/exterior_textbox/";

    [MenuItem("Tools/Human Bartender/Dialogue/Configure Outside Bubble Parts")]
    public static void ApplyOutsidePrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(OutsidePrefabPath);
        try
        {
            Configure(root.GetComponent<DynamicSpeechBubble>());
            PrefabUtility.SaveAsPrefabAsset(root, OutsidePrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }

    public static void Configure(DynamicSpeechBubble bubble)
    {
        if (bubble == null || bubble.bubble == null) throw new ArgumentException("Bubble RectTransform 연결이 필요합니다.");
        bool play = bubble.name == "PlayDialogueTextBox" ||
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(bubble.gameObject) == PlayPrefabPath;
        Undo.RecordObject(bubble, "Configure speech bubble parts");
        var visual = bubble.GetComponent<SpeechBubbleVisual>();
        if (visual == null) visual = Undo.AddComponent<SpeechBubbleVisual>(bubble.gameObject);
        Undo.RecordObject(visual, "Configure speech bubble parts");
        visual.enabled = true;
        var parts = visual.Parts;
        if (parts == null)
        {
            var go = new GameObject("BodyParts", typeof(RectTransform), typeof(SpeechBubblePartsView));
            Undo.RegisterCreatedObjectUndo(go, "Create speech bubble parts");
            go.layer = bubble.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(bubble.bubble, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            parts = go.GetComponent<SpeechBubblePartsView>();
            if (play)
            {
                var geometry = new SerializedObject(parts);
                geometry.FindProperty("cornerSize").vector2Value = new Vector2(20, 16);
                geometry.ApplyModifiedProperties();
            }
            var visualData = new SerializedObject(visual);
            visualData.FindProperty("parts").objectReferenceValue = parts;
            visualData.ApplyModifiedProperties();
        }
        Undo.RecordObject(parts.gameObject, "Rename speech bubble body parts");
        parts.name = "BodyParts";

        var data = new SerializedObject(parts);
        // The PNGs' 1px transparent gutter is excluded without modifying shared art imports.
        // Opaque corner/left exports are tinted to the same 242/255 opacity as the other source pieces.
        float opaquePartAlpha = play ? 1 : 242f / 255;
        AddPart(data, "topLeft", "GRD_textbox_1", play, opaquePartAlpha);
        AddPart(data, "top", "GRD_textbox_2", play);
        AddPart(data, "topRight", "GRD_textbox_3", play, opaquePartAlpha);
        AddPart(data, "left", "GRD_textbox_4", play, opaquePartAlpha);
        AddPart(data, "center", "GRD_textbox_5", play);
        AddPart(data, "right", "GRD_textbox_6", play);
        AddPart(data, "bottomLeft", "GRD_textbox_7", play, opaquePartAlpha);
        AddPart(data, "bottom", "GRD_textbox_8", play);
        AddPart(data, "bottomRight", play ? "GRD_textbox_9" : "GRD_textbox_11", play, opaquePartAlpha);
        data.ApplyModifiedProperties();
        ConfigureTail(bubble.centerArrow, "CenterTail", play ? "GRD_textbox_tail_B" : "GRD_textbox_tail_A", parts, play);
        ConfigureTail(bubble.leftArrow, "LeftTail", play ? "GRD_textbox_tail_A" : "GRD_textbox_tail_B", parts, play);
        ConfigureTail(bubble.rightArrow, "RightTail", "GRD_textbox_tail_C", parts, play);
        bubble.visualMode = SpeechBubbleVisualMode.Parts;
        EditorUtility.SetDirty(bubble);
        PrefabUtility.RecordPrefabInstancePropertyModifications(bubble);
        visual.Refresh();
    }

    static void ConfigureTail(GameObject target, string name, string resource, SpeechBubblePartsView body, bool play)
    {
        if (target == null || !target.TryGetComponent<Image>(out var image))
            throw new InvalidOperationException(name + "에 기존 꼬리 Image를 연결하세요.");
        Undo.RecordObject(target, "Rename shared speech bubble tail");
        target.name = name;
        var duplicate = body.transform.Find(name);
        var oldGraphic = duplicate != null ? duplicate.GetComponent<SpeechBubblePartGraphic>() : null;
        if (!target.TryGetComponent<SpeechBubbleTail>(out var tail))
        {
            // 기존 GameObject/Image/RectTransform의 식별자와 연결을 유지한다.
            var rect = image.rectTransform;
            bool upward = Vector3.Dot(rect.up, rect.parent.up) < 0;
            bool flipped = rect.localScale.x < 0;
            // AddComponent가 에디터 Canvas 갱신을 유발할 수 있으므로 먼저 원본을 캡처한다.
            Sprite singleSprite = image.sprite;
            Vector2 singleSize = rect.sizeDelta;
            Vector2 singleOffset = rect.anchoredPosition;
            float anchor = rect.anchorMin.x;
            tail = Undo.AddComponent<SpeechBubbleTail>(target);
            var data = new SerializedObject(tail);
            data.FindProperty("horizontalAnchor").floatValue = anchor;
            data.FindProperty("horizontalOffset").floatValue = singleOffset.x;
            data.FindProperty("pointUp").boolValue = upward;
            data.FindProperty("flipX").boolValue = flipped;
            data.FindProperty("singleImageSprite").objectReferenceValue = singleSprite;
            data.FindProperty("singleImageSize").vector2Value = singleSize;
            data.FindProperty("singleImageJoinDepth").floatValue = singleOffset.y * (upward ? -1 : 1);
            data.FindProperty("partsSprite").objectReferenceValue = oldGraphic != null ? oldGraphic.Sprite : LoadSprite(resource, play);
            if (play)
            {
                data.FindProperty("partsHeight").floatValue = 14;
                data.FindProperty("partsJoinDepth").floatValue = .65f;
            }
            data.ApplyModifiedProperties();
        }
        // 이전 구성에서 만들어진 표시 전용 꼬리만 제거한다. 본체와 공통 꼬리는 재생성하지 않는다.
        if (oldGraphic != null) Undo.DestroyObjectImmediate(duplicate.gameObject);
    }

    static Sprite LoadSprite(string resource, bool play)
    {
        // MainScreen의 _2에는 꼬리가 포함되어 있다. 본체용으로 지정한 Sprite 영역만 사용한다.
        string spriteName = play && resource == "GRD_textbox_2" ? "GRD_textbox_2_Body" : null;
        var sprite = AssetDatabase.LoadAllAssetsAtPath((play ? PlayArtPath : ArtPath) + resource + ".png")
            .OfType<Sprite>().FirstOrDefault(s => spriteName == null || s.name == spriteName);
        if (sprite == null) throw new InvalidOperationException("파츠 스프라이트가 없습니다: " + resource);
        return sprite;
    }

    static void AddPart(SerializedObject owner, string field, string resource, bool play, float alpha = 1)
    {
        var property = owner.FindProperty(field);
        if (property.objectReferenceValue != null) return; // Re-running keeps authored sprite/geometry adjustments.
        var sprite = LoadSprite(resource, play);
        var parent = ((SpeechBubblePartsView)owner.targetObject).transform;
        var go = new GameObject(char.ToUpperInvariant(field[0]) + field.Substring(1), typeof(RectTransform), typeof(CanvasRenderer), typeof(SpeechBubblePartGraphic));
        Undo.RegisterCreatedObjectUndo(go, "Create speech bubble part");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var part = go.GetComponent<SpeechBubblePartGraphic>();
        part.raycastTarget = false;
        part.color = new Color(1, 1, 1, alpha);
        var data = new SerializedObject(part);
        data.FindProperty("sprite").objectReferenceValue = sprite;
        data.FindProperty("sourceInsetPixels").floatValue = play ? 0 : 1;
        data.ApplyModifiedProperties();
        property.objectReferenceValue = part;
    }
}
