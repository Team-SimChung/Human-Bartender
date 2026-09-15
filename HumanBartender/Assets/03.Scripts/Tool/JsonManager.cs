using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Networking;

/// <summary>
/// JSON 데이터를 저장/로드하는 제네릭 정적 유틸리티.
/// StreamingAssets 동기 로드, PersistentDataPath 저장/로드, UnityWebRequest 비동기 로드,
/// Addressables 비동기 로드를 지원한다.
/// </summary>
public static class JsonManager<T>
{
    public static bool SaveGame(T data, string saveFileName)
    {
        string filePath = Path.Combine(Application.persistentDataPath, saveFileName);
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(filePath, json);

        Debug.Log($"저장 완료! 경로: {filePath}");

        return true;
    }
    public static T LoadGameData_PersistentDataPath(string fileName)
    {
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            T data = JsonConvert.DeserializeObject<T>(json);

            Debug.Log("데이터 로드 성공!");
            return data;
        }
        else
        {
            Debug.LogWarning("저장된 파일이 없습니다. 기본 데이터를 생성합니다.");
            return default(T);
        }
    }
    public static T LoadGameData_StreamingAssets(string fileName = "")
    {
        // 1. 경로 설정 (StreamingAssets 혹은 PersistentDataPath)
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);

        if (File.Exists(filePath))
        {
            string jsonText = File.ReadAllText(filePath);
            T data = JsonConvert.DeserializeObject<T>(jsonText);
            return data;
        }
        else
        {
            Debug.LogError("파일을 찾을 수 없습니다: " + filePath);
            return default(T);
        }
    }

    /// <summary>
    /// StreamingAssets의 json 하나를 비동기로 읽는다.
    ///
    /// 실패하면 예외를 던진다. 데이터 하나가 빠진 채로 진행하면 그 데이터를 쓰는 곳에서 한참 뒤에
    /// 엉뚱한 모습으로 터지므로, 읽지 못한 자리에서 멈추는 편이 낫다.
    ///
    /// 대신 어느 파일인지를 예외에 담는다. UnityWebRequest도 Newtonsoft도 원문 메시지에 파일 이름을
    /// 남기지 않아서 — "HTTP/1.1 404 Not Found", "Requested value 'system' was not found" —
    /// 그대로 두면 무엇이 잘못됐는지는 알아도 어디가 잘못됐는지는 찾아다녀야 한다.
    /// </summary>
    public static async UniTask<T> LoadAsync<T>(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        using var req = UnityWebRequest.Get("file://" + path);

        try
        {
            // UniTask의 awaiter는 실패를 예외로 알린다. req.result를 따로 보지 않는 이유다.
            await req.SendWebRequest();
        }
        catch (System.Exception e)
        {
            throw new System.InvalidOperationException(
                $"데이터 파일을 읽지 못했습니다: {fileName} ({path}) — {e.Message}", e);
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(req.downloadHandler.text);
        }
        catch (System.Exception e)
        {
            throw new System.InvalidOperationException(
                $"데이터 파일의 형식이 맞지 않습니다: {fileName} — {e.Message}", e);
        }
    }

    public static async UniTask<T> LoadDataAsync<T>(string addressableKey)
    {
        var handle = Addressables.LoadAssetAsync<TextAsset>(addressableKey);
        TextAsset textAsset = await handle.ToUniTask();

        if (textAsset == null)
        {
            Debug.LogError($"어드레서블 로드 실패: {addressableKey}");
            return default;
        }

        T data = JsonConvert.DeserializeObject<T>(textAsset.text);

        Addressables.Release(handle);

        return data;
    }
}