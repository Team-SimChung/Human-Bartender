using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 메인(타이틀) 씬 전용 매니저.
/// 기존 타이틀 버튼의 TempStart UnityEvent를 유지한다.
/// </summary>
public class MainManager : MonoBehaviour
{
    static readonly Rect ContinueButton = new(20, 20, 150, 38);
    bool starting;
    bool continueOpen;
    string startError;

    /// <summary>Day 0의 2부 Play에서 새 게임을 시작한다.</summary>
    public void TempStart()
    {
        // 타이틀의 새 게임 버튼은 전체 화면을 덮는다. IMGUI 이어하기 영역의 클릭은 새 게임으로 보내지 않는다.
        var pointer = Input.mousePosition;
        if (continueOpen || (Input.GetMouseButtonUp(0) && ContinueButton.Contains(
                new Vector2(pointer.x, Screen.height - pointer.y)))) return;
        if (!starting) StartNewGameAsync().Forget(Debug.LogException);
    }

    async UniTask StartNewGameAsync()
    {
        starting = true;
        try
        {
            var scope = LifetimeScope.Find<ProjectLifetimeScope>();
            if (scope?.Container == null) throw new System.InvalidOperationException("프로젝트 진행 서비스가 준비되지 않았습니다.");
            var progression = scope.Container.Resolve<GameProgressionService>();
            var player = scope.Container.Resolve<PlayerDataSO>();
            int previousMoney = player.HasMoney();
            int previousSkill = player.GetSkillValue();
            var previousAffinity = player.ReadAffinity();
            var previousFlags = player.ReadFlags();
            GameProgressionResult result = await progression.StartNewGameAsync(
                player.Init,
                () => player.ReplaceProgress(previousMoney, previousSkill, previousAffinity, previousFlags),
                this.GetCancellationTokenOnDestroy());
            startError = result.Succeeded ? null : result.Message ?? result.Outcome.ToString();
            if (startError != null) Debug.LogError($"[Main] 새 게임 시작 실패: {startError}");
        }
        catch (System.Exception error)
        {
            startError = error.Message;
            Debug.LogException(error);
        }
        finally { starting = false; }
    }

    async UniTask ContinueAsync(int slot)
    {
        starting = true;
        try
        {
            var scope = LifetimeScope.Find<ProjectLifetimeScope>();
            var saves = scope?.Container?.Resolve<SaveManager>();
            if (saves == null) throw new System.InvalidOperationException("저장 서비스가 준비되지 않았습니다.");
            GameProgressionResult result = await saves.LoadSlotAsync(slot, this.GetCancellationTokenOnDestroy());
            startError = result.Succeeded ? null : result.Message ?? result.Outcome.ToString();
            if (startError != null) Debug.LogError($"[Main] 이어하기 실패: {startError}");
        }
        catch (System.Exception error)
        {
            startError = error.Message;
            Debug.LogException(error);
        }
        finally { starting = false; }
    }

    void OnGUI()
    {
        if (GUI.Button(ContinueButton, continueOpen ? "이어하기 닫기" : "이어하기"))
            continueOpen = !continueOpen;
        if (continueOpen)
        {
            var scope = LifetimeScope.Find<ProjectLifetimeScope>();
            var saves = scope?.Container?.Resolve<SaveManager>();
            GUI.Box(new Rect(20, 65, 490, 275), "수동 저장 슬롯");
            for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
            {
                string label = "저장 서비스 준비 중";
                bool canLoad = false;
                if (saves != null) saves.TryDescribeSlot(slot, out label, out canLoad);
                GUI.Label(new Rect(35, 86 + 47 * (slot - 1), 365, 38), label);
                GUI.enabled = !starting && canLoad;
                if (GUI.Button(new Rect(405, 88 + 47 * (slot - 1), 85, 32), "불러오기"))
                    ContinueAsync(slot).Forget(Debug.LogException);
                GUI.enabled = true;
            }
        }
        if (string.IsNullOrEmpty(startError)) return;
        GUI.Box(new Rect(20, 350, 490, 68), "진행 요청에 실패했습니다.");
        GUI.Label(new Rect(32, 378, 460, 25), startError);
    }
}
