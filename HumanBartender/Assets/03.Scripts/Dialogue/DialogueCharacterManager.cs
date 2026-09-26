using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>캐릭터가 배치될 수 있는 화면 슬롯 위치.</summary>
public enum ESlotType
{
    /* Inside */

    Left,
    Right,
    Middle,
    None,
    /* Outside */


}


/// <summary>
/// 슬롯 하나가 가진 캐릭터 파츠 배열, 현재 로드된 리소스 핸들, 취소 토큰 등을 담는 컨테이너.
/// spriteHandles/animHandles는 현재 표시 중인 핸들, ~RemoveHandles는 교체 과정에서 해제 대기 중인 이전 핸들이다.
/// </summary>
[System.Serializable]
public class SlotCharacterPart
{
    public GameObject slot;
    public ESlotType type;
    public string slotCharacterName = "";
    public string expression = "";
    public CharacterPart[] parts;
    public SpriteRenderer portaitSpriteRenderer;
    

    [System.NonSerialized] public Stack<AsyncOperationHandle<Sprite>?> spriteHandles = new();
    [System.NonSerialized] public Stack<AsyncOperationHandle<AnimationClip>?> animHandles = new();

    [System.NonSerialized] public Stack<AsyncOperationHandle<Sprite>?> spriteRemoveHandles = new();
    [System.NonSerialized] public Stack<AsyncOperationHandle<AnimationClip>?> animRemoveHandles = new();

    [System.NonSerialized] public CancellationTokenSource cts;
    [System.NonSerialized] public UniTaskCompletionSource loadFinished;
    [System.NonSerialized] public int requestVersion;
}


/// <summary>
/// 대화 씬에 등장하는 모든 캐릭터 슬롯을 관리한다.
/// 슬롯별 캐릭터 배치/교체(SetCharacterAsync), 대사 시작/종료 애니메이션 트리거, 페이드 인/아웃,
/// 그리고 리소스 핸들 수명 관리(로드/해제)를 담당하는 핵심 매니저.
/// </summary>
public class DialogueCharacterManager : MonoBehaviour, ICharacterSetter, IDialogueFader
{
    [Header("DATA")]
    [SerializeField] private NewExpressionDataSO animConfig;

   
    [Header("Parts")]
    [SerializeField] private SlotCharacterPart[] slotParts;

    private Dictionary<ESlotType, SlotCharacterPart> _slotMap;

    private CharacterLoader characterLoader;


    private const string SLOT_INTRO = "Intro";
    private const string SLOT_LOOP = "Loop";
    private const string SLOT_DIALOGUE = "Dialogue";


    /// <summary>슬롯 배열을 타입별 딕셔너리로 인덱싱하고, 각 파츠의 AnimatorOverrideController를 초기화한다.</summary>
    private void Awake()
    {
        _slotMap = new Dictionary<ESlotType, SlotCharacterPart>();
        characterLoader = new CharacterLoader();

        slotParts ??= Array.Empty<SlotCharacterPart>();
        foreach (var slot in slotParts)
        {
            if (!_slotMap.TryAdd(slot.type, slot)) throw new InvalidOperationException("Duplicate character slot: " + slot.type);
            foreach (var part in slot.parts)
                part.Initialize();
        }
    }

    /// <summary>현재 캐릭터가 배치되어 있는 슬롯 수를 센다.</summary>
    public int GetCharacterCount()
    {
        int n = 0;
        foreach (var part in slotParts)
        {
            if (part.slotCharacterName != "")
                n++;
        }

        return n;
    }
    /// <summary>지정된 characterId가 배치된 슬롯의 월드 좌표를 반환한다. 없으면 Vector3.zero.</summary>
    public Vector3 GetCharacterPosition(string characterId)
    {
        for (int i = 0; i < slotParts.Length; i++)
        {
            if (slotParts[i].slotCharacterName == "")
                continue;

            if (characterId == slotParts[i].slotCharacterName)
            {
                return slotParts[i].slot.transform.position;
            }
        }

        return Vector3.zero;
    }



    /// <summary>
    /// Slot이 따로 지정되지 않았기에 검사를 통해 위치 획득
    /// 둘 다 비어있다면 우측부터
    /// 둘다 빈게 아니라면 검사 후 characterId가 동일한 쪽으로, 아니라면 무시'
    /// 
    /// </summary>
    /// <param name="characterId"></param>
    /// <param name="expression"></param>
    /// <returns></returns>
    /// <summary>
    /// 캐릭터를 지정 슬롯(또는 이미 배치된 슬롯)에 표시한다. 동일 캐릭터/표정이면 스킵하고,
    /// 표정이 스프라이트 전용이면 초상화만 로드, 아니면 각 파츠를 비동기로 병렬 로드 후 Intro 애니메이션을 재생한다.
    /// 로드 중 취소되면 새로 받은 핸들만 정리하고, 예외 발생 시 현재 핸들을 정리한다.
    /// </summary>
    public UniTask SetCharacterAsync(string characterId, string expression, ESlotType slotType = ESlotType.None) =>
        SetCharacterAsync(characterId, expression, slotType, default);

    public async UniTask SetCharacterAsync(string characterId, string expression, ESlotType slotType, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ESlotType slot = ESlotType.None;

        //위치가 지정된 경우는 문제가 없지만 지정되지 않았을 경우.

        if (slotType != ESlotType.None)
            slot = slotType;
        else
        {
            for (int i = 0; i < slotParts.Length; i++)
            {
                if (slotParts[i].slotCharacterName == "")
                    continue;

                if (characterId == slotParts[i].slotCharacterName)
                {
                    slot = slotParts[i].type;
                }
            }
        }




        if (!_slotMap.TryGetValue(slot, out var slotData)) //Slot 존재 여부
        {
            Logger.LogWarning($"[DialogueCharacterManager] Slot '{slot}' not found");
            return;
        }


        int version = ++slotData.requestVersion;
        var previous = slotData.loadFinished;
        slotData.cts?.Cancel();
        if (previous != null) await previous.Task;
        token.ThrowIfCancellationRequested();
        if (version != slotData.requestVersion) throw new OperationCanceledException("Character request was replaced.");
        if (slotData.slotCharacterName == characterId && slotData.expression == expression) return;
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        var finished = new UniTaskCompletionSource();
        slotData.cts = source;
        slotData.loadFinished = finished;
        token = source.Token;
        MoveCurrentHandlesToRemove(slotData);
        try
        {
            if (animConfig == null) throw new InvalidOperationException("Character expression data is missing.");
            foreach (var part in slotData.parts) part.SetInactive();
            if (slotData.portaitSpriteRenderer != null) slotData.portaitSpriteRenderer.sprite = null;
            bool isSprite = animConfig.IsPortraitSprite(characterId, expression);
            if (!isSprite)
            {
                if (slotData.portaitSpriteRenderer != null) slotData.portaitSpriteRenderer.gameObject.SetActive(false);
                var loads = new List<UniTask<Exception>>();
                foreach (var part in slotData.parts)
                {
                    var partData = animConfig.GetPartData(characterId, expression, part.partName);
                    var fallback = animConfig.GetDefaultPartData(characterId, part.partName);
                    loads.Add(ObserveChild(characterLoader.LoadPartAsync(slotData, part, partData, fallback, token)));
                }
                await AwaitChildren(loads);
                token.ThrowIfCancellationRequested();
                ReleaseRemoveHandles(slotData);
                var intros = new List<UniTask<Exception>>();
                foreach (var part in slotData.parts)
                    if (!string.IsNullOrEmpty(part.partCurAnim)) intros.Add(ObserveChild(part.PlayAnimation(SLOT_INTRO, token)));
                await AwaitChildren(intros);
            }
            else
            {
                string path = animConfig.GetSpritePath(characterId, expression);
                if (!await characterLoader.LoadPortaitSpriteAsync(slotData, path, token))
                    throw new InvalidOperationException("Character portrait is missing: " + path);
            }
            token.ThrowIfCancellationRequested();
            slotData.slotCharacterName = characterId;
            slotData.expression = expression;
            ReleaseRemoveHandles(slotData);
        }
        catch
        {
            foreach (var part in slotData.parts) part.SetInactive();
            if (slotData.portaitSpriteRenderer != null) slotData.portaitSpriteRenderer.sprite = null;
            slotData.slotCharacterName = "";
            slotData.expression = "";
            ReleaseCurrentHandles(slotData);
            ReleaseRemoveHandles(slotData);
            throw;
        }
        finally
        {
            if (ReferenceEquals(slotData.cts, source)) { slotData.cts = null; slotData.loadFinished = null; }
            finished.TrySetResult();
        }
    }

    // 실패해도 모든 자식 로드가 끝난 뒤 핸들을 정리한다.
    static async UniTask<Exception> ObserveChild(UniTask child)
    {
        try { await child; return null; }
        catch (Exception e) { return e; }
    }
    static async UniTask AwaitChildren(List<UniTask<Exception>> children)
    {
        var errors = await UniTask.WhenAll(children);
        foreach (var error in errors) if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    /// <summary>검증 도구도 런타임과 같은 표정 데이터·주소 규칙을 사용한다.</summary>
    public IReadOnlyCollection<string> GetConfiguredResourceKeys(string characterId, string expression)
    {
        var keys = new HashSet<string>();
        if (animConfig == null || animConfig.expressionData == null) return keys;
        if (animConfig.IsPortraitSprite(characterId, expression))
        {
            var path = animConfig.GetSpritePath(characterId, expression);
            if (!string.IsNullOrEmpty(path)) keys.Add(path);
        }
        else
            foreach (EAnimationPart part in Enum.GetValues(typeof(EAnimationPart)))
            {
                foreach (var key in CharacterLoader.ResourceKeys(animConfig.GetPartData(characterId, expression, part))) keys.Add(key);
                foreach (var key in CharacterLoader.ResourceKeys(animConfig.GetDefaultPartData(characterId, part))) keys.Add(key);
            }
        return keys;
    }

    /// <summary>슬롯 타입으로 지정된 캐릭터의 모든 파츠에 대사 시작 애니메이션을 트리거한다.</summary>
    public void OnDialogueStart(ESlotType slot)
    {
        if (_slotMap == null || !_slotMap.TryGetValue(slot, out var slotData)) return;
        foreach (var part in slotData.parts)
            part.OnDialogueStart();
    }
    /// <summary>화자 이름(speaker)으로 슬롯을 찾아 대사 시작 애니메이션을 트리거한다.</summary>
    public void OnDialogueStart(string speaker)
    {
        foreach (var slot in slotParts)
        {
            if(slot.slotCharacterName == speaker)
            {
                foreach (var part in slot.parts)
                    part.OnDialogueStart();
            }
        }
    }

    /// <summary>슬롯 타입으로 지정된 캐릭터의 모든 파츠에 대사 종료 애니메이션을 트리거한다.</summary>
    public void OnDialogueEnd(ESlotType slot)
    {
        if (_slotMap == null || !_slotMap.TryGetValue(slot, out var slotData)) return;
        foreach (var part in slotData.parts)
            part.OnDialogueEnd();
    }
    /// <summary>화자 이름(speaker)으로 슬롯을 찾아 대사 종료 애니메이션을 트리거한다.</summary>
    public void OnDialogueEnd(string speaker)
    {
        foreach (var slot in slotParts)
        {
            if (slot.slotCharacterName == speaker)
            {
                foreach (var part in slot.parts)
                    part.OnDialogueEnd();
            }
        }
    }
    /// <summary>모든 슬롯의 모든 파츠에 대사 종료 애니메이션을 트리거한다.</summary>
    public void OnDialogueEnd()
    {
        foreach (var slot in slotParts)
        {
            foreach (var part in slot.parts)
                part.OnDialogueEnd();

        }
    }

    /// <summary>슬롯을 비우고(캐릭터명/표정 초기화, 파츠 비활성화) 해당 슬롯의 리소스 핸들을 모두 해제한다.</summary>
    public void ResetCharacter(ESlotType slot)
    {
        if (_slotMap == null || !_slotMap.TryGetValue(slot, out var slotData)) return;

        slotData.requestVersion++;
        slotData.cts?.Cancel();
        slotData.slotCharacterName = "";
        slotData.expression = "";

        foreach (var part in slotData.parts)
            part.SetInactive();

        if (slotData.portaitSpriteRenderer != null)
            slotData.portaitSpriteRenderer.sprite = null;

        ReleaseCurrentHandles(slotData);
        ReleaseRemoveHandles(slotData);
    }
    /// <summary>모든 슬롯을 초기화한다.</summary>
    public void ResetCharacter()
    {
        foreach (var slot in slotParts)
        {
            ResetCharacter(slot.type);
        }
    }

    /// <summary>
    /// 슬롯이 서 있는 world x를 알려 준다. 2부 카메라가 어디로 갈지 여기서 읽는다.
    ///
    /// 좌석 좌표를 따로 표로 들지 않는다. 인물이 실제로 서 있는 곳이 정본이고, 표를 한 벌 더 두면
    /// 씬에서 슬롯만 옮겼을 때 카메라가 조용히 빈 자리를 비춘다.
    /// </summary>
    public bool TryGetSlotX(ESlotType slot, out float x)
    {
        if (_slotMap.TryGetValue(slot, out var slotData) && slotData.slot != null)
        {
            x = slotData.slot.transform.position.x;
            return true;
        }

        x = 0f;
        return false;
    }

    /// <summary>지정 슬롯의 모든 파츠를 동시에 페이드 인 시킨다.</summary>
    public async UniTask FadeInAsync(ESlotType slot, CancellationToken token)
    {
        if (!_slotMap.TryGetValue(slot, out var slotData)) //Slot 존재 여부
        {
            Logger.LogWarning($"[DialogueCharacterManager] Slot '{slot}' not found");
            return;
        }

        var tasks = new UniTask[slotData.parts.Length];
        
        for (int i = 0; i < slotData.parts.Length; i++)
            tasks[i] = slotData.parts[i].FadeIn(token);

        await UniTask.WhenAll(tasks);
    }
    /// <summary>지정 슬롯의 모든 파츠를 동시에 페이드 아웃 시킨다.</summary>
    public async UniTask FadeOutAsync(ESlotType slot, CancellationToken token)
    {
        if (!_slotMap.TryGetValue(slot, out var slotData)) //Slot 존재 여부
        {
            Logger.LogWarning($"[DialogueCharacterManager] Slot '{slot}' not found");
            return;
        }

        var tasks = new UniTask[slotData.parts.Length];

        for (int i = 0; i < slotData.parts.Length; i++)
            tasks[i] = slotData.parts[i].FadeOut(token);

        await UniTask.WhenAll(tasks);
    }




    /// <summary>현재 사용 중인 핸들 스택을 remove 스택으로 옮긴다. 새 리소스 로드 중 이전 리소스를 유지하기 위함.</summary>
    private void MoveCurrentHandlesToRemove(SlotCharacterPart slot)
    {
        while (slot.spriteHandles.Count > 0)
            slot.spriteRemoveHandles.Push(slot.spriteHandles.Pop());

        while (slot.animHandles.Count > 0)
            slot.animRemoveHandles.Push(slot.animHandles.Pop());
    }

    
    /// <summary>새 리소스 로드가 끝난 뒤, 더 이상 필요 없는 이전(remove) 핸들들을 해제한다.</summary>
    private void ReleaseRemoveHandles(SlotCharacterPart slot)
    {
        while (slot.spriteRemoveHandles.Count > 0)
        {
            var item = slot.spriteRemoveHandles.Pop();
            ResourceLoader.ReleaseHandle<Sprite>(ref item);
        }

        while (slot.animRemoveHandles.Count > 0)
        {
            var item = slot.animRemoveHandles.Pop();
            ResourceLoader.ReleaseHandle<AnimationClip>(ref item);
        }
    }

    /// <summary>로드 실패/취소 시 새로 받은(현재) 핸들들을 해제한다.</summary>
    private void ReleaseCurrentHandles(SlotCharacterPart slot)
    {
        while (slot.spriteHandles.Count > 0)
        {
            var item = slot.spriteHandles.Pop();
            ResourceLoader.ReleaseHandle<Sprite>(ref item);
        }

        while (slot.animHandles.Count > 0)
        {
            var item = slot.animHandles.Pop();
            ResourceLoader.ReleaseHandle<AnimationClip>(ref item);
        }
    }

    /// <summary>모든 슬롯의 리소스 핸들을 해제하고 각 파츠의 오버라이드 컨트롤러를 파괴한다.</summary>
    public void ReleaseAll() => ResetCharacter();

    private void OnDisable() => ResetCharacter();

    private void OnDestroy()
    {
        ResetCharacter();
        foreach (var slot in slotParts)
            foreach (var part in slot.parts) part.Release();
    }
}
