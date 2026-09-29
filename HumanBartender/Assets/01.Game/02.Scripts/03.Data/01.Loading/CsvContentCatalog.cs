using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;

/// <summary>CSV 표와 CSV 스키마를 읽어 기존 게임 데이터 타입에 직접 매핑한다. JSON 파일이나 파서를 사용하지 않는다.</summary>
public sealed class CsvContentCatalog
{
    readonly Dictionary<string, Dictionary<string, string>> records = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> recordTables = new(StringComparer.Ordinal);
    readonly Dictionary<(string table, string parent, string field), List<Dictionary<string, string>>> children = new();
    readonly Dictionary<string, Node> templates = new(StringComparer.Ordinal);
    readonly Dictionary<string, string> sources = new(StringComparer.Ordinal);
    readonly Dictionary<(string table, string column), string> types = new();
    readonly Dictionary<string, object> values = new(StringComparer.Ordinal);
    readonly HashSet<string> visited = new(StringComparer.Ordinal);
    readonly HashSet<string> active = new(StringComparer.Ordinal);

    sealed class Node
    {
        public Dictionary<string, string> Data;
        public readonly List<Node> Fields = new();
        public string Get(string key) => Data.TryGetValue(key, out var value) ? value : "";
    }

    public static CsvContentCatalog Parse(IReadOnlyDictionary<string, string> files)
    {
        var result = new CsvContentCatalog();
        result.Initialize(files);
        return result;
    }

    public bool Contains(string sourceId) => sources.ContainsKey(sourceId);
    public IEnumerable<string> SourceIds => sources.Keys;
    public T Read<T>(string sourceId) => (T)Read(sourceId, typeof(T));
    public object Read(string sourceId, Type type)
    {
        if (!values.TryGetValue(sourceId, out var data)) throw new InvalidOperationException($"CSV 데이터 묶음 없음: {sourceId}");
        try { return ConvertValue(data, type, sourceId); }
        catch (Exception e) { throw new InvalidOperationException($"CSV 타입 매핑 실패: {sourceId} → {type.Name}: {e.Message}", e); }
    }
    public object ReadUntyped(string sourceId) => values[sourceId];

    void Initialize(IReadOnlyDictionary<string, string> files)
    {
        List<Dictionary<string, string>> Rows(string file) => ParseRows(files.TryGetValue(file, out var text)
            ? text : throw new InvalidOperationException($"필수 CSV 없음: {file}"), file);
        var tableSpecs = Rows("_Tables.csv");
        var columns = Rows("_Columns.csv");
        foreach (var c in columns) types.Add((c["table"], c["column"]), c["types"]);
        var nodes = new Dictionary<(string, string), Node>();
        foreach (var row in Rows("_Schemas.csv")) nodes.Add((row["template_id"], row["node_id"]), new Node { Data = row });
        foreach (var node in nodes.Values)
        {
            string parent = node.Get("parent_node"), template = node.Get("template_id");
            if (parent == "") templates.Add(template, node);
            else if (nodes.TryGetValue((template, parent), out var owner)) owner.Fields.Add(node);
            else throw new InvalidOperationException($"CSV 스키마 부모 없음: {template}/{parent}");
        }
        foreach (var node in nodes.Values) node.Fields.Sort((a, b) => Order(a.Get("field_order")).CompareTo(Order(b.Get("field_order"))));
        foreach (var root in Rows("_Sources.csv")) sources.Add(root["dataset_id"], root["template_id"]);
        var tableNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in tableSpecs)
        {
            string name = t["table"];
            if (!tableNames.Add(name)) throw new InvalidOperationException($"중복 CSV 표: {name}");
            string[] expected = columns.Where(c => c["table"] == name).OrderBy(c => Order(c["column_order"])).Select(c => c["column"]).ToArray();
            var matrix = ParseCsv(files[name + ".csv"], name);
            if (matrix.Count == 0 || !matrix[0].SequenceEqual(expected)) throw new InvalidOperationException($"CSV 헤더 불일치: {name}");
            foreach (var row in Rows(name + ".csv"))
            {
                string id = row["row_id"];
                if (string.IsNullOrWhiteSpace(id) || !records.TryAdd(id, row)) throw new InvalidOperationException($"CSV row_id 누락 또는 중복: {name}/{id}");
                if (!templates.TryGetValue(row["template_id"], out var template) || template.Get("owner") != name)
                    throw new InvalidOperationException($"CSV template_id 불일치: {name}/{id}");
                Order(row["source_order"]);
                recordTables.Add(id, name);
                string parentField = IsNull(row["parent_field"]) ? "" : row["parent_field"];
                var key = (name, row["parent_id"], parentField);
                if (!children.TryGetValue(key, out var list)) children[key] = list = new();
                list.Add(row);
            }
        }
        foreach (var entry in children)
        {
            var orders = new HashSet<int>();
            foreach (var row in entry.Value) if (!orders.Add(Order(row["source_order"]))) throw new InvalidOperationException($"CSV 저장 순서 중복: {entry.Key}");
            entry.Value.Sort((a, b) => Order(a["source_order"]).CompareTo(Order(b["source_order"])));
        }
        // All content, including currently unregistered sources, is structurally checked before publication.
        foreach (var source in sources) values.Add(source.Key, Build(templates[source.Value], null, null, source.Key));
        var orphan = records.Keys.FirstOrDefault(id => !visited.Contains(id));
        if (orphan != null) throw new InvalidOperationException($"부모에 연결되지 않은 CSV 행: {orphan}");
    }

    static int Order(string text)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 0)
            throw new InvalidOperationException($"CSV 순서는 0 이상의 정수여야 합니다: {text}");
        return n;
    }

    object BuildRecord(string id, string source)
    {
        if (!records.TryGetValue(id, out var row)) throw new InvalidOperationException($"필수 CSV 행 없음: {id}");
        if (row["dataset_id"] != source) throw new InvalidOperationException($"CSV 부모와 dataset_id 불일치: {id}");
        if (!active.Add(id)) throw new InvalidOperationException($"CSV 부모 연결 순환: {id}");
        try
        {
            var node = templates[row["template_id"]];
            var allowed = new HashSet<string> { "context", "dataset_id", "row_id", "parent_id", "parent_field", "source_order", "template_id", "value_type", node.Get("map_key") };
            void Collect(Node n) { if (n.Get("kind") == "cell") allowed.Add(n.Get("column")); foreach (var child in n.Fields) Collect(child); }
            Collect(node);
            foreach (var cell in row) if (!allowed.Contains(cell.Key) && !IsNull(cell.Value))
                throw new InvalidOperationException($"CSV 행 형식에 없는 필드: {id}/{cell.Key}");
            visited.Add(id);
            return Build(node, id, row, source);
        }
        finally { active.Remove(id); }
    }

    object Build(Node node, string parent, Dictionary<string, string> row, string source)
    {
        switch (node.Get("kind"))
        {
            case "record": return BuildRecord(node.Get("row_id"), source);
            case "empty_array": return new List<object>();
            case "object":
                var obj = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var field in node.Fields) obj.Add(field.Get("field"), Build(field, parent, row, source));
                return obj;
            case "cell":
                row.TryGetValue(node.Get("column"), out string raw);
                string kind = node.Get("type");
                if (kind == "NoneType")
                {
                    types.TryGetValue((recordTables[parent], node.Get("column")), out var declared);
                    kind = declared == "bool" ? "bool" : declared == "int" || declared == "float" || declared == "float|int" ? "float" : "str";
                }
                return Scalar(raw, kind);
            case "children":
                string root = node.Get("parent");
                var key = (node.Get("table"), root == "" ? parent : root, node.Get("slot"));
                children.TryGetValue(key, out var matches);
                matches ??= new();
                if (node.Get("container") == "list") return matches.Select(r => BuildRecord(r["row_id"], source)).ToList();
                var dict = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var match in matches)
                {
                    string mapKey = match[node.Get("key_column")];
                    if (IsNull(mapKey)) throw new InvalidOperationException($"CSV 사전 키 없음: {match["row_id"]}");
                    dict.Add(mapKey, BuildRecord(match["row_id"], source));
                }
                return dict;
            default: throw new InvalidOperationException($"알 수 없는 CSV 스키마 종류: {node.Get("kind")}");
        }
    }

    static bool IsNull(string raw) => string.IsNullOrEmpty(raw) || raw == @"\N";
    static object Scalar(string raw, string kind)
    {
        if (IsNull(raw)) return null;
        if (raw == @"\E") return "";
        if (kind == "str") return raw.StartsWith(@"\\", StringComparison.Ordinal) ? raw.Substring(1) : raw;
        if (kind == "bool") return bool.Parse(raw);
        if (kind == "int") return long.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
        if (kind == "float")
        {
            double value = double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidOperationException("CSV 숫자는 유한해야 합니다.");
            return value;
        }
        throw new InvalidOperationException($"지원하지 않는 CSV 값 타입: {kind}");
    }

    static object ConvertValue(object value, Type type, string path)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (value == null)
        {
            if (!type.IsValueType || nullable != null) return null;
            throw new InvalidOperationException($"null을 {type.Name}에 넣을 수 없습니다: {path}");
        }
        if (nullable != null) type = nullable;
        if (type == typeof(object)) return value;
        if (type == typeof(string))
        {
            // Some existing DTOs intentionally expose a numeric source as text (e.g. an open score bound).
            if (value is double number)
            {
                string formatted = number.ToString("R", CultureInfo.InvariantCulture);
                return formatted.IndexOfAny(new[] { '.', 'E', 'e' }) < 0 ? formatted + ".0" : formatted;
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        if (type.IsEnum)
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                if (string.Equals(field.Name, text, StringComparison.OrdinalIgnoreCase) || field.GetCustomAttribute<EnumMemberAttribute>()?.Value == text) return field.GetValue(null);
            throw new InvalidOperationException($"지원하지 않는 {type.Name}: {text} ({path})");
        }
        if (type.IsPrimitive || type == typeof(decimal))
        {
            if (value is double fractional && type != typeof(float) && type != typeof(double) && type != typeof(decimal)
                && fractional != Math.Truncate(fractional)) throw new InvalidOperationException($"정수 필드에 소수가 들어왔습니다: {path}");
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        if (type.IsArray)
        {
            var list = (IList)value;
            var element = type.GetElementType();
            var array = Array.CreateInstance(element, list.Count);
            for (int i = 0; i < list.Count; i++) array.SetValue(ConvertValue(list[i], element, path + $"[{i}]"), i);
            return array;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var result = (IDictionary)Activator.CreateInstance(type);
            Type itemType = type.GetGenericArguments()[1];
            foreach (var item in (Dictionary<string, object>)value) result.Add(item.Key, ConvertValue(item.Value, itemType, path + "." + item.Key));
            return result;
        }
        var data = (Dictionary<string, object>)value;
        object instance = Activator.CreateInstance(type);
        // Existing attributes supply column aliases only; no JSON reader/serializer is invoked.
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanWrite || property.GetIndexParameters().Length != 0 || property.IsDefined(typeof(JsonIgnoreAttribute))) continue;
            string key = property.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? property.Name;
            if (data.TryGetValue(key, out var cell)) property.SetValue(instance, ConvertValue(cell, property.PropertyType, path + "." + key));
        }
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (field.IsInitOnly || field.IsDefined(typeof(JsonIgnoreAttribute))) continue;
            string key = field.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? field.Name;
            if (data.TryGetValue(key, out var cell)) field.SetValue(instance, ConvertValue(cell, field.FieldType, path + "." + key));
        }
        return instance;
    }

    public static List<Dictionary<string, string>> ParseRows(string text, string file)
    {
        var matrix = ParseCsv(text, file);
        if (matrix.Count == 0) throw new InvalidOperationException($"빈 CSV 파일: {file}");
        var header = matrix[0];
        if (header.Any(string.IsNullOrWhiteSpace) || header.Distinct(StringComparer.Ordinal).Count() != header.Length) throw new InvalidOperationException($"CSV 헤더 누락 또는 중복: {file}");
        var result = new List<Dictionary<string, string>>();
        for (int i = 1; i < matrix.Count; i++)
        {
            if (matrix[i].All(string.IsNullOrEmpty)) continue;
            if (matrix[i].Length != header.Length) throw new InvalidOperationException($"CSV 열 수 불일치: {file}, record {i + 1}");
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int j = 0; j < header.Length; j++) row.Add(header[j], matrix[i][j]);
            result.Add(row);
        }
        return result;
    }

    /// <summary>RFC 4180: 쉼표·따옴표·CRLF/LF·셀 내부 줄바꿈·UTF-8 BOM을 보존한다.</summary>
    public static List<string[]> ParseCsv(string text, string file = "CSV")
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false, closed = false, started = false;
        int index = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
        for (; index < text.Length; index++)
        {
            char c = text[index];
            if (quoted)
            {
                if (c == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"') { cell.Append('"'); index++; }
                    else { quoted = false; closed = true; }
                }
                else cell.Append(c);
                continue;
            }
            if (c == ',' || c == '\r' || c == '\n')
            {
                row.Add(cell.ToString()); cell.Clear(); closed = false; started = false;
                if (c != ',')
                {
                    if (c == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                    rows.Add(row.ToArray()); row.Clear();
                }
                continue;
            }
            if (closed) throw new InvalidOperationException($"CSV 닫힌 따옴표 뒤 잘못된 문자: {file}, offset {index}");
            if (c == '"')
            {
                if (started) throw new InvalidOperationException($"CSV 셀 중간의 따옴표: {file}, offset {index}");
                quoted = true; started = true;
            }
            else { cell.Append(c); started = true; }
        }
        if (quoted) throw new InvalidOperationException($"CSV 따옴표 미종료: {file}");
        if (started || closed || cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }
}
