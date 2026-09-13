#if !UNITY_6000_3_OR_NEWER
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio
{
    [InitializeOnLoad]
    public class ToolbarRegisterCore
    {
        private static VisualElement _toolbarRoot;
        private static VisualElement _toolbarLeft;
        private static VisualElement _toolbarRight;
        private static VisualElement _toolbarCenterLeft;
        private static VisualElement _toolbarCenterRight;

        static ToolbarRegisterCore()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            TryInitialize();
            ToolbarRegister.OnDrawToolbarLeft(_toolbarLeft);
            ToolbarRegister.OnDrawToolbarRight(_toolbarRight);
            ToolbarRegister.OnDrawToolbarCenterLeft(_toolbarCenterLeft);
            ToolbarRegister.OnDrawToolbarCenterRight(_toolbarCenterRight);

            EditorApplication.update -= OnUpdate;
        }

        private static void TryInitialize()
        {
            if (_toolbarRoot != null)
            {
                Debug.LogError("ToolbarRoot is already initialized");
                return;
            }

            var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
            var toolbarObj = toolbarType.GetField("get").GetValue(null);
            _toolbarRoot =
                (VisualElement)toolbarType.GetField("m_Root", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(
                    toolbarObj);

            _toolbarLeft = _toolbarRoot.Q("ToolbarZoneLeftAlign");
            _toolbarRight = _toolbarRoot.Q("ToolbarZoneRightAlign");
            var center = _toolbarRoot.Q("ToolbarZonePlayMode");
            var playModeLayout = center.Q<VisualElement>("PlayMode");
            
            if (_toolbarCenterLeft == null)
            {
                _toolbarCenterLeft = new VisualElement();
                _toolbarCenterLeft.name = "CenterLeftGroup";
                _toolbarCenterLeft.style.flexDirection = FlexDirection.RowReverse;

                playModeLayout.Insert(0, _toolbarCenterLeft);
            }

            if (_toolbarCenterRight == null)
            {
                _toolbarCenterRight = new VisualElement();
                _toolbarCenterRight.name = "CenterRightGroup";
                _toolbarCenterRight.style.flexDirection = FlexDirection.Row;

                playModeLayout.Add(_toolbarCenterRight);
            }
        }
    }
}
#endif