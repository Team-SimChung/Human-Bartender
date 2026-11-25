using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

public sealed class GuestSeatAppearanceTests
{
    GameObject seatObject;
    GuestSlot seat;
    SpriteRenderer body;
    Texture2D texture;
    Sprite sprite;

    [SetUp]
    public void SetUp()
    {
        seatObject = new GameObject("GuestSeat");
        seatObject.SetActive(false);
        seat = seatObject.AddComponent<GuestSlot>();
        body = seatObject.AddComponent<SpriteRenderer>();
        texture = new Texture2D(2, 2);
        sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f));

        using var data = new SerializedObject(seat);
        var parts = data.FindProperty("partRenderers");
        parts.arraySize = 1;
        var part = parts.GetArrayElementAtIndex(0);
        part.FindPropertyRelative("slot").intValue = (int)EGuestBodySlot.Body;
        part.FindPropertyRelative("renderer").objectReferenceValue = body;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        seat.Clear();
        Object.DestroyImmediate(seatObject);
        Object.DestroyImmediate(sprite);
        Object.DestroyImmediate(texture);
    }

    [Test]
    public void ImmediatelyCompletedLoadAppliesToTheAssignedGuest()
    {
        var guest = new Guest();
        var loaded = new GuestBodySprites(sprite);
        Guest seatedWhenLoadStarted = null;

        seat.Seat(guest, (_, _) =>
        {
            seatedWhenLoadStarted = seat.CurrentGuest;
            return UniTask.FromResult(loaded);
        });

        Assert.That(seatedWhenLoadStarted, Is.SameAs(guest));
        Assert.That(seat.WaitForEntranceAsync().Status, Is.EqualTo(UniTaskStatus.Succeeded));
        Assert.That(body.sprite, Is.SameAs(sprite));
        Assert.That(body.color.a, Is.EqualTo(1));
    }

    [Test]
    public void LoadCompletingAfterTheSeatWasClearedReleasesItsHandle()
    {
        var pending = new UniTaskCompletionSource<GuestBodySprites>();
        var guest = new Guest();
        seat.Seat(guest, (_, _) => pending.Task);
        seat.Clear();

        var late = new GuestBodySprites();
        var handle = Addressables.ResourceManager.CreateCompletedOperation(sprite, null);
        late.Set(EGuestBodySlot.Body, handle);
        pending.TrySetResult(late);

        Assert.That(body.sprite, Is.Null);
        Assert.That(guest.bodySprites, Is.Null);
        Assert.That(handle.IsValid(), Is.False);
    }

    [Test]
    public void RequiredPartsAreCheckedEvenWhenThereIsNoBody()
    {
        var loaded = new GuestBodySprites();
        try
        {
            foreach (var slot in NewGuestBodyDataBase.AllSlots)
            {
                if (NewGuestBodyDataBase.IsOptional(slot) || slot == EGuestBodySlot.Body) continue;
                loaded.Set(slot, Addressables.ResourceManager.CreateCompletedOperation(sprite, null));
            }
            Assert.That(loaded.HasRequiredSlots(), Is.False);
            loaded.Set(EGuestBodySlot.Body, Addressables.ResourceManager.CreateCompletedOperation(sprite, null));
            Assert.That(loaded.HasRequiredSlots(), Is.True);
        }
        finally { loaded.Release(); }
    }
}
