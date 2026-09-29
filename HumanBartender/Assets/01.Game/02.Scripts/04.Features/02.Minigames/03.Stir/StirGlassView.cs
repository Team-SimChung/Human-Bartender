using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 탑다운 잔의 표시 담당. 판정은 전혀 하지 않고 StirManager가 넘겨준 상태를 그리기만 한다.
///
/// 구성 요소는 셋이다 — 중심에 피벗을 둔 바 스푼 시침, 4방위 키 배지, 잔 둘레의 제한시간 링.
/// 잔 속 얼음은 아트 확정 후에 붙는다(명세 §2.2).
/// </summary>
public class StirGlassView : MonoBehaviour
{
    [Header("Spoon")]
    [Tooltip("바 스푼 시침. 잔 중심에 피벗을 두고 Z 회전만 한다. " +
             "프로토타입처럼 헤드(bowl)가 중심에 오고 손잡이 쪽 축이 현재 방위를 가리킨다.")]
    [SerializeField] RectTransform spoon;
    [Tooltip("90° 회전을 따라잡는 속도. 높을수록 딱딱 끊어지고, 낮을수록 늘어진다. " +
             "제한시간이 2초라 너무 낮추면 스푼이 판정을 못 따라온다.")]
    [SerializeField] float spoonFollowSpeed = 18f;

    [Header("Badges")]
    [Tooltip("W, D, S, A 순서로 넣는다 — EStirDirection의 값 순서와 같아야 한다.")]
    [SerializeField] StirKeyBadge[] badges = new StirKeyBadge[StirDirections.Count];

    [Header("Timer Ring")]
    [Tooltip("남은 시도 시간을 그리는 Radial360 Filled 이미지.")]
    [SerializeField] Image timerRing;
    [SerializeField] Color timerColor = new Color(0.35f, 0.82f, 0.78f);
    [Tooltip("경고색으로 바뀌는 잔여 비율. 명세 §2.2의 34%.")]
    [SerializeField] float warnRatio = 0.34f;
    [SerializeField] Color warnColor = new Color(0.92f, 0.36f, 0.34f);

    /// <summary>스푼의 목표 각도. 방위로 되돌리지 않고 계속 빼기만 해서 항상 시계 방향으로만 돈다.</summary>
    float targetAngle;
    float currentAngle;

    // 노드의 글자·화살표·순번은 에디터 셋업(StirSceneSetup)이 만들 때 한 번 박아둔다.

    void Update()
    {
        if (spoon == null) return;

        // 프레임률에 무관한 지수 보간. Lerp에 deltaTime을 그대로 넣으면 프레임률에 따라 속도가 달라진다.
        currentAngle = Mathf.Lerp(currentAngle, targetAngle,
                                  1f - Mathf.Exp(-spoonFollowSpeed * Time.deltaTime));

        spoon.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }

    /// <summary>스푼을 특정 방위로 즉시 옮긴다. 스터 시작 시점에만 쓴다.</summary>
    public void ResetSpoon(int direction)
    {
        targetAngle = StirDirections.ToAngle(direction);
        currentAngle = targetAngle;

        if (spoon != null) spoon.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
    }

    /// <summary>정답 하나마다 시계 방향으로 90° 굴린다.</summary>
    public void AdvanceSpoon() => targetAngle -= 90f;

    /// <summary>현재 방위와 다음에 눌러야 할 방위를 배지에 반영한다. 없는 방위는 -1을 넘긴다.</summary>
    public void SetBadges(int current, int next)
    {
        for (int i = 0; i < badges.Length; i++)
        {
            if (badges[i] == null) continue;

            // Next가 Current보다 우선이다 — 눌러야 할 키를 놓치는 쪽이 더 치명적이다.
            StirKeyBadge.EState state =
                i == next ? StirKeyBadge.EState.Next :
                i == current ? StirKeyBadge.EState.Current :
                StirKeyBadge.EState.Idle;

            badges[i].SetState(state);
        }
    }

    /// <summary>남은 시도 시간 비율(0~1).</summary>
    public void SetTimer(float ratio)
    {
        if (timerRing == null) return;

        timerRing.fillAmount = Mathf.Clamp01(ratio);
        timerRing.color = ratio < warnRatio ? warnColor : timerColor;
    }

    /// <summary>판정이 모두 끝난 상태. 배지를 끄고 링을 비운다.</summary>
    public void SetIdle()
    {
        for (int i = 0; i < badges.Length; i++)
        {
            badges[i]?.SetState(StirKeyBadge.EState.Idle);
        }

        SetTimer(0f);
    }
}
