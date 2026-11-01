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
        private bool pressed, started;
        private int pointerId = PointerId.invalidPointerId;
        private Vector2 origin;
        internal StudioAssetDrag(VisualElement element, Func<Object> asset, Func<StudioKind?> kind, Action<string> beginDrag)
        {
            this.element = element;
            this.asset = asset;
            this.kind = kind;
            this.beginDrag = beginDrag;
            // Button의 Clickable이 호환 Mouse 이벤트를 소비하기 전에 같은 포인터 입력을 받음.
            element.RegisterCallback<PointerDownEvent>(StartGesture, TrickleDown.TrickleDown);
            element.RegisterCallback<PointerMoveEvent>(BeginDrag, TrickleDown.TrickleDown);
            element.RegisterCallback<PointerUpEvent>(EndGesture, TrickleDown.TrickleDown);
            element.RegisterCallback<PointerCaptureOutEvent>(CancelGesture);
            element.RegisterCallback<PointerCancelEvent>(_ => ResetGesture());
            element.RegisterCallback<DetachFromPanelEvent>(_ => ResetGesture());
        }

        private void StartGesture(PointerDownEvent e)
        {
            if (e.button != 0 || !element.enabledInHierarchy)
                return;
            // 카드 안의 추가 버튼은 카드 드래그의 시작점으로 취급하지 않음.
            for (var target = e.target as VisualElement; target != null && target != element; target = target.parent)
                if (target is Button)
                    return;
            pressed = true;
            started = false;
            pointerId = e.pointerId;
            origin = e.position;
            // Button은 Clickable의 캡처를 사용하고 일반 카드는 직접 캡처함.
            if (element is not Button)
                element.CapturePointer(pointerId);
        }

        private void EndGesture(PointerUpEvent e)
        {
            if (e.pointerId != pointerId || e.button != 0)
                return;
            pressed = false;
            if (started)
                e.StopImmediatePropagation();
            if (started || element is not Button)
                ReleasePointer();
            started = false;
        }

        private void CancelGesture(PointerCaptureOutEvent e)
        {
            if (e.pointerId == pointerId)
                pressed = false;
        }

        private void ResetGesture()
        {
            pressed = started = false;
            ReleasePointer();
            pointerId = PointerId.invalidPointerId;
        }

        private void ReleasePointer()
        {
            if (pointerId != PointerId.invalidPointerId && element.HasPointerCapture(pointerId))
                element.ReleasePointer(pointerId);
        }

        private void BeginDrag(PointerMoveEvent e)
        {
            if (!pressed || e.pointerId != pointerId)
                return;
            if ((e.pressedButtons & 1) == 0)
            {
                ResetGesture();
                return;
            }
            if (Vector2.Distance(origin, e.position) < 4)
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
            started = true;
            // 네이티브 드래그로 넘기기 전에 클릭 캡처를 해제해 중복 추가를 막음.
            ReleasePointer();
            beginDrag(value != null ? value.name : StudioEditorAssets.Label(clipKind.Value));
            e.StopImmediatePropagation();
        }
    }
}
