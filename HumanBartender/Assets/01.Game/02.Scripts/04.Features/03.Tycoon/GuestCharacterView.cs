using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 좌석 하나에 단골(카메오)의 그림을 붙이는 곳.
///
/// 랜덤 손님은 파츠 스프라이트 아홉 장을 겹쳐 그리지만, 단골은 표정과 애니메이션까지 딸린 한 벌이라
/// 다루는 방식이 다르다. 그 한 벌을 불러오는 일은 2부 대화가 쓰는 CharacterLoader가 이미 하고 있어서
/// 여기서 다시 만들지 않는다 — 두 벌이 되면 같은 캐릭터가 1부와 2부에서 다르게 보이기 시작한다.
///
/// 좌석 밖에서 슬롯 번호로 이 자리를 찾아오게 하지 않는다. 리그가 이 좌석에 붙어 있으므로
/// 연결도 이 좌석 인스펙터 한 곳에서 끝나고, 좌석과 리그가 어긋날 여지가 없다.
/// </summary>
public class GuestCharacterView : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("캐릭터별 표정·파츠 애니메이션 정보. 2부 대화가 쓰는 것과 같은 에셋을 꽂는다.")]
    [SerializeField] NewExpressionDataSO animConfig;

    [Header("Rig")]
    [Tooltip("이 좌석의 캐릭터 리그. parts에 파츠별 Animator/SpriteRenderer를, portaitSpriteRenderer에 " +
             "표정이 통짜 그림으로 들어오는 경우에 쓸 렌더러를 연결한다.")]
    [SerializeField] SlotCharacterPart rig;

    const string SlotIntro = "Intro";

    readonly CharacterLoader loader = new();

    CancellationTokenSource cts;

    /// <summary>지금 이 자리에 붙어 있는 캐릭터 id. 비어 있으면 아무도 없다.</summary>
    public string CharacterId => rig == null ? null : rig.slotCharacterName;

    void Awake()
    {
        if (rig?.parts == null) return;

        foreach (var part in rig.parts)
            part.Initialize();
    }

    void OnDestroy()
    {
        Clear();
    }

    /// <summary>
    /// 이 자리에 캐릭터를 붙인다. 같은 캐릭터·같은 표정이면 아무것도 하지 않는다.
    ///
    /// 새 리소스를 다 받을 때까지 이전 것을 그대로 두고, 받은 뒤에 이전 것을 놓는다.
    /// 먼저 놓으면 받는 동안 자리가 비어 깜빡인다.
    /// </summary>
    public async UniTask SetCharacterAsync(string characterId, string expression)
    {
        if (!IsReady()) return;
        if (rig.slotCharacterName == characterId && rig.expression == expression) return;

        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cts.Token, this.GetCancellationTokenOnDestroy());
        CancellationToken token = linked.Token;

        MoveCurrentHandlesToRemove();

        rig.slotCharacterName = characterId;
        rig.expression = expression;

        try
        {
            if (animConfig.IsPortraitSprite(characterId, expression))
                await ApplyPortraitAsync(characterId, expression, token);
            else
                await ApplyPartsAsync(characterId, expression, token);
        }
        catch (OperationCanceledException)
        {
            ReleaseCurrentHandles();
            ReleaseRemoveHandles();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GuestCharacter] '{characterId}'({expression}) 로드 실패\n{e}");
            ReleaseCurrentHandles();
        }
    }

    /// <summary>표정이 통짜 그림 하나로 들어오는 경우. 파츠는 전부 끈다.</summary>
    async UniTask ApplyPortraitAsync(string characterId, string expression, CancellationToken token)
    {
        foreach (var part in rig.parts)
            part.SetInactive();

        await loader.LoadPortaitSpriteAsync(rig, animConfig.GetSpritePath(characterId, expression), token);

        ReleaseRemoveHandles();
    }

    /// <summary>파츠별로 애니메이션 또는 스프라이트를 불러와 붙이고, 등장 애니메이션을 재생한다.</summary>
    async UniTask ApplyPartsAsync(string characterId, string expression, CancellationToken token)
    {
        if (rig.portaitSpriteRenderer != null)
            rig.portaitSpriteRenderer.gameObject.SetActive(false);

        var loads = new UniTask[rig.parts.Length];

        for (int i = 0; i < rig.parts.Length; i++)
        {
            PartAnimData data = animConfig.GetPartData(characterId, expression, rig.parts[i].partName);
            PartAnimData defaultData = animConfig.GetDefaultPartData(characterId, rig.parts[i].partName);

            loads[i] = loader.LoadPartAsync(rig, rig.parts[i], data, defaultData, token);
        }

        await UniTask.WhenAll(loads);

        ReleaseRemoveHandles();

        var intros = new UniTask[rig.parts.Length];

        for (int i = 0; i < rig.parts.Length; i++)
            intros[i] = rig.parts[i].PlayAnimation(SlotIntro, token);

        await UniTask.WhenAll(intros);
    }

    /// <summary>자리를 비운다. 손님이 퇴장할 때 부른다.</summary>
    public void Clear()
    {
        if (rig == null) return;

        cts?.Cancel();
        cts?.Dispose();
        cts = null;

        rig.slotCharacterName = "";
        rig.expression = "";

        if (rig.parts != null)
        {
            foreach (var part in rig.parts)
                part.SetInactive();
        }

        if (rig.portaitSpriteRenderer != null)
            rig.portaitSpriteRenderer.sprite = null;

        ReleaseCurrentHandles();
        ReleaseRemoveHandles();
    }

    bool IsReady()
    {
        if (animConfig == null)
        {
            Debug.LogError($"[GuestCharacter] {name}의 animConfig가 비어 있어 단골 그림을 붙일 수 없습니다.");
            return false;
        }

        if (rig == null || rig.parts == null || rig.parts.Length == 0)
        {
            Debug.LogError($"[GuestCharacter] {name}의 캐릭터 리그(parts)가 비어 있어 붙일 곳이 없습니다.");
            return false;
        }

        return true;
    }

    // ── 리소스 핸들 ─────────────────────────────────────────────────────

    void MoveCurrentHandlesToRemove()
    {
        while (rig.spriteHandles.Count > 0) rig.spriteRemoveHandles.Push(rig.spriteHandles.Pop());
        while (rig.animHandles.Count > 0) rig.animRemoveHandles.Push(rig.animHandles.Pop());
    }

    void ReleaseRemoveHandles()
    {
        while (rig.spriteRemoveHandles.Count > 0)
        {
            var handle = rig.spriteRemoveHandles.Pop();
            ResourceLoader.ReleaseHandle(ref handle);
        }

        while (rig.animRemoveHandles.Count > 0)
        {
            var handle = rig.animRemoveHandles.Pop();
            ResourceLoader.ReleaseHandle(ref handle);
        }
    }

    void ReleaseCurrentHandles()
    {
        while (rig.spriteHandles.Count > 0)
        {
            var handle = rig.spriteHandles.Pop();
            ResourceLoader.ReleaseHandle(ref handle);
        }

        while (rig.animHandles.Count > 0)
        {
            var handle = rig.animHandles.Pop();
            ResourceLoader.ReleaseHandle(ref handle);
        }
    }
}
