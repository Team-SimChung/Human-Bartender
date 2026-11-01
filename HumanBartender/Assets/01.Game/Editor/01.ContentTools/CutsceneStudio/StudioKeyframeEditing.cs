using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace HumanBartender.CutsceneStudio.Editor
{
    public static class StudioKeyframeEditing
    {
        private static StudioPropertyKeys Editable(TimelineClip clip, StudioProperty property)
        {
            var asset = (StudioClip)clip.asset;
            var channel = StudioKeyframes.Channel(asset, property);
            if (channel != null)
                return channel;
            channel = new StudioPropertyKeys
            {
                Property = property,
                Keys = StudioKeyframes.GetKeys(asset, property, clip.duration)
            };
            var channels = new List<StudioPropertyKeys>(asset.PropertyKeys ?? Array.Empty<StudioPropertyKeys>());
            channels.Add(channel);
            asset.PropertyKeys = channels.ToArray();
            return channel;
        }

        public static int Set(TimelineClip clip, StudioProperty property, int index, float time, float value, bool record = true)
        {
            var asset = (StudioClip)clip.asset;
            if (record)
                Undo.RegisterCompleteObjectUndo(asset, "키프레임 편집");
            var channel = Editable(clip, property);
            var keys = new List<StudioFloatKey>();
            foreach (var key in channel.Keys ?? Array.Empty<StudioFloatKey>())
                if (key != null)
                    keys.Add(key);
            time = Mathf.Clamp(time, 0, (float)clip.duration);
            // 다른 키와 겹치면 옆에서 멈추어 기존 키 삭제를 방지함.
            if (index >= 0 && index < keys.Count)
            {
                const float gap = .0001f;
                float min = index > 0 ? keys[index - 1].Time + gap : 0;
                float max = index + 1 < keys.Count ? keys[index + 1].Time - gap : (float)clip.duration;
                time = Mathf.Clamp(time, min, Mathf.Max(min, max));
                keys[index].Time = time;
                keys[index].Value = StudioKeyframes.Limit(property, value);
            }
            else
            {
                index = FindAtTime(keys, time);
                if (index >= 0)
                    keys[index].Value = StudioKeyframes.Limit(property, value);
                else
                {
                    var added = new StudioFloatKey(time, StudioKeyframes.Limit(property, value));
                    keys.Add(added);
                    SortKeys(keys);
                    index = keys.IndexOf(added);
                }
            }

            channel.Keys = keys.ToArray();
            EditorUtility.SetDirty(asset);
            return index;
        }

        public static void Delete(TimelineClip clip, StudioProperty property, int index)
        {
            var asset = (StudioClip)clip.asset;
            Undo.RegisterCompleteObjectUndo(asset, "키프레임 삭제");
            var channel = Editable(clip, property);
            var keys = new List<StudioFloatKey>(channel.Keys ?? Array.Empty<StudioFloatKey>());
            if (index >= 0 && index < keys.Count)
                keys.RemoveAt(index);
            channel.Keys = keys.ToArray();
            EditorUtility.SetDirty(asset);
        }

        private static int FindAtTime(List<StudioFloatKey> keys, float time)
        {
            for (int i = 0; i < keys.Count; i++)
                if (Mathf.Abs(keys[i].Time - time) < .0001f)
                    return i;
            return -1;
        }

        private static void SortKeys(List<StudioFloatKey> keys)
        {
            // 시간이 같은 기존 키의 순서를 유지함.
            for (int i = 1; i < keys.Count; i++)
            {
                var key = keys[i];
                int index = i;
                while (index > 0 && keys[index - 1].Time > key.Time)
                {
                    keys[index] = keys[index - 1];
                    index--;
                }

                keys[index] = key;
            }
        }
    }
}
