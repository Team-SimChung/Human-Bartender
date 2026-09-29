using System;
using Cysharp.Threading.Tasks;

/// <summary>서빙 위치/UI 수명만 제공한다. 판정, 정산, 주문 상태는 소유하지 않는다.</summary>
public interface IOrderPresentation
{
    void Open(Func<CraftedDrink, bool> receive);
    void Unbind();
    UniTask CloseAsync();
}
