using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

[Serializable]
/// <summary>이미지 오브젝트의 등장·위치·크기·페이드를 정의하는 PlayableBehaviour.</summary>
public class CutSceneImageBehaviour : PlayableBehaviour
{
    // ── 데이터 (인스펙터에서 설정) ────────────────────────────────────
    [Header("Image")]
    [Tooltip("이미지 고유 키 (항상 필수). 다른 트랙(SpriteAnim, Move 등)에서 이 값으로 참조")]
    public string imagePath;

    [Header("스프라이트 소스 (선택)")]
    [Tooltip("비어있으면 Resources/Cutscenes/{imagePath}에서 단일 이미지 로드.\n채우면 이 시트에서 특정 프레임을 로드")]
    public string sheetPath;
    [Tooltip("시트에서 몇 번째 프레임 (0부터). sheetPath가 있을 때만 사용")]
    public int frameIndex = 0;

    [Header("Position")]
    public AnchorType anchor = AnchorType.Center;
    public float offsetX;
    public float offsetY;

    [Header("Layer")]
    [Tooltip("렌더 순서. 값이 클수록 앞에 표시 (다른 이미지 위에 그려짐)")]
    public int sortOrder = 0;

    [Header("Enter")]
    public EEneterPreset enterType = EEneterPreset.FadeIn;
    public float enterDuration = 0.3f;
    public Ease enterEase = Ease.Unset;
    [Range(0.1f, 2f)] public float enterSlideDistance = 1.0f;

    [Header("Exit")]
    public EExitPreset exitType = EExitPreset.FadeOut;
    public float exitDuration = 0.3f;
    public Ease exitEase = Ease.Unset;
    [Range(0.1f, 2f)] public float exitSlideDistance = 1.0f;

    // ── 런타임 상태 (Mixer에서 관리) ──────────────────────────────────
    [NonSerialized] public Image assignedImage;
    [NonSerialized] public bool isActive;
    [NonSerialized] public bool enterDone;
    [NonSerialized] public bool exitDone;

    public override void OnPlayableCreate(Playable playable)
    {
        isActive = false;
        enterDone = false;
        exitDone = false;
    }
}
