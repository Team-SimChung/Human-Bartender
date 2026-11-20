using System;
using UnityEngine;

/// <summary>대본의 장소 이동 명령에 연결하는 표현 설정. 대사와 이동 규칙은 기존 CSV/진행 서비스가 소유한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(WorldInteractionTarget))]
public sealed class WorldDialogueTravelSettings : MonoBehaviour
{
    [SerializeField] private Vector2 playerPosition = new(2.02f, -.7f);
    [SerializeField] private bool faceRight = true;
    [SerializeField, Min(0)] private float fadeOutDuration = .35f;
    [SerializeField, Min(0)] private float delayBeforeLoad = .05f;
    [Tooltip("페이드 아웃 완료 후 화면 복귀를 시작하기까지의 최소 시간. 씬 준비가 더 오래 걸리면 준비를 기다립니다.")]
    [SerializeField, Min(0)] private float minimumBlackDuration = .35f;
    [SerializeField, Min(0)] private float fadeInDuration = .45f;
    [Tooltip("도착한 씬에서 말풍선 화자와 NPC를 연결할 기존 Story Scene Id입니다.")]
    [SerializeField] private string destinationTargetId = "neighborhood-homeCat";

    public readonly struct Snapshot
    {
        public readonly Vector2 PlayerPosition;
        public readonly bool FaceRight;
        public readonly float FadeOutDuration, DelayBeforeLoad, MinimumBlackDuration, FadeInDuration;
        public readonly string DestinationTargetId;
        internal Snapshot(WorldDialogueTravelSettings value)
        {
            PlayerPosition = value.playerPosition;
            FaceRight = value.faceRight;
            FadeOutDuration = value.fadeOutDuration;
            DelayBeforeLoad = value.delayBeforeLoad;
            MinimumBlackDuration = value.minimumBlackDuration;
            FadeInDuration = value.fadeInDuration;
            DestinationTargetId = value.destinationTargetId;
        }
        public void Validate()
        {
            if (!float.IsFinite(PlayerPosition.x) || !float.IsFinite(PlayerPosition.y) ||
                !Valid(FadeOutDuration) || !Valid(DelayBeforeLoad) || !Valid(MinimumBlackDuration) || !Valid(FadeInDuration) ||
                string.IsNullOrWhiteSpace(DestinationTargetId))
                throw new InvalidOperationException("대화 장소 이동의 위치·시간·도착 대상을 확인하세요.");
        }
        private static bool Valid(float value) => float.IsFinite(value) && value >= 0;
    }

    public Snapshot Capture()
    {
        var result = new Snapshot(this);
        result.Validate();
        return result;
    }
    public void Configure(Vector2 position, bool right, float fadeOut, float beforeLoad,
        float minimumBlack, float fadeIn, string targetId)
    {
        playerPosition = position; faceRight = right; fadeOutDuration = fadeOut;
        delayBeforeLoad = beforeLoad; minimumBlackDuration = minimumBlack; fadeInDuration = fadeIn;
        destinationTargetId = targetId;
        Capture();
    }
}
