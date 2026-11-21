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
    IAudioLoop street, rain;
    string group;
    Scene musicScene;
    NewGameOpening opening;
    bool hadFocus = true;
    // 재료 획득 알림은 여기서 띄우지 않는다. 선반 해금 조건이 바뀌었다고 알리면 받지 않은 재료까지 '획득'으로 뜬다
    // (엔진 수정 사항 24). 웹처럼 실제로 받는 지점(거리 대본의 지급 행 등)이 직접 알린다.
    public SceneAudioController(SoundManager sound, PlayerDataSO player, IPopupService popups)
    { this.sound = sound; this.player = player; this.popups = popups; }

    public void Start()
    {
        player.MoneyAdded += OnMoney;
        player.AffinityAdded += OnAffinity;
        player.FlagChanged += OnFlag;
        Tick();
    }

    public void Tick()
    {
        if (sound == null || !sound.isActiveAndEnabled) return;
        bool focused = Application.isFocused;
        if (focused != hadFocus)
        {
            if (focused) sound.ResumeBGM(); else sound.PauseBGM();
            hadFocus = focused;
        }
        var transition = SceneTransitionManager.Instance;
        bool revealing = transition == null || !transition.IsBusy || transition.CurrentState == SceneTransitionState.FadingIn;
        var activeScene = SceneManager.GetActiveScene();
        if (musicScene != activeScene)
        {
            musicScene = activeScene;
            opening = null;
            if (activeScene.name == "Home")
                foreach (var root in activeScene.GetRootGameObjects())
                    if ((opening = root.GetComponentInChildren<NewGameOpening>()) != null) break;
        }
        string scene = activeScene.name;
        string next = GameAudioRules.MusicGroup(scene);
        var state = GameStateManager.Instance;
        bool openingIntro = opening != null && opening.isActiveAndEnabled && !opening.IntroFinished &&
            state.CurrentDay == 0 && state.GameFlow == EGameFlow.CommuteIn && !player.CheckFlag(NewGameOpening.SeenFlag);
        if (openingIntro)
        {
            // 안내 화면이 뜨기 전에 타이틀 음악을 정리하고 Home 음악을 아직 요청하지 않는다.
            if (group != null) sound.StopBGM(0);
            group = null;
        }
        else if (revealing && next != group)
        {
            group = next;
            switch (group)
            {
                case "home": sound.PlayPlaylist(group, new[] { "BGM_bar_02" }); break;
                case "street": sound.PlayPlaylist(group, new[] { "BGM_outside" }, .6f); break;
                case "bar": sound.PlayPlaylist(group, new[] { "BGM_bar_01", "BGM_bar_02", "BGM_bar_03" }); break;
                default: sound.StopBGM(scene == "TempEnding" ? 0 : 1); break;
            }
        }
        bool cinematic = GameAudioContext.Cinematic || state.CurrentGameState == GameState.Effect;
        bool outside = group == "street" && scene == "OutSide" && focused && !cinematic;
        if (outside)
        {
            street ??= sound.StartLoop("SFX_street_loop", .55f);
            bool paused = Time.timeScale <= 0f || UIDisplayOptions.IsOptionOpen;
            float gain = GameAudioRules.AmbienceGain(GameAudioContext.AnyDialogue, paused);
            street?.SetGain(gain);
            bool rainy = GameAudioRules.IsRainy(state.CurrentDay, state.GameFlow);
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
        if (!value || !key.EndsWith("_met", StringComparison.Ordinal)) return;
        string id = key.Substring(0, key.Length - 4);
        if (id == "chris") return;
        foreach (var character in NewDataLoadManager.StoryCharacters ?? Array.Empty<NewCharacterData>())
            if (character.Id == id) { popups.Popup(PopupType.CharacterRegistered, id).Forget(Debug.LogException); break; }
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
    }
}
