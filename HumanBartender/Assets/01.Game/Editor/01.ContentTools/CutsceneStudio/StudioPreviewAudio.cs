using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 미리보기 전용 자원과 믹서 설정의 수명만 관리하고 재생 규칙은 공용 코드에 위임함.
    internal sealed class StudioPreviewAudio : IDisposable
    {
        private readonly Scene scene;
        private readonly GameObject owner;
        private readonly StudioAudioPlayback playback;
        private readonly StudioPreviewMixer mixer;
        private bool disposed;

        internal StudioPreviewAudio()
        {
            mixer = new StudioPreviewMixer(AudioOutputSettings.Shared, new PlayerPrefsAudioVolumeStore());
            scene = EditorSceneManager.NewPreviewScene();
            owner = new GameObject("Cutscene Studio Preview Audio") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(owner, scene);
            owner.AddComponent<AudioListener>();
            playback = new StudioAudioPlayback(owner, mixer.Output);
            try
            {
                StudioEditorAudio.Update(owner.transform);
                AssemblyReloadEvents.beforeAssemblyReload += Dispose;
                EditorApplication.quitting += Dispose;
                EditorApplication.playModeStateChanged += OnPlayModeChanged;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal void Evaluate(StudioSequence sequence, double time, bool waiting, float speed)
        {
            if (disposed) return;
            playback.Speed = speed;
            playback.Evaluate(sequence, time, waiting);
            StudioEditorAudio.Update(owner.transform);
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            EditorApplication.quitting -= Dispose;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            playback.Dispose();
            Object.DestroyImmediate(owner);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            mixer.Dispose();
            StudioEditorAudio.RestoreListener();
        }
    }
}
