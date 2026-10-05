/// <summary>
/// 컷씬 타임라인 트랙이 인스펙터에서 고르는 값들.
///
/// 원래 TestCutScene.cs가 들고 있었는데 그 실험용 SO는 걷어냈다. 타임라인 트랙
/// (CutSceneImageBehaviour·CutSceneDialogueBehaviour·CutSceneRootMoveBehaviour)이 계속 쓰므로 여기 남긴다.
///
/// 값의 순서를 바꾸면 안 된다. TimelineAsset에 정수로 구워져 있어서, 순서가 바뀌면 이미 만들어 둔
/// 컷씬들의 등장·퇴장 연출이 조용히 다른 것으로 바뀐다.
/// </summary>
public enum ECutSceneCameraMoveType
{
    None = 0,
    Right,
    Left,
    Down,
    Up,
}

/// <summary>컷씬 요소가 화면에 들어오는 방식.</summary>
public enum EEneterPreset
{
    None = 0,
    Cut,
    FadeIn,

    SlideLeft,
    SlideRight,
    SlideUp,
    SlideDown,

    ScaleUp,
}

/// <summary>컷씬 요소가 화면에서 나가는 방식.</summary>
public enum EExitPreset
{
    None = 0,
    Cut,
    FadeOut,

    SlideLeft,
    SlideRight,
    SlideUp,
    SlideDown,

    ScaleDown,
}
