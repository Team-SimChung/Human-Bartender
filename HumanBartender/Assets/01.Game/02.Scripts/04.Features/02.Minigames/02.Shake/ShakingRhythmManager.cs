using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// 위에서 내려오는 노트를 A(왼쪽) / D(오른쪽)로 처리하는 쉐이킹 리듬게임.
/// UI는 씬/프리팹에서 연결하며, 플레이 중에는 배치된 노트 샘플만 복제한다.
///
/// 읽는 순서: Start → ResetRound → StartGame → Update → Judge/CheckMiss → Record → CompleteMade.
/// 외부 제조 실행기는 PlayAsync로 완료를 기다리고 GimmickResult를 받아 기록한다.
/// ICraftGimmickProgress는 진행 상태, ICraftGimmickManualEnd는 조기 종료,
/// ICraftGimmickOwnHud는 공통 HUD와 중복되는 진행 표시의 소유권을 알려준다.
/// </summary>
public class ShakingRhythmManager : MonoBehaviour, ICraftGimmick,
    ICraftGimmickProgress, ICraftGimmickManualEnd, ICraftGimmickOwnHud
{
    [Inject] ISoundEffectPlayer sound;
    [Inject] AudioOutputSettings audioOutput;
    // 독립 테스트 씬에서만 종료 후 R키 재시작을 허용한다. 제조 실행용 프리팹은 false.
    [SerializeField] private bool isTest;
    // 기존 캐릭터 클립의 ClipSpeed를 초기화하는 보조 컴포넌트.
    [SerializeField] private AnimSpeedController characterAnim;
    // 실제 재생/정지는 Animator.speed로 제어한다. "Play" 상태가 루프 클립을 사용한다.
    [SerializeField] private Animator characterAnimator;
    // 배경 음악 재생 전용. 재생 위치나 BPM을 노트 이동/판정에 사용하지 않는다.
    [SerializeField] private AudioSource bgmSource;
    // 기믹 전용 카메라로 렌더링하는 캔버스. 독립 씬에서는 메인 카메라를 사용한다.
    [SerializeField] private Canvas gameCanvas;

    [Header("Scene UI / shared lane coordinates")]
    // 아래 네 배열은 [0] = 왼쪽 A, [1] = 오른쪽 D 순서로 연결해야 한다.
    // 생성점/판정점/노트 샘플은 각 laneRoot의 직접 자식이고 중앙 앵커를 사용한다.
    // anchoredPosition을 비교하므로 서로 다른 부모/앵커 좌표계를 섞으면 안 된다.
    [SerializeField] private RectTransform[] laneRoots;
    // 노트 중심이 생성되는 위치. 두 레인의 생성점→판정점 Y거리는 같아야 한다.
    [SerializeField] private RectTransform[] spawnPoints;
    // 노트 중심과 비교할 기준 위치. 장식용 링은 이 위치 아래에 배치되어 있다.
    [SerializeField] private RectTransform[] judgementLines;
    // 편집 중 보이는 노트 샘플. Start에서 숨기고 SpawnNote에서 자식 장식까지 복제한다.
    [SerializeField] private Image[] noteTemplates;
    // 처리된 노트 수 / 목표 수. 빈 입력은 처리된 노트 수에 포함되지 않는다.
    [SerializeField] private Text status;
    // 현재 연속 성공 수. Perfect와 Good 모두 콤보를 늘린다.
    [SerializeField] private Text comboLabel;
    // 5콤보/10콤보 단계의 HEAT와 배속 안내. 기본 단계에서는 빈 문자열이다.
    [SerializeField] private Text heatLabel;
    // 마지막 PERFECT / GOOD / MISS를 잠시 표시하고 서서히 숨긴다.
    [SerializeField] private Text feedback;
    // 첫 노트가 도착하기 전의 3→2→1 시작 안내. 노트 이동 시계와 별도로 실제 시간을 쓴다.
    [SerializeField] private Text countdown;
    // 완료 문구, 최대 콤보, 점수와 테스트 모드 재시작 안내.
    [SerializeField] private Text finishLabel;
    // 시작 버튼이 들어 있는 팝업. 시작하거나 대기 시간이 4초를 넘으면 숨긴다.
    [SerializeField] private GameObject introPanel;
    // 노트 처리 순서대로 채우는 결과 막대. 빈 입력에는 막대 한 칸을 소모하지 않는다.
    [SerializeField] private Image[] resultSegments = new Image[0];
    // 미리 배치된 속도선. 1단계에서는 앞 4개, 2단계에서는 전부 표시한다.
    [SerializeField] private Image[] speedLines = new Image[0];
    // 레인별 판정 타깃의 테두리. 최고 단계에서는 금색으로 바뀐다.
    [SerializeField] private Graphic[] targetFaces = new Graphic[0];
    // 레인별 판정 순간에 퍼지는 링. 생성하지 않고 같은 링의 크기/투명도를 재사용한다.
    [SerializeField] private Graphic[] hitRings = new Graphic[0];
    // 콤보 숫자 뒤의 빛. 콤보 단계가 높을수록 진해진다.
    [SerializeField] private Graphic comboGlow;
    // 패널 가장자리의 빛. 노트나 판정선을 가리지 않도록 배치된 장식이다.
    [SerializeField] private Image[] edgeLights = new Image[0];

    [Header("Reference tuning / independent of music")]
    // 한 판에 실제로 생성하고 처리할 노트 수. 빈 입력이 있어도 이 수는 변하지 않는다.
    [SerializeField, Min(1)] private int targetNoteCount = ShakingRhythmRules.NoteCount;
    // 기본 1배속에서 생성점→판정점까지 걸리는 초. Start에서 이동 거리로 기본 속도를 계산한다.
    [SerializeField, Min(0.1f)] private float leadSeconds = ShakingRhythmRules.LeadSeconds;
    // 이동 시계 기준 생성 간격(초). 실제 시간 간격은 이 값 / 현재 배속이다.
    [SerializeField, Min(0.1f)] private float spawnInterval = ShakingRhythmRules.IntervalSeconds;
    // 판정선에 도착할 것으로 추정되는 시점과 입력 사이의 허용 오차(초), 앞/뒤 대칭.
    [SerializeField, Min(0.01f)] private float perfectSeconds = ShakingRhythmRules.PerfectSeconds;
    // Perfect 바깥부터 이 범위까지는 Good. 이 범위를 벗어난 입력이나 통과는 Miss.
    [SerializeField, Min(0.01f)] private float hitWindowSeconds = ShakingRhythmRules.HitWindowSeconds;

    /// <summary>한 번 생성된 노트의 이동 대상과 잔상 참조. 씬의 샘플 노트와 구분한다.</summary>
    private sealed class LiveNote
    {
        public RectTransform Rect;
        public Image Trail;
    }
    // 각 레인은 생성 순서대로 보관한다. 모든 노트에 같은 이동량을 적용하므로 추월이 없고,
    // Peek으로 맨 앞 노트를 판정한 뒤 Dequeue로 제거하는 큐 구조를 유지할 수 있다.
    private readonly Queue<LiveNote>[] lanes = { new Queue<LiveNote>(), new Queue<LiveNote>() };
    // 마지막 판정 이후 흐른 실제 초와 그 판정의 색. 1초로 시작해 초기 링이 보이지 않게 한다.
    private readonly float[] hitAges = { 1, 1 };
    private readonly Color[] hitColors = { Color.white, Color.white };
    private static readonly Color Cyan = new Color(0.56f, 0.91f, 1);
    private static readonly Color Gold = new Color(0.93f, 0.86f, 0.67f);
    private static readonly Color MissColor = new Color(0.85f, 0.55f, 0.62f);
    private static readonly Color EmptyColor = new Color(0.13f, 0.2f, 0.25f);
    // ready: 씬 참조 검증/초기화 완료, playing: 노트 이동/입력 판정 진행 중,
    // finished: 결과 확정, endedManually: 외부 다음 버튼으로 종료했는지.
    // ResetRound 후에는 ready=true, playing=false, finished=false인 시작 대기 상태다.
    private bool ready, playing, finished, endedManually;
    // target: 목표 노트 수, spawned: 생성 수, processed: 실제 노트 판정 완료 수.
    // successes: Perfect+Good, failures: 놓친 노트+빈 입력, extraPresses: 빈 입력만 따로 센 수.
    // combo: 현재 연속 성공, bestCombo: 이번 판의 최대 연속 성공(종료 UI에만 표시).
    // processed와 successes+failures는 빈 입력이 있으면 서로 다르다는 점에 주의한다.
    private int target, spawned, processed, successes, failures, extraPresses, combo, bestCombo;
    // trackTime: 배속이 반영된 공통 이동 시계(가상 초). 생성 시점과 이동에 사용한다.
    // elapsed: 시작 이후 실제 초(카운트다운), uiAge: 시작 대기를 포함한 UI 수명(팝업).
    // feedbackLeft: 판정 문구의 남은 표시 시간, baseSpeed: 1배속의 UI 좌표 단위/초.
    // 실제 제조 전체 시간은 별도의 craftTimer가 관리하며 여기서 직접 더하지 않는다.
    private float trackTime, elapsed, uiAge, feedbackLeft, baseSpeed;
    // GimmickRunner의 await를 풀어주는 완료 신호. 독립 씬에서는 연결되지 않아 null이다.
    private UniTaskCompletionSource runnerCompletion;
    // 외부에서 전달받는 제조 전체 타이머. 결과에 종료 시각을 적을 때만 읽는다.
    private CraftTimer craftTimer;

    /// <summary>실행기가 제조 시간을 셀 구간. 시작 입력을 기다리는 시간도 포함한다.</summary>
    public bool IsManualInputActive => ready && !finished;
    /// <summary>초기화 후 결과 확정 전까지 외부 다음 버튼으로 끝낼 수 있다. 시작 대기도 포함한다.</summary>
    public bool CanEndNow => IsManualInputActive;
    /// <summary>실제 노트 처리 수만 표시한다. 빈 타이밍 입력으로 진행률이 늘어나지 않는다.</summary>
    public string ProgressText => $"{processed} / {target}";
    /// <summary>쉐이킹은 목표 수량형 기믹이 아니므로 공통 O.D 표시는 사용하지 않는다.</summary>
    public bool ShowOkMark => false;
    /// <summary>진행 표시는 자체 보드가 맡는다. 공통 HUD의 시간/대상 이름/다음 버튼은 유지한다.</summary>
    public ECraftHudPart OwnedParts => ECraftHudPart.Progress;
    /// <summary>콤보 0~4: 1, 5~9: 1.25, 10 이상: 1.5. 실패 시 콤보와 함께 1로 복귀한다.</summary>
    public float SpeedMultiplier => ShakingRhythmRules.Multiplier(combo);
    /// <summary>현재 이동 속도(UI 좌표 단위/초). CanvasScaler 적용 후 화면 픽셀 수와는 다를 수 있다.</summary>
    public float CurrentSpeed => baseSpeed * SpeedMultiplier;

    /// <summary>씬 참조를 검증하고 기본 속도를 계산한 뒤 시작 대기 상태를 만든다.</summary>
    private void Start()
    {
        if (!HasLanes(laneRoots) || !HasLanes(spawnPoints) || !HasLanes(judgementLines) ||
            !HasLanes(noteTemplates) || status == null || feedback == null || comboLabel == null)
        {
            Debug.LogError("[Rhythm Shake] Connect the scene's lane and text references.", this);
            enabled = false;
            return;
        }
        // 동일한 속도로 움직여도 두 레인의 도착 시간이 같으려면 이동 거리가 같아야 한다.
        // 위치 비교는 각 레인 안의 로컬 UI 좌표로 한다. 월드 좌표와 섞지 않는다.
        float distance = spawnPoints[0].anchoredPosition.y - judgementLines[0].anchoredPosition.y;
        float other = spawnPoints[1].anchoredPosition.y - judgementLines[1].anchoredPosition.y;
        if (distance <= 0 || Mathf.Abs(distance - other) > 0.1f)
        {
            Debug.LogError("[Rhythm Shake] Both lanes must have the same positive travel distance.", this);
            enabled = false;
            return;
        }
        // 속도 = 거리 / 시간. 이후 배속만 바꾸고, 기본 속도는 매 프레임 다시 계산하지 않는다.
        baseSpeed = distance / Mathf.Max(0.1f, leadSeconds);
        // 제조 무대가 바에서 떨어져 있을 수 있어 무대 카메라를 우선 찾는다.
        if (gameCanvas != null) gameCanvas.worldCamera = CraftGimmickStage.Find(this);
        // 샘플은 생성 원본으로 남겨 두되, 플레이 중에는 생성된 노트만 보이게 한다.
        foreach (var template in noteTemplates) template.gameObject.SetActive(false);
        ready = true;
        ResetRound();
    }

    /// <summary>필수 레인 배열이 정확히 2칸이고 양쪽 참조가 모두 연결되어 있는지 확인한다.</summary>
    private static bool HasLanes<T>(T[] values) where T : Object =>
        values != null && values.Length == 2 && values[0] != null && values[1] != null;

    /// <summary>
    /// 남은 노트와 이번 판의 상태/UI를 초기화한다. 처음 진입과 테스트 모드 R 재시작에 사용한다.
    /// 외부 제조 실행기는 판마다 새 프리팹을 만들므로 완료 신호/외부 타이머는 여기서 교체하지 않는다.
    /// </summary>
    private void ResetRound()
    {
        ClearNotes();
        target = Mathf.Max(1, targetNoteCount);
        spawned = processed = successes = failures = extraPresses = combo = bestCombo = 0;
        trackTime = elapsed = uiAge = feedbackLeft = 0;
        playing = finished = endedManually = false;
        hitAges[0] = hitAges[1] = 1;
        feedback.text = "";
        if (introPanel != null) introPanel.SetActive(true);
        if (finishLabel != null) finishLabel.gameObject.SetActive(false);
        if (countdown != null) countdown.text = "";
        foreach (var segment in resultSegments) if (segment != null) segment.color = EmptyColor;
        SetMotion(false);
        RefreshStatus();
        UpdateEffects(0);
    }

    /// <summary>
    /// 한 프레임의 순서: UI 연출 → 대기/완료 상태 처리 → 전체 노트 이동 → 생성 보충
    /// → 지나간 노트 정리 → 키 입력 판정 → 카운트다운.
    /// 이동을 위한 foreach가 끝난 뒤에만 큐에 노트를 추가하거나 제거한다.
    /// </summary>
    private void Update()
    {
        if (!ready) return;
        float dt = Time.deltaTime;
        uiAge += dt;
        feedbackLeft = Mathf.Max(0, feedbackLeft - dt);
        UpdateEffects(dt);
        var keyboard = Keyboard.current;
        if (finished)
        {
            if (isTest && keyboard != null && keyboard.rKey.wasPressedThisFrame) ResetRound();
            return;
        }
        if (!playing)
        {
            // 팝업이 사라져도 게임은 시작 대기 상태이고 Space/A/K로 시작할 수 있다.
            if (introPanel != null && uiAge >= 4) introPanel.SetActive(false);
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.aKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)) StartGame();
            return;
        }

        // 실제 시간과 배속이 들어간 시간을 분리한다. 음악 재생 시간은 참조하지 않는다.
        elapsed += dt;
        float advance = dt * SpeedMultiplier;
        trackTime += advance;
        float movement = baseSpeed * advance;
        // 이번 프레임에는 화면의 모든 노트를 같은 거리만큼 내린다.
        // 아래 판정에서 콤보가 바뀌면 다음 이동 프레임부터 새 배속을 사용한다.
        foreach (var queue in lanes)
            foreach (var note in queue) note.Rect.anchoredPosition += Vector2.down * movement;

        // N번째 노트의 생성 시점 = N * spawnInterval(이동 시계 기준).
        // 예: 간격 0.6초 / 배속 1.5 = 실제 0.4초 간격. 노트 사이의 기본 공간 간격은 유지된다.
        // 긴 프레임에 생성 시점을 여러 번 지났다면 while로 빠진 노트를 모두 보충한다.
        while (spawned < target && trackTime >= spawned * spawnInterval)
        {
            float overdue = trackTime - spawned * spawnInterval;
            SpawnNote(ShakingRhythmRules.Lane(spawned), overdue);
        }
        // 입력 전에 이미 허용 범위를 지난 노트를 빼야 늦은 입력으로 성공 처리되지 않는다.
        CheckMiss(0);
        CheckMiss(1);
        if (!playing) return;
        if (keyboard != null)
        {
            // 두 키를 별도로 확인하므로 한 프레임에 두 레인을 모두 입력할 수 있다.
            if (keyboard.aKey.wasPressedThisFrame) Judge(0);
            if (keyboard.dKey.wasPressedThisFrame) Judge(1);
        }
        // 카운트다운의 구간 길이 0.6초는 안내용 고정값이며 생성 간격 필드와 별개다.
        if (countdown != null)
            countdown.text = playing && elapsed < leadSeconds ? Mathf.Clamp(3 - Mathf.FloorToInt(elapsed / 0.6f), 1, 3).ToString() : "";
    }

    /// <summary>
    /// Space/A/D, 시작 팝업 또는 외부 코드에서 호출하는 시작 진입점.
    /// 준비가 안 됐거나 이미 시작/완료된 상태에서는 재실행하지 않는다.
    /// 시작 입력 자체는 노트 판정으로 소비하지 않는다.
    /// </summary>
    public void StartGame()
    {
        if (!ready || playing || finished) return;
        playing = true;
        if (introPanel != null) introPanel.SetActive(false);
        characterAnim?.Init();
        if (characterAnimator != null) characterAnimator.Play("Play", 0, 0);
        SetMotion(true);
        // 배경음은 선택 사항이다. 옛 메트로놈 연결을 제거했으므로 새 음악을 지정한 경우에만 재생한다.
        // 재생 시점/위치로 노트 시계를 보정하지 않는다.
        if (bgmSource != null && bgmSource.clip != null)
        {
            audioOutput.Route(bgmSource, AudioBus.Music);
            bgmSource.Play();
        }
    }

    /// <summary>왼쪽 UI 버튼/외부 입력용. 대기 중이면 시작, 플레이 중이면 A 레인 판정.</summary>
    public void LeftInput() { if (!playing && !finished) StartGame(); else Judge(0); }
    /// <summary>오른쪽 UI 버튼/외부 입력용. 대기 중이면 시작, 플레이 중이면 D 레인 판정.</summary>
    public void RightInput() { if (!playing && !finished) StartGame(); else Judge(1); }

    /// <summary>씬의 샘플을 해당 레인에 복제하고 큐의 마지막에 등록한다.</summary>
    /// <param name="lane">0 = A, 1 = D. 생성 순서는 ShakingRhythmRules의 패턴으로 결정한다.</param>
    /// <param name="overdue">
    /// 예정 생성 시점부터 이미 지난 이동 시계의 초. 실제 초가 아니라 배속이 반영된 값이다.
    /// </param>
    private void SpawnNote(int lane, float overdue)
    {
        var image = Instantiate(noteTemplates[lane], laneRoots[lane], false);
        image.name = "Note " + spawned;
        // 생성이 프레임 경계 때문에 늦어졌다면 그동안 내려왔어야 할 위치부터 시작한다.
        // 보정하지 않으면 긴 프레임에서 보충한 노트들이 생성점에 겹치게 된다.
        image.rectTransform.anchoredPosition = spawnPoints[lane].anchoredPosition + Vector2.down * (overdue * baseSpeed);
        image.gameObject.SetActive(true);
        // 선택 장식인 Trail은 생성 시 한 번만 찾고, 이후에는 저장한 참조로 갱신한다.
        var trail = image.transform.Find("Trail");
        lanes[lane].Enqueue(new LiveNote { Rect = image.rectTransform, Trail = trail != null ? trail.GetComponent<Image>() : null });
        spawned++;
        UpdateTrails();
    }

    /// <summary>
    /// 입력한 레인의 첫 노트만 검사한다. 다른 레인의 노트를 대신 처리하지 않는다.
    /// 빈 레인/범위 밖 입력은 빈 입력 Miss로 기록하고 노트는 큐에 남겨 둔다.
    /// </summary>
    private void Judge(int lane)
    {
        if (!playing) return;
        if (lanes[lane].Count == 0) { Record(false, "MISS", lane, false); return; }
        // 현재 위치 차이 / 현재 속도 = 도착 시점까지의 추정 시간 차이(초).
        // 절댓값을 쓰므로 일찍/늦게 누른 입력 모두 같은 범위로 검사한다.
        // 고정 거리 대신 시간을 비교해 빠른 단계에서도 ±75ms / ±180ms를 유지한다.
        float seconds = Mathf.Abs(lanes[lane].Peek().Rect.anchoredPosition.y - judgementLines[lane].anchoredPosition.y) / CurrentSpeed;
        if (seconds > hitWindowSeconds) { Record(false, "MISS", lane, false); return; }
        // 허용 범위 안이면 먼저 큐에서 빼고 성공을 기록한다. 재입력으로 중복 판정되지 않는다.
        RemoveNote(lane);
        Record(true, seconds <= perfectSeconds ? "PERFECT" : "GOOD", lane, true);
    }

    /// <summary>
    /// 판정선 아래로 지나간 첫 노트를 Miss로 소비한다. 긴 프레임에는 여러 개를 한 번에 정리한다.
    /// 판정선 - 노트 Y가 양수일 때만 늦은 방향이므로 아직 위에 있는 노트는 처리하지 않는다.
    /// </summary>
    private void CheckMiss(int lane)
    {
        while (playing && lanes[lane].Count > 0 &&
            (judgementLines[lane].anchoredPosition.y - lanes[lane].Peek().Rect.anchoredPosition.y) / CurrentSpeed > hitWindowSeconds)
        {
            RemoveNote(lane);
            Record(false, "MISS", lane, true);
        }
    }

    /// <summary>한 번의 판정을 진행 수, 콤보, 실패 수, UI, 캐릭터 동작에 반영하는 공통 지점.</summary>
    /// <param name="success">Perfect/Good이면 true, 놓침/빈 입력이면 false.</param>
    /// <param name="judgement">판정 문구와 Perfect 링의 색을 결정하는 문자열.</param>
    /// <param name="lane">판정 연출을 보여줄 레인.</param>
    /// <param name="consumesNote">
    /// 실제 노트를 맞히거나 놓쳤으면 true. 빈 입력은 false이며 진행 수 대신 점수 분모를 늘린다.
    /// </param>
    private void Record(bool success, string judgement, int lane, bool consumesNote)
    {
        if (consumesNote)
        {
            if (processed < resultSegments.Length && resultSegments[processed] != null)
                resultSegments[processed].color = success ? Cyan : MissColor;
            processed++;
        }
        else extraPresses++;
        // 실패 종류에 관계없이 콤보는 즉시 끊긴다. Multiplier도 콤보에서 읽으므로 배속이 복귀한다.
        if (success) { successes++; combo++; bestCombo = Mathf.Max(bestCombo, combo); sound?.PlaySE("SFX_shaker_hit"); }
        else { failures++; combo = 0; }
        hitAges[lane] = 0;
        hitColors[lane] = success ? (judgement == "PERFECT" ? Color.white : Cyan) : MissColor;
        feedback.text = judgement;
        feedback.color = success ? Cyan : MissColor;
        feedbackLeft = 0.42f;
        // 실패하면 캐릭터를 현재 프레임에서 멈추고, 다음 성공 시 현재 콤보 배속으로 이어 재생한다.
        SetMotion(success);
        UpdateTrails();
        RefreshStatus();
        // 종료 기준은 실제 노트 처리 수다. 빈 입력을 많이 해도 목표 노트를 모두 처리해야 끝난다.
        if (processed >= target) CompleteMade();
    }

    /// <summary>캐릭터 루프를 멈추거나 배속으로 재개한다. 노트 시계는 멈추지 않는다.</summary>
    private void SetMotion(bool active)
    {
        if (characterAnimator != null) characterAnimator.speed = active ? SpeedMultiplier : 0;
    }

    /// <summary>진행 수/콤보/단계 문구를 갱신한다. 초기화와 판정 직후에 호출한다.</summary>
    private void RefreshStatus()
    {
        status.text = ProgressText;
        comboLabel.text = combo.ToString();
        int tier = ShakingRhythmRules.Tier(combo);
        comboLabel.color = tier > 0 ? Gold : Cyan;
        if (heatLabel != null) heatLabel.text = tier == 2 ? "HEAT II / x1.50" : tier == 1 ? "HEAT I / x1.25" : "";
    }

    /// <summary>현재 큐의 잔상 표시를 콤보 단계에 맞춘다. 큐 항목을 추가/제거하지 않는다.</summary>
    private void UpdateTrails()
    {
        int tier = ShakingRhythmRules.Tier(combo);
        foreach (var queue in lanes)
            foreach (var note in queue)
                if (note.Trail != null)
                {
                    note.Trail.gameObject.SetActive(tier > 0);
                    note.Trail.rectTransform.sizeDelta = new Vector2(6, 22 * tier);
                }
    }

    /// <summary>
    /// 씬에 배치된 연출의 색/위치/크기만 바꾼다. 연출용 GameObject를 추가 생성하지 않는다.
    /// 판정 문구와 퍼지는 링은 실제 시간, 속도선은 이동 시계를 사용한다.
    /// </summary>
    /// <param name="dt">실제 프레임 시간(초). ResetRound에서는 0으로 즉시 초기 표시를 적용한다.</param>
    private void UpdateEffects(float dt)
    {
        // 완료 화면에는 최고 콤보가 남더라도 움직이는 속도 연출은 기본 단계로 정리한다.
        int tier = playing ? ShakingRhythmRules.Tier(combo) : 0;
        Color accent = tier == 2 ? Gold : Cyan;
        Color verdict = feedback.color;
        // 마지막 0.16초에만 투명도를 줄여 문구가 서서히 사라지게 한다.
        verdict.a = Mathf.Clamp01(feedbackLeft / 0.16f);
        feedback.color = verdict;
        foreach (var targetFace in targetFaces) if (targetFace != null) targetFace.color = accent;
        foreach (var edge in edgeLights)
            if (edge != null) edge.color = new Color(accent.r, accent.g, accent.b, 0.08f + tier * 0.12f);
        if (comboGlow != null) comboGlow.color = new Color(Gold.r, Gold.g, Gold.b, tier * 0.13f);
        for (int i = 0; i < speedLines.Length; i++)
        {
            if (speedLines[i] == null) continue;
            // 0~1 반복 위상을 각 선마다 다르게 줘 같은 위치에 몰리지 않게 한다.
            // 144와 276은 현재 임시 디자인의 속도선 시작 Y와 이동 길이(UI 단위)다.
            // 레인 레이아웃을 크게 바꾸면 이 장식 범위도 함께 조절해야 한다.
            float phase = Mathf.Repeat(trackTime * 0.7f + i * 0.173f, 1);
            var rect = speedLines[i].rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 144 - phase * 276);
            speedLines[i].color = new Color(accent.r, accent.g, accent.b,
                tier == 0 || (tier == 1 && i >= 4) ? 0 : Mathf.Sin(phase * Mathf.PI) * 0.28f);
        }
        for (int lane = 0; lane < 2; lane++)
        {
            hitAges[lane] += dt;
            if (lane >= hitRings.Length || hitRings[lane] == null) continue;
            // 최근 판정 후 0.5초 동안 링을 1→2.5배로 키우면서 1→0으로 투명하게 만든다.
            float t = Mathf.Clamp01(hitAges[lane] / 0.5f);
            var color = hitColors[lane];
            color.a = 1 - t;
            hitRings[lane].color = color;
            hitRings[lane].rectTransform.localScale = Vector3.one * (1 + t * 1.5f);
        }
    }

    /// <summary>큐의 첫 노트를 제거하고 화면에서도 즉시 숨긴 뒤 프레임 종료 시 파괴한다.</summary>
    private void RemoveNote(int lane)
    {
        var note = lanes[lane].Dequeue();
        // Destroy는 즉시 파괴하지 않으므로 먼저 비활성화한다. 큐에서는 이미 Dequeue한 상태다.
        note.Rect.gameObject.SetActive(false);
        Destroy(note.Rect.gameObject);
    }
    /// <summary>초기화/완료/비활성화 시 남아 있는 생성 노트를 모두 정리한다. 샘플은 큐에 없다.</summary>
    private void ClearNotes()
    {
        for (int lane = 0; lane < 2; lane++) while (lanes[lane].Count > 0) RemoveNote(lane);
    }

    /// <summary>
    /// 이번 판을 한 번만 확정한다. 자동 완료와 EndNow가 공유하며, 외부 await의 완료 신호를 보낸다.
    /// 프리팹을 여기서 파괴하지는 않는다. 외부 GimmickRunner가 반환 결과를 받은 후 정리한다.
    /// </summary>
    public void CompleteMade()
    {
        if (finished) return;
        playing = false;
        finished = true;
        SetMotion(false);
        bgmSource?.Stop();
        ClearNotes();
        if (countdown != null) countdown.text = "";
        if (finishLabel != null)
        {
            finishLabel.gameObject.SetActive(true);
            // 점수 = 성공 수 / (목표 노트 수 + 빈 입력 수) * 100.
            // 모든 노트를 맞혀도 빈 입력이 있으면 ALL CLEAR가 아니고 점수도 감소한다.
            finishLabel.text = (successes == target && extraPresses == 0 ? "ALL CLEAR" : "SHAKE COMPLETE") +
                $"\nBEST {bestCombo} / SCORE {100f * successes / (target + extraPresses):0}" + (isTest ? "\nR : RESTART" : "");
        }
        // 독립 씬에서는 null이라 UI만 완료된다. 외부 실행 중이면 PlayAsync의 대기를 푼다.
        runnerCompletion?.TrySetResult();
    }
    /// <summary>공통 HUD/외부 코드의 조기 종료 진입점. 남은 노트를 성공으로 채워 주지 않는다.</summary>
    public void EndNow() { if (CanEndNow) { endedManually = true; CompleteMade(); } }

    /// <summary>
    /// 제조 실행기가 새 프리팹을 띄운 뒤 호출하는 외부 계약. 시작 버튼을 대신 누르지 않는다.
    /// 완료 신호를 기다렸다가 결과를 반환한다. Start 이전에 호출될 수 있으므로 여기서 초기화하지 않는다.
    /// 결과 저장은 호출자인 CraftFlowController의 session.Actual.Record(result)가 담당한다.
    /// </summary>
    /// <param name="step">공통 기믹 정보. 현재 쉐이킹의 노트 수/판정/속도를 바꾸는 데 사용하지 않는다.</param>
    /// <param name="context">칵테일/잔/도구/재료 문맥. 공통 계약으로 받지만 현재 연출에는 반영하지 않는다.</param>
    /// <param name="timer">제조 전체 누적 조작 시간. GimmickRunner가 IsManualInputActive를 보고 갱신한다.</param>
    /// <param name="token">제조 흐름 취소 시 대기를 취소한다. 프리팹 정리는 실행기의 finally에서 수행한다.</param>
    /// <returns>성공/실패, 점수 분모, 종료 방식, 제조 전체 종료 시각을 담은 GimmickResult.</returns>
    public async UniTask<GimmickResult> PlayAsync(GimmickStep step, CraftContext context,
        CraftTimer timer, CancellationToken token)
    {
        craftTimer = timer;
        runnerCompletion = new UniTaskCompletionSource();
        // 완료되거나 취소되면 using이 취소 등록을 해제한다. 취소 시 아래 결과 반환까지 진행하지 않는다.
        using (token.Register(() => runnerCompletion.TrySetCanceled(token))) await runnerCompletion.Task;
        // TargetStackCount는 이 버전에서 점수 분모 역할도 하므로 빈 입력 수를 더해 전달한다.
        // 예: 20노트 성공 + 빈 입력 2회 → CompletionScore = 20/22. processed 목표는 여전히 20이다.
        // 최대 콤보는 종료 UI 전용이며 현재 GimmickResult 계약에는 포함되지 않는다.
        return GimmickResult.Mix(ECraftGimmick.Shake, successes, failures, target + extraPresses,
            endedManually ? ECraftEndType.ManualNext : ECraftEndType.AutoTarget, craftTimer != null ? craftTimer.ElapsedSec : 0);
    }
    /// <summary>
    /// 외부 실행기가 프리팹을 치우거나 오브젝트가 비활성화될 때의 정리.
    /// 대기 중인 실행기가 남지 않도록 취소 신호를 보내고, Animator 배속도 기본값으로 돌린다.
    /// 정상 완료 후에는 이미 완료된 신호이므로 TrySetCanceled가 결과를 덮어쓰지 않는다.
    /// </summary>
    private void OnDisable()
    {
        playing = false;
        ClearNotes();
        bgmSource?.Stop();
        if (characterAnimator != null) characterAnimator.speed = 1;
        runnerCompletion?.TrySetCanceled();
    }
}
