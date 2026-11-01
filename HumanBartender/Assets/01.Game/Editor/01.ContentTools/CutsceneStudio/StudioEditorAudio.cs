using System;
using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    // Unity의 Scene 뷰와 동일한 편집 모드 오디오 갱신 API를 한곳에서 연결함.
    internal static class StudioEditorAudio
    {
        private static readonly Type AudioUtil = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");
        private static readonly Action UpdateAudio = Bind<Action>("UpdateAudio");
        private static readonly Action<Transform> SetListenerTransform = Bind<Action<Transform>>("SetListenerTransform");

        private static T Bind<T>(string method) where T : Delegate
        {
            var info = AudioUtil?.GetMethod(method);
            if (info == null)
                throw new NotSupportedException("이 Unity 버전에서 컷씬 미리보기 오디오를 초기화하지 못했습니다: " + method);
            return (T)info.CreateDelegate(typeof(T));
        }

        internal static void Update(Transform listener)
        {
            SetListenerTransform(listener);
            UpdateAudio();
        }

        internal static void RestoreListener()
        {
            Transform listener = null;
            foreach (SceneView view in SceneView.sceneViews)
                if (view != null && view.audioPlay && view.camera != null)
                    listener = view.camera.transform;
            SetListenerTransform(listener);
            UpdateAudio();
        }
    }
}
