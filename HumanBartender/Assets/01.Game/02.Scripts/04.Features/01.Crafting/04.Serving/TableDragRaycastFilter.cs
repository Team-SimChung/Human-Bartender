using UnityEngine;

/// <summary>테이블 물건을 드래그하는 동안에는 화면 대화 UI 너머의 월드 드롭 영역에 입력을 전달한다.</summary>
public sealed class TableDragRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
{
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
        CoasterDragItem.Active == null && DrinkDragItem.Active == null;
}
