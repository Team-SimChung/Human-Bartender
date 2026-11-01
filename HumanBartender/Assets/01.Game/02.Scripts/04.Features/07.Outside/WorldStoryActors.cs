using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>한 상호작용 대상의 월드 표현만 소유한다. 게임 플래그·대본·씬 이동은 변경하지 않는다.</summary>
[DisallowMultipleComponent]
public sealed class WorldStoryActors : MonoBehaviour
{
    [Serializable]
    public sealed class Actor
    {
        public string Id;
        public Transform Root;
        public SpriteRenderer Renderer;
        public bool VisibleOutsideSession = true;
    }

    [SerializeField] Actor[] actors = Array.Empty<Actor>();
    [SerializeField] WorldDialogueSpeakers speakers;
    [SerializeField, Min(.05f)] float moveDuration = .65f;
    CancellationTokenSource action;
    Pose[] original;
    Vector3[] homes;
    bool leased;
    public bool IsPlaying => action != null;
    public WorldDialogueSpeakers Speakers => speakers;

    readonly struct Pose
    {
        readonly Transform root;
        readonly SpriteRenderer renderer;
        readonly Vector3 position, scale;
        readonly Color color;
        readonly bool visible, flip;
        internal Pose(Actor actor)
        {
            root = actor.Root; renderer = actor.Renderer;
            position = root.localPosition; scale = root.localScale;
            color = renderer.color; visible = renderer.enabled; flip = renderer.flipX;
        }
        internal void Restore()
        {
            if (root != null) { root.localPosition = position; root.localScale = scale; }
            if (renderer != null) { renderer.color = color; renderer.enabled = visible; renderer.flipX = flip; }
        }
    }

    internal void Configure(Actor[] values, WorldDialogueSpeakers dialogueSpeakers)
    {
        if (leased) throw new InvalidOperationException("표현 실행 중에는 화자 배치를 바꿀 수 없습니다.");
        actors = values; speakers = dialogueSpeakers;
    }

    void Validate()
    {
        if (actors.Any(actor => actor == null || string.IsNullOrEmpty(actor.Id) || actor.Root == null || actor.Renderer == null) ||
            actors.Select(actor => actor.Id).Distinct(StringComparer.Ordinal).Count() != actors.Length)
            throw new InvalidOperationException("월드 사건의 화자·Transform·SpriteRenderer 연결을 확인하세요: " + name);
        if (actors.Any(actor => actor.Root.gameObject.scene != gameObject.scene || !actor.Root.IsChildOf(transform) ||
            actor.Renderer.transform != actor.Root && !actor.Renderer.transform.IsChildOf(actor.Root)))
            throw new InvalidOperationException("월드 표현은 자신의 대상 아래에 배치된 화자만 제어할 수 있습니다: " + name);
        homes ??= actors.Select(actor => actor.Root.position).ToArray();
    }

    public void RefreshVisibility(bool eligible, IConditionUtil conditions)
    {
        if (leased) return;
        Validate();
        bool outsideCat = GetComponent<WorldInteractionTarget>()?.StorySceneId == "neighborhood-reveal";
        foreach (var actor in actors)
        {
            bool visible = actor.VisibleOutsideSession;
            if (outsideCat && actor.Id == "boobi") visible = conditions.CheckRequired("flag.boobi_revealed");
            actor.Renderer.enabled = eligible && visible;
        }
    }

    public void BeginSession()
    {
        if (!isActiveAndEnabled) throw new InvalidOperationException("비활성 월드 표현은 시작할 수 없습니다.");
        if (leased) throw new InvalidOperationException("이 월드 표현은 이미 사용 중입니다.");
        Validate();
        original = actors.Select(actor => new Pose(actor)).ToArray();
        leased = true;
    }

    public void EndSession()
    {
        action?.Cancel();
        if (!leased) return;
        foreach (var pose in original) pose.Restore();
        original = null;
        leased = false;
    }

    void OnDisable() => EndSession();

    Actor Require(string id) => actors.FirstOrDefault(actor => actor.Id == id)
        ?? throw new InvalidOperationException("월드 사건 화자가 연결되지 않았습니다: " + id + " / " + name);

    Vector3 Home(Actor actor) => homes[Array.IndexOf(actors, actor)];

    public async UniTask PlayAsync(OutsideStoryWorldAction kind, Transform player, ISoundEffectPlayer sound, CancellationToken token)
    {
        if (!isActiveAndEnabled || !leased || action != null) throw new InvalidOperationException("월드 사건 표현이 비활성, 세션 없음 또는 실행 중입니다.");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        action = source;
        bool completed = false;
        try
        {
            source.Token.ThrowIfCancellationRequested();
            var ct = source.Token;
            switch (kind)
            {
                case OutsideStoryWorldAction.RevealCat:
                    var revealed = Require("boobi"); revealed.Renderer.enabled = true;
                    revealed.Root.position = Home(revealed) - Vector3.up * .25f;
                    await MoveAsync(revealed, Home(revealed), ct);
                    await PulseAsync(revealed.Root, .2f, ct);
                    break;
                case OutsideStoryWorldAction.FeedCat:
                case OutsideStoryWorldAction.PetCat:
                case OutsideStoryWorldAction.CatMeow:
                    var cat = Require("boobi"); cat.Renderer.enabled = true;
                    await PulseAsync(cat.Root, kind == OutsideStoryWorldAction.FeedCat ? .3f : .15f, ct);
                    break;
                case OutsideStoryWorldAction.AdoptCat:
                    var adopted = Require("boobi");
                    await adopted.Renderer.DOFade(0, .35f).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
                    adopted.Renderer.enabled = false;
                    break;
                case OutsideStoryWorldAction.NeighborEnter:
                    var neighbor = Require("neighbor"); neighbor.Renderer.enabled = true;
                    await MoveAsync(neighbor, Require("boobi").Root.position + Vector3.right * .8f, ct);
                    break;
                case OutsideStoryWorldAction.NeighborExit:
                    await DepartAsync(Require("neighbor"), Home(Require("neighbor")), ct);
                    break;
                case OutsideStoryWorldAction.ChrisEnter:
                    var chris = Require("chris"); chris.Root.position = Home(chris) + Vector3.right;
                    chris.Renderer.enabled = true;
                    await MoveAsync(chris, Home(chris), ct);
                    break;
                case OutsideStoryWorldAction.RepairElevator:
                    await PulseAsync(Require("repair").Root, .2f, ct);
                    break;
                case OutsideStoryWorldAction.SamhoDepart:
                case OutsideStoryWorldAction.NotionSamhoExit:
                    await DepartAsync(Require("samho"), Home(Require("samho")) + Vector3.right * 2, ct);
                    break;
                case OutsideStoryWorldAction.NotionApproach:
                    var dealer = Require("dealer"); dealer.Renderer.enabled = true;
                    await MoveAsync(dealer, Require("samho").Root.position + Vector3.right * .9f, ct);
                    break;
                case OutsideStoryWorldAction.NotionDealerExit:
                    await DepartAsync(Require("dealer"), Home(Require("dealer")) + Vector3.right * 2, ct);
                    break;
                case OutsideStoryWorldAction.ThugPunch:
                    if (player == null) throw new InvalidOperationException("타격 표현의 플레이어가 없습니다.");
                    var thug = Require("thug"); var before = thug.Root.position;
                    await MoveAsync(thug, Vector3.Lerp(before, player.position, .55f), ct);
                    await MoveAsync(thug, before, ct);
                    break;
                case OutsideStoryWorldAction.ThugDepart:
                    await DepartAsync(Require("thug"), Home(Require("thug")) + Vector3.right * 2, ct);
                    break;
                case OutsideStoryWorldAction.DoorCue:
                    if (sound == null) throw new InvalidOperationException("문 효과음 서비스가 없습니다.");
                    sound.PlaySE("SFX_door_open_close");
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
            source.Token.ThrowIfCancellationRequested();
            completed = true;
        }
        finally
        {
            if (ReferenceEquals(action, source)) action = null;
            if (!completed && original != null)
                foreach (var pose in original) pose.Restore();
        }
    }

    async UniTask DepartAsync(Actor actor, Vector3 destination, CancellationToken token)
    {
        await MoveAsync(actor, destination, token);
        actor.Renderer.enabled = false;
    }
    UniTask MoveAsync(Actor actor, Vector3 destination, CancellationToken token)
    {
        actor.Renderer.flipX = destination.x < actor.Root.position.x;
        return actor.Root.DOMove(destination, moveDuration).SetEase(Ease.Linear).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
    }
    static async UniTask PulseAsync(Transform actor, float amount, CancellationToken token)
    {
        var scale = actor.localScale;
        try { await actor.DOScale(scale * (1 + amount), .18f).SetLoops(2, LoopType.Yoyo).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token); }
        finally { if (actor != null) actor.localScale = scale; }
    }
}
