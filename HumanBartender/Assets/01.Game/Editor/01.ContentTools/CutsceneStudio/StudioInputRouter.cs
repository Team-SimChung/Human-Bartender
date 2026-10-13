using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    internal enum StudioInputCommand
    {
        None, Save, ClearSelection, Undo, Redo, Copy, Cut, Paste,
        DeleteClip, DeleteKey, TogglePlay, Advance, FirstFrame, PreviousFrame, NextFrame
    }

    internal interface IStudioInputHost : IStudioPanelHost
    {
        bool CanHandleInput { get; }
        bool KeyframeMode { get; }
        bool HandlePreviewToolKeys(Event input);
        void ExecuteInput(StudioInputCommand command);
    }

    // IMGUI와 UI Toolkit의 단축키 해석을 같은 규칙으로 처리함.
    internal sealed class StudioInputRouter
    {
        private readonly IStudioInputHost host;

        internal StudioInputRouter(IStudioInputHost host) { this.host = host; }

        internal bool NativeTextEditing()
        {
            var focused = host.Window.rootVisualElement.focusController?.focusedElement as VisualElement;
            for (var element = focused; element != null; element = element.parent)
                if (element is ToolbarSearchField || element.ClassListContains("unity-base-text-field"))
                    return true;
            return false;
        }

        private bool Active => host.CanHandleInput && EditorWindow.focusedWindow == host.Window;

        internal void HandleIMGUI()
        {
            if (!Active)
                return;
            var input = Event.current;
            if (host.HandlePreviewToolKeys(input))
                return;
            bool editing = EditorGUIUtility.editingTextField || NativeTextEditing();
            var command = input.type switch
            {
                EventType.KeyDown => ResolveKey(input.keyCode, input.control || input.command, input.shift, editing, host.KeyframeMode),
                EventType.ValidateCommand or EventType.ExecuteCommand => ResolveClipboard(input.commandName, editing, host.KeyframeMode),
                _ => StudioInputCommand.None
            };
            if (!Dispatch(command, input.type == EventType.ValidateCommand))
                return;
            input.Use();
        }

        internal void HandleToolkitKey(KeyDownEvent input)
        {
            if (!Active || IsIMGUIEvent(input.target))
                return;
            var command = ResolveKey(input.keyCode, input.ctrlKey || input.commandKey, input.shiftKey, NativeTextEditing(), host.KeyframeMode);
            if (Dispatch(command, false))
                input.StopPropagation();
        }

        internal void ValidateToolkitCommand(ValidateCommandEvent input)
        {
            if (HandleToolkitCommand(input.target, input.commandName, true))
                input.StopPropagation();
        }

        internal void ExecuteToolkitCommand(ExecuteCommandEvent input)
        {
            if (HandleToolkitCommand(input.target, input.commandName, false))
                input.StopPropagation();
        }

        private bool HandleToolkitCommand(IEventHandler target, string command, bool validate)
        {
            return Active && !IsIMGUIEvent(target) && Dispatch(ResolveClipboard(command, NativeTextEditing(), host.KeyframeMode), validate);
        }

        private static bool IsIMGUIEvent(IEventHandler target)
        {
            for (var element = target as VisualElement; element != null; element = element.parent)
                if (element is IMGUIContainer)
                    return true;
            return false;
        }

        private bool Dispatch(StudioInputCommand command, bool validate)
        {
            if (command == StudioInputCommand.None || GUIUtility.hotControl != 0)
                return false;
            if (!validate)
                host.ExecuteInput(command);
            return true;
        }

        internal static StudioInputCommand ResolveKey(KeyCode key, bool modified, bool shift, bool editing, bool keyframes)
        {
            // 저장은 입력 중에도 허용하며 나머지 텍스트 편집 명령은 입력창에 맡김.
            if (modified && key == KeyCode.S)
                return StudioInputCommand.Save;
            if (editing)
                return StudioInputCommand.None;
            if (modified)
                return key switch
                {
                    KeyCode.Z => shift ? StudioInputCommand.Redo : StudioInputCommand.Undo,
                    KeyCode.Y => StudioInputCommand.Redo,
                    KeyCode.C when !keyframes => StudioInputCommand.Copy,
                    KeyCode.X when !keyframes => StudioInputCommand.Cut,
                    KeyCode.V when !keyframes => StudioInputCommand.Paste,
                    _ => StudioInputCommand.None
                };
            return key switch
            {
                KeyCode.Escape => StudioInputCommand.ClearSelection,
                KeyCode.Delete => keyframes ? StudioInputCommand.DeleteKey : StudioInputCommand.DeleteClip,
                KeyCode.Space => StudioInputCommand.TogglePlay,
                KeyCode.E or KeyCode.Return => StudioInputCommand.Advance,
                KeyCode.Home => StudioInputCommand.FirstFrame,
                KeyCode.LeftArrow => StudioInputCommand.PreviousFrame,
                KeyCode.RightArrow => StudioInputCommand.NextFrame,
                _ => StudioInputCommand.None
            };
        }

        internal static StudioInputCommand ResolveClipboard(string command, bool editing, bool keyframes)
        {
            if (editing || keyframes)
                return StudioInputCommand.None;
            return command switch
            {
                "Copy" => StudioInputCommand.Copy,
                "Cut" => StudioInputCommand.Cut,
                "Paste" => StudioInputCommand.Paste,
                _ => StudioInputCommand.None
            };
        }
    }
}
