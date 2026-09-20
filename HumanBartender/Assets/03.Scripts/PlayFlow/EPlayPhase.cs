/// <summary>
/// Play 씬 내부에서 하루가 진행되는 국면. 1부(Tycoon)와 2부(Dialogue)를 구분한다.
/// </summary>
public enum EPlayPhase
{
    None = -1,
    StokcIn = 0,
    BarOpen = 1,
    Tycoon = 2,
    Dialogue = 3,
}
