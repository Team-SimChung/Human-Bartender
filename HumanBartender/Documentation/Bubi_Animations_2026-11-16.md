# Bubi 월드 애니메이션

리소스 `01.ArtResources/01.Characters/02.Outside/04.Bubi/`의 `02.Walk`, `03.Box`, `04.Sleep`을 사용한다. 원본 이미지와 Sprite Editor의 슬라이스·피벗은 유지한다.

애니메이션 위치: `Assets/01.Game/03.Content/07.Animations/01.Characters/04.Bubi/`

| 클립 (`01.Clips/` 기준) | 프레임 수 | 반복 |
| --- | ---: | --- |
| `03.Walk/Bubi_Walk.anim` | 6 | 켬 |
| `04.Box/Bubi_Box_Idle.anim` | 8 | 켬 |
| `04.Box/Bubi_Box_Eyes.anim` | 22 | 끔 |
| `04.Box/Bubi_Box_Jump.anim` | 15 | 끔 |
| `04.Box/Bubi_Box_Meow.anim` | 8 | 끔 |
| `04.Box/Bubi_Box_Shake.anim` | 8 | 끔 |
| `05.Sleep/Bubi_Sleep.anim` | 12 | 켬 |

처음 재생 속도는 모두 12 Samples/초다. Animation 창에서 해당 클립의 Samples를 조절한다. 컨트롤러의 상태별 Speed로 배속을 조절할 수도 있다.

## Animator Controller

`02.Controllers/`에 두 컨트롤러를 둔다.

- `Bubi_Field.controller`: 기본 상태 `Sleep`, 상태 `Sleep`, `Walk`.
- `Bubi_Box.controller`: 기본 상태 `Idle`, 상태 `Idle`, `Eyes`, `Jump`, `Meow`, `Shake`.

각 클립은 **Animator와 같은 GameObject의 SpriteRenderer**를 재생한다. Transform의 위치·회전·크기는 애니메이션으로 덮어쓰지 않는다. 같은 컨트롤러를 여러 씬의 Animator에서 공유할 수 있으며 재생 상태는 각 Animator에 따로 유지된다.

상태는 `animator.Play("Walk", 0, 0f)` 또는 `animator.Play("Jump", 0, 0f)`처럼 호출한다. 자동 전이는 없다. 반복하지 않는 클립은 한 번 재생한 후 마지막 프레임을 유지한다. 특히 `Jump`가 끝나자마자 `Idle`로 복귀해 튀어나온 부비가 상자 안으로 돌아가는 현상을 방지한다. 복귀할 때는 호출하는 쪽에서 상태를 지정한다.

씬이 달라도 렌더러 구조와 상태 전환 규칙이 같으면 컨트롤러를 공유한다. 상자 연출과 걷기·수면처럼 필요한 상태가 다르면 위와 같이 역할별로 나눈다. 기존 바 대화용 파츠 애니메이션과 `NpcInteraction`의 웹 연출 컨트롤러는 별도 용도로 유지한다. 이번 작업은 새 클립·컨트롤러 정리이며 기존 씬의 Animator 연결은 변경하지 않는다.
