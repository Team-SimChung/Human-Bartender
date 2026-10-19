using System;
using UnityEngine;

namespace HumanBartender.CutsceneStudio
{
    // 게임 상태에 대한 의존성을 이곳에 모으고 재생 전 값을 복원함.
    internal sealed class StudioGameplayScope : IDisposable
    {
        private readonly GameStateManager manager;
        private readonly GameState previousState;
        private readonly float previousScale;
        private readonly bool pause;
        private bool disposed;
        internal StudioGameplayScope(bool pause)
        {
            manager = GameStateManager.Instance;
            previousState = manager.CurrentGameState;
            previousScale = Time.timeScale;
            this.pause = pause;
            if (pause)
                Time.timeScale = 0;
            manager.CurrentGameState = GameState.Effect;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (pause)
                Time.timeScale = previousScale;
            if (manager.CurrentGameState == GameState.Effect)
                manager.CurrentGameState = previousState;
        }
    }
}
