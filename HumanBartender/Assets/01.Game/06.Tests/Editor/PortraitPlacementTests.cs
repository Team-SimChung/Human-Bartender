using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PortraitPlacementTests
{
    [TestCase(0f)]
    [TestCase(.5f)]
    [TestCase(1f)]
    public void PortraitOffsetIsPivotIndependentAndDoesNotAccumulate(float pivotY)
    {
        var texture = new Texture2D(130, 153);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 130, 153), new Vector2(.5f, pivotY), 100);
        var go = new GameObject("PortraitPlacementTest", typeof(SpriteRenderer));
        try
        {
            go.transform.localPosition = new Vector3(0, 0, 2);
            var rig = new SlotCharacterPart { portaitSpriteRenderer = go.GetComponent<SpriteRenderer>() };
            var offset = new Vector2Int(0, 35);
            rig.ApplyPortrait(sprite, offset);
            rig.ApplyPortrait(sprite, offset);
            Assert.That(go.transform.localPosition.y + sprite.bounds.min.y, Is.EqualTo(.35f).Within(.0001f));
            Assert.That(go.transform.localPosition.z, Is.EqualTo(2));
            rig.ApplyPortrait(sprite);
            Assert.That(go.transform.localPosition.y + sprite.bounds.min.y, Is.EqualTo(0).Within(.0001f), "다음 미등록 캐릭터에 이전 보정을 남기지 않는다.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void SetupPreservesEditedPlacementAndDoesNotMoveOtherCharacters()
    {
        var data = ScriptableObject.CreateInstance<NewExpressionDataSO>();
        try
        {
            ShibaPortraitSetup.AddMissingPlacement(data);
            var owner = new SerializedObject(data);
            var entries = owner.FindProperty("portraitPlacements");
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("offsetPixels").vector2IntValue = new Vector2Int(2, 42);
            owner.ApplyModifiedPropertiesWithoutUndo();
            ShibaPortraitSetup.AddMissingPlacement(data);
            owner.Update();
            Assert.That(entries.arraySize, Is.EqualTo(1));
            Assert.That(data.GetPortraitOffsetPixels("shiba"), Is.EqualTo(new Vector2Int(2, 42)));
            Assert.That(data.GetPortraitOffsetPixels("chris"), Is.EqualTo(Vector2Int.zero));
        }
        finally { Object.DestroyImmediate(data); }
    }
}
