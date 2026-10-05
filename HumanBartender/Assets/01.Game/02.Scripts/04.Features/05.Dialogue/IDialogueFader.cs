using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>대화 캐릭터 슬롯의 페이드 인/아웃을 담당하는 인터페이스.</summary>
public interface IDialogueFader
{
    public UniTask FadeInAsync(ESlotType slot, CancellationToken token);
    public UniTask FadeOutAsync(ESlotType slot, CancellationToken token);
}
