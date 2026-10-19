using UnityEngine;

/// <summary>말풍선/텍스트 표시용 트래커. 부모(UIOutsideTracker)의 추적 로직을 그대로 사용하는 타입 구분용 서브클래스.</summary>
public class UIOutsideTextview : UIOutsideTracker
{
    public override void OnEnable()
    {
        base.OnEnable();
    }

    public override void OnDisable()
    {
        base.OnDisable();
    }

    public override void LateUpdate()
    {
        base.LateUpdate();
    }
}
