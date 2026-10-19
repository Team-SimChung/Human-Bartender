using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;
using Cysharp.Threading.Tasks;

/// <summary>화면이 드러나는 씬을 기준으로 장소 음악과 거리의 두 환경음 레이어를 소유한다.</summary>
public sealed class SceneAudioController : IStartable, ITickable, IDisposable
{
    readonly SoundManager sound;
    readonly PlayerDataSO player;
    readonly IPopupService popups;
    readonly IConditionUtil conditions;
    HashSet<string> knownIngredients;
    IAudioLoop street, rain;
    string group;
    bool hadFocus = true;
    public SceneAudioController(SoundManager sound, PlayerDataSO player, IPopupService popups, IConditionUtil conditions)
    { this.sound = sound; this.player = player; this.popups = popups; this.conditions = conditions; }

    public void Start()
    {
        player.MoneyAdded += OnMoney;
        player.AffinityAdded += OnAffinity;
        player.FlagChanged += OnFlag;
        player.ProgressReplaced += ResetIngredients;
        Tick();
    }

    public void Tick()
    {
        if (sound == null || !sound.isActiveAndEnabled) return;
        if (knownIngredients == null && NewDataLoadManager.IsLoaded) RefreshIngredients(false);
        bool focused = Application.isFocused;
        if (focused != hadFocus)
        {
            if (focused) sound.ResumeBGM(); else sound.PauseBGM();
            hadFocus = focused;
        }
        var transition = SceneTransitionManager.Instance;
        bool revealing = transition == null || !transition.IsBusy || transition.CurrentState == SceneTransitionState.FadingIn;
        string scene = SceneManager.GetActiveScene().name;
        string next = GameAudioRules.MusicGroup(scene);
        if (revealing && next != group)
        {
            group = next;
            switch (group)
            {
                case "home": sound.PlayPlaylist(group, new[] { "BGM_bar_02" }); break;
                case "street": sound.PlayPlaylist(group, new[] { "BGM_outside" }, .6f); break;
                case "bar": sound.PlayPlaylist(group, new[] { "BGM_bar_01", "BGM_bar_02", "BGM_bar_03" }); break;
                default: sound.StopBGM(); break;
            }
        }
        bool cinematic = GameAudioContext.Cinematic || GameStateManager.Instance.CurrentGameState == GameState.Effect;
        sound.SetMusicSuppressed(GameAudioContext.MusicSuppressed);
        bool outside = group == "street" && scene == "OutSide" && focused && !cinematic;
        if (outside)
        {
            street ??= sound.StartLoop("SFX_street_loop", .55f);
            bool paused = Time.timeScale <= 0f || UIDisplayOptions.IsOptionOpen;
            float gain = GameAudioRules.AmbienceGain(GameAudioContext.AnyDialogue, paused);
            street?.SetGain(gain);
            bool rainy = GameAudioRules.IsRainy(GameStateManager.Instance.CurrentDay, GameStateManager.Instance.GameFlow);
            if (rainy) { rain ??= sound.StartLoop("SFX_rain_loop", .55f); rain?.SetGain(gain); }
            else { rain?.Dispose(); rain = null; }
        }
        else StopAmbience(!focused || cinematic);
    }

    void OnMoney(int delta) { if (delta != 0) sound.PlaySE("SFX_money_change"); }
    void OnAffinity(string id, int delta)
    {
        if (delta == 0) return;
        string name = id;
        foreach (var character in NewDataLoadManager.StoryCharacters ?? Array.Empty<NewCharacterData>())
            if (character.Id == id)
            {
                name = GameStateManager.Instance.Language == ELanguage.En && !string.IsNullOrEmpty(character.Name.En)
                    ? character.Name.En : character.Name.Ko ?? id;
                break;
            }
        popups.Popup(delta > 0 ? PopupType.AffinityIncreased : PopupType.AffinityDecreased, name).Forget(Debug.LogException);
    }

    void OnFlag(string key, bool value)
    {
        if (knownIngredients != null) RefreshIngredients(true);
        if (!value || !key.EndsWith("_met", StringComparison.Ordinal)) return;
        string id = key.Substring(0, key.Length - 4);
        if (id == "chris") return;
        foreach (var character in NewDataLoadManager.StoryCharacters ?? Array.Empty<NewCharacterData>())
            if (character.Id == id) { popups.Popup(PopupType.CharacterRegistered, id).Forget(Debug.LogException); break; }
    }

    void ResetIngredients() => knownIngredients = null;
    void RefreshIngredients(bool notify)
    {
        var next = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in NewDataLoadManager.StoryShelfItems ?? Array.Empty<NewShelfItemData>())
        {
            if (!item.IsIngredient || item.UnlockDay > GameStateManager.Instance.CurrentDay ||
                (!string.IsNullOrWhiteSpace(item.UnlockWhen) && !conditions.CheckRequired(item.UnlockWhen))) continue;
            next.Add(item.Id);
            if (notify && knownIngredients != null && !knownIngredients.Contains(item.Id))
                popups.Popup(PopupType.IngredientAcquired, item.Id).Forget(Debug.LogException);
        }
        knownIngredients = next;
    }

    void StopAmbience(bool immediately = false)
    {
        if (immediately) { street?.StopImmediately(); rain?.StopImmediately(); }
        else { street?.Dispose(); rain?.Dispose(); }
        street = rain = null;
    }
    public void Dispose()
    {
        StopAmbience();
        player.MoneyAdded -= OnMoney;
        player.AffinityAdded -= OnAffinity;
        player.FlagChanged -= OnFlag;
        player.ProgressReplaced -= ResetIngredients;
    }
}
