using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>이번에 검토한 제조 CSV 네 파일만 게시하는 임시 도구. 적용 확인 후 삭제한다.</summary>
public static class WebCraftingImportOnce
{
    [MenuItem("Tools/Human Bartender/Crafting/Apply Validated Web Crafting CSV")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[WebCraftingImport] Play 모드를 끄고 실행해 주세요."); return; }
        string project = Path.GetDirectoryName(Application.dataPath);
        string stage = Path.Combine(project, "Temp/WebCraftingSync");
        var plan = JObject.Parse(File.ReadAllText(Path.Combine(stage, "plan.json")));
        string target = Path.Combine(Application.streamingAssetsPath, "csv");
        var selected = ((JObject)plan["before"]).Properties().Select(p => p.Name).ToArray();
        foreach (var name in Directory.GetFiles(Path.Combine(stage, "csv"), "*.csv", SearchOption.AllDirectories))
        {
            string relative = name.Substring(Path.Combine(stage, "csv").Length + 1).Replace('\\', '/');
            if (!selected.Contains(relative) && Hash(name) != Hash(Path.Combine(target, relative)))
                throw new InvalidOperationException("[WebCraftingImport] 스테이징 이후 CSV가 변경됐습니다: " + relative);
        }
        foreach (string name in selected)
        {
            if (Hash(Path.Combine(target, name)) != (string)plan["before"][name] ||
                Hash(Path.Combine(stage, "csv", name)) != (string)plan["after"][name])
                throw new InvalidOperationException("[WebCraftingImport] 검토한 CSV 해시와 다릅니다: " + name);
        }
        string manifest = Path.Combine(project, "ContentAuthoring/web_content_source.json");
        string idMap = Path.Combine(project, "ContentAuthoring/web_id_map.json");
        if (Hash(manifest) != (string)plan["manifestBefore"] || Hash(idMap) != (string)plan["idMapBefore"])
            throw new InvalidOperationException("[WebCraftingImport] 반입 기록이 변경됐습니다.");
        CsvContentValidator.ValidateCatalog(CsvDataReader.LoadDirectory(Path.Combine(stage, "csv")));
        var writes = selected.ToDictionary(n => Path.Combine(target, n), n => File.ReadAllBytes(Path.Combine(stage, "csv", n)));
        writes[manifest] = File.ReadAllBytes(Path.Combine(stage, "manifest.json"));
        writes[idMap] = File.ReadAllBytes(Path.Combine(stage, "id-map.json"));
        var backups = writes.Keys.ToDictionary(p => p, File.ReadAllBytes);
        try
        {
            foreach (var entry in writes) File.WriteAllBytes(entry.Key, entry.Value);
            CsvContentValidator.ValidateCatalog(CsvDataReader.LoadDirectory(target));
        }
        catch { foreach (var entry in backups) File.WriteAllBytes(entry.Key, entry.Value); throw; }
        AssetDatabase.Refresh();
        Debug.Log("[WebCraftingImport] " + (string)plan["commit"] + " 제조 CSV 4개 검증·적용 완료. 대사 CSV는 변경하지 않았습니다.");
    }
    static string Hash(string path)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }
}
