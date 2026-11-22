using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
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
/// PopupService 컴포넌트에서 각 UI의 Popup(string)을 연결한다.
/// 각 슬롯에는 해당 팝업 UI 컴포넌트를 직접 연결한다.
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

/// <summary>팝업 표시를 요청한다. 닫힘 신호와 UI 수명은 연결된 UI에서 관리한다.</summary>
[DisallowMultipleComponent]
public sealed class PopupService : MonoBehaviour, IPopupService
{
    [SerializeField] PopupBindings bindings = new PopupBindings();
    [Inject] ISoundEffectPlayer sound;
    readonly System.Collections.Generic.Dictionary<UIPopupView, Action> audioHandlers = new();

    void Awake()
    {
        foreach (PopupType type in Enum.GetValues(typeof(PopupType)))
        {
            var view = bindings.Get(type);
            if (view == null || audioHandlers.ContainsKey(view)) continue;
            string key = type == PopupType.ObjectiveCompleted ? "SFX_goal_completed" : "SFX_notification";
            Action handler = () => sound?.PlaySE(key);
            audioHandlers.Add(view, handler);
            view.Shown += handler;
        }
    }

    void OnDestroy()
    {
        foreach (var pair in audioHandlers) if (pair.Key != null) pair.Key.Shown -= pair.Value;
        audioHandlers.Clear();
    }

    public UniTask<bool> Popup(PopupType type, string data)
    {
        try
        {
            if (type == PopupType.CharacterRegistered && (data == "chris" || data == "크리스"))
                return UniTask.FromResult(true);
            var popup = bindings.Get(type);
            if (popup == null)
            {
                Debug.LogWarning($"[Popup] No connected UI for {type}.", this);
                return UniTask.FromResult(false);
            }

            return UniTask.FromResult(popup.Popup(data));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return UniTask.FromResult(false);
        }
    }

}
