/// <summary>CSV 출입 동작을 하루 진행 요청으로 변환한다.</summary>
public static class OutsideActions
{
    public const string ElevatorToggle = "elevator_toggle";

    public static bool TryGetDestination(string actionRef, out GameProgressionDestination destination)
    {
        switch (actionRef)
        {
            case "enter_bar":
                destination = GameProgressionDestination.Bar;
                return true;
            case "enter_home":
                destination = GameProgressionDestination.Home;
                return true;
            case "exit_home":
                destination = GameProgressionDestination.OutsideFromHome;
                return true;
            default:
                destination = default;
                return false;
        }
    }
}
