using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HumanBartender.CutsceneStudio;

public enum WorldInteractionOutput { SpeechBubble, Image, Cutscene, Action }

[Serializable]
public sealed class WorldInteractionTimelineBinding
{
    [SerializeField] private string id;
    [SerializeField] private StudioSequence sequence;
    public string Id => id;
    public StudioSequence Sequence => sequence;
}

/// <summary>외부/홈의 표시 설정. 실행 조건은 scenes.csv의 ID로, 컷씬 산출물은 Inspector로 연결함.</summary>
[DisallowMultipleComponent]
public sealed class WorldInteractionTarget : MonoBehaviour
{
    [Header("CSV interaction")]
    [Tooltip("story/scenes.csv의 id. source_id와 다른 값입니다.")]
    [SerializeField] private string storySceneId;
    [Tooltip("기존 interact_points의 source ID를 사용할 때 연결함. 연결하면 Story Scene Id 대신 이 데이터를 사용함.")]
    [SerializeField] private WorldInteractionContent content;
    [SerializeField, Min(0.01f)] private float interactionRadius = 1.5f;
    [SerializeField] private string interactionLabel = "살펴보기";
    [Header("Presentation")]
    [SerializeField] private WorldInteractionOutput output = WorldInteractionOutput.SpeechBubble;
    [SerializeField] private Transform speechAnchor;
    [SerializeField] private Vector2 bubbleOffset = new(0, 8);
    [Tooltip("이미지 출력에서 확대할 Sprite. 비어 있으면 이 오브젝트의 SpriteRenderer를 사용합니다.")]
    [SerializeField] private Sprite inspectionImage;
    [SerializeField] private OutlineHighlight highlight;
    [Header("Cutscene Studio")]
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
    public bool IsAmbient => content != null && content.IsProximity;
    public float InteractionRadius => interactionRadius;
    internal string RegistryKey => content != null ? "source:" + content.SourceId : "scene:" + storySceneId;
    public string Label => interactionLabel;
    public WorldInteractionOutput Output => output;
    public StudioSequence CutsceneSequence => cutsceneSequence;
    public IWorldInteractionAction Action => action != null ? action as IWorldInteractionAction : null;
    public Transform SpeechAnchor => speechAnchor != null ? speechAnchor : transform;
    public Vector2 BubbleOffset => bubbleOffset;
    public Sprite InspectionImage => inspectionImage != null ? inspectionImage : GetComponent<SpriteRenderer>()?.sprite;

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
        Gizmos.DrawLine(transform.position, SpeechAnchor.position);
        Gizmos.DrawWireSphere(SpeechAnchor.position, .04f);
    }
}
