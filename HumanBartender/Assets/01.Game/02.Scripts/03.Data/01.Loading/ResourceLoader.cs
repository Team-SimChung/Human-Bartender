using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Unity Addressables를 통해 에셋을 비동기 로드하는 정적 유틸리티.
/// TryLoadAsync: 주소 존재 확인 후 로드, 실패 시 핸들 자동 릴리즈.
/// ExistsInAddressables: 키 존재 여부만 확인.
/// ReleaseHandle: 핸들 안전 릴리즈.
/// </summary>
public static class ResourceLoader
{
    static IReadOnlyDictionary<string, string> bindings = new Dictionary<string, string>();

    public static async UniTask<IReadOnlyDictionary<string, string>> ReadBindingsAsync(CancellationToken token)
    {
        var text = await CsvDataReader.ReadStreamingTextAsync("unity/resource_bindings.csv", token);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in CsvContentCatalog.ParseRows(text, "unity/resource_bindings.csv"))
        {
            if (string.IsNullOrWhiteSpace(row["source_key"]) || string.IsNullOrWhiteSpace(row["unity_key"]))
                throw new InvalidOperationException("Resource binding keys cannot be empty.");
            string type = row["asset_type"];
            if (type != "Sprite" && type != "AnimationClip" && type != "AudioClip" && type != "TimelineAsset" && type != "GameObject")
                throw new InvalidOperationException("Unsupported resource binding type: " + type);
            result.Add(type + "|" + row["source_key"], row["unity_key"]);
        }
        foreach (var binding in result)
        {
            string type = binding.Key.Substring(0, binding.Key.IndexOf('|'));
            if (result.ContainsKey(type + "|" + binding.Value))
                throw new InvalidOperationException("Resource bindings must point directly to a Unity key: " + binding.Key);
        }
        return result;
    }

    public static void PublishBindings(IReadOnlyDictionary<string, string> values) => bindings = values;
    public static string ResolveKey(string key, Type type = null) => key != null && type != null && bindings.TryGetValue(type.Name + "|" + key, out var value) ? value : key;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetBindings() => bindings = new Dictionary<string, string>();
    /// <summary>
    /// 로드 성공 시 handle 반환, 실패/예외 시 null 반환.
    /// 실패한 handle은 내부에서 즉시 릴리즈.
    /// TODO : ExistsInAddressables 함수는 순수 파일 존재 여부만 검사하기에 따로 또 처리해야함.
    /// </summary>
    public async static UniTask<AsyncOperationHandle<T>?> TryLoadAsync<T>(
        string address,
        CancellationToken token)
    {
        address = ResolveKey(address, typeof(T));
        //해당 주소 존재 여부 확인
        bool exists = await ExistsInAddressables(address, token);
        if (!exists)
        {
            Debug.LogWarning($"Addressable key not found: {address}");
            return null;
        }

        var handle = Addressables.LoadAssetAsync<T>(address);
        
        try
        {
            await handle.ToUniTask(cancellationToken:token);

            if (handle.Status == AsyncOperationStatus.Succeeded)
                return handle;

            // 로드 실패 — 핸들 즉시 릴리즈
            if (handle.IsValid()) Addressables.Release(handle);
            return null;
        }
        catch (OperationCanceledException)
        {
            if (handle.IsValid()) Addressables.Release(handle);
            throw;
        }
        catch (Exception e)
        {
            Logger.Log(e.Message);
            if (handle.IsValid()) Addressables.Release(handle);
            return null;
        }
    }

    public async static UniTask<bool> ExistsInAddressables(string key, CancellationToken token)
    {
        key = ResolveKey(key);
        var handle = Addressables.LoadResourceLocationsAsync(key);

        try
        {
            var locations = await handle.ToUniTask(cancellationToken:token);
            bool exists = locations != null && locations.Count > 0;
            return exists;
        }
        finally
        {
            if(handle.IsValid()) Addressables.Release(handle);
        }
    }

    public static void ReleaseHandle<T>(ref AsyncOperationHandle<T>? handle)
    {
        if (handle.HasValue && handle.Value.IsValid())
        {
            Addressables.Release(handle.Value);
            handle = null;
        }
    }
}
