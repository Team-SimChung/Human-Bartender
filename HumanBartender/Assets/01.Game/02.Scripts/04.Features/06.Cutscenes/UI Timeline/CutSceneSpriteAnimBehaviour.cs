using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using static UnityEngine.InputSystem.OnScreen.OnScreenStick;

/// <summary>
/// 클립 구간 동안 UI Image에 스프라이트 시트 기반 프레임 애니메이션 재생.
///
/// 스프라이트 시트 설정:
///   1. 텍스처의 Sprite Mode를 Multiple로 설정
///   2. Sprite Editor에서 슬라이스
///   3. Resources 폴더 안에 배치
///   4. spriteSheetPath에 텍스처 경로 입력 (확장자 제외)
///
/// 예시:
///   파일 위치: Resources/Cutscenes/luna_walk_sheet.png
///   spriteSheetPath: "Cutscenes/luna_walk_sheet"
///   → 서브 스프라이트 luna_walk_sheet_0, _1, _2... 가 자동 로드됨
/// </summary>
[Serializable]
public class CutSceneSpriteAnimBehaviour : PlayableBehaviour
{
    [Header("대상")]
    [Tooltip("ImageTrack에서 등록한 imagePath와 동일한 값")]
    public string imagePath;

    [Header("스프라이트 시트")]
    [Tooltip("Resources 하위 경로 (확장자 제외). 예: Cutscenes/luna_walk_sheet")]
    public string spriteSheetPath;

    [Tooltip("초당 프레임 수")]
    public float fps = 12f;

    [Tooltip("클립 내에서 반복 재생")]
    public bool loop = true;

    [Tooltip("특정 프레임 구간만 재생 (0이면 전체)")]
    public int startFrame = 0;
    [Tooltip("끝 프레임 (0이면 마지막까지)")]
    public int endFrame = 0;

    [Header("Pivot 보정")]
    [Tooltip("이 애니메이션의 기준점. Image의 RectTransform.pivot을 덮어씀")]
    public bool overridePivot = false;
    [Tooltip("Pivot (0,0)=좌하단, (0.5,0.5)=중앙, (0.5,0)=하단중앙(발 기준)")]
    public Vector2 pivot = new Vector2(0.5f, 0.5f);
    [Tooltip("Pivot 변경으로 인한 위치 틀어짐을 자동 보정")]
    public bool autoCompensate = true;

    // ── 런타임 ────────────────────────────────────────────────────────
    [NonSerialized] internal CutSceneTimelineManager manager;
    [NonSerialized] private Sprite[] frames;
    [NonSerialized] private bool loaded;
    [NonSerialized] private Image targetImage;
    [NonSerialized] private int actualStartFrame;
    [NonSerialized] private int actualEndFrame;
    [NonSerialized] private Vector2 prevPivot;
    [NonSerialized] private bool pivotApplied;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        loaded = false;
        targetImage = null;
        pivotApplied = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        manager = playerData as CutSceneTimelineManager;
        if (manager == null) return;

        // 대상 Image 찾기
        if (targetImage == null)
        {
            targetImage = manager.GetActiveImage(imagePath);
            if (targetImage == null) return;
        }

        // Pivot 적용 (한 번만)
        if (overridePivot && !pivotApplied)
        {
            pivotApplied = true;
            RectTransform rect = targetImage.GetComponent<RectTransform>();
            prevPivot = rect.pivot;

            if (autoCompensate)
            {
                // pivot 변경 시 위치가 틀어지므로 보정
                // 보정량 = (newPivot - oldPivot) * size
                Vector2 pivotDelta = pivot - prevPivot;
                Vector2 size = rect.rect.size;
                rect.pivot = pivot;
                rect.anchoredPosition += new Vector2(pivotDelta.x * size.x, pivotDelta.y * size.y);
            }
            else
            {
                rect.pivot = pivot;
            }
        }

        // 스프라이트 시트에서 서브 스프라이트 로드 (한 번만)
        if (!loaded)
        {
            loaded = true;

            // LoadAll<Sprite>는 해당 텍스처의 모든 서브 스프라이트를 반환
            Sprite[] allSprites = Resources.LoadAll<Sprite>($"Cutscenes/{spriteSheetPath}");

            if (allSprites == null || allSprites.Length == 0)
            {
                Debug.LogWarning(
                    $"[SpriteAnim] 스프라이트를 찾을 수 없음: {spriteSheetPath}\n" +
                    "확인사항: Sprite Mode가 Multiple인지, Resources 폴더 안에 있는지");
                return;
            }

            // _숫자 기준 정렬 (Unity 슬라이스: sheetName_0, sheetName_1, ...)
            Array.Sort(allSprites, (a, b) =>
            {
                int numA = int.Parse(a.name.Substring(a.name.LastIndexOf('_') + 1));
                int numB = int.Parse(b.name.Substring(b.name.LastIndexOf('_') + 1));
                return numA.CompareTo(numB);
            });

            // 프레임 범위 계산
            actualStartFrame = Mathf.Clamp(startFrame, 0, allSprites.Length - 1);
            actualEndFrame = (endFrame <= 0 || endFrame >= allSprites.Length)
                ? allSprites.Length - 1
                : endFrame;

            // 범위만큼 잘라서 저장
            int count = actualEndFrame - actualStartFrame + 1;
            frames = new Sprite[count];
            Array.Copy(allSprites, actualStartFrame, frames, 0, count);
        }

        if (frames == null || frames.Length == 0) return;

        // 현재 시간 → 프레임 인덱스
        float elapsed = (float)playable.GetTime();
        int frameIndex = Mathf.FloorToInt(elapsed * fps);

        if (loop)
        {
            frameIndex = frameIndex % frames.Length;
        }
        else
        {
            frameIndex = Mathf.Min(frameIndex, frames.Length - 1);
        }

        targetImage.sprite = frames[frameIndex];
        targetImage.SetNativeSize();
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        // Pivot 복구 — 다음 SpriteAnim 클립이 자기 pivot을 설정하므로
        // 클립 사이에 빈 구간이 있을 때만 의미 있음
        if (overridePivot && pivotApplied && targetImage != null)
        {
            RectTransform rect = targetImage.GetComponent<RectTransform>();

            if (autoCompensate)
            {
                Vector2 pivotDelta = prevPivot - rect.pivot;
                Vector2 size = rect.rect.size;
                rect.pivot = prevPivot;
                rect.anchoredPosition += new Vector2(pivotDelta.x * size.x, pivotDelta.y * size.y);
            }
            else
            {
                rect.pivot = prevPivot;
            }
        }

        targetImage = null;
        pivotApplied = false;
    }
}
