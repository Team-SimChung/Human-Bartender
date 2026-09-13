using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

[System.Serializable]
public struct Entity
{
    public string id;
    public InteractiveEntity entity;

    public Entity(string id, InteractiveEntity entity)
    {
        this.id = id;
        this.entity = entity;
    }
}

/// <summary>테스트 모드에서 강제로 세팅할 플래그 값.</summary>
[System.Serializable]
public struct TestFlag
{
    public string flag;
    public bool bValue;
}

/// <summary>
/// 실외 씬의 모든 상호작용 엔티티(오브젝트/NPC/트리거)를 현재 날짜·게임 흐름·조건에 맞춰
/// 스폰 여부와 표시 가능한 대사(FlowData)를 매 씬 진입 시 갱신하는 총괄 매니저.
/// </summary>
public class InteractiveEntityManager : MonoBehaviour
{
    [Header("Test")]
    [SerializeField] bool isTest = false;
    [SerializeField] int testDay = 0;
    [SerializeField] EGameFlow testFlow = EGameFlow.CommuteIn;
    [SerializeField] List<TestFlag> testFlags = new List<TestFlag>();

    [Header("Data")]
    //=================================================================
    [SerializeField] protected NewInteractPointDataSO InteractPointData;
    [SerializeField] protected NewStreetDataSO streetData;
    [SerializeField] protected NewSpotDataSO SpotData;

    //===========================================
    [SerializeField] private List<Entity> entityList = new();
    private HashSet<string> spawnHistory = new HashSet<string>();
    private HashSet<string> onceHistory = new HashSet<string>();
    [Header("Player")]
    [SerializeField] GameObject player;


    [Inject] IPlayerDataWriter testPlayerWriter;
    [Inject] IPlayerDataReader playerData;
    [Inject] ISoundManager soundManager;
    [Inject] IObjectResolver resolver;
    [Inject] IConditionUtil conditionUtil;
    //day 값 보고 검사 하기
    void Awake()
    {
    }
    //기본값 주입
    [SerializeField] protected ITrackedbleEvent OnTrackedText;
    [SerializeField] protected DialogueRunner runner;
    [SerializeField] protected OutsideDialoguePresenter presenter;
    [SerializeField] protected InteractableEvent OnInteracted;
    [SerializeField] protected VoidEvent OnRefreshCondition;
    /// <summary>
    /// (테스트 모드면 날짜/흐름/플래그를 강제 세팅 후) BGM 재생, 출입구 활성화, 엘리베이터 위치,
    /// 플레이어 스폰 위치를 현재 GameFlow에 맞춰 초기화하고 엔티티 상태를 갱신한다.
    /// </summary>
    private void Start()
    {
        if (isTest)
        {
            GameStateManager.Instance.CurrentDay = testDay;
            GameStateManager.Instance.GameFlow = testFlow;

            foreach (var flag in testFlags)
            {
                testPlayerWriter.AddFlag(flag.flag, flag.bValue);
            }
        }

        Logger.Log("Entity Init");
        soundManager.PlayBGM("BGM_outside");


        // 자식 Entity 들에 의존성 주입
        InjectAndRegisterChildEntities();

        RefreshEntity();
        if(player != null)
        player.GetComponent<Player>().setpos();
    }

    private void InjectAndRegisterChildEntities()
    {
        entityList.Clear();

        var entities = GetComponentsInChildren<InteractiveEntity>(true);

        foreach (var entity in entities)
        {
            // 1. null 체크 및 ID 유효성 검사 (null, "", " " 일 때 모두 제외)
            if (entity == null || string.IsNullOrWhiteSpace(entity.souceid))
            {
                Debug.LogWarning($"[EntityManager] sourceId가 널이거나 비어있어 등록에서 제외됨: {entity?.gameObject.name}");
                continue;
            }

            // 2. 의존성 주입 (Init)
            if (entity is InteractiveNPCEntity npcEntity)
            {
                npcEntity.Init(OnInteracted, OnRefreshCondition, OnTrackedText, runner, presenter,this);
            }
            else
            {
                entity.Init(OnInteracted, OnRefreshCondition);
            }

            // 3. 리스트에 등록
            entityList.Add(new Entity(entity.souceid, entity));
        }

        Logger.Log($"[EntityManager] 총 {entityList.Count}개의 Entity가 주입 및 등록되었습니다.");
    }

    public void RefreshEntity()
    {
        spawnHistory.Clear();

        foreach (var entity in InteractPointData.interactPointData)
        {
            // 1. 엔티티 존재 여부 안전 검사
            Entity found = entityList.Find(e => e.id == entity.SourceId);
            InteractiveEntity curentity = found.entity;
            if (curentity == null)
            {
                // 딕셔너리에서 검색 실패했을 때와 동일한 예외 처리
                Debug.LogWarning($"[EntityManager] {entity.SourceId}에 해당하는 엔티티를 찾을 수 없습니다.");
                continue;
            }
            // 2. 스폰 조건 검사 (조건 불만족 시 비활성화 후 스킵)
            if (!conditionUtil.Check(entity.SpawnWhen) || (GameStateManager.Instance.GameFlow != entity.Phase&& EGameFlow.Both != entity.Phase))
            {
                if (!spawnHistory.Contains(entity.SourceId))
                {
                    curentity.gameObject.SetActive(false);
                }
                continue;
            }

            // 3. 스팟 좌표 정보 조회
            if (!SpotData.TryGetData(entity.SpotId, out var curSpot))
            {
                Debug.LogError($"[InteractEntityManager] {entity.Id}: Spot({entity.SpotId}) 정보를 불러오는 데 실패했습니다.");
            }
            else
            {
                curentity.gameObject.transform.position = curSpot.Position;
            }

                // 4. 엔티티 활성화 및 기본 정보 설정
                curentity.gameObject.SetActive(true);
            spawnHistory.Add(entity.SourceId);

            curentity.kind = entity.Kind;
            curentity.ActivationMode = entity.ActivationMode;
            curentity.ActionType = entity.ActionType;

            // 5. 상호작용 조건 및 대화 흐름(Dialogue Flow) 결정
            if (entity.InteractWhen == null || conditionUtil.Check(entity.InteractWhen))
            {
                curentity.isInteract = true;

                int minSeq = int.MaxValue; // 안전한 최소값 비교용 초기화
                string targetSceneId = string.Empty;

                if (entity.DialogueFlows != null)
                {
                    foreach (var data in entity.DialogueFlows)
                    {
                        // 조건 검사
                        if (data.When != null && !conditionUtil.Check(data.When)) continue;
                        if (data.PlayType == EPlayType.Once && onceHistory.Contains(data.SceneId)) continue;

                        // 우선순위가 더 높은(flow_seq가 더 작은) 대화 씬 선택
                        if (data.FlowSeq < minSeq)
                        {
                            minSeq = data.FlowSeq;
                            targetSceneId = data.SceneId;
                        }
                    }
                }

                // 선택된 대화 씬 적용
                if (string.IsNullOrEmpty(targetSceneId))
                {
                    if (curentity.ActionType == EActionType.Dialogue)
                    {
                        curentity.isInteract = false; curentity.steps = null;
                    }
                }
                else
                {
                    if (streetData.TryGetSceneData(targetSceneId, out NewSceneData sceneData))
                    {
                        curentity.steps = sceneData.Steps;
                        curentity.DialogueSceneId = targetSceneId;
                    }
                    else
                    {
                        Debug.LogError($"[InteractEntityManager] {entity.Id}: Scene({targetSceneId}) 정보를 불러오는 데 실패했습니다.");
                    }
                }
            }
            else
            {
                curentity.isInteract = false;
            }
        }
    }
    public void CompleteDialogue(string sceneId)
    {
        if (!string.IsNullOrEmpty(sceneId))
            onceHistory.Add(sceneId);
    }

    /// <summary>
    /// OutsideCondition(오브젝트/NPC 스폰 조건용) 하나를 검사한다. null이면 항상 통과.
    /// 아래 Condition 오버로드와 조건 검사 로직이 동일하게 중복 구현되어 있으니 함께 참고할 것.
    /// </summary>
    public bool CheckCondition(OutsideCondition? checkType)
    {
        if (checkType == null) return true;

        switch (checkType.Value.Type)
        {
            case EConditionCheckType.None:
                break;

            case EConditionCheckType.Affinity:
                int characterTier = playerData.GetCurCharacterAffinityValue(checkType.Value.Character);
                int targettier = checkType.Value.Min;
                return characterTier >= targettier;

            case EConditionCheckType.Skill:
                return playerData.GetSkillValue() >= checkType.Value.Min;

            case EConditionCheckType.Money:
                return playerData.HasEnoughMoney(checkType.Value.Min);

            case EConditionCheckType.Flag:
                return playerData.CheckFlag(checkType.Value.FlagId) == checkType.Value.BValue;
        }

        return false;
    }
    /// <summary>Condition(대사 FlowData 조건용) 하나를 검사한다. null이면 항상 통과.</summary>
    public bool CheckCondition(Condition? checkType)
    {
        if (checkType == null) return true;

        switch (checkType.Value.Type)
        {
            case EConditionCheckType.None:
                break;

            case EConditionCheckType.Affinity:
                int characterTier = playerData.GetCurCharacterAffinityValue(checkType.Value.Character);
                int targettier = checkType.Value.Min;
                return characterTier >= targettier;

            case EConditionCheckType.Skill:
                return playerData.GetSkillValue() >= checkType.Value.Min;

            case EConditionCheckType.Money:
                return playerData.HasEnoughMoney(checkType.Value.Min);

            case EConditionCheckType.Flag:
                return playerData.CheckFlag(checkType.Value.FlagId) == checkType.Value.BValue;
        }

        return false;
    }
}
