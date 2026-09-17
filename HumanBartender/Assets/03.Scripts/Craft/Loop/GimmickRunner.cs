using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>
/// CraftFlowController가 요청한 기믹 한 단계를 실행한다.
/// 프리팹·카메라·HUD와 입력 가능한 동안의 시간 갱신을 맡는다.
/// 실행 순서, 제조 기록과 작업 종료는 CraftFlowController가 소유한다.
///
/// 한 잔 전체의 시계도 여기서 돌린다. 시계는 기믹이 아니라 제조에 속한 것이라, 기믹이 바뀌어도
/// 멈추거나 초기화되지 않고 이어져야 하기 때문이다.
/// </summary>
public class GimmickRunner : MonoBehaviour, ICraftExecutor
{
    [Serializable]
    public struct GimmickPrefabEntry
    {
        public ECraftGimmick type;
        public GameObject prefab;
    }

    [Tooltip("기믹 종류별 프리팹. 필업은 비워 둬도 된다 — 따르기와 같은 화면을 쓰므로 따르기 것을 대신 쓴다.")]
    [SerializeField] GimmickPrefabEntry[] prefabs;

    [Tooltip("띄운 기믹을 놓을 자리. 비어 있으면 이 오브젝트 아래에 만든다.")]
    [SerializeField] Transform gimmickRoot;

    [Tooltip("기믹이 바뀌어도 남아 있는 공통 표시. 실행 전 연결을 검증한다.")]
    [SerializeField] CraftGimmickHud hud;

    [Tooltip("기믹만 비추는 카메라. 바에서 멀리 떨어진 자리를 찍으므로 바의 배경과 손님이 함께 담기지 않는다.")]
    [SerializeField] Camera gimmickCamera;

    [Tooltip("기믹 화면이 자리잡는 그리기 순서의 바닥. 바 UI가 쓰는 값보다 확실히 높아야 한다. " +
             "막은 이 값 바로 아래, 기믹은 이 값 위, 공통 표시는 그보다 더 위에 놓인다.")]
    [SerializeField] int sortingBase = 1000;

    [Tooltip("공통 표시를 기믹보다 얼마나 위에 둘지. 기믹 안에서 쓰는 순서보다 커야 한다.")]
    [SerializeField] int hudSortingOffset = 500;

    [Inject] IObjectResolver resolver;

    CraftTimer runningTimer;

    /// <summary>제조 중 잠시 꺼 둔 바 화면의 UI. 끝나면 그대로 되돌린다.</summary>
    readonly List<Canvas> hiddenBarCanvases = new();

    /// <summary>지금 돌고 있는 기믹. 시계를 재울지 굴릴지 이 기믹에게 묻는다.</summary>
    ICraftGimmick currentGimmick;

    void Update()
    {
        // 플레이어가 실제로 조작할 수 있는 동안만 시간을 센다. 기믹 사이의 전환, 성공 연출,
        // 프리팹을 띄우는 사이의 빈 시간은 손쓸 수 없는 구간이라 빼는 게 맞다.
        if (currentGimmick == null || !currentGimmick.IsManualInputActive) return;

        runningTimer?.Tick(Time.deltaTime);
    }

    /// <summary>공통 화면을 준비한다. 실패하더라도 호출자는 End를 호출해야 한다.</summary>
    public void Begin(CraftTimer timer, CraftRunDisplay display)
    {
        if (gimmickCamera == null)
            throw new InvalidOperationException("기믹 카메라가 없습니다.");
        if (hud == null)
            throw new InvalidOperationException("기믹 HUD가 없습니다.");
        runningTimer = timer;
        ShowCraftScreen(true);
        hud.BeginCraft(timer, display.TimeLimitSec, display.HideTime);
    }

    /// <summary>화면을 복구한다. 단계 진행과 세션 종료는 CraftFlowController가 담당한다.</summary>
    public void End()
    {
        runningTimer = null;
        try { hud?.EndCraft(); }
        finally { ShowCraftScreen(false); }
    }
    /// <summary>기믹 프리팹을 띄우고 끝날 때까지 기다린 뒤 치운다.</summary>
    public async UniTask<GimmickResult> ExecuteAsync(GimmickStep step, CraftContext context,
                                              CraftTimer timer, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        GameObject prefab = ResolvePrefab(step.Type);

        if (prefab == null)
        {
            throw new InvalidOperationException($"'{step.Type}' 기믹의 프리팹이 없습니다: {step}");
        }

        GameObject instance = Instantiate(prefab, gimmickRoot != null ? gimmickRoot : transform);

        try
        {
            LiftAboveBarUi(instance);
            var gimmick = instance.GetComponentInChildren<ICraftGimmick>();

            if (gimmick == null)
            {
                throw new InvalidOperationException($"'{prefab.name}'에 ICraftGimmick 구현이 없습니다.");
            }

            // 기믹 안에서도 데이터나 사운드 같은 걸 주입받을 수 있어야 한다.
            resolver?.Inject(gimmick);

            // 진행 표시를 이 기믹에 연결한다. 기믹이 시작되기 전에 붙여야 첫 프레임부터 값이 보인다.
            hud?.BindGimmick(step, gimmick);

            currentGimmick = gimmick;

            var result = await gimmick.PlayAsync(step, context, timer, token);
            token.ThrowIfCancellationRequested();
            if (result == null) throw new InvalidOperationException($"'{step.Type}' 결과가 없습니다.");
            return result;
        }
        finally
        {
            // 이 기믹이 사라진 뒤에도 시계가 돌면, 다음 기믹을 띄우는 사이의 시간이 끼어든다.
            currentGimmick = null;

            try { hud?.UnbindGimmick(); }
            finally { if (instance != null) Destroy(instance); }
        }
    }

    /// <summary>
    /// 제조 화면을 켜고 끈다.
    ///
    /// 가리는 일이 둘로 나뉜다. 배경과 손님 같은 월드는 기믹 카메라가 단색으로 지우고 그 위에 그린다.
    /// 화면에 겹쳐 그리는 UI는 카메라와 무관하게 맨 위에 나오므로 카메라로는 지워지지 않는다 —
    /// 그건 덮지 않고 아예 꺼 버린다.
    ///
    /// 덮지 않는 이유가 있다. 겹쳐 그리는 막으로 바 UI를 가리면 그 막이 기믹의 월드 부분까지
    /// 함께 덮어 버린다. 병이나 고리처럼 UI가 아닌 기믹은 그렇게 하면 보이지 않는다.
    /// </summary>
    void ShowCraftScreen(bool visible)
    {
        if (visible)
        {
            HideBarCanvases();
            MatchMainCameraProjection();
        }
        else
        {
            RestoreBarCanvases();
        }

        if (gimmickCamera != null) gimmickCamera.gameObject.SetActive(visible);
        else if (visible) throw new InvalidOperationException("기믹 카메라가 없습니다.");
    }

    /// <summary>
    /// 바 화면의 UI를 잠시 끈다. 제조 루프가 가진 것(공통 표시, 띄운 기믹)은 건드리지 않는다.
    ///
    /// 어떤 UI가 있는지 미리 알아 두지 않고 그때그때 찾는다. 바에 UI가 늘어나도 여기를 고칠 일이
    /// 없고, 무엇을 꺼야 하는지 목록으로 관리하다 빠뜨리는 일도 생기지 않는다.
    /// </summary>
    void HideBarCanvases()
    {
        // 되돌리기 전에 또 부르면 껐던 목록을 잃어버려 바 UI가 영영 꺼진 채로 남는다.
        if (hiddenBarCanvases.Count > 0)
        {
            Debug.LogWarning("[GimmickRunner] 바 UI를 이미 꺼 둔 상태에서 또 껐습니다. 먼저 되돌립니다.");
            RestoreBarCanvases();
        }

        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var canvas in canvases)
        {
            if (!canvas.isRootCanvas || !canvas.enabled) continue;

            // 겹쳐 그리는 UI만 문제가 된다. 카메라에 붙거나 월드에 놓인 UI는 기믹 카메라가 알아서 지운다.
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;

            // 제조 루프가 가진 것은 남긴다.
            if (canvas.transform.IsChildOf(transform)) continue;

            canvas.enabled = false;
            hiddenBarCanvases.Add(canvas);
        }

        Debug.Log($"[GimmickRunner] 바 UI {hiddenBarCanvases.Count}개를 껐습니다: " +
                  string.Join(", ", hiddenBarCanvases.ConvertAll(c => c.name)));
    }

    /// <summary>꺼 뒀던 바 UI를 되돌린다. 그 사이 사라진 것은 건너뛴다.</summary>
    void RestoreBarCanvases()
    {
        int restored = 0;

        foreach (var canvas in hiddenBarCanvases)
        {
            if (canvas == null) continue;

            canvas.enabled = true;
            restored++;
        }

        if (hiddenBarCanvases.Count > 0)
            Debug.Log($"[GimmickRunner] 바 UI {restored}/{hiddenBarCanvases.Count}개를 되돌렸습니다.");

        hiddenBarCanvases.Clear();
    }

    /// <summary>
    /// 기믹 카메라의 화각을 메인 카메라와 맞춘다.
    ///
    /// 기믹은 바에서 멀리 떨어진 자리에 놓고 그곳만 따로 찍는다. 레이어를 나누거나 메인 카메라의
    /// 설정을 건드리지 않고도 바와 완전히 분리되는 방법이라, 기믹이 늘어도 손댈 곳이 없다.
    /// 다만 화면에 담기는 크기는 같아야 원래 만든 대로 보이므로 투영값만 그때그때 복사한다.
    /// </summary>
    void MatchMainCameraProjection()
    {
        Camera main = Camera.main;
        if (main == null) return;

        gimmickCamera.orthographic = main.orthographic;
        gimmickCamera.orthographicSize = main.orthographicSize;
        gimmickCamera.fieldOfView = main.fieldOfView;
        gimmickCamera.nearClipPlane = main.nearClipPlane;
        gimmickCamera.farClipPlane = main.farClipPlane;
    }

    /// <summary>
    /// 띄운 기믹이 바 쪽 UI에 가리지 않도록 그리기 순서를 손본다.
    ///
    /// 손대는 것은 **겹쳐 그리는 캔버스(ScreenSpaceOverlay)뿐**이다. 그것만 제조 루프가 계속 띄워 두는
    /// 패널들과 같은 값을 놓고 겨루기 때문이다. 나머지는 건드리지 않는다.
    ///
    /// 예전에는 기믹 안의 캔버스를 전부 1000씩 올렸는데, 그러면 기믹이 스스로 잡아 둔 앞뒤 관계가
    /// 깨진다. 셰이킹이 그 경우다 — Game Canvas(화면 공간, 11)는 1011로 올라가는데 그 위에 놓이도록
    /// 만들어 둔 타겟·스트라이크 노드(스프라이트, 19·20)는 그대로라, 캔버스가 노드를 덮어 버렸다.
    ///
    /// 스프라이트도 같이 올리는 방법은 쓰지 않는다. 셰이킹의 타겟 노드는 풀에서 게임이 시작된 뒤에
    /// 나오므로, 띄우는 순간 한 번 훑는 방식으로는 잡히지 않는다. 프리팹 값에 미리 1000을 더해 두는
    /// 것도 답이 아니다 — 그 프리팹을 단독 테스트 씬에서 켜면 반대로 깨지고, 기믹이 늘 때마다
    /// 사람이 기억해서 더해 줘야 한다.
    ///
    /// 화면 공간 캔버스와 월드 스프라이트는 기믹 카메라가 배경을 지우고 그 위에 그리므로, 바 쪽과
    /// 겹칠 일이 애초에 없다. 게다가 겹쳐 그리는 캔버스는 값과 무관하게 언제나 그것들보다 위에 나와서,
    /// 올려 봐야 얻는 것도 없었다.
    /// </summary>
    void LiftAboveBarUi(GameObject instance)
    {
        var canvases = instance.GetComponentsInChildren<Canvas>(true);
        var lifted = new List<string>();
        var kept = new List<string>();

        foreach (var canvas in canvases)
        {
            // 자식 캔버스는 부모를 따라가므로 건드리지 않는다. 건드리면 부모 안에서의 순서가 뒤집힌다.
            if (!canvas.isRootCanvas) continue;

            // 카메라부터 이어 준다. 프리팹 안의 카메라 참조는 씬 밖 오브젝트라 끊겨 있는데,
            // 카메라가 없는 화면 공간 캔버스를 Unity는 겹쳐 그리는 것으로 취급하고 renderMode까지
            // Overlay로 돌려준다. 그래서 이어 주기 전에 모드를 읽으면 무엇이든 Overlay로 읽힌다 —
            // 아래 판정이 통째로 뒤집히므로 순서를 바꾸면 안 된다.
            if (canvas.worldCamera == null && gimmickCamera != null)
                canvas.worldCamera = gimmickCamera;

            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                kept.Add($"{canvas.name}({canvas.renderMode}, {canvas.sortingOrder})");
                continue;
            }

            if (!EnsureSceneInstance(canvas.gameObject)) continue;

            canvas.overrideSorting = true;
            canvas.sortingOrder += sortingBase;
            lifted.Add($"{canvas.name}({canvas.renderMode}, {canvas.sortingOrder})");
        }

        // 무엇을 올렸고 무엇을 그대로 뒀는지 남긴다. 기믹이 가려질 때 정렬 값을 하나씩 눌러 보지 않고
        // 이 줄만 봐도 원인이 갈린다 — 올리지 말아야 할 것을 올렸는지, 애초에 다른 곳에서 밀렸는지.
        Debug.Log($"[GimmickRunner] 정렬 조정 — 올림: {(lifted.Count > 0 ? string.Join(", ", lifted) : "없음")} / " +
                  $"유지: {(kept.Count > 0 ? string.Join(", ", kept) : "없음")}");

        if (hud != null && EnsureSceneInstance(hud.gameObject))
            hud.SetSortingOrder(sortingBase + hudSortingOffset);
    }

    /// <summary>
    /// 정렬 순서를 바꿔도 되는 대상인지 — 씬에 띄운 사본인지 프로젝트의 프리팹 에셋인지 확인한다.
    ///
    /// 에셋을 만지면 그 값이 디스크에 저장된다. 화면에는 아무 표시도 나지 않고, 나중에 관계없는
    /// 프리팹이 수정 상태로 잡혀 커밋에 섞여 들어온다. 게다가 여기서 더하는 값은 누적되므로
    /// 플레이할 때마다 1000씩 밀려 올라가고, 어느 순간 정렬 순서가 실제로 깨진다.
    ///
    /// 조용히 잘못되는 종류의 사고라 넘어가지 않고 어떤 오브젝트인지 이름을 찍어 멈춘다.
    /// 빌드에서는 프리팹 에셋을 참조할 방법이 없어 검사 자체가 빠진다.
    /// </summary>
    static bool EnsureSceneInstance(GameObject target)
    {
#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(target))
        {
            Debug.LogError($"[GimmickRunner] '{target.name}'은 씬에 띄운 사본이 아니라 프리팹 에셋입니다. " +
                           "정렬 순서를 올리면 에셋에 저장되므로 건드리지 않고 넘어갑니다. " +
                           "인스펙터에 프로젝트 창의 프리팹이 직접 꽂혀 있는지 확인하세요.");
            return false;
        }
#endif
        return target != null;
    }

    /// <summary>
    /// 기믹 종류에 맞는 프리팹을 찾는다.
    /// 필업은 전용 프리팹이 없으면 따르기 것을 쓴다 — 둘은 같은 화면을 쓰기 때문이다.
    /// </summary>
    GameObject ResolvePrefab(ECraftGimmick type)
    {
        GameObject found = FindPrefab(type);
        if (found != null) return found;

        if (type == ECraftGimmick.FillUp) return FindPrefab(ECraftGimmick.Pour);

        return null;
    }

    GameObject FindPrefab(ECraftGimmick type)
    {
        if (prefabs == null) return null;

        foreach (var entry in prefabs)
        {
            if (entry.type == type && entry.prefab != null) return entry.prefab;
        }

        return null;
    }
}
