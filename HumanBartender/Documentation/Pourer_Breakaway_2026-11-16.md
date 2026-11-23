# 따르기 푸어러 분리

## 동작

- `NewPourGame.prefab`을 공유하는 따르기와 필업에 적용한다.
- 푸어러가 있는 병을 잡고 160도 이상 기울이면 한 번 분리된다. 최대 기울기는 170도이다.
- 기존 `03.pour/pourer.png`가 병에서 날아가며 `펑!`(영문 `POP!`)을 표시한다.
- 따르기 전용 병·캔 21종은 `03.pour/03.Ingredients/*_off.png`를 명시적으로 연결하며, 투명 여백을 제외한 입구 상단 중앙을 피벗으로 사용한다. 레시피·해금 화면의 닫힌 병 그림은 별도 연결을 유지한다.
- 푸어러의 검은 띠 바로 아래에 피벗을 두고 병 입구와 맞춘다. 회색 삽입부는 입구 아래로 넣고 병 그림 뒤에서 그린다. 액체 → 부착 푸어러 → 병 → 분리 푸어러 순서로 겹친다.
- 액체는 푸어러가 붙어 있으면 그 끝에서, 분리된 뒤에는 병 스프라이트의 실제 입구에서 나온다. 부착부터 분리까지 병의 위치·배율을 유지하며, 날아가는 푸어러를 출수구가 따라가지 않는다. 다음 기믹에서 분리 상태를 초기화한다.
- 분리 후 실제 SPH 입자 방출량이 95/42배(약 2.26배)가 된다. 병을 세워도 유지되며, 다음 기믹의 새 병에서는 초기화된다.
- 분리 후 분사 속도도 같은 배율로 늘려, 유량 증가로 입자 줄이 압축되어 양옆으로 갈라지는 현상을 방지한다. 독립 실행의 방출 간격에는 배율을 중복 적용하지 않는다.
- 푸어러가 붙어 있을 때 유입 물줄기 입자의 가로 렌더 두께를 20%로 줄인다(프리팹에서 조절돼 있던 25%보다 추가 축소). 잔 내부에 진입했어도 아직 수량으로 집계되지 않은 입자는 가는 두께를 유지한다. Inspector에서 10~100% 범위로 조절할 수 있다. 입자의 축 방향 길이, 실제 방출량·물리와 실제로 담긴 액체 크기는 유지하며, 분리 후 원래 두께로 돌아온다.
- 잔에 도착한 입자만 투입량으로 기록한다. 입자당 ml와 목표 단위 환산에는 분리 배율을 다시 곱하지 않는다.
- 현재량 표시는 일반 잔 280ml, 바이젠잔 420ml에서 멈춘다. oz 화면은 이 상한을 환산한다. 실제 투입량·목표 달성 판정·최종 결과 기록은 실측값을 사용한다.
- 맥주(실제 재료 ID `beer`)는 기본 출수량의 1.5배를 방출한다. 분사 속도도 같은 비율로 높여 입자 간격을 유지하며, 병 이미지 테스트용 ID는 배율에 영향을 주지 않는다.
- 액체보다 병·푸어러를 앞에 그린다. 잔과 배경의 기존 순서는 유지하며, 원본 프리팹의 병 스프라이트와 대체 실루엣, 두 푸어러에 정렬을 저장한다.
- 맥주, 탄산수, 콜라, 크랜베리 주스, 우유, 오렌지 주스, 레드 와인, 샴페인은 푸어러를 쓰지 않는다.
- 기울기 HUD의 빨간 구간은 푸어러 분리 각도를 표시하며, 분리 후 사라진다.

각도·유량 비율·제외 재료·안내 문구는 [웹빌드 소스](https://github.com/junseo874/junseo874.github.io/blob/main/bar-playtest/src/features/crafting/pour-fluid.js)와 [표시 코드](https://github.com/junseo874/junseo874.github.io/blob/main/bar-playtest/src/features/crafting/pour-view.js)를 참고했다. 기존 Unity의 기울기별 유량 곡선과 입자 물리는 유지한다.

## 편집 위치

- 원본: `Assets/01.Game/03.Content/08.Prefabs/01.Crafting/02.Minigames/03.Pour/NewPourGame.prefab`
- 루트의 `PourerBreakaway`: 분리 각도, 유량 배율, 제외 재료, 날아가는 속도·중력·회전·시간, 문구·표시 시간.
- `PourerBreakaway > Attached Stream > Attached Stream Width Scale`: 분리 전 유입 물줄기 입자의 가로 두께 배율.
- `PourManager > Volume Readout Limits`: 일반 잔·바이젠잔의 현재량 표시 상한. 잔의 물리 용량 설정과 별도로 조절한다.
- `PourManager > Pour Settings > Beer Flow Multiplier`: 맥주 출수량 배율.
- `Breakaway Sound (Optional) > Detach Clip`: 나중에 추가할 분리 효과음 슬롯. 현재는 비어 있다. 클립을 넣으면 분리 순간 한 번 재생하며, `Detach Gain`으로 음량을 조절한다. 저장된 전용 AudioSource가 공용 효과음 믹서를 사용하고, 일시정지·포커스 상실·기믹 종료 시 소리를 끊는다.
- `Bottle/AttachedPourer`: 병에 붙어 있는 스프라이트 크기. 시작할 때 윗끝을 액체 출구에 맞추고, 병 그림은 푸어러 길이만큼 아래로 이동한다. 분리 시 병 그림은 복원된다.
- `Pour Root/DetachedPourer`: 분리된 푸어러. 병과 독립된 좌표에서 이동한다.
- 기존 HUD의 `Panel/PourerPopRoot/PourerPopText`: 글자 위치, 폰트, 크기, 색.
- 연결 도구: `Tools > Crafting > Connect Pourer Breakaway`. 이미 연결된 프리팹은 수정하지 않는다.

UI와 스프라이트는 프리팹에 미리 저장한다. 실행 중 생성·삭제·부모 변경은 하지 않는다. 텍스트 연출은 DOTween, 분리된 스프라이트 이동은 프레임 시간에 따른 포물선으로 처리하며 일시정지 시 멈춘다.

## 검증

입자 두께 후속 변경 전인 2026-11-16, Unity 종료를 확인한 뒤 `PourerBreakawayTests`, `PourAccountingTests`, `PourHudSceneTests`, `PourTargetHeightTests`의 EditMode 테스트 58개를 실행해 모두 통과했다(실패·생략 0). 결과는 `Logs/PourBeerFlow/results.xml`에 기록했다.

실제 프리팹의 리소스·HUD·선택 효과음·병의 앞뒤 정렬, 분리 조건·초기화, 실제 입자 방출량과 밀도, 맥주 1.5배 적용 및 다른 재료 보존, 일반 잔·바이젠잔 표시 상한과 실측 판정 유지, 목표 높이와 기존 HUD 동작을 검사했다.

효과음 클립은 비어 있으며 전용 출력과 공용 효과음 믹서 연결은 원본 프리팹에 저장돼 있다. 일회성 연결 스크립트와 메타 파일은 제거됐다.

입자 두께 후속 변경은 C# 런타임·에디터·테스트 코드 컴파일을 통과했다. 잔 내부에 들어왔다는 이유만으로 두께를 복원하던 조건을 수정해, `insideContainer && volumeReceived`인 입자만 원래 크기로 그린다. 입구가 잔과 가까워도 아직 낙하 중인 유입 물줄기에는 두께 설정이 적용된다. `PourTargetHeightTests.StreamWidthControlNarrowsAirborneParticlesWithoutChangingLengthOrContainedLiquid`는 GPU 필드를 읽어 유입 입자의 가로 두께 감소, 잔 내부 낙하 중 두께 유지, 축 방향 길이 유지와 실제 담긴 액체 크기 유지를 검사한다. 열린 에디터의 Test Runner에서 새 GPU 검사와 `PourerBreakawayTests`를 실행해야 한다.

Game 창에서의 수동 시각 확인은 수행하지 않았다.
