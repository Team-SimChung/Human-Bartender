using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer;

/// <summary>귀가 후 Home의 수동 슬롯 파일 경계. 런타임 국면이나 정산 내역은 기록하지 않는다.</summary>
public sealed class SaveManager
{
    public const int SlotCount = 5;
    const int SaveVersion = 1;
    static readonly string[] Fields =
        { "save_version", "saved_at", "current_day", "money", "skill_amount", "affinity", "flags" };

    readonly PlayerDataSO player;
    readonly GameProgressionService progression;
    readonly GameStateManager gameState;
    readonly string saveDirectory;

    [Inject]
    public SaveManager(PlayerDataSO player, GameProgressionService progression, GameStateManager gameState)
        : this(player, progression, gameState, Path.Combine(Application.persistentDataPath, "manual-saves")) { }

    internal SaveManager(PlayerDataSO player, GameProgressionService progression,
        GameStateManager gameState, string saveDirectory)
    {
        this.player = player;
        this.progression = progression;
        this.gameState = gameState;
        this.saveDirectory = saveDirectory;
    }

    string SlotPath(int slot)
    {
        if (slot < 1 || slot > SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
        return Path.Combine(saveDirectory, $"slot-{slot}.json");
    }

    public bool HasSlotFile(int slot) => slot >= 1 && slot <= SlotCount && File.Exists(SlotPath(slot));

    public bool TryDescribeSlot(int slot, out string description, out bool canLoad)
    {
        canLoad = false;
        if (slot < 1 || slot > SlotCount)
        {
            description = "잘못된 슬롯";
            return false;
        }
        string path = SlotPath(slot);
        if (!File.Exists(path))
        {
            description = $"{slot}번: 비어 있음";
            return true;
        }
        try
        {
            ReadAndValidate(path, out int day, out _, out _, out _, out _, out string savedAt);
            description = $"{slot}번: Day {day} · {savedAt}";
            canLoad = true;
        }
        catch (Exception error)
        {
            description = $"{slot}번: 읽을 수 없음 ({error.Message})";
        }
        return true;
    }

    public bool TrySaveSlot(int slot, out string message)
    {
        if (slot < 1 || slot > SlotCount)
        {
            message = "슬롯은 1~5번만 사용할 수 있습니다.";
            return false;
        }
        if (!NewDataLoadManager.IsLoaded || !NewDataLoadManager.TryGetDayInfo(gameState.CurrentDay, out _))
        {
            message = "현재 일차 데이터가 준비되지 않았습니다.";
            return false;
        }
        if (!progression.TryBeginManualSave(out message)) return false;

        string temporary = null;
        string backup = null;
        try
        {
            var affinity = new JObject();
            foreach (var pair in player.ReadAffinity()) affinity.Add(pair.Key, pair.Value);
            var flags = new JObject();
            foreach (var pair in player.ReadFlags()) flags.Add(pair.Key, pair.Value);
            var document = new JObject
            {
                ["save_version"] = SaveVersion,
                ["saved_at"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["current_day"] = gameState.CurrentDay,
                ["money"] = player.HasMoney(),
                ["skill_amount"] = player.GetSkillValue(),
                ["affinity"] = affinity,
                ["flags"] = flags,
            };
            string path = SlotPath(slot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string unique = Guid.NewGuid().ToString("N");
            temporary = path + "." + unique + ".tmp";
            backup = path + "." + unique + ".bak";
            File.WriteAllText(temporary, document.ToString(Formatting.Indented), new UTF8Encoding(false));
            ReadAndValidate(temporary, out _, out _, out _, out _, out _, out _);
            if (File.Exists(path)) File.Replace(temporary, path, backup);
            else File.Move(temporary, path);
            temporary = null;
            message = $"{slot}번 슬롯에 저장했습니다.";
            return true;
        }
        catch (Exception error)
        {
            message = $"저장 실패: {error.Message}";
            return false;
        }
        finally
        {
            progression.EndManualSave();
            try
            {
                if (temporary != null && File.Exists(temporary)) File.Delete(temporary);
                if (backup != null && File.Exists(backup)) File.Delete(backup);
            }
            catch (Exception cleanupError) { Debug.LogWarning($"[Save] 임시 파일 정리 실패: {cleanupError.Message}"); }
        }
    }

    public async UniTask<GameProgressionResult> LoadSlotAsync(int slot,
        CancellationToken cancellationToken = default)
    {
        if (slot < 1 || slot > SlotCount)
            return new GameProgressionResult(GameProgressionOutcome.Rejected, "슬롯은 1~5번만 사용할 수 있습니다.");
        try
        {
            if (!NewDataLoadManager.IsLoaded)
                return new GameProgressionResult(GameProgressionOutcome.Rejected, "일차 데이터가 아직 준비되지 않았습니다.");
            ReadAndValidate(SlotPath(slot), out int day, out int money, out int skill,
                out var affinity, out var flags, out _);
            int oldMoney = player.HasMoney();
            int oldSkill = player.GetSkillValue();
            var oldAffinity = player.ReadAffinity();
            var oldFlags = player.ReadFlags();
            return await progression.RestoreHomeAsync(day,
                () => player.ReplaceProgress(money, skill, affinity, flags),
                () => player.ReplaceProgress(oldMoney, oldSkill, oldAffinity, oldFlags),
                cancellationToken);
        }
        catch (OperationCanceledException error)
        {
            return new GameProgressionResult(GameProgressionOutcome.Canceled, error.Message, error);
        }
        catch (Exception error)
        {
            return new GameProgressionResult(GameProgressionOutcome.Rejected, error.Message, error);
        }
    }

    static void ReadAndValidate(string path, out int day, out int money, out int skill,
        out Dictionary<string, int> affinity, out Dictionary<string, bool> flags, out string savedAt)
    {
        using var file = new StreamReader(path, Encoding.UTF8, true);
        using var reader = new JsonTextReader(file) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 };
        var document = JObject.Load(reader, new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
        });
        if (reader.Read()) throw new InvalidDataException("JSON 뒤에 추가 데이터가 있습니다.");
        if (document.Properties().Count() != Fields.Length ||
            Fields.Any(field => document.Property(field, StringComparison.Ordinal) == null))
            throw new InvalidDataException("저장 필드가 누락되었거나 허용되지 않은 필드가 있습니다.");
        if (ReadInt(document["save_version"], "save_version") != SaveVersion)
            throw new InvalidDataException("지원하지 않는 저장 버전입니다.");
        if (document["saved_at"]?.Type != JTokenType.String ||
            !DateTime.TryParseExact(document["saved_at"].Value<string>(), "O", CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out _))
            throw new InvalidDataException("저장 시각이 올바르지 않습니다.");
        savedAt = document["saved_at"].Value<string>();
        day = ReadInt(document["current_day"], "current_day");
        money = ReadInt(document["money"], "money");
        skill = ReadInt(document["skill_amount"], "skill_amount");
        if (money < 0 || day < 0 || !NewDataLoadManager.TryGetDayInfo(day, out _))
            throw new InvalidDataException("저장된 일차 또는 재화 범위가 올바르지 않습니다.");
        if (document["affinity"] is not JObject affinityObject || document["flags"] is not JObject flagObject)
            throw new InvalidDataException("호감도 또는 플래그 형식이 올바르지 않습니다.");
        affinity = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var property in affinityObject.Properties())
        {
            if (string.IsNullOrWhiteSpace(property.Name)) throw new InvalidDataException("빈 캐릭터 ID입니다.");
            affinity.Add(property.Name, ReadInt(property.Value, property.Name));
        }
        flags = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var property in flagObject.Properties())
        {
            if (property.Value.Type != JTokenType.Boolean)
                throw new InvalidDataException($"플래그 값이 bool이 아닙니다: {property.Name}");
            string key = PlayerDataSO.CanonicalFlagKey(property.Name);
            if (key != property.Name || !flags.TryAdd(key, property.Value.Value<bool>()))
                throw new InvalidDataException($"플래그 키가 중복되거나 canonical 형식이 아닙니다: {property.Name}");
        }
    }

    static int ReadInt(JToken token, string name)
    {
        if (token?.Type != JTokenType.Integer) throw new InvalidDataException($"정수 필드가 아닙니다: {name}");
        long value = token.Value<long>();
        if (value < int.MinValue || value > int.MaxValue)
            throw new InvalidDataException($"정수 범위를 벗어났습니다: {name}");
        return (int)value;
    }
}
