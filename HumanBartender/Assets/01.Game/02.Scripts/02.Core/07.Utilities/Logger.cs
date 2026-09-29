using UnityEngine;

/// <summary>
/// ENABLE_LOGS 심볼이 정의된 빌드에서만 동작하는 조건부 디버그 로거.
/// 릴리즈 빌드에서는 메서드 호출 자체가 컴파일러에 의해 제거된다.
/// </summary>
public static class Logger
{
    [System.Diagnostics.Conditional("ENABLE_LOGS")]
    public static void Log(object message)
    {
        Debug.Log(message);
    }

    [System.Diagnostics.Conditional("ENABLE_LOGS")]
    public static void Log(object message, Object context)
    {
        Debug.Log(message, context);
    }

    [System.Diagnostics.Conditional("ENABLE_LOGS")]
    public static void LogWarning(object message)
    {
        Debug.LogWarning(message);
    }

    [System.Diagnostics.Conditional("ENABLE_LOGS")]
    public static void LogWarning(object message, Object context)
    {
        Debug.LogWarning(message, context);
    }


    [System.Diagnostics.Conditional("ENABLE_LOGS")]
    public static void LogError(object message)
    {
        Debug.LogError(message);
    }

    [System.Diagnostics.Conditional("ENABLE_LOGS")]
    public static void LogError(object message, Object context)
    {
        Debug.LogError(message, context);
    }
}