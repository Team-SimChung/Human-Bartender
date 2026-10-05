using System;
using UnityEngine;

/// <summary>
/// 왼쪽 연출 영역의 정면 잔(명세 §2.1). 판정에는 전혀 관여하지 않고 보여주기만 한다.
///
/// 오른쪽 탑다운 얼음을 흉내 내지 않는다 — StirIceSwirl이 굴리고 있는 바로 그 조각들의 좌표를
/// 읽어 정면 시점으로 투영한다. 그래서 부딪혀 뭉치거나 한쪽으로 몰리는 움직임까지 두 화면이
/// 저절로 같아진다. 속도만 공유했다면 "비슷하게 도는 다른 얼음"이 됐을 것이다.
///
/// 투영은 탑다운 원을 납작한 타원으로 눕히는 것뿐이다 — x는 그대로 가로, y는 깊이가 되어
/// 화면 위아래 + 크기 + 앞뒤 순서로 나뉜다. 프로토타입도 스푼 끝을 sin/cos로 같은 타원에
/// 태운다(Stir_Minigame.html의 spoonBottomX/Y).
///
/// 조각의 높이와 처음 기울기는 씬에 놓인 대로 읽는다. 가로 위치와 깊이는 오른쪽에서 오므로,
/// 여기서 손으로 정하는 건 "잔 속 어느 높이에 뜬 얼음인가"뿐이다.
/// </summary>
public class StirSideGlassView : MonoBehaviour
{
    [Serializable]
    public struct SideCube
    {
        [Tooltip("얼음 조각. 잔 중심을 원점으로 하는 좌표계에 놓여 있어야 한다. y는 잔 속 높이로 쓴다.")]
        public RectTransform target;
        [Tooltip("굴러가는 속도 배수. 오른쪽 조각의 자전값과 부호를 맞춰 두면 같은 조각으로 읽힌다.")]
        public float rollFactor;
    }

    [Tooltip("좌표를 가져올 오른쪽 탑다운 얼음. 비우면 이 잔은 그냥 정지 화면이 된다.")]
    [SerializeField] StirIceSwirl source;
    [Tooltip("오른쪽 조각과 같은 순서로 넣는다 — i번이 서로 같은 얼음이어야 한다.")]
    [SerializeField] SideCube[] cubes;

    [Header("Projection")]
    [Tooltip("가장 바깥 조각이 그리는 타원의 가로 반지름(UI 단위). 잔 안쪽 폭에서 조각 크기를 뺀 값으로 잡는다.")]
    [SerializeField] float orbitRadius = 31f;
    [Tooltip("타원의 세로/가로 비. 작을수록 눈높이에서 본 것처럼 납작해진다.\n" +
             "0이면 얼음이 가로로만 미끄러져 도는 느낌이 사라지고, 1이면 정면인데 탑다운처럼 보인다.")]
    [SerializeField] float depthSquash = 0.34f;

    [Header("Depth")]
    [Tooltip("가장 앞(잔 앞면 쪽) 조각의 크기 배수.")]
    [SerializeField] float nearScale = 1.08f;
    [Tooltip("가장 뒤 조각의 크기 배수. near보다 작아야 원근으로 읽힌다.")]
    [SerializeField] float farScale = 0.86f;
    [SerializeField] float nearAlpha = 0.95f;
    [Tooltip("뒤쪽 조각의 투명도. 앞 조각과 액체 너머로 비쳐 보이는 정도다.")]
    [SerializeField] float farAlpha = 0.62f;

    Vector2[] bases;
    float[] rolls;
    CanvasGroup[] groups;

    /// <summary>깊이 순으로 형제 순서를 바꿀 때 쓰는 작업용 인덱스. 매 프레임 새로 할당하지 않는다.</summary>
    int[] order;

    /// <summary>멈춘 뒤 한 프레임만 더 반영하고 쉬기 위한 표시.</summary>
    bool settled;

    void Awake()
    {
        int count = cubes?.Length ?? 0;

        bases = new Vector2[count];
        rolls = new float[count];
        groups = new CanvasGroup[count];
        order = new int[count];

        for (int i = 0; i < count; i++)
        {
            RectTransform target = cubes[i].target;
            order[i] = i;

            if (target == null) continue;

            bases[i] = target.anchoredPosition;
            rolls[i] = target.localEulerAngles.z;
            groups[i] = target.GetComponent<CanvasGroup>();
        }

        // 시작하자마자 오른쪽 배치가 반영돼 있어야 한다. 켜자마자 얼음이 순간이동하는 걸 막는다.
        Apply(0f);
    }

    void LateUpdate()
    {
        // LateUpdate인 이유가 있다 — StirIceSwirl이 이번 프레임에 굴린 결과를 읽어야 한다.
        // Update끼리는 순서가 정해져 있지 않아서 한 프레임 뒤처진 좌표를 볼 수 있다.
        if (source == null || bases == null) return;

        bool moving = source.SwirlSpeed > 0f;

        // 멈춰 있으면 좌표도 그대로다. 마지막 한 프레임만 반영하고 쉰다.
        if (!moving && settled) return;
        settled = !moving;

        Apply(Time.deltaTime);
    }

    void Apply(float dt)
    {
        if (source == null) return;

        float inverseRadius = 1f / Mathf.Max(source.LayoutRadius, 0.0001f);
        int count = bases.Length;

        for (int i = 0; i < count; i++)
        {
            RectTransform target = cubes[i].target;
            if (target == null) continue;

            Vector2 top = source.GetCubePosition(i) * inverseRadius;

            // 탑다운의 +y는 잔의 안쪽(뒤). 정면에서는 그게 화면 위로 올라가면서 멀어지는 방향이 된다.
            float depth = Mathf.Clamp(top.y, -1f, 1f);
            float t = (depth + 1f) * 0.5f;

            target.anchoredPosition = new Vector2(
                bases[i].x + top.x * orbitRadius,
                bases[i].y + depth * orbitRadius * depthSquash);

            target.localScale = Vector3.one * Mathf.Lerp(nearScale, farScale, t);

            rolls[i] -= source.SwirlSpeed * cubes[i].rollFactor * dt;
            target.localRotation = Quaternion.Euler(0f, 0f, rolls[i]);

            if (groups[i] != null) groups[i].alpha = Mathf.Lerp(nearAlpha, farAlpha, t);
        }

        SortByDepth();
    }

    /// <summary>
    /// 앞쪽 조각이 뒤쪽을 가리도록 형제 순서를 다시 매긴다. uGUI는 계층 순서가 곧 그리는 순서라
    /// 이걸 하지 않으면 뒤로 돌아간 얼음이 앞 얼음 위에 그려져 깊이가 뒤집혀 보인다.
    ///
    /// 조각이 6개뿐이라 삽입 정렬이면 충분하다 — 거의 정렬된 상태로 들어오므로 대개 비교만 하고 끝난다.
    /// </summary>
    void SortByDepth()
    {
        int count = order.Length;

        for (int i = 1; i < count; i++)
        {
            int current = order[i];
            float key = Depth(current);
            int j = i - 1;

            // 깊이 내림차순 — 먼 조각이 앞 인덱스, 즉 먼저 그려진다.
            while (j >= 0 && Depth(order[j]) < key)
            {
                order[j + 1] = order[j];
                j--;
            }

            order[j + 1] = current;
        }

        for (int i = 0; i < count; i++)
        {
            RectTransform target = cubes[order[i]].target;

            // 순서가 그대로면 건드리지 않는다. SetSiblingIndex는 캔버스를 다시 짜게 만든다.
            if (target != null && target.GetSiblingIndex() != i) target.SetSiblingIndex(i);
        }
    }

    float Depth(int index) => source.GetCubePosition(index).y;
}
