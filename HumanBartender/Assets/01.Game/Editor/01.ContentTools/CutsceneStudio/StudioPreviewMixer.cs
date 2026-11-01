using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 편집 모드는 SetFloat를 허용하지 않으므로 공용 믹서의 임시 복사본에 저장 음량을 적용함.
    // 원본 그룹·효과·스냅샷 연결을 복사하고 디스크 에셋과 사용자 설정은 수정하지 않음.
    internal sealed class StudioPreviewMixer : IDisposable
    {
        private readonly Dictionary<Object, Object> copies = new();
        internal AudioOutputSettings Output { get; private set; }

        internal StudioPreviewMixer(AudioOutputSettings source, IAudioVolumeStore volumes)
        {
            try
            {
                CopyMixer(source.GetGroup(AudioBus.SoundEffects).audioMixer);
                CopyMixer(source.GetGroup(AudioBus.Music).audioMixer);
                Output = (AudioOutputSettings)Copy(source);
                foreach (var copy in copies.Values) RemapReferences(copy);
                ApplyVolume(AudioBus.SoundEffects, AudioVolumeSettings.EffectiveVolume(volumes, AudioBus.SoundEffects));
                ApplyVolume(AudioBus.Music, AudioVolumeSettings.EffectiveVolume(volumes, AudioBus.Music));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private Object Copy(Object source)
        {
            if (copies.TryGetValue(source, out var existing)) return existing;
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            copy.hideFlags = HideFlags.HideAndDontSave;
            copies.Add(source, copy);
            return copy;
        }

        private void CopyMixer(AudioMixer mixer)
        {
            if (copies.ContainsKey(mixer)) return;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(mixer)))
                Copy(asset);
        }

        private void RemapReferences(Object copy)
        {
            using var serialized = new SerializedObject(copy);
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue != null && copies.TryGetValue(property.objectReferenceValue, out var replacement))
                    property.objectReferenceValue = replacement;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void ApplyVolume(AudioBus bus, float linear)
        {
            var mixer = Output.GetGroup(bus).audioMixer;
            var type = mixer.GetType();
            var parameters = type.GetProperty("exposedParameters")?.GetValue(mixer) as Array;
            var snapshot = type.GetProperty("TargetSnapshot")?.GetValue(mixer) as Object;
            if (parameters != null && snapshot != null && copies.ContainsValue(snapshot))
                foreach (var parameter in parameters)
                {
                    var parameterType = parameter.GetType();
                    if ((string)parameterType.GetField("name").GetValue(parameter) != AudioOutputSettings.VolumeParameter(bus)) continue;
                    object guid = parameterType.GetField("guid").GetValue(parameter);
                    snapshot.GetType().GetMethod("SetValue").Invoke(snapshot, new[] { guid, (object)AudioOutputSettings.ToDecibels(linear) });
                    return;
                }
            throw new InvalidOperationException(bus + " 미리보기 믹서의 음량 파라미터를 찾지 못했습니다.");
        }

        public void Dispose()
        {
            foreach (var copy in copies.Values)
                if (copy != null) Object.DestroyImmediate(copy);
            copies.Clear();
            Output = null;
        }
    }
}
