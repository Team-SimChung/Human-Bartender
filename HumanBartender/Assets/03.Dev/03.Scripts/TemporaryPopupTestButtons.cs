using Cysharp.Threading.Tasks;
using UnityEngine;

// Delete TEMP_PopupTestButtons_DELETE_ME and this script after testing.
public sealed class TemporaryPopupTestButtons : MonoBehaviour
{
    [SerializeField] PopupService popupService;
    [SerializeField] string ingredientId = "gin";
    [SerializeField] string characterName = "톰 거너";
    [SerializeField] string objectiveType = "메인 목표";

    public void Show(int popupIndex)
    {
        if (popupService == null)
        {
            Debug.LogWarning("[Popup Test] PopupService is not connected.", this);
            return;
        }

        var type = (PopupType)popupIndex;
        string data = type == PopupType.IngredientAcquired ? ingredientId
            : type == PopupType.AffinityIncreased || type == PopupType.AffinityDecreased
                ? characterName
                : type == PopupType.NewObjective || type == PopupType.ObjectiveCompleted
                    ? objectiveType : string.Empty;
        ShowAsync(type, data).Forget();
    }

    async UniTask ShowAsync(PopupType type, string data)
    {
        if (!await popupService.Popup(type, data))
            Debug.LogWarning($"[Popup Test] Could not show {type} with data '{data}'.", this);
    }
}
