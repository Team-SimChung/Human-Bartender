using UnityEngine;

/// <summary>
/// 기믹이 놓이는 무대. 띄운 기믹의 부모에 붙어서, 이 화면을 비추는 카메라가 무엇인지 알려 준다.
///
/// 기믹 무대는 바에서 멀리 떨어진 자리에 있고 전용 카메라가 그곳만 비춘다. 화면 좌표를 월드로
/// 바꾸거나 캔버스를 카메라에 붙여야 하는 기믹은 메인 카메라가 아니라 이 카메라를 봐야 한다 —
/// 메인 카메라를 쓰면 바가 있는 자리에 좌표를 잡아 무대 밖에 놓이게 된다.
///
/// 기믹이 이걸 몰라도 된다. 화면에 겹쳐 그리는 UI만 쓰는 기믹은 카메라와 무관하게 잘 나온다.
/// </summary>
public class CraftGimmickStage : MonoBehaviour
{
    [SerializeField] Camera stageCamera;

    /// <summary>이 무대를 비추는 카메라. 배선되지 않았으면 null이다.</summary>
    public Camera StageCamera => stageCamera;

    /// <summary>
    /// 무대 카메라를 찾는다. 무대 밖(독립 테스트 씬 등)에서 돌고 있으면 메인 카메라로 물러선다.
    /// </summary>
    public static Camera Find(Component gimmick)
    {
        if (gimmick == null) return Camera.main;

        var stage = gimmick.GetComponentInParent<CraftGimmickStage>();
        Camera found = stage != null ? stage.StageCamera : null;

        return found != null ? found : Camera.main;
    }
}
