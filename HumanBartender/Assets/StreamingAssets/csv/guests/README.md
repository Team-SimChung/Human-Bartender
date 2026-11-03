# 손님 등장·성격·대사·외형

등장 순서는 random_waves.csv와 regular_slots.csv, 상황별 대사는 barks.csv, 외형은 guest_parts.csv에서 설정합니다.

| CSV 파일 | 엑셀 시트 | 설정하는 내용 |
|---|---|---|
| `bark_situations.csv` | BarkSituations | 일반 손님 대사 상황별 기본 표정 |
| `barks.csv` | Barks | 일반 손님 상황별 대사와 추첨 가중치 |
| `guest_settings.csv` | GuestSettings | 일반 손님 외형의 기본 조합 |
| `guest_parts.csv` | GuestParts | 부위별 일반 손님 외형·가중치 |
| `guest_personalities.csv` | GuestPersonalities | 외형 부품에 허용된 성격. 현재 원본은 모두 빈 목록 |
| `guest_exclusions.csv` | GuestExclusions | 동시에 사용할 수 없는 외형 조합 |
| `personalities.csv` | Personalities | 손님 성격·팁·인내심 배율 |
| `random_waves.csv` | RandomWaves | 날짜별 일반 손님 등장 순서 |
| `regular_slots.csv` | RegularSlots | 단골 등장·주문·서빙 효과 |

[전체 폴더 안내](../README.md)

## 일반 손님 이미지

`guest_parts.csv`가 외형 후보의 편집 원본이며 `sprite`에는 `Guest/body_f` 같은 논리 주소를 씁니다.
`StreamingAssets/unity/resource_bindings.csv`가 이를 `Guest/body_f[body_0]` 같은 Unity Sprite 주소로 연결합니다.
현재 엔진 이미지에 맞춰 남성 21개·여성 22개, 총 43개 파츠를 등록했습니다.
없는 상의·겉옷을 추첨 후보로 넣지 않으며, `guest_exclusions.csv`도 존재하는 파츠만 참조합니다.

현재 없는 파츠는 남성 상의 `outfit_m_4`~`outfit_m_8`과 겉옷 `outerwear_m_1`입니다.
남성 `cyberware_1`~`cyberware_3` 이미지는 있지만 기존 9개 부위에 대응하는 슬롯이 없어 이번 후보에는 넣지 않았습니다.
남성 팔 장식은 민소매 `outfit_m_3`와 조합할 수 있고, 여성 팔 장식과 긴소매 겉옷은 함께 추첨하지 않습니다.

일반 손님은 착석할 때 성별·성격에 맞춰 파츠를 조합하며, 이미 앉아 있는 손님과 같은 조합은 피합니다.
Play 씬의 `GuestManager > Use Temp Appearance`는 꺼 두어야 이 테이블이 적용됩니다.

파츠 그림은 같은 캔버스에서 잘라낸 위치 정보를 유지해야 합니다. 렌더러는 몸 그림의 원본 중앙 X와 하단 Y를
공통 기준으로 삼아 각 스프라이트의 원본 사각형·피벗·Pixels Per Unit으로 위치를 복원합니다.
눈·머리·옷을 서로 다른 크기로 잘라도 맞춰지며, 캔버스나 배율을 바꿀 때는 함께 사용하는 파츠도 확인해야 합니다.
눈·눈썹처럼 분리된 조각이 있는 이미지는 바인딩된 스프라이트 사각형이 모든 조각을 포함해야 합니다.
현재 해당 이미지의 `_0` 스프라이트는 원본 캔버스 전체를 포함하며, 기존 GUID·스프라이트 ID는 유지합니다.

## 일반 손님 입 애니메이션

입 후보는 남녀 각각 `mouth_1`~`mouth_3`이며, 삭제한 `mouth_4`, `mouth_5`는 후보에서 제외했습니다.
파일을 재정렬하면서 내부 스프라이트 이름은 이전 이름이 남을 수 있습니다. 예를 들어 여성 `mouth_1.png`의
내부 이름은 `mouth_4_0`입니다. 이는 현재 입 1번의 하위 이미지이며 삭제한 입 4번 후보를 다시 참조하는 것이 아닙니다.
내부 이름을 파일명으로 추측하지 않고 `resource_bindings.csv`에 기록된 주소를 사용합니다.

동일한 논리 키에 `Sprite`와 `AnimationClip`을 각각 연결합니다. 예를 들어 `Guest/mouth_f_1`의
Sprite는 정지 이미지, AnimationClip은 `Guest/talk_mouth_f_1`입니다. 말하기 클립은
`03.Content/07.Animations/01.Characters/09.Customers`에서 해당 `02.Talk/face_bottom_1`의 4개 프레임과 위치 보정을 관리합니다.
기존 파츠 추첨기는 변경하지 않으며 `GuestManager`가 선택된 입의 클립을 함께 불러옵니다.
`GuestSlot`이 대사 시작·종료를 소유하고 `GuestMouthAnimation`은 연결된 클립만 재생합니다.
즉시 표시 대사는 말풍선이 보이는 동안, 타이핑 대사는 글자 출력이 끝날 때까지 입이 움직입니다.
숨기기·대사 교체·좌석 비우기·비활성화 시에는 정지 입으로 복원하고 로드한 클립도 손님 퇴장 시 반납합니다.
