using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 따르기(Pour) 미니게임의 메인 매니저. IMiniGameController를 구현한다.
///
/// 병 안은 비어 있다 — 액체를 병에 채워두고 물리로 흘려보내는 게 아니라, 기울기가 임계각을 넘으면
/// 입구에서 파티클을 뿜어내는 방출기다. 병 안 액체를 시뮬레이션하면 "몇 도부터 나오는지"를
/// 수면 높이가 정해버려서(액체가 줄면 그 각도가 계속 올라간다) 확정적인 조작감을 만들 수 없고,
/// 보이지도 않을 액체에 물리 비용을 그대로 쓴다. 방출기는 임계각이 곧 설정값이고, 살아있는
/// 파티클도 공중에 떠 있는 줄기와 잔에 담긴 것뿐이다.
///
/// 인터페이스를 둘 구현한다. ICraftGimmick은 1부의 기믹 큐가 부르는 새 경로이고,
/// IMiniGameController는 2부 대화에서 컷씬과 함께 돌던 기존 경로다. 조작과 액체 표현은 양쪽이 공유한다.
///
/// 투입량을 재는 방식이 다르다. 기존 경로는 잔 영역에 들어온 파티클 개수를 셌지만, 기믹 큐가
/// 돌릴 때는 방출률(ml/초) × 기울인 시간으로 쌓아 레시피의 oz와 같은 단위로 만든다. 파티클은
/// 눈에 보이는 줄기일 뿐이라 프레임과 물리 설정에 따라 개수가 흔들리는데, 그 흔들림이 점수에
/// 섞이면 안 되기 때문이다. 이 화면에는 잔을 그리지도 않는다.
///
/// 필업(Fill-up)도 이 구현을 그대로 쓴다. 조작과 화면이 같고 큐에서의 위치만 다르다.
/// </summary>
public class PourManager : MonoBehaviour, IMiniGameController, ICraftGimmick,
                           ICraftGimmickProgress, ICraftGimmickManualEnd
{
    [SerializeField] bool isTest = false;

    [Header("Data")]
    [SerializeField] CraftStationData data;
    [SerializeField] CategoryColorData colorData;
    [SerializeField] NewCocktailDataSO cocktailDataSO;
    [Tooltip("실제로 따르는 재료의 액체 색을 읽어온다.")]
    [SerializeField] NewShelfItemDataSO shelfData;
    [Tooltip("pour_emit_rate_ml_per_sec(방출률)와 단위 환산 계수를 읽어온다.")]
    [SerializeField] NewBalanceDataSO balanceData;

    [Header("Bottle")]
    [SerializeField] BottleTiltController bottle;
    [Tooltip("병 외곽선을 그리는 컴포넌트. 아래 병 모양 값을 그대로 받아 그리므로 " +
             "보이는 입구와 액체가 나오는 위치가 항상 일치한다. 비워두면 외곽선을 그리지 않는다.")]
    [SerializeField] BottleSilhouette bottleSilhouette;
    [Tooltip("병 몸통 내부의 절반 크기(스케일 적용 전 병 로컬 좌표). 병을 그리는 데만 쓰인다 — " +
             "안에 액체가 없으므로 여기에 가두는 벽은 없다.")]
    [SerializeField] Vector2 bottleInteriorHalfExtents = new Vector2(0.4f, 0.4f);

    // ── 병목 ────────────────────────────────────────────────────────────
    // 아래에서부터 몸통 → 어깨(사선) → 통로다. 액체가 나오는 입구 폭이자 병을 그리는 모양이다.
    [Range(2f, 5f)]
    [Tooltip("병목 통로의 폭을 파티클 몇 개로 잡을지. 그대로 물줄기의 굵기가 된다.\n" +
             "  2~3개: 한 줄기로 가늘게 (권장)\n" +
             "  4개 이상: 콸콸 쏟아지는 굵은 줄기\n" +
             "길이가 아니라 개수인 이유는 이 값이 프로파일의 spacing과 짝이기 때문이다 — " +
             "2개 폭이 안 되면 줄기가 한 줄로 늘어서 알갱이처럼 끊어져 보인다.")]
    [SerializeField] float neckWidthInParticles = 2.5f;
    [Range(0f, 1f)]
    [Tooltip("어깨가 시작되는 높이(몸통의 끝). 0=바닥, 1=입구. 이 아래는 몸통 너비 그대로다.")]
    [SerializeField] float neckStartHeight01 = 0.6f;
    [Range(0f, 1f)]
    [Tooltip("어깨가 끝나고 통로가 시작되는 높이. neckStartHeight01 ~ 이 값 사이에서 몸통 너비가 " +
             "통로 너비까지 사선으로 좁아지고, 그 위로는 입구까지 같은 폭이 유지된다.")]
    [SerializeField] float neckChannelHeight01 = 0.75f;

    // ── 배출 ────────────────────────────────────────────────────────────
    [Header("Pour Gate")]
    [Tooltip("이 각도 미만에서는 한 방울도 나오지 않는다. 병 안 액체를 시뮬레이션하지 않으므로 " +
             "이 값이 곧 임계각이다 — 원하는 각도를 그대로 적으면 된다.")]
    [SerializeField] float pourStartAngle = 45f;
    [Tooltip("유량이 최대가 되는 각도. pourStartAngle과 벌려둘수록 졸졸 → 콸콸로 부드럽게 이어지고, " +
             "붙여두면 임계각을 넘는 순간 최대 유량으로 쏟아진다.\n" +
             "BottleTiltController의 maxTiltAngle보다 낮아야 최대 유량에 도달할 수 있다.")]
    [SerializeField] float pourFullOpenAngle = 75f;
    [Tooltip("최대 유량에서 액체가 입구를 떠나는 속도. 올리면 멀리 뻗어 나가고, 낮추면 입구에서 " +
             "바로 떨어진다. 방출 간격이 이 속도에서 유도되므로 줄기 굵기는 그대로다.")]
    [SerializeField] float exitSpeed = 1.6f;

    [Header("Glass")]
    [Tooltip("잔 스프라이트 자신의 Transform. InverseTransformPoint로 로컬 판정 영역을 계산한다.")]
    [SerializeField] Transform glassCenter;
    [Tooltip("잔에 담긴 것으로 칠 판정 영역(스케일 적용 전 잔 로컬 좌표).")]
    [SerializeField] Vector2 glassInteriorHalfExtents = new Vector2(0.45f, 0.45f);

    [Header("Liquid (SPH)")]
    [SerializeField] SphLiquidRenderer liquidRenderer;
    [Tooltip("액체의 물성과 질감 묶음. 술마다 다른 프로파일을 꽂으면 물처럼 찰랑이는 것부터 " +
             "시럽처럼 늘어지는 것까지 다르게 표현된다.")]
    [SerializeField] LiquidProfile liquidProfile;
    [Tooltip("켜면 병이 마르지 않는다(테스트용). 아래 총량을 무시하고 계속 쏟을 수 있다.")]
    [SerializeField] bool unlimitedLiquid = false;
    [Tooltip("병에 든 액체의 총량(파티클 수). 이만큼 다 쏟으면 더 나오지 않는다. " +
             "unlimitedLiquid가 켜져 있으면 무시된다.")]
    [SerializeField] int bottleParticleCount = 400;
    [Tooltip("동시에 살아있을 수 있는 파티클 수(줄기 + 잔에 담긴 것). 재사용 풀 크기라 총량과 별개이고, " +
             "'한 번에 얼마나 많은 액체가 보이는가'는 총량이 아니라 이 값이 정한다.\n" +
             "렌더러가 2패스로 바뀐 뒤로는 여기에 렌더 한계가 없다 — 이제 걸리는 건 SPH 계산 쪽이다. " +
             "프레임이 떨어지면 이 값을 내리거나 프로파일의 spacing을 키울 것.")]
    [SerializeField] int maxLiveParticles = 2000;
    [Tooltip("이 깊이 아래로 떨어진 파티클은 흘린 것으로 보고 치운다(풀로 돌아간다). " +
             "이 기믹의 원점에서 잰 상대 높이라, 무대가 어디에 놓이든 같게 동작한다. " +
             "잔 바닥보다 확실히 아래로 잡아야 담긴 액체가 사라지지 않는다.")]
    [SerializeField] float despawnBelowY = -6f;
    [Tooltip("SPH 공간분할그리드가 병/잔 위치 기준 사방으로 확보하는 여유 폭.")]
    [SerializeField] float gridPadding = 3f;

    [Header("UI")]
    [SerializeField] Canvas buttonCanvas;
    [SerializeField] GradientRatioController gageBar;

    [Header("Pour Settings")]
    [Tooltip("isTest일 때 목표 파티클 개수로 사용. 실제 모드에서도 recipe 기반 목표 연결 전까지는 임시로 이 값을 쓴다.")]
    [SerializeField] int testTargetParticleCount = 60;
    [SerializeField] int testToleranceCount = 15;

    [Header("Craft Event")]
    [SerializeField] VoidEvent craftServe;
    [SerializeField] VoidEvent craftRetry;

    int targetParticleCount;
    int toleranceCount;
    int glassParticleCount;
    bool isPlay;
    bool isFinished;

    SphSimulation simulation;
    float simulationAccumulator;

    /// <summary>비활성 파티클 보관소. 흘려서 사라진 파티클을 재사용한다.</summary>
    readonly Stack<SphParticle> pool = new Stack<SphParticle>();
    readonly List<SphParticle> live = new List<SphParticle>();

    /// <summary>병에 남은 액체(파티클 수). 0이 되면 더 나오지 않는다.</summary>
    int remainingInBottle;
    float emitAccumulator;

    /// <summary>병목 통로의 반너비(병 로컬). spacing에서 유도되므로 Start에서 한 번만 잡는다.</summary>
    float neckHalfWidth;

    /// <summary>프로파일의 물성값을 짧게 쓰기 위한 별칭.</summary>
    SphConfig Physics => liquidProfile.physics;
    float Spacing => liquidProfile.physics.spacing;

    UniTaskCompletionSource tcs;

    void Start()
    {
        if (isTest)
        {
            if (cocktailDataSO.TryGet(data.targetCocktailId, out NewCocktailData cocktail))
                data.targetCocktailData = cocktail;
            else
                Debug.LogWarning($"[Pour] 테스트 칵테일 '{data.targetCocktailId}'를 찾지 못했습니다.");

            data.targetCraft_tolerance = testToleranceCount;
        }

        // 파티클 개수 목표는 기존 경로에서만 쓴다. 기믹 큐가 돌릴 때는 레시피의 oz를 그대로 목표로 삼고,
        // 파티클은 눈에 보이는 줄기 역할만 한다.
        targetParticleCount = testTargetParticleCount;

        int tolerance = data != null ? data.targetCraft_tolerance : 0;
        toleranceCount = tolerance > 0 ? tolerance : testToleranceCount;

        mlPerSecondAtFullFlow = ResolveEmitRateMlPerSec();
        pouredMl = 0f;

        isFinished = false;
        glassParticleCount = 0;
        remainingInBottle = bottleParticleCount;

        Color liquidColor = GetLiquidColor();

        // 액체는 화면을 덮는 쿼드에 합성해 그린다. 어느 화면인지 알려 주지 않으면 메인 카메라에
        // 그려지는데, 기믹 무대는 바에서 멀리 떨어져 있어 그러면 아무것도 안 보인다.
        liquidRenderer.SetTargetCamera(CraftGimmickStage.Find(this));
        liquidRenderer.Init(liquidProfile, liquidColor);

        // 통로 폭은 파티클 간격이 정한다. 병 모양보다 먼저 잡아야 실루엣이 같은 폭으로 그려진다.
        float scaleX = Mathf.Max(Mathf.Abs(bottle.BottleVisual.lossyScale.x), 0.0001f);
        neckHalfWidth = Spacing * neckWidthInParticles * 0.5f / scaleX;

        if (bottleSilhouette != null)
            bottleSilhouette.Build(bottleInteriorHalfExtents, neckHalfWidth, ShoulderLocalY(), ChannelLocalY());

        simulation = new SphSimulation(Physics, ComputeGridMin(), ComputeGridMax());

        CreatePool();
        CalibrateRestDensity();

        // 이 게이지는 파티클 개수를 기준으로 그린다. 큐가 돌릴 때는 투입량을 방출량으로 재기 때문에
        // 눈금이 실제 수량과 어긋난다 — 공통 표시가 정확한 수치를 보여주므로 여기서는 감춘다.
        if (drivenByRunner)
        {
            if (gageBar != null) gageBar.gameObject.SetActive(false);
        }
        else
        {
            gageBar.UpdateValues(targetParticleCount + toleranceCount,
                                 targetParticleCount - toleranceCount, toleranceCount * 2f, 0f);
        }

        isPlay = true;
    }

    Vector2 ComputeGridMin()
    {
        Vector2 bottlePos = bottle.BottleVisual.position;
        Vector2 glassPos = glassCenter.position;
        return Vector2.Min(bottlePos, glassPos) - Vector2.one * gridPadding;
    }

    Vector2 ComputeGridMax()
    {
        Vector2 bottlePos = bottle.BottleVisual.position;
        Vector2 glassPos = glassCenter.position;
        return Vector2.Max(bottlePos, glassPos) + Vector2.one * gridPadding;
    }

    // ── 파티클 풀 ───────────────────────────────────────────────────────

    Transform particlesRoot;

    void CreatePool()
    {
        particlesRoot = new GameObject("Sph Particles").transform;
        particlesRoot.SetParent(transform, false);

        int size = Mathf.Max(1, maxLiveParticles);

        for (int i = 0; i < size; i++)
        {
            var go = new GameObject("Particle");
            go.transform.SetParent(particlesRoot, false);

            var particle = go.AddComponent<SphParticle>();
            particle.Init(Physics);
            particle.Deactivate();

            pool.Push(particle);
        }
    }

    /// <summary>
    /// 기준 밀도는 "정지 격자에서의 밀도"라 실측이 필요한데, 이제 병 안에 상주하는 액체가 없다.
    /// 그래서 잠깐 격자를 깔아 재고 바로 치운다. 값을 절대값으로 적어두면 spacing을 조금만 바꿔도
    /// 기준이 어긋나 액체가 부풀거나 눌린다.
    /// </summary>
    void CalibrateRestDensity()
    {
        const int side = 5; // 가운데 파티클이 이웃을 빠짐없이 갖는 최소 크기

        // 공간 격자가 덮는 범위 안에 깔아야 이웃 탐색이 제대로 돈다.
        Vector2 center = bottle.BottleVisual.position;

        var temporary = new List<SphParticle>();

        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                if (pool.Count == 0) break;

                SphParticle p = pool.Pop();
                p.Activate(center + new Vector2(x - (side - 1) * 0.5f, y - (side - 1) * 0.5f) * Spacing);

                simulation.Register(p);
                temporary.Add(p);
            }
        }

        simulation.CalibrateRestDensity();

        foreach (var p in temporary)
        {
            simulation.Unregister(p);
            p.Deactivate();
            pool.Push(p);
        }
    }

    // ── 방출 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 지금 기울기의 유량(0=안 나옴, 1=최대). 이 값이 곧 "몇 도부터 나오는가"를 정한다 —
    /// 병 안 수면을 시뮬레이션하지 않으므로 기하학이 끼어들지 않는다.
    /// </summary>
    float FlowRate01()
    {
        float fullOpen = Mathf.Max(pourFullOpenAngle, pourStartAngle + 0.01f);
        return Mathf.Clamp01(Mathf.InverseLerp(pourStartAngle, fullOpen, bottle.CurrentAngle));
    }

    /// <summary>
    /// 입구에서 액체를 뿜는다. 방출 간격을 속도에서 유도하는 게 핵심이다 — 간격을 고정값으로 두면
    /// 유량이 늘 때 파티클이 겹쳐 뭉치거나, 반대로 벌어져 알갱이로 끊어진다.
    /// "파티클이 spacing만큼 이동할 때마다 한 줄"이라야 줄기가 굵기를 유지한 채 이어진다.
    /// </summary>
    void EmitFromNeck(float dt)
    {
        float flow01 = FlowRate01();

        // 투입량은 파티클 개수가 아니라 "얼마나 오래, 얼마나 세게 기울였는가"로 쌓는다.
        // 파티클은 눈에 보이는 줄기일 뿐이라 프레임이나 물리 설정에 따라 개수가 흔들리는데,
        // 방출률 × 시간은 그런 것에 흔들리지 않고 레시피의 oz 단위로 바로 환산된다.
        // 이 함수가 물리와 같은 고정 시간축(fixedStep)에서 돌기 때문에 프레임이 튀어도 값이 안 변한다.
        if (flow01 > 0f && (unlimitedLiquid || remainingInBottle > 0))
            pouredMl += mlPerSecondAtFullFlow * flow01 * dt;

        if (flow01 <= 0f || (!unlimitedLiquid && remainingInBottle <= 0))
        {
            emitAccumulator = 0f;
            return;
        }

        float speed = exitSpeed * flow01;
        if (speed <= 0.001f) return;

        float interval = Spacing / speed;

        emitAccumulator += dt;

        // 물리 스텝 하나에 여러 줄이 나가는 건 유량이 아주 높을 때뿐이라 보통은 0~1줄이다.
        // 그래도 상한을 둬서 어떤 설정에서도 한 스텝이 뭉텅이가 되지 않게 막는다.
        int emitted = 0;
        const int maxRowsPerStep = 4;

        while (emitAccumulator >= interval && emitted < maxRowsPerStep)
        {
            emitAccumulator -= interval;

            // 한 프레임에 여러 줄이 나갈 때는 그 줄들이 원래 프레임 '중간중간'에 나갔어야 한다.
            // 전부 입구에 그대로 놓으면 같은 자리에 겹쳐 쌓여 밀도가 폭발하고, 압력이 사방으로
            // 튕겨낸다(입구에서 가끔 튀는 원인). 밀린 시간만큼 진행 방향으로 미리 보내주면
            // 줄 간격이 정확히 spacing이 되어 그냥 이어진 줄기가 된다.
            EmitRow(speed, emitAccumulator);
            emitted++;
        }

        if (emitted >= maxRowsPerStep)
            emitAccumulator = 0f;
    }

    /// <summary>
    /// 입구 폭을 가로질러 한 줄 분량을 내보낸다. 줄기 굵기는 이 줄의 파티클 수가 정한다.
    /// age는 이 줄이 원래 나갔어야 할 시점부터 지금까지의 시간이다(0이면 지금 막 나간 줄).
    /// </summary>
    void EmitRow(float speed, float age)
    {
        Transform container = bottle.BottleVisual;

        int lanes = Mathf.Max(1, Mathf.FloorToInt(neckWidthInParticles));
        float laneStep = neckHalfWidth * 2f / lanes;

        // 입구는 병 로컬 +Y 끝이고, 액체는 그 바깥을 향해 나간다.
        Vector2 outward = container.up;

        // 밀린 시간만큼 미리 날아간 상태로 놓는다. 위치뿐 아니라 속도도 그동안 중력을 받았어야
        // 맞다 — 위치만 옮기고 속도를 그대로 두면 뒤따르는 줄과 속도가 어긋나 그 지점에서 뭉친다.
        Vector2 headStart = outward * (speed * age);
        Vector2 gravityGain = Vector2.down * (Physics.gravity * age);

        for (int i = 0; i < lanes; i++)
        {
            if ((!unlimitedLiquid && remainingInBottle <= 0) || pool.Count == 0) return;

            float localX = (i - (lanes - 1) * 0.5f) * laneStep;

            // 격자처럼 딱 떨어지면 줄기가 기계적으로 보인다. 아주 조금 흔들어준다.
            float jitter = Random.Range(-0.15f, 0.15f) * laneStep;

            Vector3 localPos = new Vector3(localX + jitter, bottleInteriorHalfExtents.y, 0f);
            Vector2 worldPos = (Vector2)container.TransformPoint(localPos) + headStart;

            SphParticle particle = pool.Pop();
            particle.Activate(worldPos);
            particle.velocity = outward * speed + gravityGain;

            simulation.Register(particle);
            live.Add(particle);

            remainingInBottle--;
        }
    }

    void Update()
    {
        // 물리는 판정이 끝난 뒤에도 계속 돌린다. 여기서 멈춰버리면 공중에 있던 액체가 그대로 얼어붙어
        // 물리 버그처럼 보인다 — 판정만 멈추고 액체는 잔으로 마저 떨어지게 둔다.
        // 방출은 StepSimulation 안에서 물리와 같은 시간축으로 돈다(아래 주석 참고).
        StepSimulation();
        liquidRenderer.UpdateBlobs(simulation.ActiveParticles);

        if (!isPlay) return;

        glassParticleCount = CountParticlesInGlass();

        // 기믹 큐가 돌릴 때는 목표를 넘겨도 저절로 끝나지 않는다. 초과한 양까지 결과에 담고,
        // 끝내는 시점은 플레이어가 정한다 — 목표 도달은 알려 주기만 하는 신호다.
        if (!drivenByRunner && glassParticleCount > targetParticleCount + toleranceCount)
            FinishPour();
    }

    /// <summary>
    /// SPH를 고정 시간 간격으로 돌린다. 오일러 적분 + 강한 압력 조합은 dt에 민감해서, Time.deltaTime을
    /// 그대로 넣으면 프레임이 튈 때 힘이 폭발하거나 기기마다 액체 거동이 달라진다.
    ///
    /// 방출도 반드시 이 루프 안에서, 같은 fixedStep으로 돌아야 한다. 프레임이 튀면(예: 결과 UI를
    /// 처음 켜느라 캔버스를 빌드하는 프레임) 물리는 상한에 걸려 33ms만 전진하는데 방출을
    /// Time.deltaTime으로 돌리면 300ms어치를 내보낸다. 그러면 새 줄기가 '앞으로 감긴' 위치에
    /// 놓이면서 아직 그 자리에 있는 기존 파티클과 겹쳐 밀도가 폭발하고, 액체가 튄다.
    /// 같은 시간축을 쓰면 둘이 어긋날 수 없다.
    /// </summary>
    void StepSimulation()
    {
        const float fixedStep = 1f / 120f;
        const int maxStepsPerFrame = 4;

        simulationAccumulator += Time.deltaTime;

        int steps = 0;
        while (simulationAccumulator >= fixedStep && steps < maxStepsPerFrame)
        {
            EmitFromNeck(fixedStep);
            simulation.Tick(fixedStep);
            ConstrainParticles();

            simulationAccumulator -= fixedStep;
            steps++;
        }

        // 한 프레임에 다 따라잡지 못했으면 밀린 시간은 버린다(따라잡으려다 더 느려지는 걸 방지).
        if (steps >= maxStepsPerFrame)
            simulationAccumulator = 0f;
    }

    /// <summary>
    /// 살아있는 파티클을 잔 안쪽으로 가두고, 잔을 빗나가 떨어진 것은 치운다.
    /// 병에는 가둘 액체가 없으므로 병 벽 처리도 없다 — 파티클은 입구를 떠난 순간부터 자유낙하다.
    /// </summary>
    void ConstrainParticles()
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            SphParticle p = live[i];

            // 절대 높이가 아니라 이 기믹을 기준으로 잰다. 무대를 옮겨도 같은 자리에서 치워진다.
            if (p.pos.y < transform.position.y + despawnBelowY)
            {
                simulation.Unregister(p);
                p.Deactivate();
                pool.Push(p);

                live.RemoveAt(i);
                continue;
            }

            ConstrainToGlass(p);
        }
    }

    void ConstrainToGlass(SphParticle particle)
    {
        const float gateBuffer = 0.1f;

        Transform container = glassCenter;
        Vector2 halfExtents = glassInteriorHalfExtents;

        // 잔은 회전하지 않으므로, 멀리 있으면(=아직 낙하 중) 그냥 건드리지 않는다.
        Vector2 worldHalfExtents = Vector2.Scale(halfExtents, container.lossyScale);
        float gateRadius = worldHalfExtents.magnitude + gateBuffer;

        if (Vector2.Distance(particle.pos, container.position) > gateRadius)
            return;

        Vector3 local = container.InverseTransformPoint(particle.pos);
        ApplyClamp(particle, container, local, halfExtents.x, halfExtents.y);
    }

    /// <summary>local 좌표를 컨테이너의 좌/우/바닥 안으로 클램프하고(위는 절대 안 막음), 벽을
    /// 뚫고 나가려던 속도 성분만 제거한다.</summary>
    static void ApplyClamp(SphParticle particle, Transform container, Vector3 local, float halfWidth, float halfHeight)
    {
        // 입구보다 위에 있으면 아직 컨테이너 안이 아니다. 이때 좌우로 끌어당기면 잔 옆으로 빗나가야 할
        // 액체까지 잔 위로 빨려들어간다 — 벽 사이 높이에 들어온 뒤에만 좌우를 막는다.
        if (local.y > halfHeight) return;

        float clampedX = Mathf.Clamp(local.x, -halfWidth, halfWidth);
        float clampedY = Mathf.Max(local.y, -halfHeight);

        bool hitSide = !Mathf.Approximately(clampedX, local.x);
        bool hitFloor = !Mathf.Approximately(clampedY, local.y);

        if (!hitSide && !hitFloor) return;

        Vector3 correctedWorld = container.TransformPoint(new Vector3(clampedX, clampedY, local.z));
        particle.pos = correctedWorld;
        particle.transform.position = correctedWorld;

        Vector2 localXAxis = container.right;
        Vector2 localYAxis = container.up;

        if (hitSide)
        {
            float velAlongX = Vector2.Dot(particle.velocity, localXAxis);
            particle.velocity -= velAlongX * localXAxis;
        }

        if (hitFloor)
        {
            float velAlongY = Vector2.Dot(particle.velocity, localYAxis);
            if (velAlongY < 0f)
                particle.velocity -= velAlongY * localYAxis;
        }
    }

    // ── 병 모양 (그리기 전용) ───────────────────────────────────────────

    /// <summary>어깨가 시작되는 로컬 Y. 이 아래는 몸통이다.</summary>
    float ShoulderLocalY() =>
        Mathf.Lerp(-bottleInteriorHalfExtents.y, bottleInteriorHalfExtents.y, neckStartHeight01);

    /// <summary>어깨가 끝나고 통로가 시작되는 로컬 Y. 거꾸로 적힌 값도 단차(어깨 없음)로 받아준다.</summary>
    float ChannelLocalY() =>
        Mathf.Max(ShoulderLocalY(),
                  Mathf.Lerp(-bottleInteriorHalfExtents.y, bottleInteriorHalfExtents.y, neckChannelHeight01));

    // ── 판정 ────────────────────────────────────────────────────────────

    int CountParticlesInGlass()
    {
        int count = 0;

        foreach (var p in live)
        {
            Vector3 local = glassCenter.InverseTransformPoint(p.pos);
            if (Mathf.Abs(local.x) <= glassInteriorHalfExtents.x && Mathf.Abs(local.y) <= glassInteriorHalfExtents.y)
                count++;
        }

        return count;
    }

    /// <summary>플레이어가 스스로 따르기를 멈추고 결과를 확정할 때(별도 UI 버튼 등에서) 호출.</summary>
    public void FinishPour()
    {
        if (!isPlay || isFinished) return;

        isFinished = true;
        isPlay = false;

        CompleteMade();

        runnerCompletion?.TrySetResult();

        // 실제 게임 흐름에서는 기믹 큐(GimmickRunner)가 결과를 받아 다음 스텝으로 넘긴다.
        // 독립 테스트 씬에는 그 흐름이 없어서, 끝났다는 신호가 전혀 없으면 그냥 멈춘 것처럼 보인다.
        if (isTest)
        {
            Logger.Log($"[Pour] 판정 종료. 잔에 담긴 파티클 {glassParticleCount} / 목표 {targetParticleCount}(±{toleranceCount})");
            OnNextButton();
        }
    }

    Color GetLiquidColor()
    {
        if (liquidProfile.overrideColor)
            return liquidProfile.color;

        // 기믹 큐가 돌릴 때는 실제로 고른 재료의 색을 쓴다. 정답 재료의 색으로 보정하지 않는다 —
        // 진 대신 럼을 골랐다면 럼 색 액체가 나와야 한다.
        if (drivenByRunner)
        {
            if (shelfData != null && shelfData.TryGet(pourIngredientId, out var item) &&
                item.TryGetLiquidColor(out Color32 color))
            {
                return color;
            }

            return Color.white;
        }

        NewCocktailTag[] tags = data != null ? data.targetCocktailData.Tags : null;

        if (tags == null || tags.Length == 0)
        {
            return Color.white;
        }

        int n = colorData.categorys.FindIndex(a => a.Contains(tags[0].Ko));
        return n >= 0 ? colorData.colors[n] : Color.white;
    }

    public void InitGame(UniTaskCompletionSource tcs)
    {
        this.tcs = tcs;
        data.craftingResult.actionFailCount = 0;
    }

    public void CompleteMade()
    {
        // 기믹 큐가 돌릴 때는 결과를 GimmickResult로 돌려주므로 이 저장소를 쓰지 않는다.
        if (data != null)
        {
            int deviation = Mathf.Abs(glassParticleCount - targetParticleCount);

            data.craftingResult.isResult = true;
            data.craftingResult.actionFailCount = deviation;
            data.craftingResult.limitFailCount = toleranceCount;
        }

        if (tcs != null)
        {
            tcs.TrySetResult();
        }
    }

    public void OnNextButton()
    {
        buttonCanvas.gameObject.SetActive(true);
    }

    public void Serve()
    {
        craftServe?.Raise(new Void());
    }

    public void Retry()
    {
        craftRetry?.Raise(new Void());
    }

    // ── ICraftGimmick ───────────────────────────────────────────────────

    /// <summary>기믹 큐가 이 기믹을 돌리고 있는지. 자동 종료와 색상·수량의 출처를 가른다.</summary>
    bool drivenByRunner;

    UniTaskCompletionSource runnerCompletion;
    CraftTimer craftTimer;
    GimmickStep pourStep;

    /// <summary>지금 따르고 있는 실제 재료. 병 색과 결과에 남길 id다.</summary>
    string pourIngredientId;

    /// <summary>기울기를 끝까지 눕혔을 때의 방출률(ml/초). balance.json에서 읽는다.</summary>
    float mlPerSecondAtFullFlow;

    /// <summary>지금까지 병에서 나온 양(ml). 기울인 세기 × 시간으로 쌓인다.</summary>
    float pouredMl;

    /// <summary>지금까지 따른 양을 이 기믹의 목표 단위로 환산한 값. 화면에 보여줄 수치다.</summary>
    public float PouredInTargetUnit => ConvertMlTo(pouredMl, pourStep.TargetUnit ?? ENewUnit.Ml);

    /// <summary>
    /// 이번 제조 시도의 사정(고른 잔·도구·재료). 실행기가 PlayAsync로 넘겨준다.
    /// 아직 그리는 데 쓰지는 않는다 — 리소스가 붙을 때 여기서 가져가면 된다.
    /// </summary>
    CraftContext craftContext;

    public async UniTask<GimmickResult> PlayAsync(GimmickStep step, CraftContext context,
                                                 CraftTimer timer, CancellationToken token)
    {
        // 액체 색은 이미 고른 재료를 따라간다(GetLiquidColor). 병 그림·크기와 받는 잔은 아직
        // 프리팹에 고정돼 있는데, 그때 쓸 값이 step.IngredientId와 context.GlassId다.
        craftContext = context;

        // 아직 그리는 데 쓰지 않으므로, 값이 제대로 도착했는지는 이 줄로만 확인한다.
        Debug.Log($"[Pour] 문맥 — 잔 {craftContext.GlassId ?? "없음"} / " +
                  $"도구 {craftContext.ToolId ?? "없음"} / 재료 {string.Join(", ", craftContext.IngredientIds)}");

        drivenByRunner = true;
        craftTimer = timer;
        pourStep = step;
        pourIngredientId = step.IngredientId;
        runnerCompletion = new UniTaskCompletionSource();

        // "이미 끝났는지"는 보지 않는다. 이 함수는 Start()보다 먼저 불려서 상태가 초기값이다.
        // 기다림을 푸는 신호는 FinishPour() 한 곳에서만 나온다.

        using (token.Register(() => runnerCompletion.TrySetCanceled()))
        {
            await runnerCompletion.Task;
        }

        // 필업도 같은 조작이라 이 구현을 그대로 쓴다. 큐에 들어온 종류를 그대로 결과에 적어
        // 따르기와 필업을 구분한다.
        return GimmickResult.Quantity(step.Type, step.IngredientId,
                                      step.TargetValue, step.TargetUnit, PouredInTargetUnit,
                                      ECraftEndType.ManualNext,
                                      craftTimer != null ? craftTimer.ElapsedSec : 0f);
    }

    /// <summary>
    /// 병을 기울일 수 있는 동안. 따르기는 시작 대기나 연출 구간이 없어서 화면에 떠 있는 내내 해당한다.
    /// </summary>
    public bool IsManualInputActive => isPlay && !isFinished;

    // ── ICraftGimmickProgress ───────────────────────────────────────────

    /// <summary>
    /// 지금까지 따른 양과 목표.
    ///
    /// 정답에 없는 재료를 골랐다면 알려 줄 정답이 없으므로 양쪽을 다 가린다. 가리는 건 화면뿐이고
    /// 실제 투입량은 그대로 쌓여 결과에 남는다 — 나중에 추가 재료로 판정할 때 그 값이 필요하다.
    /// </summary>
    public string ProgressText
    {
        get
        {
            if (!drivenByRunner) return string.Empty;

            if (!pourStep.IsTargetVisible) return "??? / ???";

            string unit = (pourStep.TargetUnit ?? ENewUnit.Ml).ToString().ToLowerInvariant();
            return $"{PouredInTargetUnit:0.00} / {pourStep.TargetValue.Value:0.00} {unit}";
        }
    }

    /// <summary>
    /// 목표에 도달했는지. 도달해도 저절로 멈추지 않고, 넘겨서 계속 따라도 켜진 채로 있다.
    /// 목표를 가린 재료에는 띄우지 않는다 — 알려 줄 목표가 없는데 도달을 알릴 수는 없다.
    /// </summary>
    public bool ShowOkMark =>
        drivenByRunner && pourStep.IsTargetVisible && PouredInTargetUnit >= pourStep.TargetValue.Value;

    // ── ICraftGimmickManualEnd ──────────────────────────────────────────

    public bool CanEndNow => isPlay && !isFinished;

    /// <summary>지금까지 따른 양으로 확정한다. 모자라든 넘치든 그 시점 값이 결과다.</summary>
    public void EndNow()
    {
        FinishPour();
    }

    /// <summary>방출률을 balance.json에서 읽는다. 값이 없으면 액체가 나와도 수량이 안 쌓이므로 알린다.</summary>
    float ResolveEmitRateMlPerSec()
    {
        float fromData = balanceData != null && balanceData.balanceData != null
            ? balanceData.balanceData.Config.PourEmitRateMlPerSec
            : 0f;

        if (fromData > 0f) return fromData;

        if (drivenByRunner)
            Logger.Log("[Pour] pour_emit_rate_ml_per_sec가 비어 있습니다. 투입량이 쌓이지 않습니다.");

        return fromData;
    }

    /// <summary>
    /// ml을 목표 단위로 바꾼다. 환산 계수도 balance.json이 정본이라 코드에 고정하지 않는다.
    /// 계수가 비어 있으면 나눗셈이 성립하지 않으므로 ml 그대로 둔다.
    /// </summary>
    float ConvertMlTo(float ml, ENewUnit unit)
    {
        var config = balanceData != null && balanceData.balanceData != null
            ? balanceData.balanceData.Config
            : default;

        switch (unit)
        {
            case ENewUnit.Oz:
                return config.UnitOzToMl > 0f ? ml / config.UnitOzToMl : ml;

            case ENewUnit.Tsp:
                return config.UnitTspToMl > 0f ? ml / config.UnitTspToMl : ml;

            default:
                return ml;
        }
    }
}
