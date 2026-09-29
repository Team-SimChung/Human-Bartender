using UnityEngine;
using UnityEditor;
using System.IO;

public class ElectricNoiseGenerator
{
    [MenuItem("Tools/Generate Electric Noise Texture")]
    public static void GenerateNoise()
    {
        int width = 256;
        int height = 256;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        // 노이즈의 밀도 조절 (숫자가 클수록 오밀조밀해짐)
        float scale = 8.0f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float xCoord = (float)x / width * scale;
                float yCoord = (float)y / height * scale;

                // 유니티 기본 펄린 노이즈 생성
                float sample = Mathf.PerlinNoise(xCoord, yCoord);

                // 흑백 컬러로 픽셀 적용
                tex.SetPixel(x, y, new Color(sample, sample, sample, 1));
            }
        }
        tex.Apply();

        // PNG 파일로 저장
        byte[] bytes = tex.EncodeToPNG();
        string path = Application.dataPath + "/ElectricNoise_Example.png";
        File.WriteAllBytes(path, bytes);

        // 에디터 새로고침하여 파일 표시
        AssetDatabase.Refresh();

        Debug.Log("전기 효과용 노이즈 텍스처가 생성되었습니다! 위치: " + path);
    }
}