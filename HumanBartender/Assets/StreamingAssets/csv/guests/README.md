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

`guest_parts.csv`의 `sprite`가 실제 Addressables 주소와 스프라이트 이름을 지정합니다.
예: `Guest/body_f[body_0]`. 현재 제공된 원본에 맞춰 38개 파츠를 등록했습니다.
없는 상의·겉옷을 추첨 후보로 넣지 않으며, `guest_exclusions.csv`도 존재하는 파츠만 참조합니다.

일반 손님은 착석할 때 성별·성격에 맞춰 파츠를 조합하며, 이미 앉아 있는 손님과 같은 조합은 피합니다.
Play 씬의 `GuestManager > Use Temp Appearance`는 꺼 두어야 이 테이블이 적용됩니다.

파츠 그림은 같은 캔버스에서 잘라낸 위치 정보를 유지해야 합니다. 렌더러는 몸 그림의 원본 중앙 X와 하단 Y를
공통 기준으로 삼아 각 스프라이트의 원본 사각형·피벗·Pixels Per Unit으로 위치를 복원합니다.
눈·머리·옷을 서로 다른 크기로 잘라도 맞춰지며, 캔버스나 배율을 바꿀 때는 함께 사용하는 파츠도 확인해야 합니다.
