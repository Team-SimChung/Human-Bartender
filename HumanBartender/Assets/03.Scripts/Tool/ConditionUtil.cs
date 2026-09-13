using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

/// <summary>
/// 데이터에 적힌 조건식(when)과 대입식(effects)을 해석한다.
///
/// 조건을 보는 곳이 둘이었다 — 실외 스폰은 이쪽, 2부 대본은 StoryConditionEvaluator. 문법이 같아
/// 보여도 구현이 달라서, 같은 식이 곳에 따라 다르게 판정될 수 있었다. 정본을 이쪽 하나로 모은다.
/// </summary>
public interface IConditionUtil
{
    /// <summary>조건식을 판정한다. 빈 값은 참이다.</summary>
    bool Check(string when);

    /// <summary>
    /// 대입식을 적용한다. token을 주면 그 토큰으로 한 번만 적용한다.
    ///
    /// 조건식과 달리 이쪽은 값을 바꾸는 일이라, 같은 것을 두 번 적용하면 호감도와 돈이 두 배가 된다.
    /// 적용할 것이 없거나 이미 적용했으면 false.
    /// </summary>
    bool Set(string statement, string token = null);

    /// <summary>적용 기록을 비운다. 하루가 끝나 같은 스텝이 다시 실행될 수 있을 때 부른다.</summary>
    void ResetAppliedTokens();

    /// <summary>
    /// 직전 서빙 결과. grade·order_match 같은 값이 여기서 나온다.
    /// 잔이 나가기 전에는 null이고, 그때 그 값을 물으면 오류다.
    /// </summary>
    StoryResultContext Result { get; set; }
}

/// <summary>
/// 조건식·대입식 해석기.
///
/// 문자열을 DataTable.Compute에 넘기던 방식을 걷어내고 직접 읽는다. 이유가 셋이다.
///
/// 하나는 등급이다. 대본은 <c>grade &gt;= excellent</c>처럼 등급 이름을 값으로 쓰는데, 식별자를 전부
/// 변수로 보는 평가기에서는 excellent가 "없는 변수"가 되어 조건이 통째로 무너진다.
///
/// 둘은 타입이다. 값을 문자열로 바꿔 넣으면 참·거짓과 수의 구분이 사라져서, <c>flag.x &gt;= 3</c>처럼
/// 뜻이 없는 식도 조용히 통과한다.
///
/// 셋은 빌드다. DataTable.Compute는 IL2CPP(AOT)에서 막힐 수 있는데, 에디터에서만 확인하면 못 잡는다.
///
/// 값을 모르는 키는 거짓으로 넘기지 않고 오류로 남긴다. 조건이 조용히 거짓이 되면 그 분기가 통째로
/// 사라지는데, 화면에는 "대사가 없는 것"과 똑같이 보인다.
/// </summary>
public class ConditionUtil : IConditionUtil
{
    readonly GameStateManager _gameStateManager;
    readonly IPlayerDataReader _playerDataReader;
    readonly IPlayerDataWriter _playerDataWriter;

    /// <summary>
    /// 이름 하나로 값을 꺼내는 표. 접두사가 붙는 것(flag. affinity.)은 Resolve가 따로 본다.
    /// 값을 새로 쓰려면 여기에 한 줄 더하면 된다.
    /// </summary>
    readonly Dictionary<string, Func<Value>> _databox;

    /// <summary>이미 적용한 대입식들. 같은 스텝을 두 번 확정해도 값이 두 번 움직이지 않게 한다.</summary>
    readonly HashSet<string> _appliedTokens = new();

    public StoryResultContext Result { get; set; }

    [Inject]
    public ConditionUtil(
        GameStateManager gameStateManager,
        IPlayerDataReader playerDataReader,
        IPlayerDataWriter playerDataWriter)
    {
        _gameStateManager = gameStateManager;
        _playerDataReader = playerDataReader;
        _playerDataWriter = playerDataWriter;

        _databox = new Dictionary<string, Func<Value>>
        {
            { "true",  () => Value.Of(true) },
            { "false", () => Value.Of(false) },

            { "day",   () => Value.Of(_gameStateManager.CurrentDay) },
            { "money", () => Value.Of(_playerDataReader.HasMoney()) },

            // 서빙 결과. grade는 현행 대본 호환 키로 final_grade와 같은 값을 준다.
            { "grade",       () => Value.Of(StoryGrade.ToRank(RequireResult("grade").FinalGrade)) },
            { "final_grade", () => Value.Of(StoryGrade.ToRank(RequireResult("final_grade").FinalGrade)) },
            { "craft_grade", () => Value.Of(StoryGrade.ToRank(RequireResult("craft_grade").CraftGrade)) },
            { "order_match", () => Value.Of(RequireResult("order_match").OrderMatch) },
        };
    }

    // ── 조건 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 조건식을 판정한다. 빈 값은 참이다 — 조건을 적지 않은 씬과 스텝은 언제나 실행된다.
    /// 식을 읽지 못하면 거짓으로 두고 오류를 남긴다.
    /// </summary>
    public bool Check(string when)
    {
        if (string.IsNullOrWhiteSpace(when)) return true;

        try
        {
            var parser = new Parser(when, this);
            bool result = parser.ParseExpression().AsBool();

            parser.ExpectEnd();
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Condition] 조건식을 판정하지 못했습니다: '{when}' — {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 식별자 하나의 값을 찾는다. 등급 이름은 부르기 전에 걸러지므로 여기 오지 않는다.
    /// 모르는 키는 예외다 — 임의의 기본값을 만들지 않는다.
    /// </summary>
    Value Resolve(string key)
    {
        if (_databox.TryGetValue(key, out Func<Value> getter)) return getter.Invoke();

        if (key.StartsWith("flag.", StringComparison.Ordinal))
            return Value.Of(_playerDataReader.CheckFlag(key));

        if (key.StartsWith("affinity.", StringComparison.Ordinal))
            return Value.Of(_playerDataReader.GetCurCharacterAffinityValue(key.Substring("affinity.".Length)));

        throw new InvalidOperationException($"'{key}'는 조건에 쓸 수 있는 값이 아닙니다.");
    }

    /// <summary>
    /// 서빙 결과를 꺼낸다. 아직 잔이 나가지 않았으면 오류다.
    /// 여기서 기본 등급을 지어내면 서빙 전에 반응 대사가 새어 나온다.
    /// </summary>
    StoryResultContext RequireResult(string key)
    {
        return Result ?? throw new InvalidOperationException(
            $"서빙 결과가 없는데 '{key}'를 물었습니다. serve보다 앞선 조건인지 확인하세요.");
    }

    // ── 대입 ────────────────────────────────────────────────────────────

    /// <summary>
    /// "flag.shiba_met = true", "affinity.chris += 1", "money -= 25"처럼 적힌 대입식을 적용한다.
    /// 세미콜론으로 여러 개를 이어 쓸 수 있다.
    ///
    /// token을 주면 그 토큰으로 한 번만 적용한다. 스텝·선택지를 가리키는 고정 값이어야 한다.
    ///
    /// 한 줄이 실패해도 나머지는 적용한다. 앞쪽 하나 때문에 뒤따르는 플래그가 통째로 빠지면,
    /// 대본은 진행되는데 상태만 어긋난 채로 남는다.
    /// </summary>
    public bool Set(string statement, string token = null)
    {
        if (string.IsNullOrWhiteSpace(statement)) return false;

        if (!string.IsNullOrEmpty(token) && !_appliedTokens.Add(token))
        {
            Debug.LogWarning($"[Condition] 이미 적용한 effects라 건너뜁니다: {token}");
            return false;
        }

        foreach (string clause in statement.Split(';'))
        {
            string trimmed = clause.Trim();
            if (trimmed.Length == 0) continue;

            try
            {
                ApplyClause(trimmed);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Condition] '{trimmed}'를 적용하지 못했습니다 (전체: '{statement}') — {e.Message}");
            }
        }

        return true;
    }

    /// <summary>적용 기록을 비운다. 하루가 끝나 같은 스텝이 다시 실행될 수 있을 때 부른다.</summary>
    public void ResetAppliedTokens()
    {
        _appliedTokens.Clear();
    }

    enum EOp { Set, Add, Subtract }

    void ApplyClause(string clause)
    {
        // "+=" 와 "-=" 를 "=" 보다 먼저 본다. "=" 를 먼저 찾으면 "affinity.x += 1"이
        // 대상 "affinity.x +" 와 값 "1"로 잘려서, 엉뚱한 이름에 값을 쓴다.
        if (!TrySplit(clause, "+=", out string target, out string raw, out EOp op) &&
            !TrySplit(clause, "-=", out target, out raw, out op) &&
            !TrySplit(clause, "=", out target, out raw, out op))
        {
            throw new InvalidOperationException("= 또는 += 가 없습니다.");
        }

        ApplyTo(target, raw, op);
    }

    static bool TrySplit(string clause, string token, out string target, out string raw, out EOp op)
    {
        target = null;
        raw = null;
        op = token switch { "+=" => EOp.Add, "-=" => EOp.Subtract, _ => EOp.Set };

        int at = clause.IndexOf(token, StringComparison.Ordinal);
        if (at < 0) return false;

        target = clause.Substring(0, at).Trim();
        raw = clause.Substring(at + token.Length).Trim();

        return target.Length > 0 && raw.Length > 0;
    }

    void ApplyTo(string target, string raw, EOp op)
    {
        if (target.StartsWith("flag.", StringComparison.Ordinal))
        {
            if (op != EOp.Set) throw new InvalidOperationException("플래그는 더하거나 뺄 수 없습니다.");

            _playerDataWriter.AddFlag(target, ToBool(raw));
            Debug.Log($"[Condition] {target} = {raw}");
            return;
        }

        if (target.StartsWith("affinity.", StringComparison.Ordinal))
        {
            string characterId = target.Substring("affinity.".Length);
            int amount = ToInt(raw);

            switch (op)
            {
                case EOp.Add: _playerDataWriter.AddCharacterAffinityAmount(characterId, amount); break;
                case EOp.Subtract: _playerDataWriter.AddCharacterAffinityAmount(characterId, -amount); break;
                default: _playerDataWriter.SetCharacterAffinityAmount(characterId, amount); break;
            }

            Debug.Log($"[Condition] {target} {Symbol(op)} {amount}");
            return;
        }

        if (target == "money")
        {
            int amount = ToInt(raw);

            switch (op)
            {
                case EOp.Add:
                    _playerDataWriter.AddMoney(amount);
                    break;

                case EOp.Subtract:
                    if (!_playerDataWriter.TrySpend(amount))
                        Debug.LogWarning($"[Condition] 돈이 모자라 {amount}를 쓰지 못했습니다.");
                    break;

                // 대입은 지금까지 번 것을 지우는 뜻이 된다. 그렇게 쓴 데이터가 없어 실수로 본다.
                default:
                    throw new InvalidOperationException("돈은 += 또는 -= 로만 바꿀 수 있습니다.");
            }

            Debug.Log($"[Condition] money {Symbol(op)} {amount}");
            return;
        }

        // 평판은 아직 담아 둘 곳이 없다. 1부의 BarReputation은 그 실행에서만 사는 값이라
        // 여기서 올려도 다음 날 남지 않는다. 조용히 넘기면 오르지 않은 것을 알아챌 수 없어 남긴다.
        if (target == "reputation")
            throw new InvalidOperationException("평판을 담아 둘 곳이 아직 없습니다. 저장 시스템이 붙을 때 잇습니다.");

        throw new InvalidOperationException($"'{target}'은 바꿀 수 있는 대상이 아닙니다.");
    }

    static string Symbol(EOp op) => op switch
    {
        EOp.Add => "+=",
        EOp.Subtract => "-=",
        _ => "=",
    };

    static bool ToBool(string raw) => raw switch
    {
        "true" => true,
        "false" => false,
        _ => throw new InvalidOperationException($"'{raw}'는 참·거짓이 아닙니다."),
    };

    static int ToInt(string raw)
    {
        return int.TryParse(raw, out int value)
            ? value
            : throw new InvalidOperationException($"'{raw}'를 수로 읽지 못했습니다.");
    }

    // ── 값 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 식 안의 값 하나. 참·거짓과 수를 구분해 둔다 — 둘을 섞으면 flag가 0/1로 비교되면서
    /// 뜻이 없는 식도 통과한다.
    /// </summary>
    readonly struct Value
    {
        public readonly bool IsBool;
        readonly bool boolValue;
        readonly double number;

        Value(bool isBool, bool boolValue, double number)
        {
            IsBool = isBool;
            this.boolValue = boolValue;
            this.number = number;
        }

        public static Value Of(bool value) => new(true, value, 0d);
        public static Value Of(double value) => new(false, false, value);

        public bool AsBool() =>
            IsBool ? boolValue : throw new InvalidOperationException("참·거짓이 와야 할 자리에 수가 있습니다.");

        public double AsNumber() =>
            IsBool ? throw new InvalidOperationException("수가 와야 할 자리에 참·거짓이 있습니다.") : number;

        public bool EqualsValue(Value other)
        {
            if (IsBool != other.IsBool) throw new InvalidOperationException("참·거짓과 수는 견줄 수 없습니다.");

            return IsBool ? boolValue == other.boolValue : Math.Abs(number - other.number) < 0.0001d;
        }
    }

    // ── 읽기 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 식을 왼쪽부터 읽어 내려간다. 받는 문법은 비교 · 부정(!) · 결합(&amp;&amp;) · 선택(||) · 괄호다.
    /// 우선순위는 || 가 가장 낮고 그다음이 &amp;&amp; 다.
    /// </summary>
    class Parser
    {
        readonly string text;
        readonly ConditionUtil owner;
        int pos;

        public Parser(string text, ConditionUtil owner)
        {
            this.text = text;
            this.owner = owner;
        }

        public Value ParseExpression() => ParseOr();

        Value ParseOr()
        {
            Value left = ParseAnd();

            while (TryTake("||"))
            {
                // 오른쪽도 반드시 읽는다. 왼쪽이 참이라고 건너뛰면 뒤에 있는 문법 오류를 놓친다.
                Value right = ParseAnd();
                left = Value.Of(left.AsBool() || right.AsBool());
            }

            return left;
        }

        Value ParseAnd()
        {
            Value left = ParseComparison();

            while (TryTake("&&"))
            {
                Value right = ParseComparison();
                left = Value.Of(left.AsBool() && right.AsBool());
            }

            return left;
        }

        Value ParseComparison()
        {
            Value left = ParseUnary();

            string op = TakeComparisonOperator();
            if (op == null) return left;

            Value right = ParseUnary();

            return op switch
            {
                "==" => Value.Of(left.EqualsValue(right)),
                "!=" => Value.Of(!left.EqualsValue(right)),
                ">=" => Value.Of(left.AsNumber() >= right.AsNumber()),
                "<=" => Value.Of(left.AsNumber() <= right.AsNumber()),
                ">" => Value.Of(left.AsNumber() > right.AsNumber()),
                _ => Value.Of(left.AsNumber() < right.AsNumber()),
            };
        }

        Value ParseUnary()
        {
            if (TryTake("!")) return Value.Of(!ParseUnary().AsBool());

            return ParsePrimary();
        }

        Value ParsePrimary()
        {
            SkipSpaces();

            if (TryTake("("))
            {
                Value inner = ParseExpression();

                if (!TryTake(")")) throw new InvalidOperationException("닫는 괄호가 없습니다.");

                return inner;
            }

            if (pos < text.Length && (char.IsDigit(text[pos]) || text[pos] == '-')) return TakeNumber();

            string token = TakeIdentifier();

            // 등급 이름은 변수가 아니라 값이다. 변수로 찾으면 없는 키가 되어 식이 무너진다.
            if (StoryGrade.TryParseRank(token, out int rank)) return Value.Of(rank);

            return owner.Resolve(token);
        }

        Value TakeNumber()
        {
            int start = pos;
            if (text[pos] == '-') pos++;

            while (pos < text.Length && (char.IsDigit(text[pos]) || text[pos] == '.')) pos++;

            string raw = text.Substring(start, pos - start);

            return double.TryParse(raw, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out double value)
                ? Value.Of(value)
                : throw new InvalidOperationException($"'{raw}'를 수로 읽지 못했습니다.");
        }

        string TakeIdentifier()
        {
            SkipSpaces();

            int start = pos;
            while (pos < text.Length && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_' || text[pos] == '.')) pos++;

            if (pos == start) throw new InvalidOperationException($"{start}번째 글자에서 읽을 것이 없습니다.");

            return text.Substring(start, pos - start);
        }

        string TakeComparisonOperator()
        {
            SkipSpaces();

            foreach (string op in Operators)
            {
                if (!Matches(op)) continue;

                pos += op.Length;
                return op;
            }

            return null;
        }

        // 두 글자짜리를 먼저 본다. ">"를 먼저 보면 ">="가 ">"와 "="로 잘린다.
        static readonly string[] Operators = { "==", "!=", ">=", "<=", ">", "<" };

        bool TryTake(string token)
        {
            SkipSpaces();

            if (!Matches(token)) return false;

            pos += token.Length;
            return true;
        }

        bool Matches(string token)
        {
            return pos + token.Length <= text.Length && string.CompareOrdinal(text, pos, token, 0, token.Length) == 0;
        }

        void SkipSpaces()
        {
            while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
        }

        /// <summary>다 읽었는지 확인한다. 남은 글자가 있으면 식을 잘못 읽은 것이다.</summary>
        public void ExpectEnd()
        {
            SkipSpaces();

            if (pos < text.Length)
                throw new InvalidOperationException($"'{text.Substring(pos)}'를 읽지 못하고 남겼습니다.");
        }
    }
}
