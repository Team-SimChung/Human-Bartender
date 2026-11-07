using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HumanBartender.CutsceneStudio;

public enum WorldInteractionOutput { SpeechBubble, Image, Cutscene, Action }

public enum WorldInteractionStartMode
{
    [InspectorName("직접 상호작용")] Interact,
    [InspectorName("범위 진입 시 자동 시작")] Automatic,
    [InspectorName("주변 대화 · 이동 허용")] Ambient,
    [InspectorName("다른 사건에서만 호출")] ScriptOnly
}

[Serializable]
public sealed class WorldInteractionStartOverride
{
    [Tooltip("선택된 interact_points 행의 id입니다. 날짜·조건에 따른 행 선택은 기존 CSV를 따릅니다.")]
    [SerializeField] private string definitionId;
    [SerializeField] private WorldInteractionStartMode startMode;
    public string DefinitionId => definitionId;
    public WorldInteractionStartMode StartMode => startMode;

    internal WorldInteractionStartOverride(string id, WorldInteractionStartMode mode)
    { definitionId = id; startMode = mode; }
}

[Serializable]
public sealed class WorldInteractionTimelineBinding
{
    [SerializeField] private string id;
    [SerializeField] private StudioSequence sequence;
    public string Id => id;
    public StudioSequence Sequence => sequence;
}

/// <summary>외부/홈의 시작 방식·범위·표시 설정. 날짜·진행 조건과 대사는 기존 CSV로 연결함.</summary>
[DisallowMultipleComponent]
public sealed class WorldInteractionTarget : MonoBehaviour
{
    [Header("CSV 대본 연결")]
    [Tooltip("story/scenes.csv의 id. source_id와 다른 값입니다.")]
    [SerializeField] private string storySceneId;
    [Tooltip("기존 interact_points의 source ID를 사용할 때 연결함. 연결하면 Story Scene Id 대신 이 데이터를 사용함.")]
    [SerializeField] private WorldInteractionContent content;
    [Header("대화 시작")]
    [Tooltip("자동/직접 상호작용 여부는 이 설정을 사용합니다. CSV의 start_mode는 이 대상의 시작 방식을 덮어쓰지 않습니다.")]
    [SerializeField] private WorldInteractionStartMode startMode = WorldInteractionStartMode.Interact;
    [Tooltip("이 오브젝트의 루트와 플레이어 사이 XY 거리입니다. CSV radius 대신 이 값을 사용합니다.")]
    [SerializeField, Min(0.01f)] private float interactionRadius = 1.5f;
    [Tooltip("같은 Content가 날짜·조건별로 서로 다른 행을 선택할 때, 특정 행의 시작 방식만 바꿀 수 있습니다. 비어 있으면 위 시작 방식을 사용합니다.")]
    [SerializeField] private WorldInteractionStartOverride[] contentStartOverrides = Array.Empty<WorldInteractionStartOverride>();
    [SerializeField, HideInInspector] private int startSettingsVersion;
    [SerializeField] private string interactionLabel = "살펴보기";
    [Header("출력 설정")]
    [SerializeField] private WorldInteractionOutput output = WorldInteractionOutput.SpeechBubble;
    [SerializeField] private Transform speechAnchor;
    [SerializeField] private Vector2 bubbleOffset = new(0, 8);
    [Tooltip("이미지 출력에서 확대할 Sprite. 비어 있으면 이 오브젝트의 SpriteRenderer를 사용합니다.")]
    [SerializeField] private Sprite inspectionImage;
    [SerializeField] private OutlineHighlight highlight;
    [Header("컷씬 연결")]
    [Tooltip("직접 컷씬 출력의 기본 Timeline. CSV에서는 이 에셋의 ID로도 호출할 수 있습니다.")]
    [SerializeField] private StudioSequence cutsceneSequence;
    [Tooltip("CSV timeline 단계의 arg와 원본 StudioSequence를 연결합니다. 날짜와 시청 조건은 CSV에서 설정합니다.")]
    [SerializeField] private WorldInteractionTimelineBinding[] timelines = Array.Empty<WorldInteractionTimelineBinding>();
    [Tooltip("이전 CSV에서 예약만 된 미제작 컷씬 ID. Timeline을 연결하기 전까지 경고 후 다음 대화로 진행함.")]
    [SerializeField] private string[] optionalTimelineIds = Array.Empty<string>();
    [Tooltip("IWorldInteractionAction을 구현한 컴포넌트. 예: OutsideElevator")]
    [SerializeField] private MonoBehaviour action;

    private static readonly HashSet<WorldInteractionTarget> registered = new();
    public string StorySceneId => storySceneId;
    public WorldInteractionContent Content => content;
    public WorldInteractionStartMode StartMode => ResolveStartMode(content?.Definition?.Id);
    public bool IsAmbient => StartMode == WorldInteractionStartMode.Ambient;
    public float InteractionRadius => interactionRadius;
    internal bool HasAuthoredStartSettings => startSettingsVersion >= 1;
    internal string RegistryKey => content != null ? "source:" + content.SourceId : "scene:" + storySceneId;
    public string Label => interactionLabel;
    public WorldInteractionOutput Output => output;
    public StudioSequence CutsceneSequence => cutsceneSequence;
    public IWorldInteractionAction Action => action != null ? action as IWorldInteractionAction : null;
    public IWorldInteractionParticipant Participant => GetComponent<IWorldInteractionParticipant>();
    public Transform SpeechAnchor => speechAnchor != null ? speechAnchor : transform;
    public Vector2 BubbleOffset => bubbleOffset;
    public Sprite InspectionImage => inspectionImage != null ? inspectionImage : GetComponent<SpriteRenderer>()?.sprite;

    internal void ConfigureStory(string id, float radius, string label)
    {
        storySceneId = id;
        interactionRadius = radius;
        interactionLabel = label;
        RegistryRevision++;
    }

    internal void ConfigureStart(WorldInteractionStartMode mode, float radius, WorldInteractionStartOverride[] overrides = null)
    {
        if (!float.IsFinite(radius) || radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
        startMode = mode;
        interactionRadius = radius;
        contentStartOverrides = overrides ?? Array.Empty<WorldInteractionStartOverride>();
        startSettingsVersion = 1;
        RegistryRevision++;
    }

    internal WorldInteractionStartMode ResolveStartMode(string definitionId)
    {
        if (!string.IsNullOrEmpty(definitionId))
            foreach (var entry in contentStartOverrides ?? Array.Empty<WorldInteractionStartOverride>())
                if (entry != null && entry.DefinitionId == definitionId) return entry.StartMode;
        return startMode;
    }

    internal void ApplyStartSettings(ref NewSceneData story)
    {
        story.Radius = interactionRadius;
        story.StartMode = StartMode switch
        {
            WorldInteractionStartMode.Interact => ENewSceneTrigger.Interact,
            WorldInteractionStartMode.Automatic or WorldInteractionStartMode.Ambient => ENewSceneTrigger.Auto,
            _ => ENewSceneTrigger.Manual
        };
    }

    public Transform ResolveSpeaker(string actor, Transform player)
    {
        if (actor == "luna") return player;
        var speakers = GetComponent<WorldDialogueSpeakers>();
        return speakers != null ? speakers.Resolve(actor) : SpeechAnchor;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        registered.Clear();
        RegistryRevision++;
    }
    internal static int RegistryRevision { get; private set; }
    private void Reset() => startSettingsVersion = 1;
    private void OnEnable()
    {
        if (registered.Add(this)) RegistryRevision++;
    }
    private void OnValidate() => RegistryRevision++;
    // 비활성 대상도 CSV ID의 소유권을 유지해 중복 배치를 검출함.
    private void OnDisable() => SetFocus(false);
    private void OnDestroy()
    {
        if (registered.Remove(this)) RegistryRevision++;
    }
    public void SetFocus(bool focused)
    {
        if (highlight != null) highlight.SetHighlight(focused);
    }

    public StudioSequence ResolveTimeline(string id)
    {
        if (TryResolveTimeline(id, out var result)) return result;
        throw new InvalidOperationException("Target의 Timeline 목록에서 컷씬을 찾을 수 없습니다: " + id);
    }

    public bool IsOptionalTimeline(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        foreach (string optional in optionalTimelineIds ?? Array.Empty<string>())
            if (optional == id) return true;
        return false;
    }

    public bool TryResolveTimeline(string id, out StudioSequence result)
    {
        result = null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in timelines ?? Array.Empty<WorldInteractionTimelineBinding>())
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.Id) || binding.Sequence == null)
                throw new InvalidOperationException("Timeline 목록의 ID와 StudioSequence를 연결하세요: " + name);
            if (!ids.Add(binding.Id)) throw new InvalidOperationException("중복된 Timeline ID: " + binding.Id);
            if (binding.Id == id) result = binding.Sequence;
        }
        if (result != null) return true;
        if (cutsceneSequence != null && (string.IsNullOrEmpty(id) || cutsceneSequence.Id == id)) result = cutsceneSequence;
        return result != null;
    }

    public static void RegisterScene(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var target in root.GetComponentsInChildren<WorldInteractionTarget>(true))
                if (registered.Add(target)) RegistryRevision++;
    }

    public static IEnumerable<WorldInteractionTarget> InScene(Scene scene)
    {
        foreach (var target in registered)
            if (target != null && target.gameObject.scene == scene) yield return target;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        // 실제 감지는 XY 거리이므로 루트 중심의 원으로 시작 범위를 표시한다.
        if (float.IsFinite(interactionRadius) && interactionRadius > 0)
        {
            const int segments = 64;
            var previous = transform.position + Vector3.right * interactionRadius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                var next = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * interactionRadius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
        Gizmos.DrawLine(transform.position, SpeechAnchor.position);
        Gizmos.DrawWireSphere(SpeechAnchor.position, .04f);
    }
}
