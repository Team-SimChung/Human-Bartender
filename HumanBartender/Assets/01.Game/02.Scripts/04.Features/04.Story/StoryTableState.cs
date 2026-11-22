using System;
using System.Collections.Generic;

/// <summary>One visit's coaster and displayed drink. Orders, money and craft results remain with their existing owners.</summary>
public sealed class StoryTableState
{
    public sealed class Visit
    {
        public bool HasCoaster { get; internal set; }
        public CraftedDrink Drink { get; internal set; }
        public bool GlassEmpty { get; internal set; }
    }

    readonly Dictionary<string, Visit> visits = new(StringComparer.Ordinal);
    public Visit Get(string actor) => actor != null && visits.TryGetValue(actor, out var visit)
        ? visit : throw new InvalidOperationException("앉아 있지 않은 손님입니다: " + actor);
    public bool Contains(string actor) => actor != null && visits.ContainsKey(actor);
    public void Enter(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor) || visits.ContainsKey(actor))
            throw new InvalidOperationException("잘못되었거나 중복된 손님 방문입니다: " + actor);
        visits.Add(actor, new Visit());
    }
    public void PlaceCoaster(string actor) => Get(actor).HasCoaster = true;
    public void Serve(string actor, CraftedDrink drink)
    {
        var visit = Get(actor);
        if (!visit.HasCoaster || drink == null) throw new InvalidOperationException("코스터와 완성된 잔이 필요합니다.");
        visit.Drink = drink;
        visit.GlassEmpty = false;
    }
    public void EmptyGlass(string actor)
    {
        var visit = Get(actor);
        if (visit.Drink != null) visit.GlassEmpty = true;
    }
    public void Leave(string actor) => visits.Remove(actor);
    public void Clear() => visits.Clear();
}
