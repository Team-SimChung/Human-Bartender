using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HumanBartender.CutsceneStudio.Editor
{
    // 에셋 카드와 클립 버튼의 드래그 수명·전달 데이터를 한곳에서 관리함.
    internal sealed class StudioAssetDrag
    {
        internal const string PayloadKey = "CutsceneStudio.ClipKind";
        private readonly VisualElement element;
        private readonly Func<Object> asset;
        private readonly Func<StudioKind?> kind;
        private readonly Action<string> beginDrag;
        private bool pressed;
        private Vector2 origin;
        internal StudioAssetDrag(VisualElement element, Func<Object> asset, Func<StudioKind?> kind, Action<string> beginDrag)
        {
            this.element = element;
            this.asset = asset;
            this.kind = kind;
            this.beginDrag = beginDrag;
            element.RegisterCallback<MouseDownEvent>(StartGesture);
            element.RegisterCallback<MouseUpEvent>(EndGesture);
            element.RegisterCallback<MouseLeaveEvent>(CancelGesture);
            element.RegisterCallback<MouseMoveEvent>(BeginDrag);
        }

        private void StartGesture(MouseDownEvent e)
        {
            if (e.button != 0 || e.target is Button && e.target != element)
                return;
            pressed = true;
            origin = e.mousePosition;
        }

        private void EndGesture(MouseUpEvent e)
        {
            pressed = false;
        }

        private void CancelGesture(MouseLeaveEvent e)
        {
            pressed = false;
        }

        private void BeginDrag(MouseMoveEvent e)
        {
            if (!pressed || (e.pressedButtons & 1) == 0 || Vector2.Distance(origin, e.mousePosition) < 4)
                return;
            var value = asset();
            var clipKind = kind();
            pressed = false;
            if (value == null && !clipKind.HasValue)
                return;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = value != null ? new[]
            {
                value
            }

            : Array.Empty<Object>();
            DragAndDrop.SetGenericData(PayloadKey, clipKind.HasValue ? (object)clipKind.Value : null);
            beginDrag(value != null ? value.name : StudioEditorAssets.Label(clipKind.Value));
            if (element.HasMouseCapture())
                element.ReleaseMouse();
            e.StopPropagation();
        }
    }
}
