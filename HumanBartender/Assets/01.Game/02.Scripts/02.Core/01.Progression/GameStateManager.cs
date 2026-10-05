using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 게임 전반의 현재 처리 상태를 나타내는 열거형.
/// </summary>
public enum GameState
{
    None,
    Play,
    Effect,
    Loading,
    MiniGame,
}


/// <summary>
/// 게임의 큰 흐름 단계(바 → 출근/퇴근 → 집)를 나타내는 열거형.
/// JSON에서 snake_case로 직렬화된다.
/// </summary>
[Serializable]
[JsonConverter(typeof(StringEnumConverter))]
public enum EGameFlow
{
    Bar,

    [EnumMember(Value = "commute_in")]
    CommuteIn,

    [EnumMember(Value = "commute_out")]
    CommuteOut,

    Home,

    [EnumMember(Value = "both")]
    Both
}

/// <summary>
/// 게임 전역 상태(GameState, EGameFlow, 현재 일차 등)를 관리하는 싱글톤.
/// MonoBehaviour를 사용하지 않는 순수 C# 싱글톤이다.
/// </summary>
public class GameStateManager
{
    private static GameStateManager instance;
    public static GameStateManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new GameStateManager();
                instance.Init();
            }
            return instance;
        }
    }



    #region Field

    GameState gameState = GameState.Play;
    EGameFlow gameFlow = EGameFlow.Bar;
    bool isDialogInitStart = false;
    bool isOutsideLoad = false;
    bool isOutsideLogo = false;
    int currentDay = 0;

    #endregion

    #region Property
    public GameState CurrentGameState { get => gameState; set => gameState = value; }
    public EGameFlow GameFlow { get => gameFlow; set => gameFlow = value; }
    public bool IsDialogInitStart { get => isDialogInitStart; set => isDialogInitStart = value; }
    public int CurrentDay { get => currentDay; set => currentDay = value; }
    public bool IsOutsideLogo { get => isOutsideLogo; set => isOutsideLogo = value; }

    public ELanguage Language = ELanguage.Ko;
    #endregion

    private void Init()
    {
        gameState = GameState.Play;
        gameFlow = EGameFlow.Bar;
        currentDay = 0;

        isOutsideLoad = false;
        isOutsideLogo = false;
        isDialogInitStart = false;
    }
}
