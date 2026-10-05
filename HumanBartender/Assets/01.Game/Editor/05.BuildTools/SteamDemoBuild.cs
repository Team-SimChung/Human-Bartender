using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Editor-only entry point. Steam upload and runtime services are separate.</summary>
public static class SteamDemoBuild
{
    public static void BuildWindows()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run Tools/Steam/Build-Windows.ps1 to create a fresh release folder.");
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            throw new InvalidOperationException("Start Unity with -buildTarget Win64.");

        string releaseRoot = Path.GetFullPath(RequiredArgument("-releaseOutput"));
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string buildsRoot = Path.Combine(projectRoot, "Builds") + Path.DirectorySeparatorChar;
        if (!releaseRoot.StartsWith(buildsRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Release output must be under this project's Builds directory.");

        string content = Path.Combine(releaseRoot, "content");
        if (Directory.Exists(content) || File.Exists(content))
            throw new InvalidOperationException("The content path must not exist. Choose a new release folder.");
        string product = PlayerSettings.productName;
        if (string.IsNullOrWhiteSpace(product) || product.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("Product name cannot be used as an executable filename.");
        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length == 0 || scenes.Any(scene => !File.Exists(scene)))
            throw new InvalidOperationException("Enabled build scenes are missing or empty.");

        var addressables = AddressableAssetSettingsDefaultObject.Settings;
        if (addressables == null)
            throw new InvalidOperationException("Addressables settings are missing.");
        var previousPolicy = addressables.BuildAddressablesWithPlayerBuild;
        BuildReport report;
        try
        {
            // Let the installed Addressables build processor build and include content once.
            addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(content, product + ".exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
        }
        finally
        {
            addressables.BuildAddressablesWithPlayerBuild = previousPolicy;
            EditorUtility.SetDirty(addressables);
            AssetDatabase.SaveAssets();
        }
        if (report == null || report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Windows build failed. Inspect the Unity build log.");

        // Unity explicitly marks these directories as unsuitable for distribution.
        foreach (string suffix in new[] { "_BurstDebugInformation_DoNotShip", "_BackUpThisFolder_ButDontShipItWithYourGame" })
        {
            string debugDirectory = Path.Combine(content, product + suffix);
            if (Directory.Exists(debugDirectory))
                Directory.Move(debugDirectory, Path.Combine(releaseRoot, product + suffix));
        }

        string data = Path.Combine(content, product + "_Data");
        string copiedIdeSettings = Path.GetFullPath(Path.Combine(data, "StreamingAssets", "json", ".idea"));
        if (Directory.Exists(copiedIdeSettings))
        {
            if (!copiedIdeSettings.StartsWith(content + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unexpected copied IDE metadata path.");
            Directory.Move(copiedIdeSettings, Path.Combine(releaseRoot, "ExcludedIDESettings"));
        }
        RequireFile(Path.Combine(content, product + ".exe"));
        RequireFile(Path.Combine(content, "UnityPlayer.dll"));
        RequireFile(Path.Combine(data, "globalgamemanagers"));
        RequireFile(Path.Combine(data, "StreamingAssets", "aa", "settings.json"));
        string csv = Path.Combine(data, "StreamingAssets", "csv");
        string aa = Path.Combine(data, "StreamingAssets", "aa");
        if (!Directory.Exists(csv) || !Directory.EnumerateFiles(csv, "*.csv", SearchOption.AllDirectories).Any())
            throw new InvalidOperationException("CSV content is missing from the player.");
        if (!Directory.EnumerateFiles(aa, "*.bundle", SearchOption.AllDirectories).Any())
            throw new InvalidOperationException("Local Addressables bundles are missing from the player.");
        if (Directory.EnumerateFiles(content, "steam_appid.txt", SearchOption.AllDirectories).Any())
            throw new InvalidOperationException("Development-only steam_appid.txt must not be distributed.");

        var info = new BuildInfo
        {
            createdUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion,
            companyName = PlayerSettings.companyName,
            productName = product,
            version = PlayerSettings.bundleVersion,
            sourceCommit = RequiredArgument("-sourceCommit"),
            target = report.summary.platform.ToString(),
            scriptingBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone).ToString(),
            options = report.summary.options.ToString(),
            addressablesPolicy = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer.ToString(),
            executable = product + ".exe",
            scenes = scenes,
            buildGuid = report.summary.guid.ToString(),
            totalBytes = Directory.EnumerateFiles(content, "*", SearchOption.AllDirectories)
                .Sum(path => new FileInfo(path).Length).ToString(),
            warnings = report.summary.totalWarnings,
            errors = report.summary.totalErrors,
        };
        File.WriteAllText(Path.Combine(releaseRoot, "build-info.json"), JsonUtility.ToJson(info, true));
        Debug.Log("[SteamDemoBuild] Build and artifact checks succeeded: " + content);
    }

    static string RequiredArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, name);
        if (index < 0 || index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
            throw new ArgumentException("Missing command-line argument: " + name);
        return arguments[index + 1];
    }

    static void RequireFile(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            throw new InvalidOperationException("Required player file is missing or empty: " + path);
    }

    [Serializable]
    sealed class BuildInfo
    {
        public string createdUtc, unityVersion, companyName, productName, version, sourceCommit;
        public string target, scriptingBackend, options, addressablesPolicy, executable, buildGuid, totalBytes;
        public string[] scenes;
        public int warnings, errors;
    }
}
