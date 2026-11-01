using System;
using UnityEditor;
using UnityEngine;

namespace HumanBartender.CutsceneStudio.Editor
{
    // JSON 변환 없이 편집 전 직렬화 데이터를 보관하며 Unity 에셋 참조를 유지함.
    [Serializable]
    internal sealed class StudioClipSnapshot : IDisposable
    {
        [SerializeField] private StudioClip data;
        internal bool HasValue => data != null;

        internal void Capture(StudioClip source)
        {
            Dispose();
            data = ScriptableObject.CreateInstance<StudioClip>();
            EditorUtility.CopySerialized(source, data);
            data.hideFlags = HideFlags.HideAndDontSave;
        }

        internal void Restore(StudioClip target)
        {
            if (data == null || target == null)
                throw new InvalidOperationException("복사할 클립 데이터가 없음.");
            string name = target.name;
            var flags = target.hideFlags;
            try
            {
                EditorUtility.CopySerialized(data, target);
            }
            finally
            {
                // 임시 버퍼의 이름과 저장 제외 플래그를 실제 에셋에 전달하지 않음.
                target.name = name;
                target.hideFlags = flags;
            }
        }

        internal static StudioActor CopyActor(StudioActor source)
        {
            if (source == null)
                return null;
            return new StudioActor
            {
                Id = source.Id,
                Name = source.Name,
                Sprite = source.Sprite,
                Position = source.Position,
                Size = source.Size,
                Color = source.Color,
                Layer = source.Layer,
                ClipControlled = source.ClipControlled
            };
        }

        public void Dispose()
        {
            if (data != null)
                UnityEngine.Object.DestroyImmediate(data);
            data = null;
        }
    }
}
