# 사운드 문서 적용 기록

기준: [Notion 사운드](https://app.notion.com/p/3f91612298dc80c09db5e49475394b19)

## 적용 위치

| 위치 | 적용한 동작 |
|---|---|
| `SceneAudioController` | Home는 Bar 02 반복, OutSide는 거리 전용 곡 반복·0.6배, Play는 Bar 01→02→03 순환. 실제 씬 공개 시점에 음악 그룹 변경 |
| `SoundManager` | 같은 그룹 재호출은 재시작하지 않음. 기존 곡 페이드아웃→곡 교체→페이드인. 교체 전 원래 그룹으로 돌아오면 재생 위치 유지 |
| `SceneAudioController` | 거리 도시 환경음 0.55, 비 레이어 0.55×0.85. 비는 0·1일차 퇴근 및 2일차 출근에만 추가. 실내에서는 두 레이어 종료 |
| `GameAudioContext`, `SceneAudioController` | 대사 중 환경음 0.65배, 옵션창·일시정지 중 0.45배. 컷씬·포커스 이탈 시 환경음 정리 |
| `CutSceneManager`, `OustideTimelineManager`, `StudioGameplayScope` | 컷씬 수명에 맞춰 차단 상태 복구. dream/intro/TV/BD/memory 계열에서는 장소 음악 감쇠 후 종료·스킵·취소 시 복구 |
| `StudioAudioPlayback` | Studio 효과음에 컷씬 출력 배율 0.55 적용. 사용자 SE 음량은 믹서에서 한 번만 적용 |
| `DialogueTextPlayer`, `OutsideDialoguePresenter` | 6개 타이핑 샘플 교대, 직접 대사 gain 0.68·배경 대사 0.34. 프레임·간격 제한과 스킵 무음 유지. 배경음은 직접 대사와 겹치지 않으며 근거리만 허용 |
| `UIPopupView.Shown`, `PopupService` | 패널이 화면에 들어온 시점에 1회. 목표 완료는 완료음, 나머지 6종은 일반 알림음. 동일 효과음의 짧은 중복 호출 억제 |
| `PlayerDataSO`, `SceneAudioController` | 실제 재화 증감만 재화음 재생. 호감도 실제 증감, 첫 만남 플래그, 재료 해금 조건 변화는 해당 팝업 표시. 로드·초기화는 변화 알림에서 제외. Chris 신규 인물 알림 제외 |
| `OutsideStoryFlow` | 자동 강제 이벤트 시작 시 이벤트 팝업을 한 번 표시. 같은 이벤트의 실패 후 재시도에서 반복 알림 방지 |
| `CraftPrepStageScreen`, `CraftPrepShelfSlot` | 선택 가능한 재료 슬롯에 새로 진입할 때 호버음. 빠르게 다른 슬롯으로 이동하면 이전 호버음을 짧게 감쇠 |
| `GameProgressionService.MoveAsync` | 실제 수락된 Home↔OutSide 이동 시작 시 집 문 효과음 1회. 바 출입과 다른 문에는 사용하지 않음 |
| `CapManager` | 개봉 성공음 gain 0.77528, 일반 pitch 1.08·퍼펙트 1.16. 탄산 잔향 gain 0.14에 진입·종료 페이드 |
| `PourManager` | 실제 유량 상태에 루프 연결. gain 0.820×sqrt(min(1.4, 유량비)), 정지 시 짧게 감쇠 |
| `ShakingRhythmManager`, `SoundManager` | 성공 타격음 gain 0.416, pitch 0.98→1.00→1.02 순환. 게임 난수에는 영향 없음 |
| `StirManager` | 실제 젓기 애니메이션 동안 기본음 gain 0.846. 진입·종료 페이드와 포커스·중단 정리 |
| `PlayerPrefsAudioVolumeStore` | 저장값이 없는 경우 BGM 0.30·SE 0.80. 기존 사용자 설정은 그대로 유지 |

`ProjectLifeScope.prefab`의 SoundManager에 문서의 개별 gain을 반영했다. 바·거리 BGM은 원본 M4A를 보관하고 변환한 OGG를 사용한다. OGG의 AudioImporter는 Streaming이다.

제조 대체음은 `SFX_stir_alternate`(0.900), `SFX_shaker_hit_alternate`(0.473)로 분리했다. 기본 제조음의 alternatives 배열에서는 제거하여 본편에서 자동 교대하지 않는다. 타이핑 미리듣기와 임시 합성음은 연결하지 않았다.

## 대상이 없어 제외한 부분

사용자 지시: 프로젝트에 없으면 적용하지 않음.

- 꿈에서 깨어나는 실제 전환 Timeline의 연락음 큐.
- 단골 손님 정보 슬롯 UI의 호버음.
- 0일차 집 문 앞 혼잣말 종료에 이어지는 ‘첫 출근하기!’ 목표 시작·완료 연출. 목표 팝업 자체의 표시 효과음은 공통 경로에 적용했다.

옷 갈아입는 음원은 등록 상태를 유지한다. 문서에 실제 재생 대상·시점이 없어 새 동작에는 붙이지 않았다.

## 검증

- 실제 Unity 참조를 이용한 런타임 및 에디터·테스트 코드 컴파일.
- 날씨·감쇠·중첩 컷씬 복구 및 기존 팝업 CSV 규칙 자동 검증.
- `AudioGuideTests`: 데이터 로드와 실제 변화 구분, 제조 대체음 분리, 팝업 화면 표시 시점 검증.

문서에 페이드의 초 단위 수치와 웹 본편 URL이 없어 장소 변경은 기본 1초, 환경음은 0.55초, 제조 루프는 0.12초를 사용했다. 기존 화면 전환에 추가 암전이나 대기 시간을 붙이지 않았다.
