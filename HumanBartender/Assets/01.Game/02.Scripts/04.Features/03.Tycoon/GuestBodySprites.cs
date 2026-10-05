using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 랜덤 손님 한 명에게 배정된 슬롯별 파츠 조합(일반 손님 외형 생성 시스템 §7).
///
/// 슬롯을 이름 붙인 칸으로 두지 않고 목록으로 든다. 슬롯이 늘어날 때 이 클래스와 렌더러, 로더를
/// 한 군데씩 고치는 대신 EGuestBodySlot 하나만 늘리면 되게 하려는 것이다.
///
/// 쓰지 않는 선택 슬롯(겉옷·목걸이·팔 액세서리)은 아예 담지 않는다. 담지 않은 슬롯은 레이어를
/// 만들지 않는다는 뜻이고, 그게 곧 §7의 null 기록이다.
/// </summary>
public class GuestBodyAppearance
{
    readonly Dictionary<EGuestBodySlot, NewGuestBodyPartData> parts = new();

    /// <summary>이 조합의 성별. 기본 조합으로 되돌릴 때 어느 쪽을 쓸지 정한다.</summary>
    public string Gender { get; }

    public GuestBodyAppearance(string gender)
    {
        Gender = gender;
    }

    public void Set(EGuestBodySlot slot, NewGuestBodyPartData part)
    {
        parts[slot] = part;
    }

    /// <summary>이 슬롯에 배정된 파츠. 쓰지 않는 선택 슬롯이면 false다.</summary>
    public bool TryGet(EGuestBodySlot slot, out NewGuestBodyPartData part)
    {
        return parts.TryGetValue(slot, out part);
    }

    /// <summary>배정된 파츠 전부. 레이어 순서는 부르는 쪽이 AllSlots로 정한다.</summary>
    public IReadOnlyDictionary<EGuestBodySlot, NewGuestBodyPartData> Parts => parts;

    /// <summary>같은 조합인지. 착석 중인 손님과 겹치는 외형을 피할 때 쓴다(§3.2).</summary>
    public bool SameAs(GuestBodyAppearance other)
    {
        if (other == null || other.parts.Count != parts.Count) return false;

        foreach (var pair in parts)
        {
            if (!other.parts.TryGetValue(pair.Key, out var part)) return false;
            if (part.Id != pair.Value.Id) return false;
        }

        return true;
    }

    public override string ToString()
    {
        var names = new List<string>();

        foreach (var slot in NewGuestBodyDataBase.AllSlots)
        {
            if (parts.TryGetValue(slot, out var part)) names.Add(part.Id);
        }

        return string.Join(", ", names);
    }
}

/// <summary>
/// GuestBodyAppearance를 addressable로 로드한 결과 스프라이트를 슬롯별로 들고 있는 컨테이너.
/// 손님이 자리를 떠날 때 Release로 핸들을 반드시 반납해야 한다.
/// </summary>
public class GuestBodySprites
{
    readonly Dictionary<EGuestBodySlot, Sprite> sprites = new();
    readonly List<AsyncOperationHandle<Sprite>?> handles = new();

    /// <summary>불러온 스프라이트를 슬롯에 담는다. 핸들이 비어 있으면(로드 실패) 담지 않는다.</summary>
    public void Set(EGuestBodySlot slot, AsyncOperationHandle<Sprite>? handle)
    {
        handles.Add(handle);

        if (handle.HasValue) sprites[slot] = handle.Value.Result;
    }

    /// <summary>이 슬롯에 그릴 스프라이트. 없으면 null이고, 렌더러는 비워 둔다.</summary>
    public Sprite Get(EGuestBodySlot slot)
    {
        return sprites.TryGetValue(slot, out var sprite) ? sprite : null;
    }

    /// <summary>몸처럼 반드시 있어야 하는 슬롯이 비었는지. 하나라도 비면 기본 조합으로 되돌린다(§5).</summary>
    public bool HasRequiredSlots(GuestBodyAppearance appearance)
    {
        foreach (var pair in appearance.Parts)
        {
            if (NewGuestBodyDataBase.IsOptional(pair.Key)) continue;
            if (!sprites.ContainsKey(pair.Key)) return false;
        }

        return true;
    }

    public void Release()
    {
        for (int i = 0; i < handles.Count; i++)
        {
            var handle = handles[i];
            ResourceLoader.ReleaseHandle(ref handle);
        }

        handles.Clear();
        sprites.Clear();
    }
}
