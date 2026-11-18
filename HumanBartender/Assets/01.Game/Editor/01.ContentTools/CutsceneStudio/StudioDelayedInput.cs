using UnityEngine;
using UnityEngine.UIElements;

namespace HumanBartender.CutsceneStudio.Editor
{
    // FocusOut은 포인터 이벤트가 끝난 뒤 도착할 수 있다. 선택/패널을 바꾸기 전에 초안을 확정한다.
    internal static class StudioDelayedInput
    {
        internal static VisualElement FocusedTextField(VisualElement root)
        {
            if (root?.panel == null) return null;
            // focusedElement는 ListView 같은 composite root로 재지정된다.
            // 공개 :focus 조회로 실제 입력 요소를 찾은 뒤 소유 필드까지 올라간다.
            var focused = (VisualElement)root.Query<TextElement>().Focused().First()
                ?? root.focusController?.focusedElement as VisualElement;
            for (var element = focused; element != null; element = element.parent)
                if (element.ClassListContains("unity-base-text-field"))
                    return element == root || root.Contains(element) ? element : null;
            return null;
        }

        internal static void CommitFocused(VisualElement root, VisualElement nextTarget = null)
        {
            var element = FocusedTextField(root);
            if (element == null || (nextTarget != null && (element == nextTarget || element.Contains(nextTarget))))
                return;
            switch (element)
            {
                case TextField field when field.isDelayed && !field.isReadOnly && field.enabledInHierarchy:
                    field.value = field.text;
                    return;
                case FloatField field when field.isDelayed && !field.isReadOnly && field.enabledInHierarchy:
                    if (ExpressionEvaluator.Evaluate(field.text, out float floatValue)) field.value = floatValue;
                    else field.SetValueWithoutNotify(field.value);
                    return;
                case DoubleField field when field.isDelayed && !field.isReadOnly && field.enabledInHierarchy:
                    if (ExpressionEvaluator.Evaluate(field.text, out double doubleValue)) field.value = doubleValue;
                    else field.SetValueWithoutNotify(field.value);
                    return;
            }
        }
    }
}
