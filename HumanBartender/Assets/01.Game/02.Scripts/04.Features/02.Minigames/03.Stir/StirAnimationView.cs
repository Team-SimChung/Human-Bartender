using UnityEngine;
using UnityEngine.UI;

/// <summary>입력마다 재생을 연장하고 3프레임 묶음의 마지막 프레임에서 멈춘다.</summary>
public class StirAnimationView : MonoBehaviour
{
    [SerializeField] Sprite[] frames;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Image image;
    [SerializeField, Min(1f)] float framesPerSecond = 12f;
    [SerializeField, Min(1)] int framesPerInput = 3;

    double framePosition;
    double stopPosition;

    public void ResetPlayback()
    {
        // 기존 클립과 동시에 스프라이트를 갱신하지 않도록 한다.
        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        framePosition = 0d;
        stopPosition = 0d;
        ShowFrame(0);
    }

    public void PlayOnInput()
    {
        if (frames == null || frames.Length == 0) return;

        int groupSize = Mathf.Max(1, framesPerInput);
        // 마지막 입력부터 최소 한 묶음을 재생한 뒤 다음 중단점에서 멈춘다.
        // 이전 입력 횟수를 누적하지 않고 현재 재생 위치를 기준으로 다시 계산한다.
        stopPosition = System.Math.Ceiling((framePosition + groupSize) / groupSize) * groupSize;
        ShowFrame((int)System.Math.Floor(framePosition));
    }

    // 매니저가 입력 처리 후 호출한다. 포커스 이탈/일시정지 시에는 진행하지 않는다.
    public void Tick(float deltaTime)
    {
        if (!isActiveAndEnabled || framePosition >= stopPosition) return;

        framePosition = System.Math.Min(stopPosition,
            framePosition + Mathf.Max(0f, deltaTime) * Mathf.Max(1f, framesPerSecond));

        int frame = framePosition >= stopPosition
            ? (int)stopPosition - 1
            : (int)System.Math.Floor(framePosition);
        ShowFrame(frame);

        // 정지할 때만 전체 루프를 제거해 장시간 연타에도 좌표가 커지지 않게 한다.
        if (framePosition >= stopPosition && frames != null && frames.Length > 0)
        {
            framePosition %= frames.Length;
            stopPosition = framePosition;
        }
    }

    void ShowFrame(int index)
    {
        if (frames == null || frames.Length == 0) return;
        Sprite sprite = frames[index % frames.Length];
        if (spriteRenderer != null) spriteRenderer.sprite = sprite;
        if (image != null && image.sprite != sprite) image.sprite = sprite;
    }
}
