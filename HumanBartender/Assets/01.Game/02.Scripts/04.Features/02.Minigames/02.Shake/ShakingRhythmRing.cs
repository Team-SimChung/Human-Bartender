using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 판정 타깃, 콤보 빛과 타격 연출에 사용하는 uGUI 원형 그래픽.
/// MaskableGraphic이므로 Image처럼 Canvas 안에서 색/투명도/마스킹을 사용할 수 있다.
/// 씬에 배치한 컴포넌트의 메시만 그리며 GameObject를 코드로 생성하지 않는다.
/// Unity UI가 편집 중에도 메시를 갱신하므로 Play 전 Scene/Game 뷰에서 확인할 수 있다.
///
/// 외곽 원과 안쪽 원 사이를 삼각형으로 연결해 링을 만든다.
/// thickness가 반지름 이상이면 안쪽 반지름이 0이 되어 채워진 원으로도 사용할 수 있다.
/// </summary>
[AddComponentMenu("UI/Shaking Rhythm Ring")]
public class ShakingRhythmRing : MaskableGraphic
{
    // UI 좌표 단위의 링 두께. RectTransform을 키우면 반지름만 커지고 이 값은 유지된다.
    // 단, Transform의 scale을 키우는 타격 연출에서는 두께도 화면에서 함께 커진다.
    [SerializeField, Min(0.1f)] private float thickness = 1.5f;
    // 원을 근사할 구간 수. 높을수록 둥글어지고 정점 수는 (segments + 1) * 2개가 된다.
    [SerializeField, Range(16, 128)] private int segments = 64;

    /// <summary>
    /// Unity UI가 그래픽을 다시 그릴 때 호출한다. RectTransform의 현재 크기와 Graphic.color로
    /// 메시를 구성한다. 색/크기가 바뀌면 UI 갱신 과정에서 이 메서드가 다시 실행될 수 있다.
    /// </summary>
    /// <param name="vh">이 그래픽의 정점/삼각형을 채우는 UI 메시 작성 도구.</param>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        // 이전 메시를 지워야 갱신할 때 삼각형이 계속 쌓이지 않는다.
        vh.Clear();
        // 픽셀 보정이 적용된 로컬 Rect. rect.center를 사용해 피벗이 중앙이 아니어도 맞게 그린다.
        Rect rect = GetPixelAdjustedRect();
        // 직사각형이면 짧은 변을 기준으로 원을 넣는다. 타원이 아니라 원을 유지한다.
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        // 두께가 반지름보다 커도 안쪽 원의 반지름이 음수가 되지 않도록 제한한다.
        float inner = Mathf.Max(0, radius - thickness);
        // Inspector 범위를 벗어나는 값이 들어와도 메시 구간 수의 허용 범위를 유지한다.
        int count = Mathf.Clamp(segments, 16, 128);
        // 마지막 i=count에서는 시작점과 같은 각도(2π)를 한 번 더 추가한다.
        // 이 중복 점으로 마지막 구간을 처음 위치까지 연결해 원의 틈을 닫는다.
        for (int i = 0; i <= count; i++)
        {
            // i/count만큼 원주를 진행한다. Cos/Sin으로 해당 방향의 단위 벡터를 구한다.
            float angle = i * Mathf.PI * 2 / count;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            // 각 각도마다 외곽/안쪽 정점을 차례대로 추가한다.
            // 텍스처 문양을 사용하지 않아 UV는 0이고, 표시 색은 Graphic.color에서 가져온다.
            vh.AddVert(rect.center + direction * radius, color, Vector2.zero);
            vh.AddVert(rect.center + direction * inner, color, Vector2.zero);
            // 첫 두 정점에는 이전 구간이 없으므로 아직 삼각형을 만들지 않는다.
            if (i == 0) continue;
            // 이전 외곽/안쪽 = end-2/end-1, 현재 외곽/안쪽 = end/end+1.
            // 이 네 점의 띠 한 조각을 두 삼각형으로 만들어 링 구간 하나를 채운다.
            int end = i * 2;
            vh.AddTriangle(end - 2, end - 1, end);
            vh.AddTriangle(end, end - 1, end + 1);
        }
    }
}
