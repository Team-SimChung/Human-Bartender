#if UNITY_EDITOR
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WingmanInspector {
    
    public static class WingmanUtility {
        
        [Serializable]
        public struct WingmanId : IEquatable<WingmanId>, IComparable {
            #if UNITY_6000_3_OR_NEWER
                public EntityId entity;
            #else
                public int legacy; 
                public bool legacyIsNull; // Can't use nullable because Unity can't seralize it
            #endif
            
            public static bool operator ==(WingmanId a, WingmanId b) => a.Equals(b); 
            public static bool operator !=(WingmanId a, WingmanId b) => !a.Equals(b); 
            
            public static WingmanId None() {
                WingmanId wingmanId = new();
                #if UNITY_6000_3_OR_NEWER
                    wingmanId.entity = EntityId.None;
                #else
                    wingmanId.legacy = 0;
                    wingmanId.legacyIsNull = true;
                #endif
                return wingmanId;
            }
            
            public bool Equals(WingmanId other) {
                #if UNITY_6000_3_OR_NEWER
                    return entity.Equals(other.entity);
                #else
                    return legacy == other.legacy;
                #endif
            }
            
            public override bool Equals(object obj) {
                return obj is WingmanId other && Equals(other);
            }

            public override int GetHashCode() {
                #if UNITY_6000_3_OR_NEWER
                    return entity.GetHashCode();
                #else
                    return legacy;
                #endif
            }

            public int CompareTo(object obj) {
                WingmanId other = obj is WingmanId id ? id : None();
                #if UNITY_6000_3_OR_NEWER
                    return entity.CompareTo(other.entity);
                #else
                    return legacy.CompareTo(other.legacy);
                #endif
            }
            
        }
        
        public static WingmanId GetWingmanId(Object obj) {
            WingmanId wingmanId = new();
            #if UNITY_6000_3_OR_NEWER
                wingmanId.entity = obj.GetEntityId();
            #else
                wingmanId.legacy = obj.GetInstanceID();
            #endif
            return wingmanId;
        }

    }
}

#endif