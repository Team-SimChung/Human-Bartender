using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using VContainer.Unity;

public enum GameControl
{
    MoveLeft, MoveRight, Sprint, Interact, Advance, PreviousSeat, NextSeat, ServicePanel,
    PrepPrevious, PrepNext, StirUp, StirRight, StirDown, StirLeft, CapPress, ShakeLeft, ShakeRight
}

/// <summary>입력 액션과 직접 읽는 미니게임 입력이 같은 키 설정을 공유한다.</summary>
public static class GameKeyBindings
{
    public readonly struct Binding
    {
        public readonly GameControl Control;
        public readonly string Label, Map, Action, Part;
        public readonly Key DefaultKey;
        public Binding(GameControl control, string label, string map, string action, Key key, string part = null)
        {
            Control = control; Label = label; Map = map; Action = action; DefaultKey = key; Part = part;
        }
    }

    public static readonly Binding[] Bindings =
    {
        new(GameControl.MoveLeft, "이동 · 왼쪽", "Outside", "Move", Key.A, "left"),
        new(GameControl.MoveRight, "이동 · 오른쪽", "Outside", "Move", Key.D, "right"),
        new(GameControl.Sprint, "달리기", "Outside", "Sprint", Key.LeftShift),
        new(GameControl.Interact, "상호작용 / 야외 대화", "Outside", "Interact", Key.E),
        new(GameControl.Advance, "바 대화 진행", "Play", "Advance", Key.Space),
        new(GameControl.PreviousSeat, "바 시점 · 왼쪽", "Play", "Left", Key.Q),
        new(GameControl.NextSeat, "바 시점 · 오른쪽", "Play", "Right", Key.E),
        new(GameControl.ServicePanel, "서비스 메뉴", "Play", "ServicePanel", Key.Tab),
        new(GameControl.PrepPrevious, "제조 준비 · 이전", "CraftPrep", "PrepPrevious", Key.A),
        new(GameControl.PrepNext, "제조 준비 · 다음", "CraftPrep", "PrepNext", Key.D),
        new(GameControl.StirUp, "젓기 · 위", "CraftGimmick", "StirUp", Key.W),
        new(GameControl.StirRight, "젓기 · 오른쪽", "CraftGimmick", "StirRight", Key.D),
        new(GameControl.StirDown, "젓기 · 아래", "CraftGimmick", "StirDown", Key.S),
        new(GameControl.StirLeft, "젓기 · 왼쪽", "CraftGimmick", "StirLeft", Key.A),
        new(GameControl.CapPress, "병따기", "Cap", "CapPress", Key.Space),
        new(GameControl.ShakeLeft, "쉐이킹 · 왼쪽", "Shake", "", Key.A),
        new(GameControl.ShakeRight, "쉐이킹 · 오른쪽", "Shake", "", Key.D),
    };

    public static event Action Changed;
    public static string PreferenceKey(GameControl control) => "key_" + control;
    public static Binding Definition(GameControl control) => Bindings[(int)control];
    public static Key GetKey(GameControl control)
    {
        var key = (Key)PlayerPrefs.GetInt(PreferenceKey(control), (int)Definition(control).DefaultKey);
        return IsAllowed(key) ? key : Definition(control).DefaultKey;
    }
    public static string Path(Key key) => "<Keyboard>/" + key.ToString().ToLowerInvariant();
    public static string DisplayName(GameControl control) =>
        InputControlPath.ToHumanReadableString(Path(GetKey(control)), InputControlPath.HumanReadableStringOptions.OmitDevice);
    public static bool WasPressed(GameControl control) => Keyboard.current?[GetKey(control)].wasPressedThisFrame == true;

    public static bool IsAllowed(Key key) => Enum.IsDefined(typeof(Key), key) && key != Key.None &&
        key != Key.Escape && key != Key.Enter && key != Key.NumpadEnter &&
        key != Key.UpArrow && key != Key.DownArrow && key != Key.LeftArrow && key != Key.RightArrow;

    public static bool TrySetKey(GameControl control, Key key, out string message)
    {
        if (!IsAllowed(key))
        {
            message = "Esc · Enter · 방향키는 기본 기능을 위해 유지됩니다. 다른 키를 눌러 주세요.";
            return false;
        }
        var binding = Definition(control);
        foreach (var other in Bindings)
            if (other.Control != control && other.Map == binding.Map && GetKey(other.Control) == key)
            {
                message = $"‘{other.Label}’에서 사용하는 키입니다. 다른 키를 눌러 주세요.";
                return false;
            }
        PlayerPrefs.SetInt(PreferenceKey(control), (int)key);
        PlayerPrefs.Save();
        Changed?.Invoke();
        message = "조작키를 저장했습니다.";
        return true;
    }

    public static void Reset()
    {
        foreach (var binding in Bindings) PlayerPrefs.DeleteKey(PreferenceKey(binding.Control));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void ApplyTo(InputActionAsset asset)
    {
        if (asset == null) return;
        foreach (var binding in Bindings)
        {
            if (string.IsNullOrEmpty(binding.Action)) continue;
            string map = binding.Control == GameControl.CapPress ? "CraftGimmick" : binding.Map;
            var action = asset.FindAction(map + "/" + binding.Action, false);
            if (action == null) continue;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var target = action.bindings[i];
                if (!string.Equals(target.path, Path(binding.DefaultKey), StringComparison.OrdinalIgnoreCase)) continue;
                if (binding.Part != null && target.name != binding.Part) continue;
                action.ApplyBindingOverride(i, Path(GetKey(binding.Control)));
            }
        }
    }
}

/// <summary>씬마다 생성되는 PlayerInput 복사본에도 저장된 키를 적용한다.</summary>
public sealed class GameKeyBindingRuntime : IStartable, IDisposable
{
    public void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameKeyBindings.Changed += Apply;
        Apply();
    }
    public void Dispose()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameKeyBindings.Changed -= Apply;
    }
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();
    static void Apply()
    {
        foreach (var player in PlayerInput.all) GameKeyBindings.ApplyTo(player.actions);
    }
}
