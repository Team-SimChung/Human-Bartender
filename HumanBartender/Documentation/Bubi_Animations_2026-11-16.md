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

씬이 달라도 렌더러 구조와 상태 전환 규칙이 같으면 컨트롤러를 공유한다. 상자 연출과 걷기·수면처럼 필요한 상태가 다르면 위와 같이 역할별로 나눈다. 기존 바 대화용 파츠 애니메이션은 별도 용도로 유지한다.

## Story actor 연결 (2026-11-17)

Home·OutSide의 `Story actor · boobi`는 **부모의 Animator가 자식 `Sprite`의 SpriteRenderer를 제어**한다. 위의 루트 바인딩 클립을 직접 넣으면 자식 그림이 재생되지 않으므로 `01.Clips/06.StoryActor/`에 `Sprite` 경로용 클립 7개를 별도로 둔다. 원본 클립의 프레임·시간·반복 설정은 그대로 유지한다.

두 씬에서 이미 사용하던 `NpcInteraction/Home_neighborhood-homeCat_boobi/Actor.controller`와 `NpcInteraction/OutSide_neighborhood-reveal_boobi/Actor.controller`에 연결한다.

- `Idle`, `Sleep`: 새 수면 클립.
- `Walk`: 새 걷기 클립. 기존 `NpcActor`가 보내는 `Speed` 값으로 `Idle`과 즉시 전환한다. 전환 임계값은 컨트롤러에서 조절한다.
- `BoxIdle`, `Eyes`, `Jump`, `Meow`, `Shake`: 각각 새 상자 대기·눈·점프·울음·흔들기 클립. 수동 호출하며 종료 후 자동 전환하지 않는다.
- 기존 `Feed`, `Pet`, `RevealEyes`, `PulseFeed`, `PulsePet`, `PulseReveal`은 기존 스토리 호출과 연출을 유지한다. `Meow`의 실제 이벤트 재생 시간은 기존 `NpcActor.PlayAnimationAsync`가 맞춘다.

자식 `Sprite`에 추가했던 `Bubi_Field` Animator는 제거하고 부모의 기존 Animator 하나로 통합한다. 위치·크기·그리기 순서와 사용자가 수정한 원본 Sprite 슬라이스는 유지한다.

복구·재연결 메뉴는 `Tools > Animation > Connect Bubi Story Actors`다. 에디터 코드 `BubiStoryAnimationSetup`은 이미 생성한 클립과 수동 교체한 다른 모션을 보존하고, 두 번 실행해도 상태나 전이를 중복 추가하지 않는다. 기존 웹 NPC 셋업에서도 이 연결을 마지막에 적용한다.
