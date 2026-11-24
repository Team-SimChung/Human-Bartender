using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 얼음 포즈 시트(IMG_shake_ice_poses.png)의 색인. 웹 poses/frames.json을 직렬화 데이터로 옮긴 것이다.
///
/// 시트는 얼음을 크기별로 미리 72방향(5° 간격) 구워 둔 그림이다. 행마다 칸 크기와 한 줄 칸 수가 달라서
/// 일정한 격자로 자를 수 없다. 화면은 이 색인으로 현재 크기·각도에 맞는 칸 하나를 골라 회전 없이 찍는다
/// (원본 얼음을 돌리거나 줄이면 도트가 깨진다).
/// </summary>
[CreateAssetMenu(fileName = "ShakeV2IcePoses", menuName = "Data/Shake V2 Ice Poses")]
public sealed class ShakeV2IcePoseSheet : ScriptableObject
{
    /// <summary>크기 하나의 행. frames.json ice 배열의 [지름, 칸 크기, 행 시작 y, 한 줄 칸 수].</summary>
    [Serializable]
    public struct Row
    {
        [Tooltip("이 행 얼음의 지름(시트 픽셀).")]
        public int diameter;
        [Tooltip("칸 한 변의 크기(시트 픽셀).")]
        public int cell;
        [Tooltip("이 행이 시작하는 y(시트 위쪽 기준 픽셀).")]
        public int top;
        [Tooltip("한 줄에 놓인 칸 수. 72방향이 이 수만큼씩 줄을 바꾼다.")]
        public int columns;

        public Row(int diameter, int cell, int top, int columns)
        {
            this.diameter = diameter; this.cell = cell; this.top = top; this.columns = columns;
        }
    }

    [Tooltip("얼음 포즈 시트 텍스처(IMG_shake_ice_poses). Point 필터로 들인다.")]
    [SerializeField] Texture2D texture;
    [Tooltip("한 바퀴를 몇 방향으로 구웠는지. frames.json iceAngles.")]
    [SerializeField, Min(1)] int angles = 72;
    [Tooltip("크기별 행. 지름이 작은 것부터 2씩 커진다. frames.json ice.")]
    [SerializeField] Row[] rows = Array.Empty<Row>();
    [Tooltip("에디터에서 색인대로 잘라 둔 칸 스프라이트. 행 순서대로, 한 행 안에서는 방향 순서(행 × 방향 수 + 방향).")]
    [SerializeField] Sprite[] sprites = Array.Empty<Sprite>();

    public Texture2D Texture => texture;
    public int Angles => angles;
    public IReadOnlyList<Row> Rows => rows;
    public IReadOnlyList<Sprite> Sprites => sprites;

    /// <summary>지름(시트 픽셀)과 각도(rad)에 맞는 칸. 반환값은 시트 왼쪽 위 기준 픽셀 사각형이다.</summary>
    public RectInt Pick(float diameter, float angle) => Pick(rows, angles, diameter, angle);

    /// <summary>
    /// 웹 ShakerArt.drawIce와 같은 규칙으로 칸을 고른다.
    /// 행은 (지름 − 첫 행 지름) / 2를 반올림, 방향은 각도 × 방향 수 / 2π를 반올림해 한 바퀴로 감는다.
    /// </summary>
    public static RectInt Pick(IReadOnlyList<Row> rows, int angles, float diameter, float angle)
    {
        if (rows == null || rows.Count == 0 || angles <= 0) return new RectInt();
        int rowIndex = Mathf.Clamp(RoundHalfUp((diameter - rows[0].diameter) / 2f), 0, rows.Count - 1);
        Row row = rows[rowIndex];
        int columns = Mathf.Max(1, row.columns);
        int direction = ((RoundHalfUp(angle * angles / (2f * Mathf.PI)) % angles) + angles) % angles;
        return new RectInt(direction % columns * row.cell, row.top + direction / columns * row.cell, row.cell, row.cell);
    }

    /// <summary>지름(원본 px)과 각도(rad)에 맞는 칸 스프라이트. 칸이 잘려 있지 않으면 null.</summary>
    public Sprite SpriteFor(float diameter, float angle)
    {
        int index = SpriteIndex(rows, angles, diameter, angle);
        return index >= 0 && index < sprites.Length ? sprites[index] : null;
    }

    /// <summary>Pick과 같은 규칙의 칸 번호(행 × 방향 수 + 방향).</summary>
    public static int SpriteIndex(IReadOnlyList<Row> rows, int angles, float diameter, float angle)
    {
        if (rows == null || rows.Count == 0 || angles <= 0) return -1;
        int rowIndex = Mathf.Clamp(RoundHalfUp((diameter - rows[0].diameter) / 2f), 0, rows.Count - 1);
        int direction = ((RoundHalfUp(angle * angles / (2f * Mathf.PI)) % angles) + angles) % angles;
        return rowIndex * angles + direction;
    }

    /// <summary>잘라 둔 칸 스프라이트의 이름. 셋업 도구와 테스트가 같이 쓴다.</summary>
    public static string SpriteName(string sheet, int diameter, int direction) => $"{sheet}_{diameter:00}_{direction:00}";

    /// <summary>시트 왼쪽 위 기준 픽셀 사각형을 RawImage.uvRect(왼쪽 아래 기준 0~1)로 바꾼다.</summary>
    public Rect UvRect(RectInt cell)
    {
        if (texture == null) return new Rect(0f, 0f, 1f, 1f);
        float width = texture.width, height = texture.height;
        return new Rect(cell.x / width, (height - cell.y - cell.height) / height, cell.width / width, cell.height / height);
    }

    // JavaScript Math.round와 같은 반올림(.5는 위로).
    static int RoundHalfUp(float value) => Mathf.FloorToInt(value + .5f);
}
