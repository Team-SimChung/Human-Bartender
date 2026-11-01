/// <summary>범위 안에 머무는 동안 재시작하지 않고, 충분히 벗어난 뒤 재진입할 때 다시 허용함.</summary>
internal sealed class WorldProximityEntry
{
    private string rule;
    private bool consumed;

    internal bool CanStart(string currentRule, float distance, float radius, float exitMargin)
    {
        if (rule != currentRule || distance > radius + exitMargin)
        {
            rule = currentRule;
            consumed = false;
        }
        return !consumed && distance <= radius;
    }

    internal void Consume() => consumed = true;
    internal void Reset() { rule = null; consumed = false; }
}
