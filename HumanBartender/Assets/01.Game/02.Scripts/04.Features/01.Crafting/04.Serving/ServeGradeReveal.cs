using System;
using UnityEngine;

/// <summary>월드 파티클과 등급 글자를 전용 카메라로 렌더링해 제조 완료 화면 위에 합성한다.</summary>
public sealed class ServeGradeReveal : MonoBehaviour
{
    [Serializable]
    private struct Entry
    {
        public ENewGrade grade;
        public GradeRevealSequence effect;
    }

    [Header("제조 등급 이펙트")]
    [Tooltip("등급 이펙트만 투명 Render Texture에 렌더링하는 카메라입니다.")]
    [SerializeField] private Camera effectCamera;
    [Tooltip("각 제조 등급에 사용할 Play 씬의 GradeRevealSequence 인스턴스입니다.")]
    [SerializeField] private Entry[] effects;

    private GradeRevealSequence playing;

    public bool TryPlay(ENewGrade grade)
    {
        Stop();
        if (effectCamera == null || effectCamera.targetTexture == null || effects == null) return false;

        foreach (var entry in effects)
        {
            if (entry.grade != grade || entry.effect == null) continue;
            playing = entry.effect;
            gameObject.SetActive(true);
            playing.gameObject.SetActive(true);
            playing.Stop();
            playing.Play();
            effectCamera.enabled = true;
            return true;
        }
        return false;
    }

    public void Stop()
    {
        Clear();
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnDisable() => Clear();

    private void Clear()
    {
        if (effectCamera != null) effectCamera.enabled = false;
        if (playing == null) return;
        playing.Stop();
        playing.gameObject.SetActive(false);
        playing = null;
    }
}
