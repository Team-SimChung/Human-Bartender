using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>독립 씬에서는 자체 액션을, Play 제조에서는 공용 입력 라우터를 사용한다.</summary>
public class StirInputHandler : MonoBehaviour, ICraftStirInput
{
    [SerializeField] private StirManager stir;

    private InputAction[] actions;
    private bool routedInput;

    private void Awake()
    {
        actions = new InputAction[StirDirections.Count];
        actions[(int)EStirDirection.Up] = CreateAction("Stir_Up", "<Keyboard>/w", "<Keyboard>/upArrow");
        actions[(int)EStirDirection.Right] = CreateAction("Stir_Right", "<Keyboard>/d", "<Keyboard>/rightArrow");
        actions[(int)EStirDirection.Down] = CreateAction("Stir_Down", "<Keyboard>/s", "<Keyboard>/downArrow");
        actions[(int)EStirDirection.Left] = CreateAction("Stir_Left", "<Keyboard>/a", "<Keyboard>/leftArrow");
        actions[(int)EStirDirection.Up].performed += OnUp;
        actions[(int)EStirDirection.Right].performed += OnRight;
        actions[(int)EStirDirection.Down].performed += OnDown;
        actions[(int)EStirDirection.Left].performed += OnLeft;
        if (stir != null) stir.Completed += DisableActions;
    }

    private void OnEnable()
    {
        if (!routedInput) EnableActions();
    }

    private void OnDisable()
    {
        DisableActions();
    }

    private void OnDestroy()
    {
        if (stir != null) stir.Completed -= DisableActions;
        if (actions == null) return;
        actions[(int)EStirDirection.Up].performed -= OnUp;
        actions[(int)EStirDirection.Right].performed -= OnRight;
        actions[(int)EStirDirection.Down].performed -= OnDown;
        actions[(int)EStirDirection.Left].performed -= OnLeft;
        foreach (InputAction action in actions) action.Dispose();
        actions = null;
    }

    public void SetRoutedInput(bool routed)
    {
        routedInput = routed;
        if (routed) DisableActions();
        else if (isActiveAndEnabled) EnableActions();
    }

    public void OnStirInput(EStirDirection direction)
    {
        if (stir != null) stir.EnqueueDirection((int)direction);
    }

    private static InputAction CreateAction(string name, string keyPath, string arrowPath)
    {
        var action = new InputAction(name, InputActionType.Button);
        action.AddBinding(keyPath);
        action.AddBinding(arrowPath);
        return action;
    }

    private void OnUp(InputAction.CallbackContext context) { OnStirInput(EStirDirection.Up); }
    private void OnRight(InputAction.CallbackContext context) { OnStirInput(EStirDirection.Right); }
    private void OnDown(InputAction.CallbackContext context) { OnStirInput(EStirDirection.Down); }
    private void OnLeft(InputAction.CallbackContext context) { OnStirInput(EStirDirection.Left); }

    private void EnableActions()
    {
        if (actions == null) return;
        foreach (InputAction action in actions) action.Enable();
    }

    private void DisableActions()
    {
        if (actions == null) return;
        foreach (InputAction action in actions) action.Disable();
    }
}
