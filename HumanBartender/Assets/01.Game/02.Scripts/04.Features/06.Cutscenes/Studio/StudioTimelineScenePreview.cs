using UnityEngine;
using UnityEngine.Playables;

namespace HumanBartender.CutsceneStudio
{
    /// <summary>Unity Timeline 창에서 시퀀스를 확인하는 씬 바인딩에 쓰임.</summary>
    [ExecuteAlways]
    public sealed class StudioTimelineScenePreview : MonoBehaviour
    {
        public StudioSequence Sequence;
        public PlayableDirector Director;
        public StudioStage Stage;
        public Canvas PreviewCanvas;
        private void OnEnable()
        {
            if (PreviewCanvas != null)
                PreviewCanvas.enabled = !Application.isPlaying;
            if (!Application.isPlaying)
                RebuildPreview();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                ClearPreview();
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                if (PreviewCanvas != null)
                    PreviewCanvas.enabled = false;
                return;
            }

            if (Stage == null || Director == null || PreviewCanvas == null)
                return;
            if (Stage.Sequence != Sequence || Sequence != null && Stage.Frame == null)
                RebuildPreview();
            if (Sequence == null)
                return;
            Stage.FitTo(PreviewCanvas.pixelRect.size);
            Stage.Evaluate(Director.time);
        }

        [ContextMenu("Rebuild Timeline Preview")]
        public void RebuildPreview()
        {
            if (Application.isPlaying || Stage == null || PreviewCanvas == null)
                return;
            ClearPreview();
            Stage.Build(Sequence);
            if (Sequence == null)
            {
                PreviewCanvas.enabled = false;
                return;
            }
            // 생성한 그래픽은 임시 객체이며 씬에는 스테이지와 Timeline 연결만 저장함.
            foreach (var child in Stage.GetComponentsInChildren<Transform>(true))
                if (child != Stage.transform)
                    child.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            PreviewCanvas.enabled = true;
            Canvas.ForceUpdateCanvases();
            Stage.FitTo(PreviewCanvas.pixelRect.size);
            Stage.Evaluate(Director != null ? Director.time : 0);
        }

        private void ClearPreview()
        {
            if (Stage == null)
                return;
            for (int i = Stage.transform.childCount - 1; i >= 0; i--)
            {
                var child = Stage.transform.GetChild(i);
                if ((child.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0)
                    DestroyImmediate(child.gameObject);
            }
        }
    }
}
