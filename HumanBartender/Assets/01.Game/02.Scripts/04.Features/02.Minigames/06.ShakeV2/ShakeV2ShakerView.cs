using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 셰이커 그림 묶음(월드). 웹 prop-art.js(drawBody)와 game.js(drawShaker·drawSpill·링·냉기·화면 흔들림)를 옮겼다.
///
/// 좌표는 세 가지다.
/// - 게임 좌표: 오른쪽 조작 화면 880×640, 원점 왼쪽 위, 아래가 +Y. 웹 수치가 모두 이 단위다.
/// - 셰이커 로컬: 이 Transform이 게임 좌표 필드의 가운데다. 게임 1단위 = Look.worldPerGameUnit 월드 단위.
/// - 도트: 원본 그림 1px. ShakerPivot 아래 자식은 이 단위(1/PPU)로 정수 위치에 놓인다.
///
/// 도트 묶음(틴·외피·단면)은 같은 격자에서 한 덩어리로 기운다. ShakerPivot의 Scale은 화면 픽셀에 맞춰 코드가 정한다
/// (웹 Surface.fit: 720p 1배, 1080p 1.75배, 1440p 2배). 외피·틴 그림은 원본 방향(큰 틴 왼쪽)이라 각각 180° 돌려 둔다.
///
/// 쉐이킹 중 단면 안의 물·얼음·성에는 ShakeV2Contents가 맡는다(물리 씬, 관성, 파손 때 쏟아지는 얼음).
/// </summary>
public sealed class ShakeV2ShakerView : MonoBehaviour
{
    [Serializable]
    public sealed class Look
    {
        [Header("배치")]
        [Tooltip("셰이커를 놓는 기준점(게임 좌표). 웹 SC.")]
        public Vector2 center = new Vector2(440f, 322f);
        [Tooltip("게임 좌표 1단위의 월드 크기. 캔버스 960×540·직교 크기 2.7 기준 게임 1단위 = 캔버스 0.5 = 월드 0.005.")]
        [Min(.0001f)] public float worldPerGameUnit = .005f;
        [Tooltip("도트 그림 원본 1px의 월드 크기(Scale 1일 때). 틴·외피 그림의 PPU 100.")]
        [Min(.0001f)] public float worldPerArtPixel = .01f;
        [Tooltip("셰이커 원본 1px이 화면에서 차지할 크기의 목표(게임 좌표 1단위 화면 픽셀 기준). 웹 B.K × 4/3.")]
        [Min(.1f)] public float artPitch = 1.3f * 4f / 3f;

        [Header("충격 링")]
        [Tooltip("링이 퍼지는 시간(초).")]
        [Min(.01f)] public float ringSec = .42f;
        [Tooltip("틴 끝 링의 시작 반지름과 커지는 양(게임 좌표). x = 시작, y = 큰 링, z = 작은 링.")]
        public Vector3 strokeRing = new Vector3(10f, 38f, 28f);
        [Tooltip("결합 링의 시작 반지름과 커지는 양(게임 좌표). x = 시작, y = 큰 링, z = 작은 링.")]
        public Vector3 sealRing = new Vector3(16f, 100f, 60f);
        [Tooltip("링 색. 불투명도는 큰 링 1, 작은 링 .6에서 사라진다.")]
        public Color ringColor = new Color32(255, 244, 214, 255);
        [Range(0f, 1f)] public float smallRingAlpha = .6f;

        [Header("화면 흔들림")]
        [Tooltip("흔들림 세기가 1초에 남는 비율. 웹 .0012.")]
        [Range(.0001f, 1f)] public float camDecayPerSec = .0012f;

        [Header("냉기")]
        [Tooltip("위 틴이 빠질 때 열린 자리에서 오르는 냉기 색. 불투명도는 아래 값까지 오른다.")]
        public Color vaporColor = new Color32(216, 240, 245, 255);
        [Range(0f, 1f)] public float vaporAlpha = .32f;

        [Header("파손 때 튀는 액체")]
        [Tooltip("처음 한꺼번에 나오는 방울 수.")]
        [Min(0)] public int spillBurst = 64;
        [Tooltip("이어서 나오는 방울 수(초당, 1초 동안 0까지 줄어든다).")]
        [Min(0f)] public float spillRate = 100f;
        [Tooltip("방울에 걸리는 중력(게임 좌표/초²).")]
        public float spillGravity = 720f;
        [Tooltip("방울 불투명도.")]
        [Range(0f, 1f)] public float spillAlpha = .85f;
        [Tooltip("한 번에 그릴 수 있는 방울 수.")]
        [Min(1)] public int spillCapacity = 160;
    }

    struct Ring
    {
        public bool Active, OnTin, Big;
        public float Start, ArtX;
        public Vector2 GamePoint;
    }

    struct Drop
    {
        public bool Active;
        public Vector2 Position, Velocity;
        public float Life, Size;
    }

    [Header("Shaker")]
    [Tooltip("스프링 위치·기울기를 받는 도트 묶음. 실행 중 화면 픽셀에 맞춰 코드가 Scale을 정한다(월드 물체).")]
    [SerializeField] Transform shakerPivot;
    [Tooltip("작은 틴이 붙는 자리. 결합 대기·분리·파손 때 움직인다.")]
    [SerializeField] Transform smallTinPivot;
    [SerializeField] SpriteRenderer smallTin;
    [Tooltip("분리된 큰 틴.")]
    [SerializeField] GameObject largeTin;
    [Tooltip("쉐이킹 중에만 보이는 결합 외피와 단면.")]
    [SerializeField] GameObject closedBody;

    [Header("Effects")]
    [Tooltip("미리 만들어 둔 충격 링. 모자라면 가장 오래된 것을 다시 쓴다.")]
    [SerializeField] SpriteRenderer[] rings;
    [SerializeField] Sprite ringLarge;
    [SerializeField] Sprite ringSmall;
    [Tooltip("파손 때 튀는 액체. 시뮬레이션은 이 클래스가 게임 시간으로 하고, 파티클은 그리기만 한다.")]
    [SerializeField] ParticleSystem spillParticles;
    [Tooltip("위 틴이 빠질 때의 냉기(받은 흰 빛 그림). 그리기만 한다.")]
    [SerializeField] ParticleSystem vaporParticles;

    [Header("Contents")]
    [Tooltip("단면 안의 물(ClosedBody/Liquid). 코드로 칠한 작은 텍스처를 실행 중에 스프라이트로 만들어 붙인다. 단면 Mask 안에서만 보인다.")]
    [SerializeField] SpriteRenderer liquid;
    [Tooltip("얼음 그림 10개(ClosedBody/Ice01~10). 물리 몸체의 위치·각도를 읽어 포즈 시트 칸을 고르고, 돌리지 않은 채 정수 도트 자리에 놓는다.")]
    [SerializeField] SpriteRenderer[] ice;
    [Tooltip("단면 위아래 벽의 성에(ClosedBody/Frost). 얼음 위, 테두리 아래.")]
    [SerializeField] SpriteRenderer frost;
    [Tooltip("얼음 포즈 시트 색인(에디터에서 잘라 둔 칸 스프라이트 포함).")]
    [SerializeField] ShakeV2IcePoseSheet icePoses;
    [Tooltip("파손 때 입구 밖으로 나온 얼음 그림(SpilledIce/SpilledIce01~10). 금속 틴 뒤에 그려지고 오른쪽 패널 밖으로 나가지 않는다.")]
    [SerializeField] SpriteRenderer[] spilledIce;

    [Header("Ice Physics")]
    [Tooltip("움직이지 않는 물리 묶음(통 기준, IcePhysics). 프리팹에서는 꺼 두고, 처음 쉐이킹 때 따로 만든 물리 씬으로 옮겨 켠다. " +
             "그래서 다른 콜라이더와 엮이지 않고, 프로젝트 물리가 아니라 게임 시간으로 직접 진행된다.")]
    [SerializeField] Transform icePhysics;
    [Tooltip("단면 둘레(Edge Collider 2D).")]
    [SerializeField] EdgeCollider2D chamberCollider;
    [Tooltip("얼음 몸체 10개(Rigidbody 2D + Box Collider 2D). 프리팹에 저장된 자리가 쉐이킹 시작 배치다.")]
    [SerializeField] Rigidbody2D[] iceBodies;

    [SerializeField] Look look = new Look();
    [Tooltip("단면 내용물(관성·물·얼음·성에) 조절 값.")]
    [SerializeField] ShakeV2ContentsTuning contents = new ShakeV2ContentsTuning();

    // 원본 그림의 고정 치수(원본 px). 분리 틴 시트의 큰 틴 끝, 테두리 높이, 작은 틴 끝, 틈.
    const float LargeTinEnd = 188f, LargeTinRim = 34f, SmallTinEnd = -186f, Seam = -30f;
    const int VaporCount = 8;

    readonly Ring[] ringState = new Ring[8];
    Drop[] drops = Array.Empty<Drop>();
    ParticleSystem.Particle[] spillBuffer = Array.Empty<ParticleSystem.Particle>();
    readonly ParticleSystem.Particle[] vaporBuffer = new ParticleSystem.Particle[VaporCount];
    float cam, pivotScale = 1f, pixelsPerWorld = 100f, lastTime = float.NaN;
    Vector2 popPosition, popVelocity;
    float popAngle, spillCarry, brokenAt;
    bool popping, captured;
    Vector3 basePosition;
    Color spillColor = Color.white;
    ShakeV2Contents contentsState;
    IReadOnlyList<ShakeV2Layer> liquidLayers;
    Color? liquidTarget;
    ShakeV2Pose breakPose;

    public Look Settings => look;
    public ShakeV2ContentsTuning ContentsSettings => contents;

    /// <summary>단면 내용물. 처음 읽을 때 만든다(물리 씬은 처음 쉐이킹 때 만든다).</summary>
    internal ShakeV2Contents Contents => contentsState ??= CreateContents();

    ShakeV2Contents CreateContents()
    {
        var created = new ShakeV2Contents(new ShakeV2Contents.Parts
        {
            ClosedBody = closedBody != null ? closedBody.transform : null,
            Liquid = liquid,
            Frost = frost,
            Ice = ice,
            Poses = icePoses,
            IcePhysics = icePhysics,
            Chamber = chamberCollider,
            Bodies = iceBodies,
            WorldPerArtPixel = look.worldPerArtPixel,
        }, contents);
        created.SetLayers(liquidLayers, liquidTarget);
        return created;
    }

    /// <summary>이번 판의 재료 층(첫 항목이 바닥)과, 따로 정한 다 섞인 색(없으면 양 가중 평균). 다음 쉐이킹부터 쓴다.</summary>
    public void SetLiquid(IReadOnlyList<ShakeV2Layer> layers, Color? target)
    {
        liquidLayers = layers;
        liquidTarget = target;
        contentsState?.SetLayers(layers, target);
    }

    void OnDestroy() => ReleaseContents();

    /// <summary>물 텍스처와 얼음 물리 씬을 정리한다. 편집 모드(테스트·캡처)에서는 직접 부른다.</summary>
    internal void ReleaseContents()
    {
        contentsState?.Dispose();
        contentsState = null;
    }
    /// <summary>원본 1px이 게임 좌표에서 차지하는 크기.</summary>
    public float GameUnitsPerArtPixel => look.worldPerArtPixel * pivotScale / look.worldPerGameUnit;

    // ── 배율 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 원본 1px이 화면에서 차지할 픽셀 수. 웹 Surface.fit: 목표 크기가 정수배에 가까우면(.85~1.08) 그 정수배,
    /// 아니면 올림한 정수배에 8분의 n 축소를 곱한다.
    /// </summary>
    public static float ArtScreenScale(float screenPixelsPerGameUnit, float artPitch)
    {
        float pitch = Mathf.Max(.01f, screenPixelsPerGameUnit * artPitch);
        int nativeZoom = Mathf.Max(1, Mathf.FloorToInt(pitch + .5f));
        float fit = nativeZoom / pitch;
        bool native = fit >= .85f && fit <= 1.08f;
        int zoom = native ? nativeZoom : Mathf.Max(1, Mathf.CeilToInt(pitch));
        int eighths = native ? 8 : Mathf.Clamp(Mathf.FloorToInt(pitch * 8f / zoom + .5f), 2, 8);
        return zoom * eighths / 8f;
    }

    /// <summary>카메라의 화면 픽셀/월드 단위에 맞춰 도트 묶음의 Scale을 정한다.</summary>
    public void UpdatePixelScale(float screenPixelsPerWorldUnit)
    {
        pixelsPerWorld = Mathf.Max(.01f, screenPixelsPerWorldUnit);
        float art = ArtScreenScale(pixelsPerWorld * look.worldPerGameUnit, look.artPitch);
        pivotScale = art / (pixelsPerWorld * look.worldPerArtPixel);
        if (shakerPivot != null) shakerPivot.localScale = new Vector3(pivotScale, pivotScale, 1f);
    }

    /// <summary>카메라에서 화면 픽셀/월드 단위를 읽는다(직교 카메라).</summary>
    public static float PixelsPerWorldUnit(Camera camera) =>
        camera != null && camera.orthographic && camera.orthographicSize > 0f ? camera.pixelHeight / (2f * camera.orthographicSize) : 100f;

    // ── 좌표 ────────────────────────────────────────────────────────────

    /// <summary>게임 좌표를 셰이커 로컬 위치(월드 단위, 필드 가운데 기준)로 바꾼다.</summary>
    public Vector2 FieldLocal(Vector2 game) => new Vector2((game.x - 440f) * look.worldPerGameUnit, (320f - game.y) * look.worldPerGameUnit);

    /// <summary>셰이커 자세에서 셰이커 몸통 좌표(원본 px, 아래가 +Y, 작은 틴 왼쪽)의 점을 게임 좌표로 바꾼다. 웹 projectTinPoint.</summary>
    public Vector2 GamePoint(ShakeV2Pose pose, Vector2 art)
    {
        float c = Mathf.Cos(pose.Angle), s = Mathf.Sin(pose.Angle), k = GameUnitsPerArtPixel;
        return look.center + pose.Offset + new Vector2(k * (art.x * c - art.y * s), k * (art.x * s + art.y * c));
    }

    /// <summary>D·E 키 상자 자리: 도착 자세에서 큰 틴의 오른쪽 아래·위 모서리. A는 왼쪽(62,322). 웹 keyPosition.</summary>
    public Vector2 KeyPoint(ShakeV2Rules rules, EShakeV2Input key)
    {
        if (key == EShakeV2Input.Back) return new Vector2(62f, look.center.y);
        Vector2 target = rules.TargetOf(key);
        float side = Mathf.Sign(target.y);
        return GamePoint(new ShakeV2Pose(target, rules.TiltAt(target.y)), new Vector2(LargeTinEnd, side * LargeTinRim));
    }

    /// <summary>"SHAKE!" 자리를 셰이커 주변에서 고른다. 웹 popShakeText. overlaps가 참이면 다른 자리를 찾는다(최대 8번).</summary>
    public Vector2 PickShakeWord(ShakeV2Rules rules, ShakeV2Pose pose, float minY, Func<Vector2, bool> overlaps)
    {
        Vector2 point = Vector2.zero;
        Vector2 shift = rules.Target - rules.Position;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float offset = Random.Range(-185f, 185f);
            float rim = ShakeV2Presentation.ChamberHalfHeight(offset);
            float height = Random.value < .5f ? -rim - Random.Range(22f, 34f) : rim + Random.Range(40f, 52f);
            point = GamePoint(pose, new Vector2(offset, height) / ShakeV2Presentation.ShakerUnitsPerArtPixel) + shift;
            point.y = Mathf.Max(minY, point.y);
            if (overlaps == null || !overlaps(point)) break;
        }
        return point;
    }

    // ── 연출 시작 ───────────────────────────────────────────────────────

    public void ResetState()
    {
        if (!captured)
        {
            captured = true;
            basePosition = transform.localPosition;
        }
        transform.localPosition = basePosition;
        cam = 0f;
        popping = false;
        spillCarry = 0f;
        lastTime = float.NaN;
        for (int i = 0; i < ringState.Length; i++) ringState[i].Active = false;
        int capacity = Mathf.Max(1, look.spillCapacity);
        if (drops.Length != capacity) drops = new Drop[capacity];
        if (spillBuffer.Length != capacity) spillBuffer = new ParticleSystem.Particle[capacity];
        for (int i = 0; i < drops.Length; i++) drops[i].Active = false;
        foreach (var ring in rings ?? Array.Empty<SpriteRenderer>()) if (ring != null) ring.gameObject.SetActive(false);
        PrepareParticles(spillParticles);
        PrepareParticles(vaporParticles);
        contentsState?.Stop();
        foreach (var piece in spilledIce ?? Array.Empty<SpriteRenderer>()) if (piece != null) piece.enabled = false;
    }

    // 파티클은 그리기만 한다. 스스로 나이 들거나 움직이지 않게 멈춘 채 두고, 매 프레임 SetParticles로 채운다.
    static void PrepareParticles(ParticleSystem particles)
    {
        if (particles == null) return;
        particles.Clear(true);
        if (!particles.isPlaying) particles.Play(true);
        particles.Pause(true);
        particles.SetParticles(Array.Empty<ParticleSystem.Particle>(), 0);
    }

    public void SetCam(float value) => cam = value;
    public void RaiseCam(float value) => cam = Mathf.Max(cam, value);

    /// <summary>결합 링. 결합 순간의 틈 자리에 고정된다.</summary>
    public void AddSealRing(ShakeV2Rules rules, ShakeV2MotionTuning motion, bool big)
    {
        var pose = ShakeV2Presentation.ShakerPose(rules, motion);
        AddRing(new Ring { Active = true, Big = big, Start = rules.GameTime, GamePoint = GamePoint(pose, new Vector2(Seam, 0f)) });
    }

    /// <summary>틴 끝 링. A는 작은 틴 왼쪽 끝, D·E는 큰 틴 오른쪽 끝에서 셰이커를 따라간다.</summary>
    public void AddStrokeRing(ShakeV2Rules rules, EShakeV2Input stroke, bool big)
    {
        AddRing(new Ring
        {
            Active = true, OnTin = true, Big = big, Start = rules.GameTime,
            ArtX = ShakeV2Rules.Side(stroke) < 0 ? SmallTinEnd : LargeTinEnd,
        });
    }

    void AddRing(Ring ring)
    {
        int count = Mathf.Min(ringState.Length, rings?.Length ?? 0), slot = 0;
        for (int i = 0; i < count; i++)
        {
            if (!ringState[i].Active) { slot = i; break; }
            if (ringState[i].Start < ringState[slot].Start) slot = i;
        }
        ringState[slot] = ring;
    }

    /// <summary>파손: 작은 틴이 튀어 나가고, 열린 틈에서 액체가 터진다.</summary>
    public void BeginBreak(ShakeV2Rules rules, ShakeV2MotionTuning motion, Color liquid)
    {
        popping = true;
        popPosition = Vector2.zero;
        popVelocity = motion.popVelocity;
        popAngle = 0f;
        brokenAt = rules.GameTime;
        spillColor = liquid;
        spillCarry = 0f;
        for (int i = 0; i < ringState.Length; i++) ringState[i].Active = false;
        var pose = ShakeV2Presentation.ShakerPose(rules, motion);
        for (int i = 0; i < look.spillBurst; i++) EmitDrop(pose, true);
        // 단면과 안의 얼음은 바로 숨고, 얼음은 입구 쪽 조각부터 같은 물리로 쏟아진다.
        breakPose = pose;
        Contents.BeginBreak(rules, pose.Angle);
    }

    void EmitDrop(ShakeV2Pose pose, bool burst)
    {
        for (int i = 0; i < drops.Length; i++)
        {
            if (drops[i].Active) continue;
            float lateral = burst ? Random.Range(-12f, 38f) : Random.Range(26f, 47f);
            drops[i] = new Drop
            {
                Active = true,
                Position = GamePoint(pose, new Vector2(-40f, lateral) / ShakeV2Presentation.ShakerUnitsPerArtPixel),
                Velocity = burst ? new Vector2(-Random.Range(140f, 370f), Random.Range(-130f, 85f))
                    : new Vector2(-Random.Range(65f, 155f), Random.Range(30f, 125f)),
                Life = Random.Range(.75f, 1.25f),
                Size = burst ? Random.Range(2f, 5f) : Random.Range(3f, 6f),
            };
            return;
        }
    }

    // ── 그리기 ──────────────────────────────────────────────────────────

    /// <summary>매 프레임 상태를 그린다. 시간은 규칙의 게임 시간을 쓰므로 일시정지·순간 정지와 함께 멈춘다.</summary>
    public void Render(ShakeV2Rules rules, ShakeV2MotionTuning motion)
    {
        if (!captured) ResetState();
        float now = rules.GameTime;
        float dt = float.IsNaN(lastTime) ? 0f : Mathf.Max(0f, now - lastTime);
        lastTime = now;

        var pose = ShakeV2Presentation.ShakerPose(rules, motion);
        bool closed = rules.Phase == EShakeV2Phase.Shake;
        if (closedBody != null && closedBody.activeSelf != closed) closedBody.SetActive(closed);
        if (largeTin != null && largeTin.activeSelf == closed) largeTin.SetActive(!closed);
        if (smallTinPivot != null && smallTinPivot.gameObject.activeSelf == closed) smallTinPivot.gameObject.SetActive(!closed);

        // 도트 묶음: 기준점 + 자세, 화면 픽셀에 맞춘 중심, 한 덩어리 회전.
        if (shakerPivot != null)
        {
            Vector2 p = FieldLocal(look.center + pose.Offset);
            shakerPivot.localPosition = new Vector3(Mathf.Round(p.x * pixelsPerWorld), Mathf.Round(p.y * pixelsPerWorld), 0f) / pixelsPerWorld;
            shakerPivot.localEulerAngles = new Vector3(0f, 0f, -pose.Angle * Mathf.Rad2Deg);
        }

        if (!closed) RenderSmallTin(rules, motion, dt);

        // 화면 흔들림(웹: 매 그리기마다 ±세기/2의 무작위 이동, 게임 시간으로 줄어든다). 셰이커·링·액체·냉기가 함께 흔들린다.
        cam *= Mathf.Pow(look.camDecayPerSec, dt);
        transform.localPosition = basePosition + new Vector3(Random.value - .5f, -(Random.value - .5f), 0f) * cam * look.worldPerGameUnit;

        RenderRings(pose, now);
        RenderVapor(rules, motion, pose);
        RenderSpill(pose, now, dt);
        RenderContents(rules, closed, dt);
    }

    // 단면 내용물: 쉐이킹이 시작되면 수평 수면·왼쪽 아래 얼음으로 채우고, 게임 시간이 흐를 때만 진행한다.
    void RenderContents(ShakeV2Rules rules, bool closed, float dt)
    {
        var state = Contents;
        if (closed && !state.Running) state.Begin(rules);
        state.Tick(rules, dt);
        if (closed) state.Draw(rules);

        var spills = state.Spills;
        int count = spilledIce?.Length ?? 0;
        for (int i = 0; i < count; i++)
        {
            var piece = spilledIce[i];
            if (piece == null) continue;
            bool visible = i < spills.Count && spills[i].Visible;
            if (piece.enabled != visible) piece.enabled = visible;
            if (!visible) continue;

            // 밖으로 나온 얼음은 화면 방향으로 찍는다(파손 순간 자세 + 단면 안 각도로 포즈를 고른다). 웹 drawSpill.
            var spill = spills[i];
            Vector2 local = FieldLocal(GamePoint(breakPose, spill.Position / ShakeV2Presentation.ShakerUnitsPerArtPixel));
            piece.transform.localPosition = new Vector3(Mathf.Round(local.x * pixelsPerWorld), Mathf.Round(local.y * pixelsPerWorld), 0f) / pixelsPerWorld;
            piece.transform.localScale = new Vector3(pivotScale, pivotScale, 1f);
            if (icePoses != null) piece.sprite = icePoses.SpriteFor(spill.Diameter, spill.Angle + breakPose.Angle);
            var color = piece.color;
            color.a = spill.Alpha;
            piece.color = color;
        }
    }

    void RenderSmallTin(ShakeV2Rules rules, ShakeV2MotionTuning motion, float dt)
    {
        var offset = ShakeV2Presentation.TinOffset(rules, motion);
        Vector2 position = offset.Position;
        float angle = offset.Angle;
        if (popping)
        {
            popPosition.x += popVelocity.x * dt;
            popVelocity.y += motion.popGravity * dt;
            popPosition.y += popVelocity.y * dt;
            popAngle += motion.popSpin * dt;
            // 틴 그림은 반 바퀴 돌아 있으므로 오른쪽으로 날아가는 만큼 X를 뺀다(웹 drawShaker).
            position = new Vector2(motion.seatedOffset - popPosition.x / motion.popNominalScale, -popPosition.y / motion.popNominalScale);
            angle = popAngle;
        }

        if (smallTinPivot != null)
        {
            // 웹은 뒤집힌 좌표(offset/1.333)에서 돈다. 그림은 그 점에서 seated/1.333만큼 왼쪽에 붙어 있으므로, 정수 위치에 둔
            // 그림을 같은 점을 중심으로 돌리도록 피벗 자리를 보정한 뒤, 뒤집힌 좌표를 셰이커 좌표(−x, −y, +180°)로 바꾼다.
            float sp = ShakeV2Presentation.ShakerUnitsPerArtPixel, phi = -angle;
            Vector2 origin = new Vector2(position.x / sp, -position.y / sp);
            Vector2 back = new Vector2(-motion.seatedOffset / sp, 0f);
            Vector2 rotated = new Vector2(back.x * Mathf.Cos(phi) - back.y * Mathf.Sin(phi), back.x * Mathf.Sin(phi) + back.y * Mathf.Cos(phi));
            Vector2 flipped = -(origin + rotated) * look.worldPerArtPixel;
            smallTinPivot.localPosition = new Vector3(flipped.x, flipped.y, 0f);
            smallTinPivot.localEulerAngles = new Vector3(0f, 0f, 180f + phi * Mathf.Rad2Deg);
        }
        if (smallTin != null)
        {
            var color = smallTin.color;
            color.a = offset.Alpha;
            smallTin.color = color;
        }
    }

    void RenderRings(ShakeV2Pose pose, float now)
    {
        int count = Mathf.Min(rings?.Length ?? 0, ringState.Length);
        for (int i = 0; i < count; i++)
        {
            var ringRenderer = rings[i];
            if (ringRenderer == null) continue;
            ref Ring ring = ref ringState[i];
            float k = (now - ring.Start) / look.ringSec;
            bool visible = ring.Active && k < 1f;
            if (!visible) ring.Active = false;
            if (ringRenderer.gameObject.activeSelf != visible) ringRenderer.gameObject.SetActive(visible);
            if (!visible) continue;

            Vector3 size = ring.OnTin ? look.strokeRing : look.sealRing;
            float radius = size.x + k * (ring.Big ? size.y : size.z);
            Vector2 point = ring.OnTin ? GamePoint(pose, new Vector2(ring.ArtX, 0f)) : ring.GamePoint;
            ringRenderer.sprite = ring.Big ? ringLarge : ringSmall;
            Vector2 local = FieldLocal(point);
            ringRenderer.transform.localPosition = new Vector3(local.x, local.y, 0f);
            float spriteSize = ringRenderer.sprite != null ? Mathf.Max(.0001f, ringRenderer.sprite.bounds.size.x) : 1f;
            float scale = 2f * radius * look.worldPerGameUnit / spriteSize;
            ringRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            var color = look.ringColor;
            color.a = (1f - k) * (ring.Big ? 1f : look.smallRingAlpha);
            ringRenderer.color = color;
        }
    }

    void RenderVapor(ShakeV2Rules rules, ShakeV2MotionTuning motion, ShakeV2Pose pose)
    {
        if (vaporParticles == null) return;
        float t = rules.WasBroken ? -1f : (rules.UnsealAge - motion.releaseSec) / motion.vaporSec;
        Vector2 origin = GamePoint(pose, new Vector2(Seam, 0f));
        int count = 0;
        for (int i = 0; i < VaporCount && t > 0f && t < 1f; i++)
        {
            float life = (t - i * .026f) / .8f;
            if (life <= 0f || life >= 1f) continue;
            var point = origin + new Vector2((i - 3.5f) * 5f + Mathf.Sin(i * 2.4f) * 27f * life, -life * (66f + i % 3 * 8f));
            float radius = 7f + life * 26f;
            var color = look.vaporColor;
            color.a = look.vaporAlpha * Mathf.Sin(Mathf.PI * life);
            vaporBuffer[count++] = Particle(FieldLocal(point), 2f * radius * look.worldPerGameUnit, color);
        }
        vaporParticles.SetParticles(vaporBuffer, count);
    }

    void RenderSpill(ShakeV2Pose pose, float now, float dt)
    {
        if (popping && dt > 0f)
        {
            float age = now - brokenAt;
            if (age < 1f)
            {
                spillCarry += dt * look.spillRate * (1f - age);
                while (spillCarry >= 1f) { EmitDrop(pose, false); spillCarry -= 1f; }
            }
        }

        int count = 0;
        for (int i = 0; i < drops.Length; i++)
        {
            ref Drop drop = ref drops[i];
            if (drop.Active && dt > 0f)
            {
                drop.Position += drop.Velocity * dt;
                drop.Velocity.y += look.spillGravity * dt;
                drop.Life -= dt;
                if (drop.Life <= 0f) drop.Active = false;
            }
            if (!drop.Active) continue;
            var color = spillColor;
            color.a = look.spillAlpha * Mathf.Clamp01(drop.Life / .25f);
            spillBuffer[count++] = Particle(FieldLocal(drop.Position), drop.Size * 2f * look.worldPerGameUnit, color);
        }
        if (spillParticles != null) spillParticles.SetParticles(spillBuffer, count);
    }

    static ParticleSystem.Particle Particle(Vector2 position, float size, Color color) => new ParticleSystem.Particle
    {
        position = new Vector3(position.x, position.y, 0f),
        startSize = size,
        startColor = color,
        startLifetime = 1000f,
        remainingLifetime = 1000f,
    };
}
