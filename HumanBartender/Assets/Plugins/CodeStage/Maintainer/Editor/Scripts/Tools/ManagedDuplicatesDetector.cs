#if UNITY_2021_2_OR_NEWER

using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Pool;

namespace CodeStage.Maintainer.Tools
{
    internal class ManagedDuplicatesDetector
    {
        private SerializedObjectTraverseInfo traverseInfo;
        private readonly HashSet<object> managedObjects = new();
            
        public static ManagedDuplicatesDetector Spawn(SerializedObjectTraverseInfo traverseInfo,
            SerializedProperty spWithManagedReference)
        {
            var instance = GenericPool<ManagedDuplicatesDetector>.Get();
            instance.Init(traverseInfo, spWithManagedReference);
            return instance;
        }

        public bool IsDuplicate(SerializedProperty iterator)
        {
            if (!AddedIsDuplicate(iterator)) 
                return false;
            
            traverseInfo.skipChildren = true;
            return true;
        }
        
        public void Dispose()
        {
            Uninit();
            GenericPool<ManagedDuplicatesDetector>.Release(this);
        }

        private void Init(SerializedObjectTraverseInfo traverseInfo, SerializedProperty spWithManagedReference)
        {
            this.traverseInfo = traverseInfo;
            AddedIsDuplicate(spWithManagedReference);
        }
        
        private void Uninit()
        {
            managedObjects.Clear();
            traverseInfo = null;
        }
        
        private bool AddedIsDuplicate(SerializedProperty iterator)
        {
            if (iterator.managedReferenceValue == null) return false;
            return !managedObjects.Add(iterator.managedReferenceValue);
        }
    }
}
#endif