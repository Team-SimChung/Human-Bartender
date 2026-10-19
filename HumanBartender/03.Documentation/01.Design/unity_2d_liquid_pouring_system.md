# Unity 2D 사실적인 액체 따르기 시스템 설계

## 1. 목표

병을 기울여 잔에 액체를 따르는 2D 게임 화면을 구현한다.

주요 요구사항:

- 병의 기울임 정도에 따라 유출량이 달라진다.
- 병 내부의 액체는 실제 중력 방향을 기준으로 움직인다.
- 병의 액체 양이 줄어든다.
- 물줄기가 병 입구에서 잔으로 이어진다.
- 잔의 액체 양이 증가한다.
- 액체 표면이 약간 출렁인다.
- 급격하게 병을 움직이면 액체가 관성에 의해 늦게 따라오는 느낌을 준다.
- SpriteMask, Custom Mesh, Shader, Particle System을 조합해 시각적 사실성을 높인다.

---

## 2. 전체 구조

```text
Bottle
├── BottleBack
├── Liquid
├── BottleFront
├── Mouth
└── PourStream
    ├── StreamMesh
    └── Droplets

Glass
├── GlassSprite
├── Liquid
└── Foam / Surface
```

액체는 병 Sprite 자체에 포함시키지 않고 별도의 오브젝트로 관리한다.

렌더링 순서는 다음을 권장한다.

```text
BottleBack
    ↓
Liquid
    ↓
BottleFront
```

액체가 병이나 잔 외부로 나오지 않도록 SpriteMask 또는 Shader 클리핑을 사용한다.

---

## 3. 핵심 설계 원칙

### 액체를 병과 함께 단순 회전시키지 않는다

병이 60° 기울어져도 액체 표면은 월드 중력 기준으로 수평을 유지해야 한다.

```text
Bottle rotation = 60°
Liquid surface ≈ World Up
```

즉:

- 병은 회전한다.
- 액체는 중력 방향을 기준으로 표면을 계산한다.
- 병 내부 좌표계로 변환하여 액체 Mesh를 표시한다.

예:

```csharp
Vector2 localUp =
    bottleTransform.InverseTransformDirection(Vector2.up);
```

---

## 4. 액체 표현 방식

단순 SpriteRenderer보다는 **Custom Mesh + Shader** 방식을 추천한다.

개념적으로는 다음과 같다.

```text
Bottle 내부

      ╲
       ╲  ← 액체 표면
   ┌──────────┐
   │██████████│
   │██████████│
   │██████████│
   └──────────┘
```

액체 Mesh를 병 내부 영역에 맞춰 만들고 SpriteMask 또는 Shader로 병 외부 영역을 제거한다.

---

## 5. SpriteMask 구조

병 이미지를 가능하면 다음처럼 분리한다.

```text
Bottle_Back.png
Bottle_LiquidMask.png
Bottle_Front.png
```

Unity Hierarchy 예시:

```text
Bottle
├── BottleBack
├── Liquid
└── BottleFront
```

렌더링:

```text
BottleBack
    ↓
Liquid
    ↓
BottleFront
```

이렇게 하면 액체가 병 내부에 있는 것처럼 보인다.

---

## 6. 액체 표면

액체 표면은 완전히 고정된 직선보다는 아주 작은 파동을 추가한다.

```text
~~~~~────~~~~
```

Shader에서 개념적으로:

```hlsl
float wave =
    sin(worldPos.x * _WaveFrequency + _Time.y * _WaveSpeed)
    * _WaveAmount;
```

권장 범위:

```text
WaveAmount ≈ 0.01 ~ 0.03
```

너무 크게 만들면 물이 아니라 젤리처럼 보일 수 있으므로 미세하게 적용한다.

---

## 7. 병 기울기와 유출량

단순히 특정 각도 이상이면 물을 흘리는 방식보다 병 입구 방향과 중력의 관계를 이용하는 것이 좋다.

병 입구 방향:

```csharp
Vector2 mouthDirection =
    bottleTransform.TransformDirection(Vector2.up);
```

중력:

```csharp
Vector2 gravity = Vector2.down;
```

기울기 요소:

```csharp
float pourAmount =
    Vector2.Dot(mouthDirection, gravity);
```

이 값은 병의 기울기에 따라 달라진다.

개념:

```text
수직
  ↑
  │
  │
  │

→ 유출 거의 없음


기울임
    ╲
     ╲
      ↓

→ 유출 시작


거의 수평
──────────→

→ 최대 유출량
```

---

## 8. AnimationCurve 사용

기울기와 실제 유량의 관계는 AnimationCurve로 조절하는 것을 추천한다.

```csharp
[SerializeField]
private AnimationCurve pourCurve;
```

개념적인 곡선:

```text
Flow
1.0 |                 ______
    |             ___/
    |          __/
    |       __/
0.0 |_______/
    +----------------------
       0° 20° 40° 60° 90°
```

예시:

```text
0~15° : 거의 안 나옴
20°   : 한두 방울
30°   : 조금씩
45°   : 보통
60°   : 많이
90°   : 최대
```

이렇게 하면 디자이너가 Inspector에서 원하는 감각으로 쉽게 조정할 수 있다.

---

## 9. 유량 계산

보다 사실적인 느낌을 위해 Torricelli 법칙을 일부 적용할 수 있다.

개념적인 식:

```text
Q = Cd * A * sqrt(2gh)
```

변수:

- Q = 유량
- Cd = 유출 계수
- A = 병 입구 면적
- g = 중력
- h = 액체 높이와 입구 높이 차이

Unity에서는 다음과 같이 시작할 수 있다.

```csharp
float head = Mathf.Max(liquidHeight - mouthHeight, 0f);

float flow =
    dischargeCoefficient *
    mouthArea *
    Mathf.Sqrt(2f * gravity * head);
```

그리고 기울기 요소를 추가한다.

```csharp
float angleFactor =
    Mathf.Clamp01(
        Vector2.Dot(mouthDirection, Vector2.down)
    );

flow *= pourCurve.Evaluate(angleFactor);
```

실제 게임에서는 물리식 자체를 정확히 재현하기보다 게임플레이에 맞춰 계수를 튜닝한다.

---

## 10. 병의 액체 감소

병의 현재 액체 양을 `liquidVolume`으로 관리한다.

```csharp
liquidVolume -= flow * Time.deltaTime;
```

그리고 최대 용량으로 정규화한다.

```csharp
liquidFill =
    liquidVolume / maxVolume;
```

예:

```text
100%

██████████
██████████
██████████
██████████
```

↓

```text
60%

██████████
██████████
██████░░░░
████░░░░░░
```

↓

```text
20%

████░░░░░░
██░░░░░░░░
░░░░░░░░░░
░░░░░░░░░░
```

---

## 11. 잔의 액체

잔도 같은 방식으로 액체 양을 관리한다.

```csharp
glassFill += flow * Time.deltaTime;
```

개념:

```text
┌──────────┐
│          │
│          │
│          │
│██████████│
└──────────┘
```

병에서 빠진 양과 잔에 들어간 양을 연결한다.

```csharp
BottleVolume -= flow * dt;
GlassVolume  += flow * dt;
```

---

## 12. 물줄기 구현

물줄기는 단일 Particle System보다 다음 3개를 조합하는 것을 추천한다.

### A. Main Stream

병 입구에서 잔까지 연결되는 Custom Mesh 또는 LineRenderer.

```text
       ╲
        ╲
         ╲
          ╲
           ╲
            ╲
             ↓
```

### B. Stream Wobble

완전히 직선인 물줄기보다 약간의 흔들림을 준다.

```text
╲
 ╲
  ╲
   ╲
    ╲
```

작은 Noise를 적용하되 너무 크게 흔들지 않는다.

### C. Droplets

소수의 작은 물방울을 Particle System으로 추가한다.

```text
     •
       •
    ╲
     ╲ •
      ╲
       ↓
```

메인 스트림 1개 + 소수의 물방울 조합이 자연스럽다.

---

## 13. 물줄기 굵기

기울기에 따라 물줄기의 굵기도 변화시키면 좋다.

```csharp
float streamWidth =
    Mathf.Lerp(
        0.01f,
        0.12f,
        flowNormalized
    );
```

적게 따를 때:

```text
      ╲
       ╲
        ·
        ·
        ↓
```

많이 따를 때:

```text
       ╲
        ╲
         ╲
          ╲
           ↓
          ↓↓
```

---

## 14. 잔에 닿는 부분

물줄기가 잔에 닿는 지점에는 작은 splash를 추가한다.

```text
             ╲
              ╲
               ╲
                ↓
              ~~~~~
             ~~~~~~~
            █████████
```

Particle System에서 시작점으로 사용할 수 있는 값:

```text
Emission Burst : 2~5
Start Speed    : 0.1 ~ 0.4
Gravity        : 0.5 ~ 1.0
Start Size     : 0.01 ~ 0.03
```

과도한 파티클은 저렴한 게임 효과처럼 보일 수 있으므로 적게 사용한다.

---

## 15. 액체 표면의 관성 / Sloshing

병을 빠르게 회전시키면 액체가 즉시 따라가지 않고 약간 늦게 움직이는 느낌을 주는 것이 좋다.

```csharp
currentSurfaceAngle = Mathf.SmoothDampAngle(
    currentSurfaceAngle,
    targetSurfaceAngle,
    ref surfaceVelocity,
    surfaceResponseTime
);
```

이렇게 하면 액체 표면이 병의 움직임을 따라가면서도 약간의 지연이 생긴다.

---

## 16. Sloshing

병을 좌우로 흔들면 액체 표면이 반대 방향으로 출렁이는 효과를 추가할 수 있다.

```csharp
float targetTilt =
    -angularVelocity * sloshAmount;

sloshAngle = Mathf.Lerp(
    sloshAngle,
    targetTilt,
    1f - Mathf.Exp(-sloshSpeed * Time.deltaTime)
);
```

개념:

```text
병을 왼쪽으로 움직임

       ╲
~~~~~~~~╲
█████████╲
██████████


반대로 움직임

        ╱
       ╱~~~~~~~~
      ╱█████████
     ███████████
```

---

## 17. 추천 Unity 구조

```text
BottleController
├── rotation
├── liquidVolume
├── mouthPosition
├── mouthDirection
└── pourAmount
```

```text
LiquidController
├── volume
├── surfaceHeight
├── surfaceAngle
├── sloshAngle
└── wave
```

```text
PourController
├── flowRate
├── streamWidth
├── streamLength
├── dropletRate
└── splash
```

```text
GlassController
├── volume
├── maxVolume
├── surfaceHeight
└── surfaceWave
```

---

## 18. 전체 동작 흐름

```text
Bottle Rotation
       │
       ▼
Calculate Mouth Direction
       │
       ▼
Calculate Pour Angle
       │
       ▼
AnimationCurve
       │
       ▼
Calculate Flow Rate
       │
       ├───────────────┐
       ▼               ▼
Bottle Volume      Stream
Decrease           Generation
       │               │
       │               ├── Stream Mesh
       │               └── Droplets
       │
       ▼
Glass Volume Increase
       │
       ▼
Glass Liquid Surface
       │
       ▼
Surface Wave / Splash
```

---

## 19. 최종 추천 구현 방식

| 요소 | 구현 |
|---|---|
| 병 | Sprite |
| 액체 내부 | Custom Mesh |
| 액체 표면 | Shader |
| 액체 표면 흔들림 | Shader + Sloshing |
| 병 기울기 | Transform |
| 유량 | 기울기 + 액체 높이 |
| 물줄기 | Custom Mesh / LineRenderer |
| 물방울 | Particle System |
| 잔 내부 | Custom Mesh |
| 잔 표면 | Shader |
| 병/잔 외부 침범 방지 | SpriteMask |
| 중력 | `Vector2.down` |

---

## 20. 결론

이런 2D 게임에서는 Navier-Stokes 기반의 실제 유체 시뮬레이션까지 사용할 필요가 없다.

오히려 다음 조합이 성능, 구현 난이도, 시각적 사실성의 균형이 좋다.

```text
Sprite
  +
SpriteMask
  +
Custom Mesh
  +
Shader
  +
Particle System
  +
간단한 물리 기반 계산
```

특히 중요한 요소는 다음 네 가지다.

1. **액체 표면은 병과 독립적으로 중력 방향을 따른다.**
2. **병 기울기와 액체 높이에 따라 유량을 계산한다.**
3. **물줄기는 Mesh + 소량의 Particle로 구성한다.**
4. **Sloshing과 표면 Wave를 추가하여 액체가 살아 움직이는 느낌을 준다.**

이 구조라면 이미지처럼 병을 기울여 잔에 정확한 양을 따르는 미니게임을 구현하면서도, 단순한 애니메이션보다 훨씬 사실적인 결과를 만들 수 있다.

---

## 다음 구현 단계

실제 Unity 프로젝트에 적용할 때는 다음 순서로 개발하는 것이 좋다.

### Phase 1 — 기본 액체
- 병 내부 Liquid Mesh
- 액체 Fill Amount
- SpriteMask
- 중력 기준 액체 표면

### Phase 2 — 따르기
- 병 입구 위치
- 기울기 계산
- AnimationCurve
- Flow Rate
- 병 → 잔 Volume 연결

### Phase 3 — 시각 효과
- 물줄기 Mesh
- Droplet Particle
- Splash
- Shader Wave

### Phase 4 — 사실성
- Sloshing
- 회전 속도 기반 관성
- 유량에 따른 물줄기 굵기
- 잔 표면 출렁임

### Phase 5 — 게임 로직
- 목표 용량 판정
- 과다/부족 판정
- 정확도 점수
- 입력에 따른 병 회전 제어
