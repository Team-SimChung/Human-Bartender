#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WingmanInspector {

    public static class WingmanClipboardUtility {

        private static readonly HashSet<Type> singleInstanceComponents = new HashSet<Type> {
            // Core module types (direct reference is safe)
            typeof(Transform), typeof(RectTransform), typeof(Collider), typeof(Camera), typeof(AudioSource), typeof(AudioListener),
            typeof(Light), typeof(MeshFilter), typeof(MeshRenderer), typeof(SkinnedMeshRenderer),
            // Non-core module types (resolved via reflection to avoid hard assembly references)
            Type.GetType("UnityEngine.RectTransform, UnityEngine.UIModule"),
            Type.GetType("UnityEngine.Canvas, UnityEngine.UIModule"),
            Type.GetType("UnityEngine.CanvasRenderer, UnityEngine.UIModule"),
            Type.GetType("UnityEngine.Rigidbody, UnityEngine.PhysicsModule"),
            Type.GetType("UnityEngine.CharacterController, UnityEngine.PhysicsModule"),
            Type.GetType("UnityEngine.Rigidbody2D, UnityEngine.Physics2DModule"),
            Type.GetType("UnityEngine.Collider2D, UnityEngine.Physics2DModule"),
            Type.GetType("UnityEngine.Animator, UnityEngine.AnimationModule"),
            Type.GetType("UnityEngine.Animation, UnityEngine.AnimationModule"),
            Type.GetType("UnityEngine.SpriteRenderer, UnityEngine.SpriteRenderer"),
            Type.GetType("UnityEngine.AI.NavMeshAgent, UnityEngine.AIModule"),
            Type.GetType("UnityEngine.AI.NavMeshObstacle, UnityEngine.AIModule"),
        }.Where(t => t != null).ToHashSet();

        public static void PasteComponentsFromEmpty(this GameObject gameObject, List<Component> comps) {
            foreach (Component refComp in comps) {
                Type refCompType = refComp.GetType();

                if (refCompType == typeof(Transform)) {
                    gameObject.GetComponent<Transform>().CopyFields(refComp);
                    continue;
                }

                if (CanOnlyHaveOneInstance(refCompType) && gameObject.TryGetComponent(refCompType, out Component _)) {
                    continue;
                }

                Component comp = Undo.AddComponent(gameObject, refCompType);
                comp.CopyFields(refComp);
            }
        }

        public static void PasteComponents(this GameObject gameObject, List<Component> comps) {
            foreach (Component refComp in comps) {
                gameObject.PasteComponent(refComp.GetType(), new SerializedObject(refComp));
            }
        }
        
        public static void PasteComponents(this GameObject gameObject, List<WingmanComponentCopy> compCopies) {
            foreach (WingmanComponentCopy compCopy in compCopies) {
                gameObject.PasteComponent(compCopy.ComponentType, compCopy.SerializedObject);
            }
        }

        private static void PasteComponent(this GameObject gameObject, Type compType, SerializedObject serReference) {
            bool hasComponent = gameObject.TryGetComponent(compType, out Component existingComp);
            
            if (hasComponent) {
                existingComp.CopyFields(serReference);
                return;
            }
            
            Undo.AddComponent(gameObject, compType).CopyFields(serReference);
        }

        private static bool CanOnlyHaveOneInstance(Type compType) {
            if (singleInstanceComponents.Contains(compType)) {
                return true;
            }
            return compType.GetCustomAttribute<DisallowMultipleComponent>() != null;
        }
        
        private static void CopyFields(this Component target, SerializedObject serReference) {
            SerializedObject serTarget = new SerializedObject(target);
            
            SerializedProperty property = serReference.GetIterator();
            if (property.NextVisible(true)) {
                do {
                    serTarget.CopyFromSerializedProperty(property);
                }
                while (property.NextVisible(false));
            }

            serTarget.ApplyModifiedProperties();
        }

        private static void CopyFields(this Component target, Component reference) {
            target.CopyFields(new SerializedObject(reference));
        }
        
    }

}

#endif