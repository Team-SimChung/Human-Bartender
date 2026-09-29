/// <summary>
/// 화면 앵커. 컷씬 이미지와 대사창을 화면 어디에 붙일지 정한다.
///
/// 구형 cutscenes.json의 position_presets가 쓰던 값이었지만 그 데이터는 걷어냈다. 지금은 컷씬
/// 타임라인 트랙(CutSceneImageBehaviour 등)이 인스펙터에서 고르는 값이라 여기 남는다.
///
/// 값의 순서를 바꾸면 안 된다. 프리팹과 TimelineAsset에 정수로 구워져 있어서, 순서가 바뀌면
/// 이미 배치해 둔 컷씬들의 앵커가 조용히 다른 곳으로 옮겨 간다.
/// </summary>
public enum AnchorType
{
    Center,
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}
