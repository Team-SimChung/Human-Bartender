using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>단일 오브젝트의 페이드 인/아웃을 담당하는 인터페이스.</summary>
public interface IFade
{
    public UniTask FadeIn(CancellationToken token);
    public UniTask FadeOut(CancellationToken token);
}
