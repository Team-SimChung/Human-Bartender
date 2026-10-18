# 따르기 액체 렌더링

Unity 6000.3 / URP 2D의 기존 `NewPourGame` 프리팹에서 사용한다. `PourManager.Start`가 실제 기믹 재료 ID로 외형을 선택하고, 기존 선반 데이터의 RGB와 `liquid_alpha`를 전달한다. 프로파일의 `overrideColor`를 켜면 지정한 RGBA를 우선한다. 병 이미지만 바꾸는 `useTestBottleId`는 액체 재료를 바꾸지 않는다.

## 렌더링 경로

1. 기존 CPU SPH가 위치, 속도, 잔 경계와 충돌을 계산한다. 각 물리 스텝의 마지막에 감속량을 보고 시각적 충격 하나를 기록한다. 바닥에 가만히 있는 입자는 계속 거품을 만들지 않는다.
2. `SphLiquidRenderer.UpdateBlobs`가 활성 입자의 위치·속도·충격을 32바이트 구조체로 GPU에 보낸다. 정점과 UV는 CPU에서 다시 만들지 않는다. 물리용 GameObject 풀은 유지하고 매 스텝의 불필요한 Transform 갱신을 끈다.
3. `LiquidBlobSplat`이 절차적 인스턴싱으로 늘어나는 물줄기를 만들고 ARGBHalf 필드에 밀도, 가중 속도와 충격을 누적한다.
4. GPU가 잔 바닥에서 이어진 액체의 수면을 추출한다. 공간 중앙값으로 가는 유입 줄기가 거품층을 위로 끌어올리는 현상을 억제한다.
5. 잔 로컬 좌표의 작은 RHalf 텍스처에서 충돌 탁도를 속도와 부력으로 이동시키고 감쇠시킨다. 수면 거품은 별도 텍스처와 수명을 사용한다.
6. 최종 셰이더가 픽셀 격자, 단계별 명암, 가상 두께, 수면 하이라이트, 탄산 기포, 과즙 입자와 거품을 합성한다. 기포는 GPU의 절차적 패턴이며 추가 SPH 입자가 아니다.

렌더링 결과를 CPU로 읽지 않는다. `ReadPixels`는 에디터 검증 테스트의 수치 검사와 캡처에만 사용한다. Shader Model 4.5를 지원하는 그래픽 장치가 필요하며 D3D11에서 검증했다.

## 재료별 기본값

| 재료 | 표현 |
| --- | --- |
| 맥주 | 충돌 지점의 밝은 미세기포 구름, 상승하는 기포, 내부 탁도보다 오래 유지되는 거품 |
| 탄산수·샴페인 | 맑은 바탕, 상승 기포, 짧은 충돌 탁도와 얇은 거품 |
| 콜라·진저에일 | 기존 재료 색, 진한 흡수색, 탄산과 따뜻한 색의 거품 |
| 진·보드카·데킬라·맑은 리큐르 | 비탄산, 낮은 불투명도, 가장자리와 수면 반사 |
| 오렌지·파인애플 | 과즙의 탁도와 미세한 과육 패턴, 약한 충돌 거품. 파인애플의 거품 생성량이 더 큼 |
| 사워믹스 | 탁한 과즙 계열, 과육 패턴 없음 |
| 우유·크림·코코넛밀크 | 불투명한 바탕, 부드러운 하이라이트, 재료에 따른 점도 |
| 그레나딘 | 높은 점도와 진한 흡수색, 비탄산 |
| 위스키·브랜디·유색 리큐르·와인 등 | 기존 RGB를 유지하면서 깊이에 따른 명암, 비탄산 |

미등록 ID는 비탄산 기본값을 사용한다. 색과 투명도 데이터가 있으면 그 값을 우선한다. 레시피의 완성 색으로 바꾸지 않는다.

## 인스펙터 조절

실제 프리팹은 `Liquid_Light.asset`을 사용한다.

- `LiquidProfile.pixelSize`: 1080 높이 기준 픽셀 크기. 물리 좌표는 양자화하지 않는다.
- `colorSteps`: 명암 단계 수.
- `surfaceDepth`: 수면의 입체감을 만드는 캡 깊이. 잔 높이 기준의 시각적 근사다.
- `appearanceOverrides`: 특정 `ingredientId`에 대한 외형을 덮어쓴다. 지정한 항목의 모든 외형 값이 적용된다.
- `carbonation`, `bubbleRise`: 탄산 기포 양과 상승 속도.
- `impactCloud`, `cloudLifetime`: 충돌 탁도의 강도와 감쇠 시간.
- `foamGain`, `foamLifetime`, `foamHeight`: 수면 거품의 생성량·감쇠 시간·최대 높이. 적은 양에서는 액체 깊이의 35% 이내로 제한한다.
- `absorption`, `turbidity`, `gloss`, `pulp`: 깊이 색, 탁도, 반사와 과육.
- `viscosityScale`: 물리 설정을 런타임에 복제하여 점도를 조절한다. 공유 프로파일 에셋을 수정하지 않는다.
- 렌더러의 `fieldResolutionScale`: 화면 공간 필드 해상도. 낮추면 GPU 채움 비용과 메모리가 줄고 작은 물방울 표현이 거칠어진다.
- `effectResolution`: 잔 내부 탁도와 수면 데이터의 해상도. 기본값 128은 128×256 탁도 텍스처 두 장과 128×1 수면 텍스처 세 장을 사용한다.
- `refractionStrength`: 기본값 0. 2D Camera Sorting Layer Texture에 액체 뒤의 배경만 들어가는 구성을 확인한 뒤 사용한다.

## 성능 범위와 한계

이 변경은 입자 형상 생성과 시각 효과를 GPU로 옮긴다. SPH 이웃 탐색·밀도 보정·충돌은 CPU에서 유지한다. 기존 입자당 정점/UV 데이터 80바이트 대신 활성 입자당 32바이트를 업로드하며, 남는 풀 용량을 매 프레임 지우거나 업로드하지 않는다. 이 수치는 데이터 크기의 비교이며 FPS 개선율은 아니다.

새로운 탁도·거품·조명 패스에는 추가 GPU 비용이 든다. 1920×1080, 필드 배율 0.5, 효과 해상도 128에서 렌더 타깃은 약 4.1 MiB이고 입자 버퍼는 별도다. 실제 장면의 CPU/GPU 시간 비교는 별도 프로파일링이 필요하다.

수면은 기존 SPH 실루엣을 따른다. 내부는 잔의 단면을 가정한 광학적 깊이이며 3D 유체 시뮬레이션은 아니다. 잔의 물리 경계는 기존 사각형을 유지한다. 여러 따르기 단계 사이의 액체 누적·재료 혼합 상태는 추가하지 않았다. 기포와 거품은 투입량·점수에 포함되지 않는다.

## 검증

`LiquidRenderingTests`, `SphPhysicsTests`, `PourMouseControlTests`, `PourMouseRuntimeTests`를 그래픽 장치가 있는 Unity EditMode 배치에서 실행한다. `-nographics`는 GPU 검증을 건너뛰므로 사용하지 않는다.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.14f1\Editor\Unity.exe' `
  -batchmode -force-d3d11 -projectPath '<HumanBartender 프로젝트 절대 경로>' `
  -runTests -testPlatform EditMode `
  -testFilter 'LiquidRenderingTests|SphPhysicsTests|PourMouseControlTests|PourMouseRuntimeTests' `
  -testResults '<결과 XML 절대 경로>' -logFile '<로그 절대 경로>'
```

검증 항목은 셰이더 컴파일, 좌표 방향, 떨어지는 방울 및 연결된 가는 줄기의 수면 제외, 충돌 탁도 감쇠와 오래 남는 맥주 거품, 재료별 차이, 카메라/잔 위치 이동, GPU 자원 해제·재활성화, 기존 물리와 실제 마우스 조작이다. 실제 프리팹 테스트는 플레이 모드에 진입해 병을 기울이고 URP 카메라 콜백으로 렌더링한다.

테스트 캡처는 `%TEMP%/PourLiquidValidation`에 저장된다. `beer-impact`는 효과를 명확히 검사하기 위해 입자에 인위적인 충격을 준 화면이고, `runtime-beer-pouring`과 `runtime-beer-settled`는 실제 프리팹·방출·충돌에서 나온 화면이다.
