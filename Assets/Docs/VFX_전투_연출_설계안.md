# 전투 연출(VFX) 강화 설계안

> 인게임 전투의 타격감·시인성을 강화하기 위한 설계안. 1차 초안(2026-08-24)을 현행 코드베이스와
> `E:\repos\ProjectBloodmoon` 대조 검증한 뒤, 핵심 방향을 확정해 다시 정리한 버전.
> 관련 배경: [[melee-only-enemies]] — 이 설계는 공격 로직/판정을 전혀 건드리지 않는다.

---

## 0. 확정된 결정 사항

| # | 결정 | 이유 |
|---|---|---|
| 1 | **ProjectBloodmoon 이식 파기.** `SpriteHitFlash.shader`/`.cs`, `PlayerDeathVisual.cs`, `AuraPulseIndicator.cs` 어느 것도 가져오지 않는다. | 아래 1절 참조 — 이식할수록 이 프로젝트에 없던 의존성(Built-in RP CGPROGRAM, DOTween)이 새로 생기고, 정작 필요한 결과물은 현재 코드가 이미 가진 패턴으로 더 적은 변경으로 얻을 수 있음이 검증됨. |
| 2 | **기존 히트플래시 시스템(`EnemyView.TickHitFlash`/`AllyUnitView.TickHitFlash`)은 코드 그대로 둔다.** `SpriteRenderer.color` 곱연산 방식의 한계(완전한 실루엣이 아니라 "틴트"로 보임)를 알고도 의도적으로 유지한다. | 이 한계를 없애려면 모든 적/아군 스프라이트 머티리얼을 URP 커스텀 셰이더로 교체해야 하는데, 그 비용 대비 체감 개선폭이 이번 스코프의 우선순위(①②③ 공격 연출, 스플래시 링, 사망 페이드)보다 낮다고 판단. 필요해지면 별도 설계로 재검토. |
| 3 | 새로 만드는 모든 시각 이펙트는 **지오메트리 + `Lerp`/`Mathf.Exp` 보간 + URP HLSL 셰이더 + 기본 `ParticleSystem`** 조합만으로 구현한다. 신규 애니메이션(스프라이트 시퀀스, 스켈레탈)은 만들지 않는다. | 이 프로젝트는 신규 애니메이션 에셋을 제작할 인력이 없음. 정적 2D 이미지(알파 채널 포함)는 생성형 모델로 제작 — 이미 `Assets/Art/AllyUnits/`, `Assets/Art/Operator Sprite/` 전체가 이 방식으로 만들어져 있어 별도 정당화가 필요 없음. |
| 4 | 판정 로직은 100% 그대로 유지. 이번 설계는 전부 `AttackFlash.Spawn` 호출 지점 근처에 "부가적으로" 붙는 순수 연출 레이어다. | `DamageEffect`/`SplashDamageEffect`/`PierceDamageEffect` 전부 데미지 계산 → `TakeDamage` → (있으면) `AttackFlash.Spawn` 순서이고, 연출 프리팹이 비어 있어도 전투에 영향 없음. 이 성질을 절대 깨지 않는다. |

---

## 1. 왜 ProjectBloodmoon 이식을 파기했는가

검증 결과, ProjectBloodmoon의 세 후보 코드는 전부 "그대로 가져오면 손해"였다.

- **`SpriteHitFlash.shader`** — `#include "UnitySprites.cginc"` + `CGPROGRAM`을 쓰는 **Built-in RP 전용** 셰이더다. 이 프로젝트는 URP라 그대로 컴파일되지 않고, `MaterialPropertyBlock`으로 `_FlashAmount`/`_DeathAmount`를 흘리는 구조 전체를 URP HLSL로 새로 써야 한다. 즉 "이식"이 아니라 "설계만 참고한 재작성"이고, 재작성할 바엔 굳이 Bloodmoon 코드를 경유할 이유가 없다.
- **`AuraPulseIndicator.cs`** — `DG.Tweening`(DOTween)에 의존한다. `Packages/manifest.json` 확인 결과 **이 프로젝트엔 DOTween이 설치돼 있지 않다.** 이 하나 때문에 새 패키지 의존성을 들이는 건 배보다 배꼽이 크고, 프로젝트 전체가 코루틴/트윈 없이 "잔여시간 필드 + `Update`에서 감산" 스타일(`AttackFlash.cs`, `EnemyView.cs`, `RangePulseVisualRuntime.cs` 전부 동일)을 일관되게 쓰고 있어 스타일도 어긋난다.
- **결정적으로, 이 프로젝트엔 이미 `AuraPulseIndicator.cs`가 하려는 일(반경만큼 확산하며 페이드되는 링)을 Bloodmoon보다 더 정교하게 하는 셰이더가 있다** — `Assets/Shaders/Unit/RangePulseAura.shader`. URP `Core.hlsl` 기반 네이티브 HLSLPROGRAM이고, 코어/글로우 분리 + 글로시 하이라이트까지 갖춘 링 셰이더다. 새로 셰이더를 짤 필요도, Bloodmoon에서 개념을 빌려올 필요도 없이 **이 셰이더를 재사용**하면 된다 (단, 구동 스크립트는 새로 필요 — 3-③ 참조).

→ 결론: ProjectBloodmoon은 "이식 소스"가 아니라 "이미 이 프로젝트에 동급 이상의 답이 있는지 대조하는 체크리스트"로만 쓰였고, 실제 이식 대상은 없었다.

---

## 2. 현재 인프라 인벤토리 (재사용 대상)

새 이펙트를 만들기 전에 반드시 확인할 기존 자산들.

| 자산 | 위치 | 재사용 방식 |
|---|---|---|
| 프리팹별 정적 풀링 패턴 | `Assets/Scripts/Runtime/AttackFlash.cs` | 모든 신규 풀링 컴포넌트(투사체/빔/링)가 따를 표준 패턴. `Dictionary<GameObject, Queue<T>>` static 필드 + `Spawn(prefab, ...)` static 진입점 + `Deactivate()`에서 큐 반납. |
| URP 링 셰이더 | `Assets/Shaders/Unit/RangePulseAura.shader` | 스플래시 충격파 링에 그대로 재사용 (§3-③). `_Progress`(0→1)로 확산 애니메이션을 이미 지원. |
| SO 기반 유닛 비주얼 확장 슬롯 | `Assets/Scripts/Effects/UnitVisual/AllyUnitVisualEffectBase.cs`, `IAllyUnitVisualRuntime.cs`, `RangePulseVisualEffect.cs`(구현 예시) | 아군 유닛에 "지속형" 비주얼을 데이터로 붙이는 기존 확장 지점. 이번 설계의 공격 이펙트(원샷)와는 성격이 달라 직접 재사용하진 않지만, SO 슬롯 설계 관례(§5)는 그대로 따른다. |
| 공격 이펙트 프리팹 슬롯 | `DamageEffect.cs`, `SplashDamageEffect.cs`, `PierceDamageEffect.cs`의 `[SerializeField] private GameObject attackFlashPrefab` | 신규 이펙트 프리팹도 이 자리에 슬롯을 추가/교체하는 방식으로 붙인다. |
| 히트플래시 | `EnemyView.cs:149-164`, `AllyUnitView.cs:233-249` | **코드 변경 없음.** 사망 페이드(§4)만 그 옆에 같은 스타일로 추가. |
| 생성형 2D 아트 파이프라인 | `Assets/Art/AllyUnits/`, `Assets/Art/Operator Sprite/` | 투사체/스파크용 단일 스프라이트 제작에 동일 방식 사용. |

---

## 3. 공격 형태별 연출 설계

### ① 일반 단일 공격형 — Fake Projectile + Hit Spark

- **판정 불변**: `DamageEffect.OnTick`은 지금처럼 즉시 `target.TakeDamage(...)`를 호출한다. 투사체는 명중을 지연시키지 않는 **순수 연출**이다.
- **연출**: `AttackFlash.Spawn` 호출 자리에, `PlayerData.projectileSpeed`/`AllyUnitData.projectileSpeed`(이미 존재하는 필드, `PlayerData.cs:27`, `AllyUnitData.cs:27`)를 사용해 발사 지점→목표 지점을 0.05~0.1초에 걸쳐 이동하는 `FakeProjectile` 오브젝트를 스폰.
- **구현**: `Assets/Scripts/Runtime/FakeProjectile.cs` 신규. `AttackFlash.cs`와 동일한 static 풀 패턴, `Update`에서 `Vector3.Lerp` 이동, 도달 시 `HitSpark`(파티클, §5) 트리거 후 자기 자신은 풀 반납.
- **아트**: 생성형 모델로 단일 탄환 스프라이트 1장(알파 포함) 제작. 신규 애니메이션 불필요 — 회전은 이동 방향으로 `Quaternion.LookRotation` 스냅이면 충분.

### ② 관통 공격형 — 2-Layer Laser Beam

- **판정 불변**: `PierceDamageEffect.OnTick`의 원뿔각 판정(`beamHalfAngleDegrees`)은 그대로.
- **현재 한계**: `AttackFlash.Spawn(prefab, ctx.self.Position, beamEnd)` 한 줄로 단색 `LineRenderer` 하나만 그려짐 (`PierceDamageEffect.cs:57-58`).
- **연출**: 신규 URP 셰이더 1개로 Inner Core(얇고 밝은 백색) + Outer Glow(넓고 반투명, 테마색) 2-Pass 구성. UV 스크롤로 에너지 노이즈 흐름, 발사 직후 폭 150%→100% 팬치(pinch) 애니메이션은 `AttackFlash.lifetime`과 동일한 잔여시간 필드로 절차적 계산.
- **셰이더 기반**: `RangePulseAura.shader`가 이미 이 프로젝트에서 검증된 "URP HLSLPROGRAM + `Core.hlsl` + `CBUFFER_START(UnityPerMaterial)`" 패턴을 쓰고 있으므로, 신규 빔 셰이더도 이 구조를 그대로 따른다 (Bloodmoon 참고 불필요 — 자체 셰이더가 이미 레퍼런스).
- **구현**: 기존 `AttackFlash`를 확장하지 않고 `Assets/Scripts/Runtime/LaserBeamView.cs` 신규 — 요구 조건이 다중 `LineRenderer`/머티리얼 프로퍼티 구동이라 단일 `LineRenderer` 전제인 `AttackFlash`와 책임이 다름. 풀링 패턴만 동일하게 따름.

### ③ 스플래시 공격형 — Lob Projectile + Shockwave Ring

- **판정 불변**: `SplashDamageEffect.OnTick`은 지금처럼 명중 즉시 스플래시 데미지를 전부 적용.
- **포물선 낙하 연출**: `y(t) = Lerp(y0, y1, t) + 4h·t(1-t)` 형태로 목표 지점까지 낙하하는 투사체. §3-①의 `FakeProjectile`과 이동 로직만 다른 변형 — 같은 클래스에 낙하 모드 플래그로 통합할지, 별도 `LobProjectile.cs`로 분리할지는 구현 시 판단.
- **충격파 링**: `splashRadius`(`SplashDamageEffect.cs:18`, 이미 존재하는 필드) 크기까지 0.15~0.2초 만에 OutQuad로 확산하며 알파 페이드아웃.
  - **셰이더는 `RangePulseAura.shader`를 그대로 재사용한다.** 신규 셰이더 작성 불필요 — `_Progress` 하나로 이미 원하는 확산 애니메이션이 구현되어 있음.
  - **구동 스크립트는 신규로 짠다.** 기존 `RangePulseVisualRuntime.cs`는 유닛 하나에 계속 붙어서 자기 사거리 기준으로 무한 반복 재생되는 **상시 오라**용 컴포넌트라 그대로 못 쓴다(`_visualObject`가 유닛 생존 동안 파괴되지 않고 `cycle = duration + interval`로 루프). 스플래시는 착탄 지점에서 **한 번만 재생되고 풀에 반납**되어야 하므로 `Assets/Scripts/Runtime/ShockwaveRing.cs`를 `AttackFlash.cs` 패턴(원샷 풀링)으로 신규 작성하고, 머티리얼만 `RangePulseAura.shader` 기반으로 공유한다.
- **폭발 파티클 & 잔흔**: 착탄 시 파티클 버스트(§5) + 0.3초 남는 바닥 그을림 데칼(단순 `SpriteRenderer` 알파 페이드, 셰이더 불필요).

---

## 4. 사망 연출 — 셰이더 없이, 기존 패턴만으로

ProjectBloodmoon의 실루엣 셰이더(§1 참조)를 파기했으므로, 사망 연출은 **현재 프로젝트에 이미 있는 도구만으로** 구성한다.

- **현재 상태**: `EnemyView.HandleRemoved`(`EnemyView.cs:174-177`), `AllyUnitView.HandleDied`(`AllyUnitView.cs:251-254`) 둘 다 이벤트 수신 즉시 `Destroy(gameObject)`. 페이드도, 파티클도 없이 그 프레임에 사라짐.
- **개선안** (두 파일 모두 동일 패턴으로 확장):
  1. `Died` 이벤트 수신 시 `Destroy`를 즉시 호출하지 않고, `Collider2D.enabled = false`로 우선 끈 뒤 `_isDying = true` + `_deathFadeRemaining = deathFadeDuration` 설정.
  2. 기존 `TickHitFlash()`가 매 프레임 도는 `LateUpdate`(`EnemyView.cs:72-80`, `AllyUnitView.cs:105-117`)에 `TickDeathFade()`를 같은 스타일(잔여시간 감산 필드)로 추가 — `_spriteRenderer.color.a`를 `Mathf.Lerp`로 1→0.
  3. 사망 확정 프레임에 죽음 파티클 버스트(§5)를 스폰하고, 페이드 완료 시점에 `Destroy(gameObject)` 실행.
- **의도적으로 하지 않는 것**: "완전한 단색 실루엣으로 고정 후 페이드"는 하지 않는다 — 이건 셰이더 없이는 불가능하고(단순 `SpriteRenderer.color` 곱연산으로는 원본 명암이 계속 비쳐 보임), 이번 결정(§0-2)에 따라 셰이더 신설을 하지 않기로 했으므로 받아들이는 트레이드오프다. 대신 **파티클 버스트 타이밍과 페이드 속도**로 시인성을 확보한다.

---

## 5. `ParticleSystem` 도입 방침

코드베이스 전체를 확인한 결과 **`ParticleSystem` 참조가 현재 0건**이다. 이번 설계로 처음 도입되는 기법이므로 풀링 전략을 명시해 둔다.

- `AttackFlash.cs`의 `Dictionary<GameObject, Queue<T>>` 패턴은 **그대로 못 쓴다.** `ParticleSystem`은 `Stop()` 직후 상태가 바로 정리되지 않으므로, 반납 전 반드시 `Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear)`을 호출해 파티클을 즉시 비운 뒤 큐에 넣는다.
- `Assets/Scripts/Runtime/ParticleBurst.cs`(가칭) 하나로 히트 스파크(§3-①)/관통 방전(§3-②)/폭발 파편(§3-③)/사망 버스트(§4)를 전부 커버 — 파라미터(색상, 개수, 속도 범위)만 프리팹별로 다르게 설정한 SO 또는 프리팹 인스펙터 값으로 분기하고 C# 로직은 공유.
- Shuriken 모듈은 `Stop Action = None`으로 설정(자동 `Destroy`/`Disable` 방지) — 풀 반납 타이밍을 코드가 직접 제어해야 하기 때문.

---

## 6. 데이터/아키텍처 정합성

`Assets/Docs/ARCHITECTURE.md`의 "합의된 4계층"(데이터 컨테이너 / 데이터 기반 SO / 렌더러 프리팹 / 매니저)에 맞춰:

- 신규 이펙트는 전부 **3번 레이어(렌더러 프리팹)** — 게임 로직을 갖지 않는다.
- `DamageEffect`/`SplashDamageEffect`/`PierceDamageEffect`(2번 레이어, SO)의 인스펙터 슬롯에 프리팹만 갈아 끼우면 새 비주얼이 적용되는 기존 구조를 유지 — 신규 오퍼레이터/적 추가 시 C# 수정 없이 프리팹 조합만으로 대응 가능해야 한다는 원칙(`ARCHITECTURE.md`에 명시된 "그래픽만 교체" 원칙)을 그대로 따름.
- Addressables(`com.unity.addressables`)는 현재 오퍼레이터/적 카탈로그·로드아웃 콘텐츠 로딩에만 쓰이고 있고(`AllyUnitRoster`, `EnemyCatalog` 등), 전투 이펙트 프리팹은 빌드에 상시 포함되는 자산이라 이번 설계에서는 Addressable화하지 않는다 — 필요해지면 별도 결정.

---

## 7. 구현 순서 제안

1. `FakeProjectile.cs` (§3-①) — 가장 단순하고, `AttackFlash` 패턴을 그대로 검증하는 선행 작업.
2. `ParticleBurst.cs` + 풀링 (§5) — ①의 히트 스파크부터 바로 소비.
3. 사망 페이드 (§4) — `EnemyView`/`AllyUnitView` 소규모 확장, 셰이더 불필요라 빠르게 처리 가능.
4. `ShockwaveRing.cs` + `LobProjectile` (§3-③) — 기존 `RangePulseAura.shader` 재사용.
5. `LaserBeamView.cs` + 신규 2-Layer 빔 셰이더 (§3-②) — 셰이더 신규 작성이 필요해 가장 오래 걸림, 마지막으로 배치.

각 단계는 독립적으로 완결되고(§0-4 원칙), 프리팹이 비어 있어도 전투는 정상 동작하므로 순서를 바꿔도 무방하다.
