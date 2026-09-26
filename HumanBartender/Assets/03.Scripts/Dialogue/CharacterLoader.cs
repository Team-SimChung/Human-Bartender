using Cysharp.Threading.Tasks;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine;

/// <summary>
/// 대화 씬에서 캐릭터 파츠(애니메이션/스프라이트/초상화)의 리소스 로드를 담당하는 클래스.
/// CharacterPart 데이터에 맞는 애니메이션 -> 스프라이트 -> 초상화 순으로 로드를 시도하고,
/// 전부 실패하면 파츠를 비활성화한다.
/// </summary>
public class CharacterLoader
{
    private const string SLOT_INTRO = "Intro";
    private const string SLOT_LOOP = "Loop";
    private const string SLOT_DIALOGUE = "Dialogue";


    //************************************************************************************//
    // 스크립트 기능 분리 검토. 단순 컴포지션 분리
    /// <summary>
    /// 애니메이션 로드시도 :  loop 클립 로드 -> intro 클립 로드 시도 -> Part.Applyanimaton
    /// 스프라이트 로드시도 : 해당 파츠 Sprite 로드 시도 -> Part.ApplySprite
    /// 디폴트 스프라이트 로드 시도 : 존재 여부 확인 후 -> Part.ApplySprite
    /// 
    /// 검사로직 수정 필요. Data 자체가 Null일 경우 default로 돌리게 만들기
    /// 모두 실패한다면 SetInactive()
    /// 
    /// 파츠 현재 애님이랑 비교해서 같다면 스킵하기
    /// 
    /// 나중에 다시 검토하기 데이터가 바뀜으로 써 수정사항이 생김.
    /// </summary>
    /// <param name="part"></param>
    /// <param name="data"></param>
    /// <param name="defaultData"></param>
    /// <returns></returns>
    public async UniTask LoadPartAsync(
        SlotCharacterPart slot,
        CharacterPart part,
        PartAnimData data,
        PartAnimData defaultData,
        CancellationToken token)
    {
        if (data == null)
        {
            Logger.LogWarning($"[CharacterManager] PartAnimData null, Default Data");

        }
        else if (data.Loop == EAnimLoopMode.None)
        {
            part.SetInactive();

            if (slot.portaitSpriteRenderer != null)
                slot.portaitSpriteRenderer.sprite = null;

            return;
        }

        if (await LoadAnimAsync(slot, part, data, token)) //Part Anim
            return;

        if (await LoadAnimAsync(slot, part, defaultData, token)) //Part Default Anim
            return;

        if (await LoadSpriteAsync(slot, part, data, token)) //Part Sprite
            return;

        if (await LoadSpriteAsync(slot, part, defaultData, token)) //Part Default Sprite
            return;

        if (part.partName != EAnimationPart.Body)  //body의 경우만 Portail Image 로드 시도.
        {
            part.SetInactive();
            return;
        }

        if (await LoadPortaitSpriteAsync(slot, data, token))
            return;

        if (await LoadPortaitSpriteAsync(slot, defaultData, token))
            return;

        part.SetInactive();

        if (slot.portaitSpriteRenderer != null)
            slot.portaitSpriteRenderer.sprite = null;
    }

    /// <summary>
    /// 파츠의 Loop/Intro/Dialogue 애니메이션 클립을 로드하여 오버라이드 컨트롤러에 적용한다.
    /// Loop 클립 로드에 성공한 경우에만 true를 반환하며, Intro/Dialogue 클립이 없으면 Loop 클립으로 대체한다.
    /// </summary>
    /// <returns>Loop 클립 로드 성공 여부</returns>
    public async UniTask<bool> LoadAnimAsync(
        SlotCharacterPart slot,
        CharacterPart part,
        PartAnimData data,
        CancellationToken token)
    {
        if (data == null) //clipData가 null이면 false, 추후 default anim 삽입.
        {
            Logger.LogWarning($"{part.partName} Null");
            return false;
        }
        
        part.SetLoopMode(data.Loop);

        string clipAddress = data.Clip;
        string introAddress = AnimationAddress(clipAddress, SLOT_INTRO);
        string loopAddress = LoopResourceKey(clipAddress);
        string dialogueAddress = AnimationAddress(clipAddress, SLOT_DIALOGUE);

        var loopHandle = await LoadOwnedAsync(slot.animHandles, loopAddress, token);

        if (loopHandle.HasValue)
        {
            //LoopSetting

            part.partCurAnim = clipAddress;
            part.SetClip(SLOT_LOOP, loopHandle);


            //Intro Setting
            var introHandle = await LoadOwnedAsync(slot.animHandles, introAddress, token);

            if (introHandle.HasValue)
                part.SetClip(SLOT_INTRO, introHandle);
            else
                part.SetClip(SLOT_INTRO, loopHandle);




            //Dialogue Setting
            var dialogueHandle = await LoadOwnedAsync(slot.animHandles, dialogueAddress, token);


            if (dialogueHandle.HasValue)
                part.SetClip(SLOT_DIALOGUE, dialogueHandle);
            else
                part.SetClip(SLOT_DIALOGUE, loopHandle);


            return true;
        }

        return false;
    }

    /// <summary>
    /// 파츠의 단일 스프라이트를 로드하여 적용한다. 애니메이션 로드가 실패했을 때의 대체 경로로 사용된다.
    /// </summary>
    /// <returns>스프라이트 로드 및 적용 성공 여부</returns>
    public async UniTask<bool> LoadSpriteAsync(
       SlotCharacterPart slot,
        CharacterPart part,
        PartAnimData data,
        CancellationToken token)
    {

        if (data == null)
            return false;

        string clipaddress = data.Clip;

        var spriteHandle = await LoadOwnedAsync(slot.spriteHandles, clipaddress, token);

        if (spriteHandle.HasValue)
        {
            part.ApplySprite(spriteHandle.Value.Result);
            return true;
        }

        Logger.LogWarning($"[CharacterPart:{part.partName}] '{clipaddress}' 리소스 없음");
        return false;
    }


    /// <summary>
    /// PartAnimData 기반으로 초상화(Portrait) 스프라이트를 로드한다.
    /// 애니메이션/스프라이트 파츠 로드가 모두 실패했을 때 Body 파츠에 한해 시도되는 최종 대체 경로.
    /// </summary>
    /// <returns>초상화 스프라이트 로드 성공 여부</returns>
    public async UniTask<bool> LoadPortaitSpriteAsync(
         SlotCharacterPart slot,
        PartAnimData data,
        CancellationToken token)
    {
        if (data == null || data.Clip == null || slot.portaitSpriteRenderer == null)
            return false;

        string clipaddress = data.Clip;

        var spriteHandle = await LoadOwnedAsync(slot.spriteHandles, clipaddress, token);

        if (spriteHandle.HasValue)
        {
            slot.portaitSpriteRenderer.sprite = spriteHandle.Value.Result;
            return true;
        }

        Logger.LogWarning($"[CharacterPart:Portait] '{clipaddress}' 리소스 없음");
        return false;
    }


    /// <summary>
    /// 경로 문자열(dataPath)을 직접 받아 초상화 스프라이트를 로드하는 오버로드.
    /// 대사 씬 외 UI(초상화 전용 표시 등)에서 직접 경로를 지정할 때 사용.
    /// </summary>
    /// <returns>초상화 스프라이트 로드 성공 여부</returns>
    public async UniTask<bool> LoadPortaitSpriteAsync(
        SlotCharacterPart slot,
        string dataPath,
        CancellationToken token)
    {
        if (dataPath == null || slot.portaitSpriteRenderer == null)
            return false;

        Logger.Log("Load Portait");

        string clipaddress = dataPath;

        var spriteHandle = await LoadOwnedAsync(slot.spriteHandles, clipaddress, token);

        if (spriteHandle.HasValue)
        {
            slot.portaitSpriteRenderer.gameObject.SetActive(true);
            slot.portaitSpriteRenderer.sprite = spriteHandle.Value.Result;
            return true;
        }

        Logger.LogWarning($"[CharacterPart:Portait] '{clipaddress}' 리소스 없음");
        return false;
    }

    public static string LoopResourceKey(string clip) => AnimationAddress(clip, SLOT_LOOP);

    static string AnimationAddress(string clip, string slot) => $"{clip}_{slot}";

    public static IEnumerable<string> ResourceKeys(PartAnimData data)
    {
        if (data == null || data.Loop == EAnimLoopMode.None || string.IsNullOrEmpty(data.Clip)) yield break;
        yield return AnimationAddress(data.Clip, SLOT_LOOP);
        yield return AnimationAddress(data.Clip, SLOT_INTRO);
        yield return AnimationAddress(data.Clip, SLOT_DIALOGUE);
        yield return data.Clip;
    }

    static async UniTask<AsyncOperationHandle<T>?> LoadOwnedAsync<T>(Stack<AsyncOperationHandle<T>?> owner, string address, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var handle = await ResourceLoader.TryLoadAsync<T>(address, token);
        try { token.ThrowIfCancellationRequested(); }
        catch { ResourceLoader.ReleaseHandle<T>(ref handle); throw; }
        if (handle.HasValue) owner.Push(handle);
        return handle;
    }
}
