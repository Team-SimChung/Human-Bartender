using System;
using UnityEngine;

/// <summary>게임 시간의 일시정지를 소유한다. UI 연출과 음향의 실제 시간은 바꾸지 않는다.</summary>
public static class GamePause
{
    static int requests;
    static int generation;
    static float resumeTimeScale;
    public static bool IsPaused => requests > 0;

    public static IDisposable Acquire()
    {
        if (requests == 0) resumeTimeScale = Time.timeScale;
        requests++;
        Time.timeScale = 0f;
        return new Handle(generation);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        requests = 0;
        generation++;
        resumeTimeScale = 1f;
        Time.timeScale = 1f;
    }

    sealed class Handle : IDisposable
    {
        readonly int acquiredGeneration;
        bool disposed;
        public Handle(int acquiredGeneration) => this.acquiredGeneration = acquiredGeneration;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // 도메인 재로드 없이 다시 실행해도 이전 실행의 핸들이 새 요청을 반납하지 않는다.
            if (acquiredGeneration != generation) return;
            if (--requests == 0) Time.timeScale = resumeTimeScale;
        }
    }
}
