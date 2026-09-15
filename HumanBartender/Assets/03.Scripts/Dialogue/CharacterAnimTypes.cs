using System;

/// <summary>캐릭터 표정 애니메이션의 파트(부위)를 구분하는 열거형.</summary>
public enum EAnimationPart
{
    Eyes = 1,
    Eyeblows = 2,
    Body = 3,
    Upper_Face = 4,
    Lower_Face = 5,
    Extra = 6,
    Etc = 7,
    Sprite = 10,
}

/// <summary>애니메이션 파트의 재생 반복 모드를 나타내는 열거형.</summary>
[Serializable]
public enum EAnimLoopMode
{
    Always,
    Always_OnDialogue,
    Special_OnDialogue,
    Once,
    None
}

/// <summary>
/// 파트 하나에 붙일 클립과 그 반복 방식.
///
/// json(NewExpressionDataSO)에서 조회할 때 만들어져 리그(CharacterPart)로 넘어간다. 리그가 쓰는
/// 말이지 파일에 적힌 모양이 아니라서 역직렬화 속성이 없다 — json 쪽 모양은 NewExpressionPartClip이다.
/// </summary>
public class PartAnimData
{
    public string Clip { get; set; }
    public EAnimLoopMode Loop { get; set; }
}
