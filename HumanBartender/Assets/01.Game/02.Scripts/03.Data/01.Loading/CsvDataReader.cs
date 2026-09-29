using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public static class CsvDataReader
{
    public const string Folder = "csv";
    static readonly string[] Metadata = { "_Columns.csv", "_Sources.csv", "_Schemas.csv" };
    static readonly Dictionary<string, string> MetadataPaths = new()
    {
        ["_Tables.csv"] = "system/table_index.csv",
        ["_Columns.csv"] = "system/columns.csv",
        ["_Sources.csv"] = "system/data_sets.csv",
        ["_Schemas.csv"] = "system/row_formats.csv",
    };

    public static async UniTask<CsvContentCatalog> LoadAsync(CancellationToken token = default)
    {
        string index = await ReadTextAsync(MetadataPaths["_Tables.csv"], token);
        var tableRows = CsvContentCatalog.ParseRows(index, "table_index.csv");
        var names = tableRows.Select(t => t["table"] + ".csv").Concat(Metadata).ToArray();
        var paths = tableRows.Select(t => ValidPath(t["path"])).Concat(Metadata.Select(m => MetadataPaths[m])).ToArray();
        var text = await UniTask.WhenAll(paths.Select(path => ReadTextAsync(path, token)));
        token.ThrowIfCancellationRequested();
        var files = new Dictionary<string, string>(StringComparer.Ordinal) { ["_Tables.csv"] = index };
        for (int i = 0; i < names.Length; i++) files.Add(names[i], text[i]);
        var catalog = CsvContentCatalog.Parse(files);
        token.ThrowIfCancellationRequested();
        return catalog;
    }

    public static CsvContentCatalog LoadDirectory(string directory)
    {
        string index = File.ReadAllText(Path.Combine(directory, MetadataPaths["_Tables.csv"]));
        var files = new Dictionary<string, string>(StringComparer.Ordinal) { ["_Tables.csv"] = index };
        foreach (var table in CsvContentCatalog.ParseRows(index, "table_index.csv"))
            files.Add(table["table"] + ".csv", File.ReadAllText(Path.Combine(directory, ValidPath(table["path"]))));
        foreach (string name in Metadata) files.Add(name, File.ReadAllText(Path.Combine(directory, MetadataPaths[name])));
        return CsvContentCatalog.Parse(files);
    }

    static string ValidPath(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".csv", StringComparison.Ordinal) || path.StartsWith("/", StringComparison.Ordinal)
            || path.Contains("..") || path.Contains(":") || path.Contains("\\") || path.Contains("//"))
            throw new InvalidOperationException($"잘못된 CSV 상대 경로: {path}");
        return path;
    }

    static async UniTask<string> ReadTextAsync(string file, CancellationToken token)
    {
        string path = Path.Combine(Application.streamingAssetsPath, Folder, file);
        string url = path.Contains("://") || path.StartsWith("jar:", StringComparison.Ordinal) ? path : new Uri(path).AbsoluteUri;
        using var request = UnityWebRequest.Get(url);
        try
        {
            await request.SendWebRequest().ToUniTask(cancellationToken: token);
            return request.downloadHandler.text;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e) { throw new InvalidOperationException($"CSV를 읽지 못했습니다: {file}: {e.Message}", e); }
    }
}
