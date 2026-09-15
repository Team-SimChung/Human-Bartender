/// <summary>Play(Bar) 씬에서 카메라를 옮기는 창구.</summary>
public interface ISlotCamera
{
    /// <summary>1부 좌석표의 슬롯으로 옮긴다. 좌석 좌표는 PlayCamera가 든다.</summary>
    public void MoveToSlot(ESlotType slot, float dur = 1f);

    /// <summary>
    /// 지정한 world x로 옮긴다. 2부가 쓰는 문이다.
    ///
    /// 2부는 이 표(slotOptions)를 지나가지 않는다. 자리도 다르고, 두 손님의 중점처럼 어느 슬롯과도
    /// 겹치지 않는 위치가 나온다. 2부 좌석은 인물이 서 있는 슬롯 오브젝트가 정본이라
    /// BarStoryPresenter가 거기서 읽어 계산한 값을 그대로 넘긴다.
    /// </summary>
    public void MoveToX(float worldX, float dur = 1f);
}
