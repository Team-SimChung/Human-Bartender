using System.Collections.Generic;
using UnityEngine;

/// <summary>화면 빗물과 바닥 파문이 같은 실제 파티클 방출 상태를 읽는다.</summary>
public static class ParticleRainSource
{
    public static void Collect(GameObject source, List<ParticleSystem> systems)
    {
        systems.Clear();
        if (source == null) return;
        source.GetComponentsInChildren(true, systems);
        for (int i = systems.Count - 1; i >= 0; i--)
            if (!systems[i].main.loop) systems.RemoveAt(i);
    }

    public static bool IsEmitting(GameObject source, List<ParticleSystem> systems, bool ambient = false)
    {
        if (source == null) return ambient;
        if (!source.activeInHierarchy) return false;
        foreach (var system in systems)
            if (system != null && system.gameObject.activeInHierarchy && system.emission.enabled && system.isEmitting)
                return true;
        return false;
    }
}
