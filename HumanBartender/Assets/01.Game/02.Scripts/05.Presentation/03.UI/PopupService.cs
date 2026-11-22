using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VContainer;


public enum PopupType
{
    IngredientAcquired = 0,
    CharacterRegistered = 1,
    AffinityIncreased = 2,
    AffinityDecreased = 3,
    NewObjective = 4,
    ObjectiveCompleted = 5,
    EventOccurred = 6
}

public interface IPopupService
{
    /// <summary>표시 함수 호출의 성공 여부를 즉시 반환한다. UI의 종료는 기다리지 않는다.</summary>
    UniTask<bool> Popup(PopupType type, string data);
}

/// <summary>
/// PopupService가 복제하고 재사용할 알림 종류별 원본 행을 연결한다.
/// </summary>
[Serializable]
public sealed class PopupBindings
{
    [SerializeField] UIPopupView ingredientAcquired;
    [SerializeField] UIPopupView characterRegistered;
    [SerializeField] UIPopupView affinityIncreased;
    [SerializeField] UIPopupView affinityDecreased;
    [SerializeField] UIPopupView newObjective;
    [SerializeField] UIPopupView objectiveCompleted;
    [SerializeField] UIPopupView eventOccurred;

    public UIPopupView Get(PopupType type)
    {
        switch (type)
        {
            case PopupType.IngredientAcquired: return ingredientAcquired;
            case PopupType.CharacterRegistered: return characterRegistered;
            case PopupType.AffinityIncreased: return affinityIncreased;
            case PopupType.AffinityDecreased: return affinityDecreased;
            case PopupType.NewObjective: return newObjective;
            case PopupType.ObjectiveCompleted: return objectiveCompleted;
            case PopupType.EventOccurred: return eventOccurred;
            default: throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown popup type.");
        }
    }
}

/// <summary>공용 알림을 요청 순서대로 표시하고, 화면에 자리가 나면 다음 알림을 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class PopupService : MonoBehaviour, IPopupService
{
    [SerializeField] PopupBindings bindings = new PopupBindings();
    [Tooltip("좌상단 여백과 알림 사이 간격을 정하는 세로 목록입니다.")]
    [SerializeField] VerticalLayoutGroup stack;
    [Inject] ISoundEffectPlayer sound;
    readonly Dictionary<PopupType, Stack<UIPopupView>> available = new();
    readonly Queue<(PopupType Type, UIPopupView View)> pending = new();
    readonly List<(PopupType Type, UIPopupView View)> visible = new();

    void LateUpdate()
    {
        if (pending.Count > 0) ShowPending();
    }

    void OnDisable()
    {
        while (pending.Count > 0) Recycle(pending.Dequeue());
        foreach (var entry in visible) Recycle(entry);
        visible.Clear();
    }

    public UniTask<bool> Popup(PopupType type, string data)
    {
        try
        {
            if (type == PopupType.CharacterRegistered && (data == "chris" || data == "크리스"))
                return UniTask.FromResult(true);
            var template = bindings.Get(type);
            if (!isActiveAndEnabled || stack == null || template == null)
            {
                Debug.LogWarning($"[Popup] No connected UI for {type}.", this);
                return UniTask.FromResult(false);
            }

            if (!available.TryGetValue(type, out var pool))
                available.Add(type, pool = new Stack<UIPopupView>());
            var view = pool.Count > 0 ? pool.Pop() : Instantiate(template, stack.transform, false);
            var entry = (Type: type, View: view);
            bool prepared;
            try
            {
                string message = type == PopupType.CharacterRegistered ? ResolveCharacterName(data) : data;
                prepared = view.TryPrepare(message);
            }
            catch
            {
                Recycle(entry);
                throw;
            }
            if (!prepared)
            {
                Recycle(entry);
                return UniTask.FromResult(false);
            }

            pending.Enqueue(entry);
            ShowPending();
            return UniTask.FromResult(true);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return UniTask.FromResult(false);
        }
    }

    void ShowPending()
    {
        if (!stack.isActiveAndEnabled) return;
        var rect = (RectTransform)stack.transform;
        float room = rect.rect.height - stack.padding.vertical;
        foreach (var entry in visible) room -= entry.View.Height + stack.spacing;

        while (pending.TryPeek(out var entry))
        {
            if (visible.Count > 0 && entry.View.Height > room) return;
            var shown = pending.Dequeue();
            room -= shown.View.Height + stack.spacing;
            visible.Add(shown);
            string cue = shown.Type == PopupType.ObjectiveCompleted ? "SFX_goal_completed" : "SFX_notification";
            shown.View.Show(() => sound?.PlaySE(cue), () =>
            {
                visible.Remove(shown);
                Recycle(shown);
                ShowPending();
            });
        }
    }

    void Recycle((PopupType Type, UIPopupView View) entry)
    {
        if (entry.View == null) return;
        entry.View.ResetView();
        available[entry.Type].Push(entry.View);
    }

    static string ResolveCharacterName(string data)
    {
        foreach (var character in NewDataLoadManager.StoryCharacters ?? Array.Empty<NewCharacterData>())
        {
            if (character.Id != data) continue;
            var name = GameStateManager.Instance.Language == ELanguage.Ko ? character.Name.Ko : character.Name.En;
            return string.IsNullOrWhiteSpace(name) ? character.Name.Ko : name;
        }
        return data;
    }
}
