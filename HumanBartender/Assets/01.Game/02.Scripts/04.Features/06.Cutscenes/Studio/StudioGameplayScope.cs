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
        private readonly bool previousAudioPause;
        private bool disposed;
        private readonly IDisposable audioScope;
        internal StudioGameplayScope(bool pause)
        {
            // 공용 매니저 밖의 미니게임 음원·예약 재생도 정지함.
            // 컷씬 음원은 StudioAudioPlayback에서 ignoreListenerPause를 사용함.
            previousAudioPause = AudioListener.pause;
            AudioListener.pause = true;
            audioScope = GameAudioContext.Cutscene();
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
            audioScope.Dispose();
            AudioListener.pause = previousAudioPause;
            if (pause)
                Time.timeScale = previousScale;
            if (manager.CurrentGameState == GameState.Effect)
                manager.CurrentGameState = previousState;
        }
    }
}
