using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 랜덤 손님의 외형 조합을 만드는 곳(일반 손님 외형 생성 시스템 §3·§4).
///
/// 슬롯마다 따로 뽑아 붙이지 않고, 성별·성격별로 쓸 수 있는 완성 조합 목록을 미리 만들어 두고
/// 거기서 하나를 뽑는다. 슬롯을 하나씩 뽑으면 금지 조합에 걸릴 때마다 다시 뽑아야 하고, 그러면
/// 어떤 조합이 얼마나 자주 나오는지가 데이터가 아니라 다시 뽑은 횟수로 정해진다.
///
/// 조합의 가중치는 들어 있는 파츠 weight를 모두 곱한 값이다. 선택 슬롯을 비우는 것도 하나의
/// 후보이고, 그 가중치는 balance.json의 guest_acc_none_weight를 쓴다.
///
/// 카메오는 이 시스템을 쓰지 않는다 — 그쪽은 캐릭터 전용 리소스다(§1).
/// </summary>
public class GuestAppearanceBuilder
{
    /// <summary>완성 조합 하나와 그 가중치.</summary>
    class Combination
    {
        public GuestBodyAppearance Appearance;
        public float Weight;
    }

    readonly NewGuestBodyDataBase data;
    readonly float noneWeight;

    /// <summary>(성별, 성격)별로 미리 만들어 둔 조합 목록. 같은 조건이 다시 오면 그대로 쓴다.</summary>
    readonly Dictionary<(string gender, string personality), List<Combination>> poolByKey = new();

    public GuestAppearanceBuilder(NewGuestBodyDataBase data, float noneWeight)
    {
        this.data = data;
        this.noneWeight = noneWeight;
    }

    /// <summary>
    /// 이 성별·성격으로 쓸 수 있는 조합 하나를 가중 추첨한다.
    ///
    /// 지금 앉아 있는 손님과 모든 슬롯이 같은 조합은 먼저 뺀다(§3.2). 빼고 나서 남는 것이 없으면
    /// 중복을 허용하되 로그를 남긴다 — 파츠가 적을 때는 자리를 비워 두는 것보다 겹치는 편이 낫다.
    ///
    /// 쓸 수 있는 조합이 하나도 없으면 null이다. 부르는 쪽이 기본 조합으로 되돌린다(§5).
    /// </summary>
    public GuestBodyAppearance Pick(string gender, string personality, IEnumerable<GuestBodyAppearance> seated)
    {
        List<Combination> pool = GetPool(gender, personality);

        if (pool.Count == 0)
        {
            Debug.LogError($"[Guest] 성별 {gender}·성격 {personality}로 만들 수 있는 외형 조합이 없습니다. " +
                           "guest_bodies.json의 파츠와 exclusions를 확인하세요.");
            return null;
        }

        List<Combination> candidates = ExcludeSeated(pool, seated);

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"[Guest] 성별 {gender}·성격 {personality}에서 앉아 있는 손님과 겹치지 않는 외형이 " +
                             "남지 않아 중복을 허용합니다.");
            candidates = pool;
        }

        return PickWeighted(candidates);
    }

    /// <summary>
    /// 데이터가 깨져 조합을 고르지 못했을 때 쓰는 성별 기본 조합(§5).
    /// 문제가 생긴 슬롯 하나만 갈지 않고 통째로 되돌린다 — 갈아 끼운 파츠가 나머지와
    /// 금지 조합을 이룰 수 있기 때문이다.
    /// </summary>
    public GuestBodyAppearance BuildDefault(string gender)
    {
        if (data.Defaults == null || !data.Defaults.TryGetValue(gender, out var defaults) || defaults == null)
        {
            Debug.LogError($"[Guest] guest_bodies.json에 성별 {gender}의 기본 조합(defaults)이 없습니다.");
            return null;
        }

        var appearance = new GuestBodyAppearance(gender);

        foreach (var slot in NewGuestBodyDataBase.AllSlots)
        {
            string id = defaults.GetId(slot);
            if (string.IsNullOrEmpty(id)) continue; // 선택 슬롯은 비운 채로 둔다.

            if (!data.TryGetPart(id, out var part))
            {
                Debug.LogError($"[Guest] 기본 조합의 파츠 '{id}'를 guest_bodies.json에서 찾지 못했습니다.");
                continue;
            }

            appearance.Set(slot, part);
        }

        return appearance;
    }

    // ── 조합 목록 만들기 ────────────────────────────────────────────────

    List<Combination> GetPool(string gender, string personality)
    {
        var key = (gender, personality);
        if (poolByKey.TryGetValue(key, out var cached)) return cached;

        var pool = BuildPool(gender, personality);
        poolByKey[key] = pool;

        Debug.Log($"[Guest] 외형 조합 {pool.Count}개 (성별 {gender}, 성격 {personality})");

        return pool;
    }

    /// <summary>
    /// 슬롯 후보를 차례로 곱해 나가며 금지 조합을 걸러 낸다.
    ///
    /// 다 만든 뒤에 거르지 않고 붙이는 도중에 거른다. 이미 고른 파츠와 부딪히는 후보를 그 자리에서
    /// 빼면, 뒤에 붙을 슬롯들까지 함께 만들어 놓고 버리는 일이 없다.
    /// </summary>
    List<Combination> BuildPool(string gender, string personality)
    {
        var pool = new List<Combination> { new() { Appearance = new GuestBodyAppearance(gender), Weight = 1f } };

        foreach (var slot in NewGuestBodyDataBase.AllSlots)
        {
            var candidates = CollectSlotCandidates(slot, gender, personality);

            // 이 성별에 아예 없는 선택 슬롯(여성 목걸이 등)은 건너뛴다.
            // 필수 슬롯이 비면 조합을 만들 수 없으므로 거기서 멈춘다.
            if (candidates.Count == 0)
            {
                if (!NewGuestBodyDataBase.IsOptional(slot))
                {
                    Debug.LogError($"[Guest] 성별 {gender}·성격 {personality}의 {slot} 슬롯에 쓸 수 있는 파츠가 없습니다.");
                    return new List<Combination>();
                }

                continue;
            }

            pool = Expand(pool, slot, candidates);
        }

        return pool;
    }

    /// <summary>이 슬롯에서 성별·성격 조건을 통과한 파츠. 선택 슬롯이면 "없음" 후보를 더한다.</summary>
    List<(NewGuestBodyPartData? part, float weight)> CollectSlotCandidates(
        EGuestBodySlot slot, string gender, string personality)
    {
        var candidates = new List<(NewGuestBodyPartData?, float)>();
        var parts = data.GetParts(slot);

        if (parts != null)
        {
            foreach (var part in parts)
            {
                if (part.Gender != gender) continue;
                if (!AllowsPersonality(part, personality)) continue;

                candidates.Add((part, Mathf.Max(1, part.Weight)));
            }
        }

        // 비워 두는 것도 하나의 후보다. 조합을 만들 때만 쓰는 가상의 값이라 손님 기록에는 남지 않는다.
        if (candidates.Count > 0 && NewGuestBodyDataBase.IsOptional(slot))
            candidates.Add((null, noneWeight));

        return candidates;
    }

    /// <summary>personalities가 비어 있으면 모든 성격이 쓸 수 있다.</summary>
    static bool AllowsPersonality(NewGuestBodyPartData part, string personality)
    {
        if (part.Personalities == null || part.Personalities.Length == 0) return true;

        foreach (string allowed in part.Personalities)
        {
            if (allowed == personality) return true;
        }

        return false;
    }

    /// <summary>지금까지의 조합에 이 슬롯의 후보를 곱한다. 금지 조합에 걸리는 짝은 만들지 않는다.</summary>
    List<Combination> Expand(List<Combination> pool, EGuestBodySlot slot,
                             List<(NewGuestBodyPartData? part, float weight)> candidates)
    {
        var expanded = new List<Combination>(pool.Count * candidates.Count);

        foreach (var combination in pool)
        {
            foreach (var (part, weight) in candidates)
            {
                if (part.HasValue && IsExcluded(combination.Appearance, part.Value.Id)) continue;

                var next = new GuestBodyAppearance(combination.Appearance.Gender);

                foreach (var pair in combination.Appearance.Parts)
                    next.Set(pair.Key, pair.Value);

                if (part.HasValue) next.Set(slot, part.Value);

                expanded.Add(new Combination { Appearance = next, Weight = combination.Weight * weight });
            }
        }

        return expanded;
    }

    /// <summary>이미 고른 파츠 가운데 이 파츠와 함께 쓸 수 없는 것이 있는지. a·b 순서는 뜻이 같다.</summary>
    bool IsExcluded(GuestBodyAppearance appearance, string partId)
    {
        if (data.Exclusions == null) return false;

        foreach (var exclusion in data.Exclusions)
        {
            string other = exclusion.A == partId ? exclusion.B
                         : exclusion.B == partId ? exclusion.A
                         : null;

            if (other == null) continue;

            foreach (var pair in appearance.Parts)
            {
                if (pair.Value.Id == other) return true;
            }
        }

        return false;
    }

    // ── 추첨 ────────────────────────────────────────────────────────────

    static List<Combination> ExcludeSeated(List<Combination> pool, IEnumerable<GuestBodyAppearance> seated)
    {
        if (seated == null) return pool;

        var candidates = new List<Combination>(pool.Count);

        foreach (var combination in pool)
        {
            bool duplicated = false;

            foreach (var other in seated)
            {
                if (!combination.Appearance.SameAs(other)) continue;

                duplicated = true;
                break;
            }

            if (!duplicated) candidates.Add(combination);
        }

        return candidates;
    }

    static GuestBodyAppearance PickWeighted(List<Combination> candidates)
    {
        float total = 0f;
        foreach (var combination in candidates) total += combination.Weight;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;

        foreach (var combination in candidates)
        {
            cumulative += combination.Weight;
            if (roll < cumulative) return combination.Appearance;
        }

        return candidates[^1].Appearance;
    }
}
