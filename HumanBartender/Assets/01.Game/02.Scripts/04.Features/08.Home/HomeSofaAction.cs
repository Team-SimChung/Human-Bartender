using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

/// <summary>공통 선택창으로 소파의 수면·저장·취소를 처리함.</summary>
public sealed class HomeSofaAction : MonoBehaviour, IWorldInteractionAction, IWorldInteractionSavePolicy
{
    private const int SlotsPerPage = 2;
    [Inject] private IGameProgressionService progression;
    [Inject] private SaveManager saves;
    private bool running;
    private bool sleeping;
    private CancellationTokenSource execution;
    public bool CanExecute => isActiveAndEnabled && !running && progression != null && saves != null;
    public bool CanSave => running && !sleeping;

    public async UniTask ExecuteAsync(WorldInteractionActionContext context, CancellationToken token)
    {
        if (!CanExecute) throw new InvalidOperationException("소파를 사용할 준비가 되지 않았습니다.");
        if (context.ChooseMenu == null) throw new InvalidOperationException("소파의 공통 선택창이 연결되지 않았습니다.");
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, this.GetCancellationTokenOnDestroy());
        execution = lifetime;
        token = lifetime.Token;
        running = true;
        try
        {
            while (true)
            {
                var selected = await context.ChooseMenu(new[]
                {
                    Option("sleep", "수면", "Sleep"),
                    Option("save", "저장", "Save"),
                    Option("cancel", "취소", "Cancel")
                }, null, token);
                switch (selected.Id)
                {
                    case "sleep":
                        if (await SleepAsync(context, token)) return;
                        break;
                    case "save":
                        await SaveAsync(context, token);
                        break;
                    case "cancel":
                        return;
                    default:
                        throw new InvalidOperationException("알 수 없는 소파 메뉴: " + selected.Id);
                }
            }
        }
        finally { running = sleeping = false; execution = null; }
    }

    private void OnDisable() => execution?.Cancel();

    private async UniTask SaveAsync(WorldInteractionActionContext context, CancellationToken token)
    {
        int page = 0;
        int pageCount = (SaveManager.SlotCount + SlotsPerPage - 1) / SlotsPerPage;
        while (true)
        {
            var selected = await context.ChooseMenu(BuildSlotOptions(page), null, token);
            if (selected.Id == "back") return;
            if (selected.Id == "next") { page = (page + 1) % pageCount; continue; }
            int slot = int.Parse(selected.Id.Substring("slot-".Length));
            if (saves.HasSlotFile(slot))
            {
                var confirmation = await context.ChooseMenu(new[]
                {
                    Option("overwrite", "덮어쓰기", "Overwrite"), Option("back", "취소", "Cancel")
                }, Localize($"{slot}번 슬롯의 저장 내용을 덮어쓸까요?", $"Overwrite the save in slot {slot}?"), token);
                if (confirmation.Id != "overwrite") continue;
            }
            token.ThrowIfCancellationRequested();
            bool saved = saves.TrySaveSlot(slot, out string message);
            await ShowMessageAsync(context, message, token);
            if (saved) return;
        }
    }

    private NewStreetOptionData[] BuildSlotOptions(int page)
    {
        var options = new List<NewStreetOptionData>();
        int first = page * SlotsPerPage + 1;
        int last = Mathf.Min(first + SlotsPerPage - 1, SaveManager.SlotCount);
        for (int slot = first; slot <= last; slot++)
        {
            saves.TryDescribeSlot(slot, out string description, out _);
            options.Add(Option("slot-" + slot, description, description));
        }
        if (SaveManager.SlotCount > SlotsPerPage)
            options.Add(Option("next", "다음 슬롯", "Next slots"));
        options.Add(Option("back", "뒤로", "Back"));
        return options.ToArray();
    }

    private async UniTask<bool> SleepAsync(WorldInteractionActionContext context, CancellationToken token)
    {
        sleeping = true;
        try
        {
            if (context.PlayNight == null) throw new InvalidOperationException("집 대화 실행기가 없습니다.");
            await context.PlayNight(token);
            var result = await progression.SleepAsync(context.RefreshConditions, token);
            token.ThrowIfCancellationRequested();
            if (result.Succeeded) return true;
            await ShowMessageAsync(context, result.Message ?? Localize("지금은 잠들 수 없습니다.", "You cannot sleep right now."), token);
            return false;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            Debug.LogException(error, this);
            await ShowMessageAsync(context, error.Message, token);
            return false;
        }
        finally { sleeping = false; }
    }

    private static async UniTask ShowMessageAsync(WorldInteractionActionContext context, string message, CancellationToken token)
    {
        await context.ChooseMenu(new[] { Option("ok", "확인", "OK") }, message, token);
    }

    private static NewStreetOptionData Option(string id, string ko, string en) =>
        new() { Id = id, Text = new Texts { Ko = ko, En = en } };
    private static string Localize(string ko, string en) => GameStateManager.Instance.Language == ELanguage.En ? en : ko;
}
