# TVD 아키텍처 노트

이 문서는 개발 중 내려진 구조적 결정과 그 이유를 기록한다. 게임 기획 자체는 Notion GDD(`GDD 초안`)를 따르며, 여기서는 "그 기획을 어떤 코드 구조로 구현할지"만 다룬다.

## 레이어 구조 (합의된 4계층)

1. **데이터 컨테이너** — `TowerData`, `EnemyData`, `PlayerData` 등. MonoBehaviour도 ScriptableObject도 아닌 순수 C# 클래스(`[Serializable]`). 스탯 수치의 "모양(shape)"만 정의한다.
2. **데이터 기반 SO** — `TowerAbility`, `EnemyAbility` 등. ScriptableObject 에셋으로, 위 Data를 감싸거나 참조하면서 인스펙터에서 드래그 앤 드롭으로 값을 채울 수 있게 한다. 특정 능력/행동 로직(전략 패턴)의 확장 지점이 된다.
3. **렌더러 프리팹** — `Attack_Tower_Default.prefab` 등. 시각적 표현만 담당하는 GameObject. 게임 로직을 갖지 않는다.
4. **매니저** — `TowerManager`, `EnemyManager`, `WaveManager` 등 MonoBehaviour. SO/Data를 읽어 스폰, 배치, 전투 로직을 조율한다.

**현재 단계: 1번(데이터 컨테이너)만 작업 중.** 2~4번은 이후 단계에서 진행하며, 이 문서에 이어서 기록한다.

## 데이터 컨테이너 설계 근거

### EnemyData
- GDD가 확정한 "베이스 3종 + 수식어" 구조를 반영. 3종(일반/돌진/탱커)은 로직이 아니라 수치(체력/속도) 차이이므로 클래스를 나누지 않고 `EnemyKind` enum으로 구분되는 단일 클래스로 설계.
- `waveCost`, `minWave`는 GDD 표에 확정값이 있음 (일반 1/W1, 돌진 1.5/W3, 탱커 2.5/W5). 나머지(`maxHealth`, `moveSpeed`, `contactDamage`, 보상)는 GDD가 "추후 논의"로 남긴 값이라 기본값을 강제하지 않고, 추후 SO 에셋 생성 시 인스펙터에서 직접 채우도록 비워둔다.
- `contactDamage`는 적이 충돌한 대상(플레이어 또는 거점)에게 주는 피해로 단일화. 플레이어 공격과 거점 공격을 분리해야 한다는 기획이 나오면 필드를 분리할 것.

### TowerData
- GDD가 타워를 공격/스킬/파워 3분류로 "확정"했고, 세 타입은 필드 자체가 다르다 (공격=데미지/사거리/공속, 스킬=오라 사거리/버프량, 파워=글로벌 버프/누적치). 그래서 억지로 필드를 공유시키지 않고 `TowerData` 추상 베이스 + `AttackTowerData`/`SkillTowerData`/`PowerTowerData` 3개 파생 클래스로 분리.
- 주의: 순수 C# 클래스 상속은 Unity 인스펙터에서 다형적으로 자동 노출되지 않는다. 2단계(SO)에서 타입별로 별도 SO 클래스를 만들거나 `[SerializeReference]`를 쓰는 방식으로 해결 예정 — 지금 단계에서는 "모양"만 정의.
- 슬롯 제한(공격3/스킬1/파워2, 카드로 확장)은 타워 개별 데이터가 아니라 전역 설정이므로 여기 포함하지 않음 → 매니저/게임 설정 단계에서 처리.

### PlayerData
- {{user}} 확인: 플레이어도 체력을 보유하고 적의 공격으로 피해를 입는다 (거점 체력과 별개). `maxHealth` 필드 포함.
- 기본 공격/스킬 수치는 GDD에 "세부는 추후 논의"로 명시되어 있어 임의로 확정하지 않고, 인스펙터에서 조정 가능한 필드만 정의 (데미지/사거리/공속/투사체 속도, 스킬 쿨다운/범위/피해).

## 그리드/이동 분리 (추후 매니저 단계 유의사항)
- 타워 설치는 그리드(Tilemap) 좌표계를 사용하지만, 적/플레이어의 이동은 그리드와 무관한 자유 좌표(Transform/Vector2) 기반이어야 함. Data 계층에는 영향 없음 — Manager/Controller 단계에서 지켜야 할 제약으로 기록.

## 웨이브 절차적 생성의 난수 요구사항 (추후 WaveManager 단계 유의사항)
- 웨이브 스폰 로직은 System.Random(고정 시드)을 웨이브 매니저 전용으로 사용해 UnityEngine.Random(전역 상태)과 분리해야 재현성(같은 시드 → 같은 웨이브 구성)과 독립성(다른 시스템의 Random 호출이 웨이브 결과에 영향 주지 않음)이 보장됨. Data 계층에는 영향 없음 — WaveManager 구현 시 반영.

## 2단계: 효과(Effect) 훅 레이어 — GDD "기술 노트" 반영

GDD에 추가된 기술 노트(2026-07-04)가 2단계("데이터 기반 SO")의 실제 설계를 지정함. 초기 지시의 "TowerAbility/EnemyAbility"라는 이름은 기술 노트의 `ITowerEffect`/`TowerEffectBase` 네이밍으로 대체됨 (더 구체적인 최신 스펙이므로 이쪽을 따름).

- **계약(인터페이스) + 편의(추상 클래스) 병행**: `ITowerEffect`(계약, 독립성 보장) / `TowerEffectBase : ScriptableObject, ITowerEffect`(빈 기본 구현 제공). 실제 효과는 필요한 훅만 오버라이드.
- **훅 시점**: 공격 타워=`OnTick`(주기 반복), 스킬 타워=`OnAllyEnterRange`/`OnAllyExitRange`(아군 타워 사거리 출입 감지), 파워 타워=`OnBuild`(건설 즉시 1회).
- **스킬 타워 버프 = 직접 가감 아님, 파이프라인 조회**: `ITowerAura.ModifyOutgoingDamage(float)`. 공격 타워는 자신에게 걸린 `List<ITowerAura>`를 들고 있다가 데미지 계산 시점에만 조회 — 사거리 이탈 시 자동 소실, 중첩도 안전.
- **Enemy도 동일 패턴으로 설계** ({{user}} 확인, 2026-07-04: Project Vertex 개발 당시 EnemyAction도 CardEffect의 거의 미러였음): `IEnemyEffect` / `EnemyEffectBase`. 훅은 Tower와 이름은 다르지만 성격은 대응: `OnSpawn`(≈OnBuild, 스폰 시 1회) / `OnTick`(반복) / `OnDealContactDamage(ctx, IDamageable target)`(≈아군 사거리 출입에 대응하는 "Enemy 고유 상호작용 시점" — 접촉 피해를 주는 순간) / `OnDeath`(사망 시 1회, 정리·보상 트리거용으로 추가).
- **StatusType은 이 프로젝트용으로 새로 정의** ({{user}} 확인: Project Vertex 원본 소스 미보유라 그대로 재활용 불가). 의미:
  - `Weak`: 대상이 주는 피해 감소
  - `Vulnerable`: 대상이 받는 피해 증가
  - `Poison`: 매 틱 고정 피해 (DoT)
  - `Slow`: 이동속도 감소 (카드14 빙결 오라 타워가 사용 — 지속시간이 아니라 스킬 타워 아군 로직처럼 "사거리 안에 있는 동안"만 적용되는 aura형 적용 예정)
  - `Regen`: 매 틱 고정 회복 (카드15 재생의 축 — 거점 대상)
  - `Strength`: 공격력 고정치 증가
  - `Dexterity`: 예약값. 이 게임엔 블록/방어 스탯 개념이 없어 현재 대응되는 효과 없음 — 필요해지면 의미를 재정의할 것. 그 전까지는 사용하지 않음.
  - 상태이상의 지속시간 추적/스택 규칙(예: `StatusEffectInstance`)은 이번 단계에 포함하지 않음 — Poison/Regen처럼 실제로 틱 기반 상태이상을 쓰는 구체 효과를 만들 때 필요한 만큼만 설계해서 추가 (지금 만들면 쓰이지 않는 반쪽 구현이 됨).
- **`IDamageable`**: `OnDealContactDamage`에서 피해 대상을 추상화하기 위해 최소 계약(`TakeDamage(float)`)만 정의. 플레이어/거점이 구현 예정 (실제 MonoBehaviour 구현체는 매니저/컨트롤러 단계에서 작성).
- **TowerInstance/TowerContext, EnemyInstance/EnemyContext**: 기술 노트가 `CardContext` → `TowerContext`로 이름만 바꾸는 것을 제안했지만 원본 `CardContext`가 없어 필요한 최소 필드만 새로 설계함 (`self` 런타임 인스턴스, `deltaTime`). 얘네는 4계층 중 어디에도 속하지 않는 "런타임 상태" 개념이라 별도 `RCCom.Runtime` 네임스페이스로 분리 (Data=설계 스탯, Runtime=게임 중 변하는 상태).

### 다음 작은 단위 (보류, 다음 턴에 진행)
- Tower/Enemy "Definition" SO 래퍼: `TowerData`(또는 그 파생) + `List<TowerEffectBase>`를 묶어 인스펙터에서 드래그 조립할 수 있는 실제 SO 에셋 클래스. 이번 턴엔 계약(인터페이스/베이스클래스)까지만 만들고, 이 래퍼는 다음 단계에서 설계 확인 후 진행.
- 구체 효과 구현체(DamageEffect, AuraBuffEffect, GlobalBuffEffect 등)도 다음 단계.

## 3단계: Tower Definition SO + 기본 효과 구현체 ({{user}} 지시: "타워 쪽 먼저")

Tower/Enemy 중 Tower를 먼저 끝까지(Definition SO + 3종 기본 효과) 완성. Enemy 쪽 Definition/구체 효과는 아직 미착수.

### TowerDefinition (`Assets/Scripts/Definitions/Tower/TowerDefinition.cs`)
- 추상 베이스 `TowerDefinition : ScriptableObject` (`effects` 리스트 + `abstract TowerData Data { get; }`) + `AttackTowerDefinition`/`SkillTowerDefinition`/`PowerTowerDefinition` 3개 concrete 클래스. `TowerData`가 상속으로 나뉜 것과 같은 이유(다형 직렬화 회피, 타입별 필드 차이)로 Definition도 동일하게 나눔.
- 매니저 등 다른 시스템은 concrete 타입을 몰라도 `TowerDefinition.Data`/`effects`만으로 동작 가능.

### TowerInstance/TowerContext 확장
- `TowerInstance.definition`(TowerDefinition 참조, `Data` 프로퍼티로 위임) + `cooldownRemaining`(공격 효과 타이머). 효과(SO)는 여러 인스턴스가 공유하는 상태 없는 자산이라, 인스턴스별로 달라지는 값은 전부 `TowerInstance`(런타임) 쪽에 둬야 한다는 원칙을 여기서도 적용.
- `TowerContext.activeEnemies`: 현재 살아있는 적 전체 목록. 사거리 필터링은 효과 쪽(타워 종류별 사거리 의미가 다름)에서 직접 수행하도록 해 매니저는 "전체 목록 제공"만 책임지도록 역할을 나눔.

### ITowerAura 확장: ModifyAttackInterval 추가
- `SkillTowerData.attackSpeedBuffMultiplier`를 실제로 반영할 파이프라인 지점이 기술 노트 스펙엔 없어서(`ModifyOutgoingDamage`만 정의) 대칭으로 `ModifyAttackInterval(float baseInterval)`을 추가. 데미지와 동일한 파이프라인 철학(직접 가감 아님, 조회 시점에만 적용) 유지.

### 구현 중 발견한 설계 간극과 해결: ITowerAura가 컨텍스트를 안 받는 문제
- 기술 노트의 `ITowerAura.ModifyOutgoingDamage(float baseDamage)`는 파라미터가 baseDamage 하나뿐이라, 버프를 "누가 제공했는지" 그 자리에서 알 수 없음. 그런데 `TowerEffectBase`(SO)는 여러 타워 인스턴스가 공유하는 상태 없는 자산이어야 해서, 효과 자산 자체에 버프 값을 박아둘 수 없음.
- **해결**: `AuraBuffEffect`가 `OnAllyEnterRange`/`OnAllyExitRange` 시점에 (제공자 TowerInstance, 대상 TowerInstance) 쌍을 key로 하는 `Dictionary`에 작은 `Aura`(ITowerAura 구현, private nested class) 인스턴스를 만들어 보관하고, 그 `Aura` 객체를 대상의 `activeAuras`에 Add/Remove. 인터페이스 시그니처는 기술 노트 그대로 유지하면서, 같은 정의(Definition)를 쓰는 스킬 타워가 여러 대 지어져도 값이 섞이지 않게 함.
- `GlobalBuffEffect`도 동일 원칙: `OnBuild` 시점에 캡처한 값을 가진 `GlobalDamageAura` 인스턴스를 만들어 `GlobalTowerAuraRegistry.Auras`(전역 static 리스트)에 등록. 파워 타워는 사거리 개념이 없으므로 스킬 타워의 "로컬 리스트" 대신 "전역 리스트"에 등록한다는 점만 다르고, 데미지 계산 시점에 조회하는 파이프라인 자체는 스킬 타워와 완전히 동일 (`DamageEffect.CalculateDamage`가 `self.activeAuras`와 `GlobalTowerAuraRegistry.Auras`를 모두 순회).

### GlobalTowerAuraRegistry (`Assets/Scripts/Runtime/GlobalTowerAuraRegistry.cs`)
- **임시방편으로 인지하고 있음**: TowerManager(4단계, 아직 없음)가 생기기 전까지 전역 버프를 모아둘 곳이 없어 static 컨테이너로 둠. Manager 도입 시 인스턴스 필드로 옮기고, 플레이 세션 시작 시 `Clear()` 하도록 바꿀 것 (에디터 도메인 리로드로 static 값이 남아있을 수 있음 — 지금 당장 문제가 되진 않지만 Manager 단계에서 반드시 정리).

### 구체 효과 3종 (`Assets/Scripts/Effects/Tower/Concrete/`)
- `DamageEffect` (공격 타워, OnTick): 쿨다운 감소 → 사거리 내 최근접 적 탐색 → `activeAuras`+전역 레지스트리로 데미지/공격주기 파이프라인 적용 → `IDamageable.TakeDamage` 호출. 투사체 등 시각 표현(렌더러 프리팹)은 포함하지 않음 — 다음 레이어에서 이 효과가 실제로 맞췄다는 걸 알리는 이벤트/콜백을 붙이거나, 매니저가 결과를 보고 시각효과를 트리거하는 방식으로 연결할 것.
- `AuraBuffEffect` (스킬 타워, OnAllyEnterRange/Exit): 위 설계 간극 해결 방식대로 구현.
- `GlobalBuffEffect` (파워 타워, OnBuild): 위 설계 간극 해결 방식대로 구현. `upgradeIncrement`(카드로 누적 강화)는 아직 미소비 — 업그레이드 카드 시스템 단계에서 등록된 `GlobalDamageAura.bonus`를 직접 증가시키는 방식으로 연결 예정.

### EnemyInstance가 IDamageable을 구현하도록 변경
- `DamageEffect`가 적에게 데미지를 넣으려면 적도 `IDamageable`이어야 함. 애초 문서엔 "플레이어/거점이 구현 예정"이라고만 적었었는데, 적도 포함하도록 범위를 넓힘 (당연한 확장이라 별도 질의 없이 진행).

### 아직 안 한 것 (다음 단계 후보)
- ~~Enemy 쪽 Definition SO + 구체 효과~~ → 아래 4단계에서 완료.
- 실제로 이 모든 걸 프레임마다 호출하는 TowerManager (OnTick 매 프레임 호출, OnBuild 건설 시 1회 호출, OnAllyEnterRange/Exit 판정 로직, activeEnemies 갱신 등) — 지금까지 만든 건 "로직은 맞지만 아무도 호출 안 하는" 상태. Definition 에셋 실제 생성(Attack/Skill/Power 기본 타워 3종 + 효과 SO 에셋들) + Manager 구현이 다음 우선순위.
- 그리드(Tilemap) 배치 시스템과의 연결 — TowerInstance.position은 있지만 그리드 슬롯 좌표 변환은 아직 없음.

## 4단계: Enemy Definition SO + 기본 효과 ({{user}} 지시: "Enemy도 Tower와 같은 패턴으로")

### EnemyDefinition (`Assets/Scripts/Definitions/Enemy/EnemyDefinition.cs`)
- `TowerDefinition`과 달리 상속 없이 concrete 클래스 하나(`EnemyDefinition : ScriptableObject` — `EnemyData data` + `List<EnemyEffectBase> effects`). 이유: `EnemyData`가 애초에 `EnemyKind`로 종류만 구분하는 단일 클래스로 설계됐기 때문(베이스 3종은 로직이 아니라 수치 차이) — Tower처럼 타입별 필드가 갈리지 않아 다형 직렬화 문제 자체가 없음.

### EnemyInstance가 EnemyDefinition을 참조하도록 변경
- `TowerInstance.definition → Data` 패턴과 동일하게 `EnemyInstance.definition(EnemyDefinition) → Data(EnemyData)` 프로퍼티로 위임하도록 변경 (기존엔 `EnemyData data` 필드를 직접 들고 있었음). 대칭성 유지 목적 — Enemy는 다형성 문제가 없어서 꼭 필요한 변경은 아니었지만, Tower와 동일한 접근 패턴(`ctx.self.Data`)을 쓰게 해서 효과 코드 스타일을 통일함.

### 구체 효과: ContactDamageEffect만 우선 구현 (`Assets/Scripts/Effects/Enemy/Concrete/ContactDamageEffect.cs`)
- GDD "다이" 섹션(플레이어 접촉 시 1회성 고정 피해, 어그로/타겟팅 없음)을 그대로 반영. `OnDealContactDamage(ctx, target)` 훅 하나만 오버라이드.
- 나머지 훅(OnSpawn/OnTick/OnDeath)은 베이스 3종에는 아직 쓸 데가 없어 구현체를 만들지 않음 — Tower가 3개 훅 전부에 대응하는 기본 효과가 있었던 것과 달리, Enemy는 현재 "접촉 피해"라는 상호작용 지점 하나만 확정 기획이라 그것만 구현 (향후 정예/무리, 보스, 독 타워 연동 등에서 나머지 훅에 대응하는 효과가 추가될 것).
- **연속 피격 방지(피격 후 짧은 무적)는 이 효과의 책임이 아님**: 대상(플레이어)의 `IDamageable.TakeDamage` 구현이 자체 무적 타이머를 갖고 스스로 걸러내야 함 — 플레이어 컨트롤러 구현 시 반드시 반영할 것 (지금 잊으면 GDD가 명시한 "동일 적에게 연속 피격 방지" 요구사항이 빠지게 됨).

## 5단계: 매니저 아키텍처 원칙 (GDD 기술 노트, Project Vertex 코드 분석 기반) — 이전 설계 일부 정정

{{user}} 확인: "그리드 처리는 MapManager가 맞고, Tower/Enemy 매니저는 굳이 필요한가 싶다" → GDD 기술 노트에 Project Vertex(이전 프로젝트) 실제 코드 분석 결과가 추가됨. **핵심 원칙: 매니저는 "게임 흐름 단계"당 1개만 두고, "오브젝트 타입"별 매니저(EnemyManager, TowerManager 등)는 만들지 않는다.** 개별 오브젝트가 스스로 상태/로직을 들고 이벤트로 알리고, 매니저는 리스트 순회나 흐름 조율만 한다.

**최종 매니저 구성**: `GameManager` / `MapManager`(그리드·배치) / `WaveManager`(웨이브 절차적 생성 + 적 리스트 순회) / `CardManager`(업그레이드 카드) — 4개뿐. TowerManager, EnemyManager 없음.

**개체별 처리 방식이 Tower/Enemy가 서로 다름** (개체 수 차이 때문):
- **Enemy = 순수 C# 인스턴스** (MonoBehaviour 아님, 성능 이유 — 실시간 웨이브라 동시 개체 수가 Vertex의 턴제 전투(~5)보다 훨씬 많을 수 있어 `MonoBehaviour.Update()` 오버헤드가 치명적). `EnemyInstance`가 스스로 `Tick(dt)`, `TakeDamage`를 갖고 `Damaged`/`Died` 이벤트로 알림. `WaveManager`가 `List<EnemyInstance>`를 들고 자기 `Update()`에서 직접 순회하며 `Tick()` 호출 (별도 EnemyManager 없음). `EnemyView`(MonoBehaviour, 스프라이트)는 로직 없이 매 프레임 `Instance.position`만 읽어 `transform.position`에 반영하고 `Died` 구독해 자기 파괴.
- **Tower = 일반 MonoBehaviour + Collider2D 트리거**. 설치 슬롯 제한(공격3/스킬1/파워2, 카드로 확장해도 6~8개 수준)으로 개체 수가 원래 적어 성능 문제가 없음 — 억지로 순수 C#화할 필요 없음. 각 타워가 자기 `Awake()`/`Update()`/`OnTriggerEnter2D`/`OnTriggerExit2D`에서 직접 `ITowerEffect` 훅을 호출 (별도 TowerManager 없음). 아군 타워 사거리 출입(스킬 타워)과 적 사거리 진입(공격 타워) 둘 다 같은 방식(Collider2D 트리거)으로 감지 — 단, 적을 감지하려면 `EnemyView`도 Collider2D를 가지고 있어야 함(트리거 감지엔 최소 한쪽이 Rigidbody2D 필요 — 프리팹 설정 시 유의).

### 이 결정이 이미 만든 코드에 미치는 영향 → 정정
- **`TowerInstance`를 순수 C#에서 `MonoBehaviour`로 전환**. 원래 Enemy와 대칭 맞추려고 순수 C#으로 설계했었는데, 위 원칙에 따르면 이건 잘못된 선택 — Tower는 개체 수가 적어서 그냥 MonoBehaviour가 맞음. `position`은 이제 `transform.position`을 그대로 쓰는 `Position` 프로�터티로 바뀜 (`DamageEffect.cs`의 `ctx.self.position` 참조도 `Position`으로 수정).
- **`TowerContext.activeEnemies`는 이제 매니저가 아니라 타워 자기 자신이 Collider2D 트리거로 채운다** — 필드 존재/타입/소비 방식(`DamageEffect`)은 그대로, "누가 채워주는지"만 바뀜(매니저 → 타워 자신).
- **`ITowerEffect`/`TowerEffectBase`/`ITowerAura`/`TowerDefinition`/`DamageEffect`/`AuraBuffEffect`/`GlobalBuffEffect`는 변경 없음** — 이 원칙은 "누가 Tick을 호출하고 범위를 감지하는가"에 대한 것이지 "효과를 어떻게 데이터 기반으로 조립하는가"와는 별개 문제라서 그대로 유효함.
- **`EnemyInstance`에 `Tick(dt)`/`Damaged`/`Died` 이벤트 추가 + `EnemyView`(MonoBehaviour) 신설** — 기존엔 효과 훅(OnSpawn/OnTick/OnDealContactDamage/OnDeath)만 정의돼 있고 아무도 호출을 안 했는데, 이제 `EnemyInstance` 스스로 `Spawn()`/`Tick()`/`TakeDamage()`/`DealContactDamageTo()` 메서드에서 자기 `definition.effects`를 구동함. `WaveManager`(아직 미구현)가 이 인스턴스들의 리스트를 들고 순회하며 `Tick()`만 불러주면 됨.
- **`GlobalTowerAuraRegistry`**: 애초 "TowerManager 도입 시 옮길 것"이라고 적어뒀는데, TowerManager 자체가 생기지 않으므로 **GameManager의 인스턴스 필드로 옮길 것**으로 정정. GameManager도 아직 없어서 지금 당장은 그대로 static 유지.

### 코드 반영 완료
- `TowerInstance`(`Assets/Scripts/Runtime/TowerRuntime.cs`): `MonoBehaviour`로 전환. `Awake()`에서 `OnBuild`, `Update()`에서 `OnTick`, `OnTriggerEnter2D/Exit2D`에서 아군(`TowerInstance`)이면 `OnAllyEnterRange/Exit`, 적(`EnemyView`)이면 `_enemiesInRange` 목록만 갱신(공격 타워용). `Position`은 `transform.position` 프록시 프로퍼티.
- `EnemyInstance`(`Assets/Scripts/Runtime/EnemyRuntime.cs`): 순수 C# 유지, `Spawn()`/`Tick(dt)`/`DealContactDamageTo(target)`/`TakeDamage(amount)` 메서드가 각각 `OnSpawn`/`OnTick`/`OnDealContactDamage`/`OnDeath` 효과 훅을 스스로 구동. `Damaged`/`Died` 이벤트 추가.
- `EnemyView`(`Assets/Scripts/Runtime/EnemyView.cs`, 신규): MonoBehaviour, 로직 없이 `LateUpdate()`에서 `Instance.position`을 `transform.position`에 반영하고 `Died` 구독해 자기 파괴.
- `DamageEffect.cs`: `ctx.self.position` → `ctx.self.Position`으로 수정 (TowerInstance가 MonoBehaviour가 되며 프로퍼티명 변경).
- `GlobalTowerAuraRegistry.cs`: 주석을 "TowerManager 도입 시" → "GameManager 도입 시"로 정정 (TowerManager는 만들지 않기로 확정됐으므로).

### 아직 없음 (다음 단계)
- `GameManager`/`MapManager`/`WaveManager`/`CardManager` 자체 — 지금까지 만든 Tower/Enemy 로직을 실제로 살아 움직이게 하려면 최소한 `WaveManager`(적 스폰 + `List<EnemyInstance>` 순회하며 `Tick()` 호출)와 `MapManager`(그리드 슬롯 계산 + 타워 프리팹 배치)가 필요.
- 적 이동(웨이포인트 추종) 로직 — `EnemyInstance.Tick()`에 자리는 마련해뒀지만 실제 이동 계산은 비어있음.
- Tower/Enemy 프리팹 실제 생성 및 Collider2D/Rigidbody2D 세팅 — 지금은 스크립트 계약만 존재.

## 6단계: 플레이어 컨트롤러

Player는 개체가 하나뿐이라 Tower와 같은 이유로 그냥 `MonoBehaviour`(`PlayerController`)로 둔다.
Tower/Enemy처럼 Definition+Effect 레이어를 새로 만들지는 않았음 — 이유: Tower/Enemy는
"교체 가능한 여러 종류"(공격/스킬/파워, 일반/돌진/탱커)가 있어서 전략 패턴(효과 SO)이 필요했지만,
Player는 영원히 하나의 정체성이고 카드로 받는 변화도 전부 숫자 비율 조정(공격력 +15% 등)이라
효과 훅 시스템이 필요 없음 — `PlayerData`(이미 있음)를 인스펙터에 직접 물리는 것으로 충분.

### 사거리 판정: 몸통 히트박스 vs 원거리 사거리 콜라이더 분리
- 접촉 피해(GDD "다이" 섹션, 근접 거리에서만 발생)와 원거리 자동 공격 사거리는 반경이 서로
  다르다. Unity는 한 오브젝트에 콜라이더가 여러 개 있어도 `OnTriggerEnter2D`에서 "내 콜라이더
  중 어느 것이 부딪혔는지" 구분해주지 않으므로, 원거리 사거리용 큰 콜라이더는 자식 오브젝트로
  분리하고 `AttackRangeTrigger`(`Assets/Scripts/Runtime/AttackRangeTrigger.cs`, 신규)라는
  최소 릴레이 컴포넌트로 이벤트만 부모에 전달한다. 플레이어 본체의 콜라이더는 몸통
  히트박스(접촉 피해 판정용)로 남긴다.
- 접촉 피해 판정 자체는 `PlayerController`가 아니라 `EnemyView`가 담당하도록 함(적이 플레이어를
  건드렸을 때 `EnemyInstance.DealContactDamageTo`를 호출) — `EnemyView.OnTriggerEnter2D`가
  `IDamageable`을 구현한 아무 대상(플레이어든 나중에 만들 거점이든)에게 범용으로 반응하도록
  작성해 거점이 생겨도 코드 변경이 필요 없게 함.

### 공유 유틸리티로 뺀 것: EnemyTargeting
- "후보 목록에서 사거리 내 가장 가까운 적 찾기"는 공격 타워(`DamageEffect`)와 플레이어 기본
  공격이 완전히 동일한 로직이 필요해서, `Assets/Scripts/Core/EnemyTargeting.cs`(신규)로 추출해
  양쪽에서 재사용. `DamageEffect`의 기존 private 메서드는 제거하고 이 유틸리티 호출로 교체.

### 입력: 기존 InputSystem_Actions 재사용, 세부는 임시 결정
- 프로젝트에 이미 있는 `InputSystem_Actions.inputactions`(Unity 기본 템플릿)의 `Player` 액션맵
  중 `Move`(이동)를 그대로 사용. 기본 공격은 GDD상 "자동 발동"(사거리 내 최근접 적, 쿨다운
  기반)이라 별도 입력이 필요 없어서, 남는 `Attack` 버튼 액션을 스킬 발동으로 재사용하기로
  임의 결정함 (GDD가 스킬 조작 방식을 확정하지 않았고, 액션 이름과 실제 용도가 다르다는 점은
  인지하고 있음 — 나중에 카드 시스템 등에서 별도 액션이 필요해지면 이름을 정리할 것).

### 아직 없음 (다음 단계)
- 실제 씬에 Player GameObject 배치 + Collider2D/Rigidbody2D/자식 AttackRangeTrigger 오브젝트
  구성 (인스펙터 수동 작업, {{user}} 몫).
- 투사체 시각 표현(렌더러 프리팹) — 지금은 데미지 계산만 있고 시각 효과 없음.
- 거점(수비 대상) — `IDamageable`만 구현하면 `EnemyView`의 접촉 피해 판정이 자동으로 적용됨.
- `CurrentHealth <= 0` 감지 → 패배 처리는 GameManager 몫 (아직 없음).

## 7단계: MapManager

4개 매니저 중 첫 번째 실제 구현 (`Assets/Scripts/Managers/MapManager.cs`, `RCCom.Managers` 네임스페이스 신설 — Data/Core/Runtime/Effects/Definitions와 구분되는 "게임 흐름 단계 매니저" 전용 폴더).

### 설치 슬롯(그리드)과 웨이포인트(자유 좌표)를 서로 다른 데이터로 분리
- {{user}} 지침("타워 설치는 그리드, 적/플레이어 이동은 그리드 무관")을 그대로 반영: `slotTilemap`(Unity Tilemap, 셀 좌표)과 `waypoints`(Transform 배열, 자유 좌표)를 완전히 별개 필드로 둠. 웨이포인트는 스테이지당 고정이라 `Awake()`에서 `Vector2[]`로 한 번만 캐싱해 `Waypoints` 프로퍼티로 노출 — 실제 "웨이포인트를 따라 이동하는" 계산 로직은 아직 없음 (WaveManager/EnemyInstance.Tick 쪽 다음 단계 몫).
- 슬롯은 물리적으로 "칠해진 셀이냐 아니냐"만 구분하고 특정 타워 종류로 제한하지 않음 — GDD가 "경로 주변에 설치 슬롯 배치"라고만 했지 슬롯별 종류 제한을 언급하지 않았고, 종류별 개수 제한(공격3/스킬1/파워2)은 슬롯이 아니라 전역 카운터(`_attackTowerCount` 등)로 별도 관리하는 게 GDD 문구에 더 맞는 해석이라 판단.
- `IncreaseMaxSlots(kind, amount)`: 업그레이드 카드(9~11번, "OO 타워 증설")가 나중에 호출할 확장 지점만 미리 마련해둠.

### 씬 세팅 완료 + 검증 완료 (2026-07-04)
- 배경용 Tilemap(무료 에셋) + `SlotMarkers` Tilemap(논리용, 분리) + 웨이포인트 배치 완료.
- `Assets/Scripts/Debug/MapManagerDebugTester.cs`(임시 디버그용)로 검증: 클릭한 셀의 `CanBuild` 결과가 콘솔에 정상 출력, Scene 뷰에 웨이포인트 순서가 의도한 경로로 표시됨 — {{user}} 확인 완료.

### 아직 없음 (다음 단계)
- 웨이포인트를 실제로 따라 이동하는 로직 — `MapManager.Waypoints`는 데이터만 제공, 소비는 WaveManager/EnemyInstance 쪽.
- 타워 설치 UI(타입 선택 → 그리드 클릭 → `MapManager.TryGetSlotCell`+`CanBuild`+`Build` 호출) — Day2 체크리스트 항목, 아직 미착수.
- `GameManager`/`WaveManager`/`CardManager` 나머지 3개.
- 실제 Tower/Enemy 프리팹 제작 (Collider2D/Rigidbody2D 포함) — 지금까지는 스크립트 계약만 존재.

## 8단계: Day1 핵심 루프 마무리 (적 이동 / 거점 / 웨이브 스폰너 / 처치 보상)

Day1 체크리스트 중 비어있던 4개를 한 번에 처리. Tower 프리팹 조립 도중, 일정상 Day2인데 Day1 루프가 안 끝난 걸 점검하고 우선순위를 다시 여기로 돌림.

### 적 웨이포인트 추종 이동 (`EnemyRuntime.cs`)
- `EnemyInstance.Spawn(path, goal)`로 시그니처 변경 — 웨이포인트 목록과 "경로 끝에서 피해를 줄 대상(거점)"을 스폰 시점에 받는다.
- `Tick()`에서 `MoveAlongPath()` 호출: 현재 목표 웨이포인트로 `moveSpeed`만큼 이동, 도달하면 다음 웨이포인트로. 마지막 웨이포인트 도달 시 `DealContactDamageTo(goal)` 호출 후 `ReachedGoal` 이벤트 발생.
- **왜 물리 충돌이 아니라 "경로 완주"로 거점 피해를 처리했는가**: 처음엔 거점에도 Collider2D를 둬서 EnemyView의 기존 접촉 판정(플레이어와 동일한 방식)을 재사용할까 했으나, `Tick()`에서 위치를 순간 이동시키고 `ReachedGoal`로 View를 그 자리에서 파괴하면 물리 엔진이 트리거를 감지하기 전에 오브젝트가 사라져버리는 타이밍 경쟁이 생길 수 있음. 그래서 거점 피해만은 경로 완주 시점에 직접 호출하는 결정론적 방식으로 처리 — 플레이어 접촉 피해(물리 트리거, 아무 때나 어디서나 부딪힐 수 있음)와는 성격이 달라 별도 메커니즘이 타당하다고 판단.
- `Died`(처치)와 `ReachedGoal`(누수) 이벤트를 분리 — 처치 보상은 `Died`에서만 지급, 거점에 도달해 사라지는 경우는 보상 없음.
- `TakeDamage`에 `_isDead` 가드 추가 — 같은 프레임에 여러 공격원(타워+플레이어 등)이 동시에 데미지를 넣어도 `Died`가 중복 발생해 보상이 두 번 지급되는 걸 방지 (발견해서 같이 고침).

### BaseController — 거점 (`Runtime/BaseController.cs`, 신규)
- Player와 같은 이유로 그냥 MonoBehaviour (개체 하나, BaseManager 없음). `IDamageable` 구현, `maxHealth` 하나만 있는 단순 구조라 별도 Data 컨테이너 클래스로 안 뺐음 (필드 1개짜리 클래스는 과함).
- `Defeated` 이벤트만 노출 — 패배 조건 판정(거점 체력 0 또는 플레이어 체력 0)을 실제로 게임오버 처리하는 건 GameManager 몫(아직 없음).

### WaveManager (`Managers/WaveManager.cs`, 신규)
- Day1 범위: 고정 간격 순차 스폰 베타버전(예산 시스템은 Day2에 이 매니저를 확장해서 추가).
- `List<EnemyInstance>`를 직접 들고 자기 `Update()`에서 역순 순회하며 `Tick()` 호출 (Died/ReachedGoal 중 리스트에서 빠질 수 있어 역순).
- **최초 지시사항 반영**: "절차적 생성 웨이브 시스템은 난수 독립성·재현성 보장" 요구를 여기서 처음 실제로 반영 — `UnityEngine.Random` 대신 시드 고정된 전용 `System.Random`을 사용해, 다른 시스템의 Random 호출과 완전히 분리되고 같은 시드면 같은 스폰 결과가 재현되게 함.
- 처치 보상(Day1 "수치는 가짜값 OK" 허용 범위): `Died` 구독 시점에 골드/경험치 누계를 콘솔에 로그만 남김 — GameManager 도입 시 그쪽으로 이관 예정.

### EnemySpawnerDebug 갱신
- `EnemyInstance.Spawn()` 시그니처 변경에 맞춰 수정. `path`/`goal`을 `null`로 넘겨 제자리에 가만히 서있게 해서, 맵/거점 세팅 없이 적 1체만 두고 타워 효과 등을 격리 테스트할 때 계속 쓸 수 있게 함.

### 아직 없음 (다음 단계)
- 씬 배치: `BaseController` 오브젝트 생성, `WaveManager`에 `mapManager`/`baseController`/`spawnPool`(적 Definition들)/`viewPrefab`/`spawnInterval`/`randomSeed` 연결 (사용자 몫).
- 돌진형/탱커형 `EnemyDefinition` 에셋 (지금은 일반형만 있음) — `spawnPool`에 여러 종류를 넣으려면 필요.
- `GameManager`/`CardManager`, 웨이브 예산 시스템, UI — 여전히 다음 단계.

## 9단계: 파일당 클래스 1개로 전면 분리 ({{user}} 지적)

{{user}} 지적: ScriptableObject/MonoBehaviour처럼 실제 .asset/컴포넌트로 만들어지는 타입을 파일명과
다른 이름으로 한 파일에 여러 개 몰아두면, 나중에 그중 하나 이름을 바꾸는 순간 Unity가 그 클래스의
스크립트 참조(GUID+fileID)를 다시 못 찾아 이미 만든 에셋/프리팹이 "Missing Script"가 될 수 있음.

### 실제로 위험했던 파일 (SO/MonoBehaviour가 파일명과 다른 이름으로 여러 개 있었음)
- `Definitions/Tower/TowerDefinition.cs` — `TowerDefinition`(추상) + `AttackTowerDefinition`/`SkillTowerDefinition`/`PowerTowerDefinition`(전부 `[CreateAssetMenu]`로 실제 .asset이 되는 타입) 4개가 한 파일에.
- `Runtime/TowerRuntime.cs` — `TowerInstance`(MonoBehaviour, 프리팹에 붙는 실제 컴포넌트)가 파일명과 다른 이름으로 `TowerContext`와 함께 있었음.

### 위험하진 않지만 일관성 있게 같이 분리한 파일 (인터페이스/추상 클래스 — abstract라 직접 .asset이 되지 않음, 또는 MonoBehaviour/ScriptableObject가 아닌 순수 C# 필드용 데이터라 GUID+fileID 참조 자체가 없음)
- `Effects/Tower/TowerEffect.cs` → `ITowerEffect.cs` + `TowerEffectBase.cs`
- `Effects/Enemy/EnemyEffect.cs` → `IEnemyEffect.cs` + `EnemyEffectBase.cs`
- `Runtime/EnemyRuntime.cs` → `EnemyInstance.cs`(순수 C#, MonoBehaviour 아님) + `EnemyContext.cs`
- `Data/TowerData.cs` → `TowerKind.cs` + `TowerData.cs` + `AttackTowerData.cs` + `SkillTowerData.cs` + `PowerTowerData.cs`
- `Data/EnemyData.cs` → `EnemyKind.cs` + `EnemyData.cs`

### 규칙 (앞으로 계속 지킬 것)
**파일 1개 = 클래스/인터페이스/enum 1개, 파일명 = 타입명.** 특히 `ScriptableObject`/`MonoBehaviour` 상속 타입은 예외 없이 지킬 것 — 이미 만든 .asset/프리팹이 있는 상태에서 이 규칙을 어기고 나중에 이름을 바꾸면 데이터가 유실될 수 있음.

### 주의 (사용자 확인 필요할 수 있음)
혹시 이 정리 전에 `AttackTowerDefinition`/`SkillTowerDefinition`/`PowerTowerDefinition` .asset을 이미 만들어두셨다면, 파일 분리 자체만으로는 기존 .asset이 깨지지 않아야 정상이지만(같은 파일 안에서도 각 클래스는 원래 자기 이름으로 fileID가 계산되어 있었음), 혹시 Unity 콘솔에 Missing Script 경고가 뜨면 해당 에셋을 다시 만들어야 할 수 있음.

## Day1 핵심 루프 검증 완료 (2026-07-04)
{{user}} 확인: 적 스폰 → 웨이포인트 추종 이동(시작~끝) → 타워/플레이어 공격 → 피해/처치 보상까지 실제 Play 모드에서 정상 동작 확인. 원인이었던 버그(바인딩 안 된 EnemyView가 씬에 남아있던 것)도 해결.

**다음 세션 시작점**: 그리드에 타워 배치(빌드 UI) 구현 — `MapManager.TryGetSlotCell`(클릭 위치 → 셀) → `CanBuild`(종류별 슬롯 한도 확인) → `Build`(프리팹 생성) 순서로 이어붙이는 작업. 지금까지 클릭 판정 로직 자체는 `MapManagerDebugTester`로 검증 완료된 상태라, UI/입력 연결만 남음.

## 10단계: 타워 설치 UI (Day2)

### GameManager 최초 도입 (`Managers/GameManager.cs`, 신규)
- 지금까지 미뤄왔던 4번째 매니저를 최소 범위로 시작 — 골드(재화) 하나만 관리 (`AddGold`/`TrySpendGold`). 이유: 타워 건설 비용을 확인/차감하려면 "재화를 들고 있는 곳"이 있어야 하는데 그게 GameManager가 맞음.
- `WaveManager`의 처치 보상 지급이 자체 로그 누계 대신 `GameManager.AddGold`를 호출하도록 변경 (경험치는 아직 소비할 곳이 없어 임시 로그 유지 — CardManager/레벨업 붙을 때 이관).

### TowerBuildController (`Runtime/TowerBuildController.cs`, 신규)
- Player/Base와 같은 이유로 그냥 MonoBehaviour (별도 "TowerManager" 아님 — 타워 오브젝트 자체를 관리하는 게 아니라 "설치 입력 처리"만 담당하는 컨트롤러).
- 흐름: 숫자키(1/2/3)로 건설할 타워 프리팹 선택 → 클릭 → `MapManager.TryGetSlotCell` → `CanBuild` → `GameManager.TrySpendGold` → `MapManager.Build`. 각 단계 실패 사유를 콘솔 로그로 남겨 UI 없이도 테스트 가능하게 함.
- 타워 종류 선택 버튼 UI는 아직 없음 — Day2 UI/HUD 작업에서 별도로 붙일 것 (지금은 숫자키가 임시 대체).
- 클릭 입력은 `InputSystem_Actions`의 `UI/Click`(마우스 좌클릭) 액션을 재사용. Skill이 이미 `Player/Attack`을 쓰고 있어 겹치지 않게 구분.

### 씬 세팅 필요 (다음 액션)
- `GameManager` 오브젝트 생성, `WaveManager`에 연결
- `TowerBuildController` 오브젝트 생성: `Map Manager`/`Game Manager`/`Tower Prefabs`(1/2/3 순서로 Attack/Skill/Power 프리팹)/`Click Action`(UI/Click) 연결

## 11단계: 웨이브 절차적 생성 (Day2, GDD 예산 방식 반영)

`WaveManager`를 고정 간격 스폰에서 GDD "웨이브 난이도 생성 규칙"의 예산 기반 절차적 생성으로 확장.

### 반영한 것 (Day2 "필수" 범위)
- **예산 공식**: `예산(n) = baseBudget + budgetGrowthPerWave × n` (Inspector 필드로 노출, GDD 예시값 10+2n을 기본값으로).
- **적 코스트/등장 시점 필터링**: 기존에 만들어두고 안 쓰던 `EnemyData.waveCost`/`minWave`를 이제 실제로 소비 — 웨이브 번호 미달 종류는 후보군에서 제외.
- **소환 로직**: 후보군에서 예산 소진까지 반복 추첨해 그 웨이브의 스폰 큐(`List<EnemyDefinition>`)를 미리 생성. **가중치는 GDD 초안에 구체 수치가 없어 균등 확률로 둠** — 플레이테스트 후 조정 대상으로 명시.
- **스폰 간격 공식**: `max(minSpawnInterval, baseSpawnInterval - decay × n)`.
- **보스 웨이브 분기**: `n % bossWaveInterval == 0`일 때 예산에 `bossBudgetMultiplier` 적용. `bossDefinition`을 비워두면(지금 상태) 일반 소환 로직으로 대체 — 실제 보스 유닛 자체는 GDD에도 "여유시"라 이번엔 안 만듦, 필드만 미리 마련.
- **웨이브 사이 빌드 페이즈**: 스폰 큐 소진 + 생존 적 0마리 = "웨이브 클리어" 판정 → `buildPhaseDuration`초 대기 후 다음 웨이브 자동 시작. GDD 게임 루프의 "웨이브 클리어 → 다음 웨이브 대기" 반영 — 단, 지금은 플레이어가 누르는 "다음 웨이브 시작" 버튼이 없어 **자동 타이머**로 대체 (UI 붙을 때 수동 트리거로 바꿀 수 있음).

### 의도적으로 제외한 것 (GDD도 "여유시"로 명시)
- 정예/무리 수식어 시스템 (확률 공식은 GDD에 있지만, 적용할 스탯 배율/상태 적용 로직 자체가 아직 없어서 지금 만들면 반쪽 구현이 됨).
- 실제 보스 유닛 — 분기 지점(`bossDefinition`)만 마련.

### 안전장치 (구현 중 자체 발견)
- 예산 소진 루프에 안전 상한(500회) 추가 — `EnemyData.waveCost`를 실수로 0 이하로 두면 무한루프로 게임이 멎을 수 있어서, 이런 데이터 실수는 "일어날 수 있는 경계 조건"으로 보고 방어 코드 추가.

### 씬 세팅 필요
- 기존 `WaveManager` 오브젝트에 새로 생긴 Inspector 필드(예산/스폰간격/보스 관련) 값 확인만 하면 됨 — 새 오브젝트 추가는 없음.

## 12단계: EnemyView 프리팹 1개로 통합, 스프라이트는 데이터로

{{user}} 지적: 적 종류마다 EnemyView 프리팹을 따로 만드는 게 이상해 보인다 — 로직 차이가 없는데 프리팹만 복제하는 건 지금까지의 데이터 기반 설계 방향과 안 맞음. GDD "툴백 라인"(로직/프리팹 재사용, 그래픽만 교체) 문구와도 정확히 일치해서 반영.

- `EnemyDefinition`에 `public Sprite sprite;` 필드 추가 (스탯 옆에 시각 표현도 데이터로 취급).
- `EnemyView`가 `[RequireComponent(typeof(SpriteRenderer))]`를 갖고, `Bind()` 시점에 `instance.definition.sprite`를 자기 `SpriteRenderer`에 반영.
- 결과: EnemyView 프리팹은 **1개만** 만들면 됨. 적 종류별 외형 차이는 각 `EnemyDefinition` 에셋의 `Sprite` 필드만 다르게 채우면 됨 — 프리팹 복제 불필요.

### 씬/에셋 정리 필요 (다음 액션)
- 기존에 종류별로 별도 EnemyView 프리팹을 만들어뒀다면 하나만 남기고 나머지는 지워도 됨(또는 그 하나를 기준으로 통일).
- `WaveManager.View Prefab` 필드가 그 단일 프리팹을 가리키는지 확인.
- 각 `EnemyDefinition`(Normal 등) 에셋의 `Sprite` 필드 채우기. 돌진형/탱커형 Definition을 만들 때도 스탯과 함께 스프라이트만 다르게 넣으면 끝.

## 13단계: Tower/Enemy Roster SO 도입

{{user}} 결정: 지금 바로 도입 ("차후 개발 용이 + 독립성"). `WaveManager.spawnPool`/`TowerBuildController.towerPrefabs`처럼 매니저마다 배열을 직접 드래그하던 방식을, 프로젝트 창에서 관리하는 "모음 SO" 하나로 교체.

- `Definitions/Tower/TowerRoster.cs`(신규): `List<TowerInstance> towers` + `FindById(towerId)`.
- `Definitions/Enemy/EnemyRoster.cs`(신규): `List<EnemyDefinition> enemies` + `FindById(enemyId)`.
- 지금까지 정의만 해두고 안 쓰던 `TowerData.towerId`/`EnemyData.enemyId` 필드가 여기서 처음 실제로 쓰임 (ID 조회 용도).
- `WaveManager.spawnPool` → `enemyRoster`, `TowerBuildController.towerPrefabs` → `towerRoster`로 교체.
- **효과**: 나중에 카드 시스템이 새 타워/적 종류를 다뤄야 할 때, 각 매니저가 배열을 따로 안 들고 이 Roster 에셋 하나만 참조하면 됨 — 종류 추가/제거 시 고칠 곳이 한 군데로 줄어듦(독립성).

### 씬/에셋 세팅 필요 (다음 액션)
- `TowerRoster`/`EnemyRoster` 에셋 각각 생성 (Create → RCCom → Tower/Enemy → Tower/Enemy Roster)
- `TowerRoster.towers`에 기존 타워 프리팹들을(1/2/3 순서 유지) 옮겨 넣기
- `EnemyRoster.enemies`에 기존 EnemyDefinition들 옮겨 넣기
- `WaveManager`/`TowerBuildController` 인스펙터에서 예전 배열 필드 대신 새 Roster 필드에 각각 연결

## 14단계: 업그레이드 카드 시스템 (1~2단계: 레벨업 + CardManager 프레임워크)

{{user}} 결정: 카드 시스템(뱀서라이크 스타일) 진행. 4단계로 나눠서 진행 — ①레벨/경험치 ②CardManager 프레임워크 ③카드 효과 15개 ④선택 UI. 이번엔 ①②까지.

### ① 레벨/경험치 (`Managers/GameManager.cs` 확장)
- `Level`/`CurrentExp`/`AddExp(amount)`/`LeveledUp` 이벤트 추가. 레벨업 필요 경험치 공식 `baseXpToLevel + xpGrowthPerLevel × level`은 GDD가 "세부 수치 추후 논의"로 남겨둬서 웨이브 예산 공식과 같은 스타일의 초안값으로 임시 결정 (플레이테스트 조정 대상).
- 한 번에 여러 레벨을 넘을 수도 있어 `while` 루프로 처리(경험치 몰아 들어오는 경우 대비).
- `WaveManager.GrantReward`가 이제 `gameManager.AddExp(exp)`를 호출 (기존 로컬 로그 누계 제거).

### ② CardManager 프레임워크 (`Effects/Card/`, `Runtime/CardContext.cs`, `Managers/CardManager.cs`, 전부 신규)
- **GDD 기술 노트 재확인 반영**: Project Vertex의 `CardEffect`는 "선택되는 순간 1회 실행되고 끝"이라 Tower/Enemy 같은 여러 훅이 필요 없음 — 그 패턴 그대로 `ICardEffect`(계약) + `CardEffectBase : ScriptableObject`(SO, 이름/설명 직접 보유) 단일 `Apply(CardContext ctx)` 진입점으로 구현.
- 카드마다 로직이 전부 달라서(플레이어 강화/타워 강화/슬롯 확장/거점/신규 타워) Tower/Enemy처럼 재사용되는 Data 컨테이너를 따로 두지 않음 — `CardEffectBase`에 `displayName`/`description`을 바로 얹음.
- `CardContext`: 카드가 건드릴 수 있는 시스템 참조 모음(`PlayerController`/`TowerRoster`/`MapManager`/`BaseController`). `CardManager`가 선택 시점에 채워 넘김.
- `CardManager`: `GameManager.LeveledUp` 구독 → 카드 풀(`List<CardEffectBase>`)에서 중복 없이 최대 3장 추첨 → `Time.timeScale = 0`으로 일시정지 → 콘솔에 후보 로그 → **숫자키(1/2/3)로 임시 선택**(TowerBuildController와 같은 패턴, 실제 버튼 UI는 4단계) → 선택된 카드의 `Apply` 호출 → `Time.timeScale = 1`로 재개.
- 카드 추첨 난수는 `UnityEngine.Random` 그대로 사용 — GDD의 "RNG 독립성·재현성" 요구는 웨이브 시스템에 한정된 조건이라 카드에는 해당 없음.
- **일시정지가 저절로 다 됨**: `Time.timeScale=0`이면 `Time.deltaTime`이 0이 되어 `WaveManager`/`TowerInstance`/`PlayerController`/`EnemyInstance.Tick` 전부가 deltaTime 기반이라 자동으로 멈춤 — 각 컨트롤러에 별도 "일시정지 체크" 코드를 추가할 필요가 없었음.

### 다음 단계 (③④, 아직 안 함)
- 카드 15장 구체 효과 구현 (`Effects/Card/Concrete/`) — 비중: 플레이어 3/타워강화 5/슬롯확장 3/거점 1/신규타워 3. 슬롯확장 카드는 `MapManager.IncreaseMaxSlots`, 신규타워 카드는 `TowerRoster.towers`에 추가하는 식으로 이미 있는 API들과 바로 연결 가능.
- 카드 선택 버튼 UI (Day2 UI/HUD 작업과 겹침, 숫자키 임시 선택을 대체).

### 씬 세팅 필요
- `CardManager` 오브젝트 생성 후 `Game Manager`/`Player`/`Tower Roster`/`Map Manager`/`Base Controller` 연결 (`Card Pool`은 ③단계에서 카드 에셋 만든 뒤 채우기).

## 15단계: 업그레이드 카드 15장 구체 효과 (③단계) + 이를 위해 필요했던 하부 확장

카드 효과를 실제로 구현하면서, 기존 시스템 몇 곳에 "카드가 실제로 작동하려면 필요한" 확장이 필요했음을 발견해 같이 처리.

### 발견한 문제와 해결
1. **사거리 카드가 눈에 안 보이는 문제**: `AttackTowerData.attackRange`/`SkillTowerData.buffRange`를 카드로 늘려도, 실제 감지 범위는 각 타워의 물리 `Collider2D` 반경이 결정하는데 그건 그대로였음. → `TowerInstance`에 씬의 모든 타워를 추적하는 정적 `All` 리스트와 `RefreshRangeCollider()`를 추가해서, 사거리 카드가 데이터를 바꾼 직후 이미 지어진 모든 타워의 콜라이더도 같이 갱신하도록 함.
2. **"축적된 힘" 카드가 이미 지은 파워 타워엔 적용 안 되는 문제**: `GlobalBuffEffect.OnBuild`가 건설 시점에 `globalDamageBonus` 값을 오라 인스턴스에 "스냅샷"으로 캡처해두는 구조라, 이후 카드로 값을 늘려도 이미 등록된 오라는 그대로였음. → `GlobalBuffEffect.IncreaseAllGlobalBonuses(roster, amount)` 정적 헬퍼 추가: 이미 등록된 오라들의 `bonus`도 직접 늘리고, Definition 쪽 기본값도 같이 늘려서 향후 지어질 타워에도 반영되게 함.
3. **"빙결 오라" 타워는 Tower의 아군 출입 훅(OnAllyEnterRange/Exit)을 못 씀**: 그 훅은 아군 타워 전용이라 적에게는 없음. → `EnemyInstance`에 지속시간 기반 `_speedMultiplier`를 추가하고, `SlowAuraEffect`가 `OnTick`마다 사거리 안의 적에게 짧은 지속시간(0.5초)의 슬로우를 계속 갱신하는 방식으로 우회 — 사거리를 벗어나면 갱신이 끊겨 자연히 만료됨.
4. **"재생의 축" 타워가 씬의 거점을 참조할 방법이 없었음**: SO(효과 에셋)에 씬 오브젝트 참조를 직접 넣는 건 어색함. → `BaseController`에 `static Instance` 싱글톤 추가 (씬에 하나뿐임이 보장되므로 정당한 사용).

### 신규 타워 효과 3종 (`Effects/Tower/Concrete/`)
- `PierceDamageEffect`: 최근접 적 방향으로 가상의 직선을 긋고, 그 각도(`beamHalfAngleDegrees`) 안의 사거리 내 적 전부에게 동시 데미지 (카드13 관통 사격).
- `SlowAuraEffect`: 사거리 내 적에게 이동속도 배율 지속 갱신 (카드14 빙결 오라).
- `BaseRegenEffect`: 매 틱 거점 체력 소량 회복 (카드15 재생의 축).
- 공통으로 쓰는 데미지/공격주기 파이프라인은 `DamageEffect`에서 `Effects/Tower/TowerDamageMath.cs`로 추출해 `PierceDamageEffect`와 공유 (중복 제거).

### 카드 15장 구현 (`Effects/Card/Concrete/`, 11개 파일)
개별 클래스 8개 + 공용 클래스 2개(파라미터만 다른 카드들을 하나로 묶음)로 15장 커버:
- `PlayerAttackDamageBoostCard`(1) / `PlayerMoveSpeedBoostCard`(2) / `PlayerSkillCooldownBoostCard`(3)
- `AttackTowerDamageBoostCard`(4) / `AttackTowerRangeBoostCard`(5)
- `SkillTowerBuffBoostCard`(6, 배율 자체가 아니라 "보너스 부분(배율-1)"을 25% 늘리는 방식 — 1.2 → 1.25) / `SkillTowerRangeBoostCard`(7)
- `PowerTowerBonusBoostCard`(8, `PowerTowerData.upgradeIncrement`를 증가폭으로 재사용)
- `TowerSlotExpansionCard`(9~11 공용, `TowerKind` 필드로 구분 — 로직이 완전히 같아 클래스 3개 대신 1개+에셋 3개)
- `BaseMaxHealthBoostCard`(12)
- `UnlockTowerCard`(13~15 공용, 해금할 프리팹을 인스펙터에 연결하는 방식 — 로직 공용, 어떤 프리팹인지만 다름)

### 씬/에셋 세팅 필요 (다음 액션)
- **카드 SO 에셋 15개 생성**: Create → RCCom → Card → (각 클래스). `TowerSlotExpansionCard`/`UnlockTowerCard`는 3개씩 만들어서 `Kind`/`Unlock Prefab`과 `Display Name`/`Description`을 직접 채울 것.
- **신규 타워 3종 프리팹/Definition 제작**: 관통 사격(Attack + PierceDamageEffect) / 빙결 오라(Skill + SlowAuraEffect) / 재생의 축(Power + BaseRegenEffect) — 각각 기존 프리팹 만들던 절차 그대로 반복하고, `UnlockTowerCard` 3개에 각각 연결.
- `CardManager.Card Pool`에 완성된 15장 에셋 전부 등록.

### 다음 단계 (④, 아직 안 함)
- 카드 선택 버튼 UI (숫자키 임시 선택을 대체) — Day2 UI/HUD 작업과 겹침.

## 16단계: Tower도 프리팹 1개로 통합 ({{user}} 지적)

{{user}} 지적: Enemy처럼 Tower도 프리팹을 종류별로 여러 개 만들지 말고 프리팹 1개 + definition만 갈아끼우면 되는 거 아니냐 — 맞는 방향이라 반영. 단, Enemy와 달리 한 가지 타이밍 문제가 있어 구조를 조금 바꿔야 했음.

### 왜 Enemy와 다르게 한 단계 더 필요했나
`TowerInstance.Awake()`가 곧바로 `definition.effects`를 읽어 `OnBuild`를 호출하는 구조였는데, Unity는 `Instantiate()` 하는 순간 그 자리에서 바로 `Awake()`를 실행해버린다. "프리팹 인스턴스화 → definition 나중에 주입" 순서로 하면 `Awake()` 시점엔 definition이 아직 비어있어 곧바로 NullReferenceException. Enemy는 `EnemyView`가 로직 없이 `Bind()`를 기다리기만 해서 문제가 없었지만, Tower는 `Awake()`가 이미 로직(OnBuild 호출)을 갖고 있어서 이 구조를 그대로 못 씀.

### 해결: OnBuild 호출을 Awake에서 Build()로 이동
- `TowerInstance.Awake()`는 이제 `All` 등록만 함. `OnBuild` 훅 호출, 스프라이트 반영, 콜라이더 반경 갱신은 새로 만든 `public void Build(TowerDefinition)` 메서드로 이동 — `MapManager.Build()`가 `Instantiate()` 직후 명시적으로 호출 (Enemy의 `EnemyView.Bind()`와 동일한 타이밍 해결 패턴).
- `_isBuilt` 플래그로 `Update()`/`OnTriggerEnter2D`가 `Build()` 호출 전에 실행되는 걸 방지 (같은 프레임 안에서는 발생 안 하지만, 방어적으로 추가).
- `TowerDefinition`에 `Sprite sprite` 필드 추가 (Enemy와 동일 패턴) — `Build()`가 이 값을 SpriteRenderer에 반영.

### 연쇄 수정
- `MapManager`: `TowerInstance towerPrefab` 필드 하나만 갖고, `Build(TowerInstance, cell)` → `Build(TowerDefinition, cell)`로 시그니처 변경 (내부적으로 공용 프리팹을 Instantiate하고 `.Build(definition)` 호출).
- `TowerRoster.towers`: `List<TowerInstance>` → `List<TowerDefinition>`로 변경 (프리팹이 아니라 "종류"만 모아두는 목록이 됨 — 개념적으로 더 자연스러움).
- `TowerBuildController`, 카드 6개(`AttackTowerDamageBoostCard`/`AttackTowerRangeBoostCard`/`SkillTowerBuffBoostCard`/`SkillTowerRangeBoostCard`/`PowerTowerBonusBoostCard`/`UnlockTowerCard`), `GlobalBuffEffect.IncreaseAllGlobalBonuses` — 전부 `TowerInstance prefab` 순회를 `TowerDefinition definition` 순회로 변경 (로직은 동일, 타입만 교체).

### 씬/에셋 세팅 변경 (다음 액션)
- 기존에 종류별로 여러 개 만들어뒀던 Tower 프리팹이 있다면 **1개만 남기고 정리** (SpriteRenderer+Rigidbody2D+CircleCollider2D+TowerInstance만 있으면 됨, definition은 비워둘 것 — Build 시점에 주입되므로).
- `MapManager`의 `Tower Prefab` 필드에 그 단일 프리팹 연결.
- `TowerRoster.towers`에는 이제 Definition 에셋들을 직접 등록 (프리팹 아님).
- 각 `TowerDefinition`(Attack/Skill/Power + 신규 3종)의 `Sprite` 필드 채우기.

## 17단계: CardRoster SO 도입

{{user}} 제안: 카드 풀도 Tower/EnemyRoster처럼 SO화. Tower/Enemy 때와 달리 지금은 CardManager 하나만 참조해서 "여러 매니저 공유" 근거는 약하지만, 씬 없이 프로젝트 창에서 15장 밸런싱을 확인/편집할 수 있는 편의 목적으로 도입 — 사용자도 그 트레이드오프에 동의하고 진행.

- `Definitions/Card/CardRoster.cs`(신규): `List<CardEffectBase> cards`.
- `CardManager.cardPool`(raw List) → `cardRoster`(CardRoster 참조)로 교체.

### 씬 세팅 필요
- `CardRoster` 에셋 생성 (Create → RCCom → Card → Card Roster), 카드 15장 등록.
- `CardManager` 인스펙터에서 `Card Roster` 필드 재연결.

## 18단계: UI/HUD (Day2 마지막 항목, 시간 압박으로 범위 축소)

{{user}} 지시: 카드 선택 UI + 나머지 HUD(체력바/골드/웨이브/타워 설치)를 한 번에. 마감이 임박해 범위를 최소로 축소.

### 축소한 이유
- 타워 설치 버튼: 종류가 고정(공격/스킬/파워, 숫자키 1/2/3과 동일)이라 전용 스크립트 없이 `TowerBuildController.SelectTower`를 public으로만 열어서, 버튼 3개의 OnClick()을 인스펙터에서 직접 연결하는 방식으로 처리 (신규 스크립트 불필요).
- 새 타워가 카드로 해금돼도 이 고정 3버튼 UI에는 안 뜸(알려진 제약, Day3 여유 있으면 동적 생성으로 개선) — 지금은 숫자키로도 여전히 선택 가능하니 기능 자체는 안 막힘.
- TextMeshPro 대신 레거시 `UnityEngine.UI.Text`/`Slider`/`Button` 사용 — 임포트 절차 없이 바로 쓸 수 있어서.

### 코드 구현 완료
- `WaveManager.CurrentWave`(public), `TowerBuildController.SelectTower`(public) 노출.
- `CardManager`에 `ChoicesPresented`/`ChoiceResolved` 이벤트 추가, `SelectChoice` public화. 숫자키와 버튼 둘 다 완전히 같은 경로를 타서 무엇으로 선택하든 동일하게 동작.
- `UI/HudController.cs`(신규): 플레이어/거점 체력바, 골드, 웨이브 번호. 매 프레임 폴링(최대체력이 카드로 바뀌어도 자동 반영).
- `UI/CardSelectionUI.cs`(신규): 카드 3장 버튼 동적 텍스트 채우기 + 선택 처리, `ChoiceResolved`로 패널 자동 닫힘.

### 씬 세팅 필요 (다음 액션, 아래 채팅에 상세 가이드)
- Canvas 1개, HUD 영역(체력바 2, 텍스트 2), 카드 선택 패널(버튼+텍스트 3세트), 타워 선택 버튼 3개.
- `HudController`/`CardSelectionUI` 오브젝트 생성 후 필드 연결.
- 타워 선택 버튼 3개의 OnClick()에 `TowerBuildController.SelectTower(0/1/2)` 직접 연결.

## UI 버그 수정 (2026-07-05)
- {{user}}가 HudController/CardSelectionUI 텍스트를 레거시 Text → TextMeshProUGUI로 전환 (그대로 유지).
- **CardSelectionUI가 시작부터 계속 떠 있던 문제**: 스크립트가 패널 자기 자신에 붙어있는데 `OnEnable()`에서 `panel.SetActive(false)`로 자기 자신을 끄면, 그 순간 Unity가 `OnDisable()`도 같이 호출해버려 이벤트 구독이 즉시 풀리고 다시는 안 켜지는 문제였음. `GameObject.SetActive` 대신 `CanvasGroup`(alpha/interactable/blocksRaycasts)으로 보이기/숨기기를 바꿔 해결 — 오브젝트 자체는 계속 활성 상태로 유지되어 이 문제가 사라짐. 초기 숨김도 `OnEnable` 대신 `Awake`로 이동.
- HudController NRE는 인스펙터 필드 미연결(Game Manager 등, 또는 Text→TMP 전환 후 재연결 누락) 문제로 진단 — 코드 변경 없음, 사용자 확인 필요.

## 웨이브 간 대기시간 10초 + 카운트다운 UI (2026-07-05)
- `WaveManager.buildPhaseDuration` 기본값 5→10초 (첫 웨이브 전 대기에도 동일 적용, 코드 변경 없이 이미 그렇게 동작하고 있었음).
- `WaveManager`에 `IsWaitingForNextWave`/`NextWaveCountdown`(빌드 페이즈 아닐 땐 0) public 프로퍼티 추가.
- `HudController`에 `waveCountdownText` 필드 추가 — 빌드 페이즈 중에만 "다음 웨이브까지 N초" 표시, 스폰 진행 중엔 자동으로 숨김.
- 씬 세팅 필요: HUD에 TMP 텍스트 하나 추가로 만들어서 `Wave Countdown Text`에 연결.

## 타워 해금: 교체 아닌 추가 방식으로 확정 (2026-07-05)
{{user}} 질문: 카드로 신규 타워 해금 시 기존 기본형을 교체할지 추가할지 — **추가(additive)로 확정**. 근거: GDD 문구가 "해금"이지 "교체"가 아님 + 슬롯 제한이 TowerKind 단위라 기본형/특수형이 이미 같은 슬롯 한도를 자연스럽게 나눠 쓰는 구조(코드 변경 없이 이미 그렇게 동작). 상황별로 골라 짓는 선택지가 생겨 전략적으로도 더 나음.
- **발견한 버그**: 숫자키가 1/2/3만 처리해서 4번째 이상 해금된 타워는 선택 자체가 불가능했음 → `TowerBuildController`에 4/5/6번 키 추가 (기본 3 + 신규 3 = 최대 6종 커버).
- UI 버튼은 아직 3개뿐이라 해금된 4번째 이후 타워는 숫자키로만 선택 가능 — 시간 남으면 버튼 추가, 아니면 이대로도 기능은 함.

## 빌드 메뉴를 스크롤 목록으로 전환 (2026-07-05)
{{user}} 제안: 고정 3버튼 대신 스크롤 가능한 목록으로 — 타워 종류가 몇 개든(기본 3 + 해금분) 대응 가능해서 반영.
- `UI/TowerBuildButton.cs`(신규): 버튼 1개, 스프라이트/이름/비용 표시 + 클릭 콜백.
- `UI/TowerBuildMenuUI.cs`(신규): `TowerRoster` 기준으로 버튼을 동적 생성해 스크롤뷰 Content에 채움. `CardManager.ChoiceResolved`(카드를 뭘 골라도 발생) 시점에만 다시 채움 — 매 프레임 폴링 안 함.
- 이전에 만든 "고정 버튼 3개 + 인스펙터 OnClick" 방식은 이걸로 대체 — 그 버튼들은 지워도 됨.

### 씬 세팅 필요
- 버튼 프리팹 1개 제작: Image(아이콘)+TMP 텍스트 2개(이름/비용)+Button, `TowerBuildButton` 스크립트 붙여서 필드 연결 → 프리팹화.
- `GameObject → UI → Scroll View` 생성, Horizontal 방향으로 설정(세로 스크롤바/이동은 꺼도 됨), Content 오브젝트에 `Horizontal Layout Group` + `Content Size Fitter`(Horizontal Fit: Preferred Size) 추가.
- 빈 오브젝트에 `TowerBuildMenuUI` 붙이고 `Tower Roster`/`Build Controller`/`Card Manager`/`Content Parent`(위 Content)/`Button Prefab` 연결.

## 빌드 버튼 프레임을 종류별 템플릿으로 (2026-07-05)
{{user}}가 Attack/Skill/Power 각각 이름이 baked-in된 프레임 이미지를 3장 준비 — `TowerBuildButton`에 `frame` Image 필드 추가, `TowerBuildMenuUI`가 `definition.Data.kind` 기준으로 3개 프레임 스프라이트 중 하나를 골라 넘겨줌. 관통/빙결/재생처럼 나중에 해금되는 타워도 kind만 맞으면 자동으로 올바른 프레임을 씀.

### 씬 세팅 필요
- 버튼 프리팹에 프레임용 자식 `Image` 오브젝트 추가(배경, 제일 뒤에) → `TowerBuildButton.Frame` 필드에 연결.
- `TowerBuildMenuUI`의 `Attack/Skill/Power Frame Sprite` 3개 필드에 각 템플릿 이미지 연결.

## 이미 해금된 타워 카드 재추첨 방지 (2026-07-05)
{{user}} 발견: 레벨업 카드 추첨 시 이미 해금한 "OO 타워 해금" 카드가 다시 뽑혀서 사실상 효과 없는 선택지가 되는 케이스 발생.
- `CardEffectBase.IsAvailable(CardContext)` virtual 메서드 추가 (기본값 true) — "지금 뽑을 가치가 있는 카드인지" 판단.
- `UnlockTowerCard`가 이를 재정의: 이미 `TowerRoster`에 들어있는 타워면 `false` 반환.
- `CardManager.PresentChoices`가 추첨 전에 `IsAvailable`로 후보를 걸러서, 이미 무의미해진 카드는 애초에 뽑히지 않도록 함.
- 다른 카드 종류(스탯 강화 등)는 몇 번을 뽑아도 유효해서 기본값(true) 그대로 — 필요한 카드만 재정의하면 되는 확장 가능한 구조.

## Day3 진입: 레벨/EXP HUD 추가 (2026-07-05)
- `GameManager.ExpRequiredForNextLevel()` private → public 전환 (HUD가 경험치 바 최대값으로 참조).
- `HudController`에 `levelText`/`expBar`/`expText` 추가, 매 프레임 갱신.

## 카메라 추적 + 배경 경계 클램프 (2026-07-05)
- `Runtime/CameraFollow.cs`(신규) — Day1 체크리스트 "여유시" 항목이었던 카메라 추적을, 배경 이탈 방지 요구사항과 함께 구현. 매니저 불필요한 카메라 전용 단일 컴포넌트라 Player/BaseController와 동일하게 그냥 MonoBehaviour 하나로 처리.
- 배경 경계는 별도 좌표 계산 없이 `Collider2D.bounds`를 그대로 활용: 배경 크기에 맞춘 BoxCollider2D(Is Trigger 체크, 물리 충돌 없음)를 씬에 두고 인스펙터에 드래그. 카메라의 `orthographicSize`/`aspect`로 half-extent를 구해 목표 위치를 Clamp, 배경이 시야보다 작은 축은 중앙 고정으로 폴백.
- **플레이어도 같은 경계를 벗어나면 안 된다는 요구사항 추가** ({{user}}): `PlayerController`에 `levelBounds`(Collider2D) 필드 추가, `Move()`에서 이동 후 위치를 그 bounds로 Clamp. `CameraFollow.Bounds`와 완전히 같은 오브젝트를 그대로 드래그해서 재사용하면 되므로 별도 경계 오브젝트를 새로 만들 필요 없음 — "맵 경계"라는 개념 하나를 카메라/플레이어 두 컴포넌트가 공유.

## 타워 철거(Alt+클릭) + 철거 비용 할인 카드 (2026-07-05)
{{user}} 제안: Alt+클릭으로 이미 지은 타워 철거, 단 환불이 아니라 건설비의 75% 정도를 "추가로" 내는 페널티 방식 (건설/철거 반복으로 슬롯 우회하는 것 방지). 이 비율을 카드로 할인해주는 것도 자연스러운 연장.

### 코드 구현 완료
- `GameManager.demolishCostRatio`(기본 0.75, private set) + `DemolishCostRatio` getter + `ReduceDemolishCostRatio(amount)`(0 밑으로는 안 내려감) — 경제 관련 전역 값이라 GameManager 소관(Gold와 같은 이유).
- `MapManager.TryGetTowerAt(cell, out TowerInstance)` / `RemoveTower(cell)`(슬롯·카운터 정리 + Destroy) 추가, 대칭으로 `DecrementCount` 신설.
- `TowerBuildController`: `Update()`가 Alt(좌/우 둘 다 인식) 여부로 `HandleBuildClick`/`HandleDemolishClick`을 분기. 클릭 위치→셀 변환 로직은 `TryGetClickedCell`로 공유 추출. 철거 비용은 `Mathf.CeilToInt(instance.Data.buildCost * gameManager.DemolishCostRatio)`.
- `CardContext`에 `gameManager` 필드 추가(`CardManager.BuildContext()`도 채우도록 수정) — 지금까지 카드가 경제 시스템(Gold/철거비)을 직접 건드릴 방법이 없었어서 필요.
- `Effects/Card/Concrete/DemolishCostDiscountCard.cs`(신규): `ReduceDemolishCostRatio` 호출, 기존 카드들과 동일한 단순 패턴.

### 씬/에셋 세팅 필요
- `DemolishCostDiscountCard` 에셋 생성 후 `CardRoster.cards`에 등록.
- 그 외 씬 변경 없음 — 기존 `TowerBuildController`/`GameManager`/`CardManager` 오브젝트 그대로 재사용.

## 건설 프리뷰 (커서 추적 반투명 스프라이트 + 설치 가능 여부 색상) (2026-07-05)
{{user}} 요청: 타워 선택 후 커서에 해당 definition 스프라이트가 반투명하게 따라오고, 설치 불가능한 곳은 빨갛게.

### 코드 구현 완료
- `MapManager.GetCellCenterWorld(cell)`(신규, `slotTilemap.GetCellCenterWorld` 공개 래퍼) — 프리뷰가 실제 건설 시 스냅될 위치(셀 중심)를 미리 보여주려면 필요.
- `TowerBuildController.SelectedDefinition`(신규 public 프로퍼티) — 지금까지 `_selectedIndex`가 private이라 외부에서 "지금 뭘 지으려는지" 알 방법이 없었음.
- `Runtime/TowerBuildPreview.cs`(신규, `[RequireComponent(SpriteRenderer)]`): 매 프레임 커서 위치→`TryGetSlotCell` 판정 → 슬롯이면 셀 중심으로 스냅 + `CanBuild` 결과로 흰색(반투명)/빨간색(반투명) 색상 결정, 슬롯이 아니면 커서 위치 그대로 따라가며 빨간색 고정. 선택된 타워가 없거나 UI(카드 패널/빌드 메뉴) 위에 마우스가 있을 때는 `SpriteRenderer.enabled = false`로 숨김(`EventSystem.IsPointerOverGameObject`로 판정).
- Player/CameraFollow와 같은 이유로 매니저 없이 단일 컴포넌트(씬에 프리뷰 오브젝트가 하나뿐이라).

### 씬 세팅 필요
- 빈 GameObject(예: `TowerBuildPreview`) 생성, `SpriteRenderer` 추가(Sorting Order를 타워보다 높게 둬서 겹쳤을 때 위에 보이도록), `TowerBuildPreview` 스크립트 부착.
- `Build Controller`/`Map Manager` 필드에 기존 오브젝트 드래그. `Valid Color`/`Invalid Color`는 기본값(반투명 흰색/빨간색) 그대로 써도 되고 취향껏 조정.

## 공격 명중 시각화 (레이저/라인 플래시) (2026-07-05)
{{user}} 요청: 별다른 이펙트 없어도 되니 공격 타워/플레이어 원거리 공격이 눈에 보여야 함 (투사체 이상적, 최소 레이저 정도).

### 왜 "투사체"가 아니라 "짧게 반짝이는 라인"으로 구현했나
데미지는 이미 `OnTick` 시점에 즉시(hitscan) 계산되는 구조라, 실제로 이동하는 투사체 오브젝트를 만들면 "투사체가 도착하는 시점"과 "데미지가 실제로 들어간 시점"이 어긋나는 문제가 생긴다(투사체 이동 시간만큼 판정을 늦추는 재설계가 필요해짐). 잼 마감이 임박해 로직 변경 없이 붙일 수 있는 가장 단순한 시각 표현으로 `LineRenderer` 기반의 즉발 라인 플래시(수명 0.08초)를 택함 — 사용자가 "하다못해 레이저는" 이라고 언급한 최소 기준과도 일치.

### 코드 구현 완료
- `Runtime/AttackFlash.cs`(신규, `[RequireComponent(LineRenderer)]`): `Spawn(prefab, from, to)` 정적 메서드 하나로 사용 — 프리팹을 Instantiate해 두 점을 잇는 라인을 잠깐 그리고 자동 파괴. 순수 렌더러 프리팹(4계층 3번)이라 데미지 로직과 완전히 분리, prefab이 비어있으면 조용히 무시(시각 효과 누락이 공격 로직을 막지 않음).
- `DamageEffect`/`PierceDamageEffect`(공격 타워 효과)에 `attackFlashPrefab` 필드 추가, 명중 처리 직후 `AttackFlash.Spawn` 호출. Pierce는 적마다 여러 번 긋지 않고 사거리 끝까지 이어지는 빔 한 줄만 그림(관통 컨셉에 더 맞음).
- `PlayerController`에도 동일하게 `attackFlashPrefab` 필드 + 원거리 자동공격 명중 시 호출.
- 스킬(AoE)은 방향성이 없는 범위 효과라 이번 스코프에서 제외 — 필요하면 Day3 "여유시" 폴리싱으로 별도 이펙트 추가.

### 씬/에셋 세팅 필요
- `AttackFlash` 프리팹 1개 제작: 빈 GameObject + `LineRenderer`(Width 적당히 얇게, Material은 기본 Sprites-Default에 색만 입혀도 충분) + `AttackFlash` 스크립트 부착 → 프리팹화.
- `DamageEffect`/`PierceDamageEffect` 에셋들과 `PlayerController`(Player 오브젝트)의 `Attack Flash Prefab` 필드에 그 프리팹 연결. (스킬 타워/파워 타워 효과에는 이 필드 자체가 없음 — 공격하지 않으니까.)

## 적 피격 틴트 + 오브젝트 풀링 여부 검토 (2026-07-05)
{{user}} 실사용 확인: `AttackFlash` 라인 정상 동작. 이어서 (1) 적도 건설 프리뷰의 "설치 불가=빨강"과 같은 방식으로 피격 시 빨갛게 틴트, (2) `AttackFlash`를 오브젝트 풀링해야 할지 질문.

### 적 피격 틴트 — 코드 구현 완료
- `EnemyView`에 `hitFlashColor`(기본 빨강)/`hitFlashDuration`(기본 0.1초) 필드 추가. 이미 존재하던 `EnemyInstance.Damaged` 이벤트를 `Bind()`에서 구독(`Died`/`ReachedGoal`과 동일 패턴)해, 맞을 때마다 `SpriteRenderer.color`를 잠깝 바꿨다 원래색(`_baseColor`, Awake에서 캐싱)으로 복귀. 코루틴 대신 프로젝트 전반 스타일(잔여시간 카운트다운 필드, `TowerInstance.cooldownRemaining`과 동일)에 맞춰 `LateUpdate()`의 타이머 필드로 처리.
- 씬 세팅 불필요 — 기존 `EnemyView` 프리팹에 필드 2개만 새로 노출됨.

### 오브젝트 풀링 — 도입하지 않기로 결정
현재 스케일(공격 타워 슬롯 제한 + 공격속도 감안 시 초당 발생량이 적음)에서는 Instantiate/Destroy 오버헤드가 체감되지 않아, 잼 마감이 임박한 지금 풀 관리 복잡도를 추가할 근거가 약하다고 판단 — "당장 필요하지 않은 최적화를 미리 하지 않는다" 원칙 적용. 실제 프로파일링에서 병목으로 확인되면 그때 붙이기로 하고, 현재 방식(수명 지나면 자동 `Destroy`) 유지.

## 레벨업 경험치 공식: 선형 → 제곱 스케일링 (2026-07-05)
{{user}} 확인: 레벨업이 너무 쉬워서 카드로 강해진 플레이어가 적을 입구에서 그냥 막아버릴 수 있는 문제(타워 배치 전략을 우회하게 됨).

### 원인과 결정
기존 공식(`baseXpToLevel + xpGrowthPerLevel × Level`, 선형)은 후반으로 갈수록 "레벨당 필요 경험치 증가폭"이 일정한데, 타워 DPS가 누적될수록 처치 속도(=초당 경험치 획득량)는 계속 빨라져서 상대적으로 후반 레벨업이 점점 쉬워지는 역설이 있었음. 지수함수(`base^level`)와 제곱식(`level²`) 중 제곱식을 선택 — 지수함수는 배율 하나만 잘못 잡아도 극단적으로 튀어(영원히 쉬움 ↔ 레벨업 벽) 튜닝 리스크가 큰데, 남은 플레이테스트 시간이 짧아 계수 2개로 감 잡기 쉬운 제곱식이 더 안전.

### 코드 구현 완료
- `GameManager.ExpRequiredForNextLevel()`: `baseXpToLevel + xpGrowthPerLevel × Level` → `baseXpToLevel + xpGrowthPerLevel × Level²`. 기존 필드(`baseXpToLevel`/`xpGrowthPerLevel`) 그대로 재사용 — 새 필드 추가 없음.
- 기본값(10, 5) 기준 참고 곡선: Lv1→2에 15, Lv4→5에 90, Lv9→10에 415 필요 — 후반으로 갈수록 확실히 가팔라짐.

### 플레이테스트 시 같이 조정할 것
- `baseXpToLevel`/`xpGrowthPerLevel` 두 계수와, 각 `EnemyData`의 처치 보상(exp) 값을 함께 맞춰봐야 실제 체감 난이도가 잡힘 — 웨이브 예산 공식과 마찬가지로 초안값이니 플레이테스트 대상.

## 웨이브별 적 체력 스케일링 추가 (2026-07-05)
{{user}} 지적: 슬롯 제한을 직접 6개로 풀어보고 나니(공격 타워 다수 확보), 절차적 생성이 "웨이브가 갈수록 더 많이" 스케일링(예산 공식)은 하지만 개체당 체력은 전혀 안 세지고 있었다는 걸 확인. "무한 디펜스" 컨셉이면 양쪽 다 계속 세져야 한다는 지적 — 레벨업 제곱식으로 바꾼 것과 완전히 같은 근본 원인(플레이어 DPS가 누적될수록 고정 스탯 상대의 콘텐츠가 상대적으로 쉬워짐).

### 코드 구현 완료
- `WaveManager.healthGrowthPerWave`(기본 0.10, 예산 공식과 같은 선형 스타일) 추가.
- `CalculateHealthMultiplier(waveNumber) = 1 + healthGrowthPerWave × waveNumber`.
- `SpawnOne()`에서 `instance.Spawn(...)` 직후 `instance.currentHealth *= CalculateHealthMultiplier(_waveNumber)` — `currentHealth`가 이미 public 필드라 `EnemyInstance`/`EnemyData`/다른 호출부(예: `EnemySpawnerDebug`) 변경 없이 이 한 줄로 끝남.
- 기본값(0.10) 기준 참고: 웨이브10=2배 체력, 웨이브20=3배 체력. 예산 공식(수량 증가)과 곱연산으로 겹치므로, 너무 가파르면 이 계수부터 낮출 것.

### 플레이테스트 시 같이 조정할 것
- `healthGrowthPerWave`는 예산(`budgetGrowthPerWave`) 및 레벨업 제곱식 계수와 서로 영향을 주고받으므로, 셋을 같이 맞춰보며 "타워 없이 버티기(입구막기)"가 불가능해지는지 확인.
- 데미지(접촉피해)는 이번엔 스케일링에서 제외 — 수량+체력 증가만으로도 총 위협량은 이미 늘어나고, 데미지까지 같이 올리면 튜닝 변수가 너무 많아져 짧은 시간에 감을 잡기 어려움. 필요해지면 나중에 별도로 추가.

## 타워 스프라이트 자동 크기 피팅 (2026-07-05)
{{user}} 상황: 아트 교체 중인데 타워 이미지마다 픽셀 해상도가 제각각(224×305, 209×368 등)이라 매번 Pixels Per Unit을 직접 계산해 넣는 게 너무 번거로움 — 코드 자동 피팅으로 전환 결정(처음엔 임포트 설정 수동 조정으로 갔었으나, 해상도 편차가 커서 재검토).

### 왜 Transform Scale이 아니라 별도 보정이 필요했나
타워 프리팹은 스프라이트와 사거리 판정용 `CircleCollider2D`가 같은 GameObject에 있어서, 스프라이트를 맞추려고 그냥 `transform.localScale`을 키우면 `CircleCollider2D`의 실제 월드 반경(로컬 반지름 × lossyScale)까지 같이 커져버려 공격 사거리가 아트 크기에 따라 달라지는 버그가 생김. 그래서 스케일을 적용하는 김에 `RefreshRangeCollider()`가 그 스케일만큼 반지름을 역보정하도록 같이 고쳐서, 시각 크기와 실제 판정 사거리를 분리함.

### 코드 구현 완료
- `Runtime/SpriteFit.cs`(신규, 정적 유틸리티): `CalculateUniformScale(sprite, targetSize)` — 스프라이트의 실제 bounds(픽셀 해상도/PPU와 무관하게 이미 그것들이 반영된 월드 유닛 크기)를 읽어, 가장 긴 축이 `targetSize`가 되는 균일 스케일값 계산. `TowerInstance`(실제 건설)와 `TowerBuildPreview`(프리뷰) 둘 다 재사용 — 프리뷰 크기와 실제 건설 크기가 어긋나지 않게.
- `TowerInstance.Build()`: 스프라이트 반영 직후 `SpriteFit.CalculateUniformScale`로 `transform.localScale` 설정. `targetVisualSize`(기본 0.9, Inspector 노출) 필드로 조정 가능.
- `TowerInstance.RefreshRangeCollider()`: `circle.radius = range` → `circle.radius = range / transform.localScale.x`로 보정 — 스케일이 몇이든 실제 판정 사거리는 항상 `range`(월드 유닛)와 정확히 일치.
- `TowerBuildPreview`에도 동일한 `targetVisualSize`(기본 0.9) 필드 추가, 매 프레임 같은 계산으로 스케일 적용 — **`TowerInstance`와 값을 반드시 맞춰야 함** (다르면 프리뷰 크기 ≠ 실제 건설 크기).

### 씬 세팅 필요
- 기존 프리팹/오브젝트 구조 변경 없음 — 이제부터는 타워 아트를 어떤 픽셀 해상도로 넣어도(PPU 기본값 그대로 둬도) 자동으로 `targetVisualSize`(기본 0.9유닛)에 맞춰짐.
- 그리드 셀 크기가 0.9와 크게 다르면 `TowerInstance`/`TowerBuildPreview` 양쪽의 `Target Visual Size` 값을 같은 숫자로 같이 조정할 것.

## 타워 로스터 확장: 종류당 6종 (2026-07-05)
{{user}} 결정: 공격/스킬/파워 타워를 각각 6종(총 18종)까지 채우기로 함. GDD가 확정한 15장 카드(신규 타워 해금 3장)보다 훨씬 큰 규모라, 이번엔 "새 이펙트 코드를 몇 개까지 진짜로 써야 하는가"를 먼저 정리해 제안 → 사용자 확인 후 진행.

### 핵심 판단: 대부분은 새 코드가 필요 없다
`AuraBuffEffect`(damageBuffMultiplier/attackSpeedBuffMultiplier)와 `GlobalBuffEffect`(globalDamageBonus)는 이미 데이터(수치)만으로 "체감이 다른 타워"를 상당수 커버하도록 설계돼 있었음 — 예: "화력 집중 오라"(사거리↓/버프↑↑)와 "광역 지원 오라"(사거리↑↑/공속버프 위주)는 같은 `AuraBuffEffect` 에셋 로직 그대로, `SkillTowerData` 수치만 다른 `TowerDefinition` 에셋 2개로 끝남. 이 판단에 따라 18종 중 **7종은 순수 데이터 변형**(신규 코드 없음), **5종만 신규 이펙트 클래스**가 필요하다고 제안 → {{user}} 승인.

### 최종 18종 구성
| 종류 | 이름 | 이펙트 | 비고 |
|---|---|---|---|
| 공격 | 기본 공격 | `DamageEffect` | 기존 |
| 공격 | 관통 사격 | `PierceDamageEffect` | 기존(카드13) |
| 공격 | 스나이퍼 | `DamageEffect` | 데이터 변형 |
| 공격 | 속사포 | `DamageEffect` | 데이터 변형 |
| 공격 | 폭발탄 | `SplashDamageEffect` | **신규** |
| 공격 | 맹독 | `PoisonDamageEffect` | **신규** |
| 스킬 | 기본 오라 | `AuraBuffEffect` | 기존 |
| 스킬 | 빙결 오라 | `SlowAuraEffect` | 기존(카드14) |
| 스킬 | 화력 집중 오라 | `AuraBuffEffect` | 데이터 변형 |
| 스킬 | 광역 지원 오라 | `AuraBuffEffect` | 데이터 변형 |
| 스킬 | 강화 빙결 오라 | `SlowAuraEffect` | 데이터 변형 |
| 스킬 | 취약 오라 | `VulnerableAuraEffect` | **신규** |
| 파워 | 기본 글로벌 버프 | `GlobalBuffEffect` | 기존 |
| 파워 | 재생의 축 | `BaseRegenEffect` | 기존(카드15) |
| 파워 | 강화된 축적 | `GlobalBuffEffect` | 데이터 변형 |
| 파워 | 강화된 재생 | `BaseRegenEffect` | 데이터 변형 |
| 파워 | 전역 가속 | `GlobalAttackSpeedEffect` | **신규** |
| 파워 | 골드 채굴 | `GoldMineEffect` | **신규** |

### 신규 코드 구현 완료
- `EnemyInstance`에 두 상태 필드 그룹 추가 — `_poisonDamagePerSecond`/`_poisonRemaining`(맹독), `_vulnerableMultiplier`/`_vulnerableRemaining`(취약). 둘 다 기존 `_speedMultiplier`/`_speedMultiplierRemaining`(Slow)과 완전히 같은 "지속시간 기반 갱신형" 패턴 — 사거리/명중이 끊기면 자연 만료. `TakeDamage`가 `amount *= _vulnerableMultiplier`로 배율을 반영.
- `SplashDamageEffect`(신규): `DamageEffect`와 동일한 쿨다운/타겟팅/데미지 파이프라인, 명중 지점 주변 `splashRadius` 내 다른 적에게 `splashDamageMultiplier` 배율 추가 피해.
- `PoisonDamageEffect`(신규): 명중 시 즉발 데미지 + `EnemyInstance.ApplyPoison` 호출로 DoT 부여.
- `VulnerableAuraEffect`(신규): `SlowAuraEffect`와 동일한 OnTick 갱신 패턴, 대상은 이동속도 대신 피해 배율.
- `GlobalAttackSpeedEffect`(신규): `GlobalBuffEffect`와 동일한 OnBuild+`GlobalTowerAuraRegistry` 등록 패턴. `TowerDamageMath.CalculateAttackInterval`이 이미 전역 오라를 조회하도록 되어 있어 배선 추가 없이 바로 반영됨.
- `GoldMineEffect`(신규): `BaseRegenEffect`와 동일한 패턴이나 대상이 골드. **주의**: 누적 타이머를 효과 SO 자신에 두지 않고 `ctx.self.cooldownRemaining`(타워 인스턴스별 상태)에 저장 — 효과 SO는 여러 타워 인스턴스가 공유하는 상태 없는 자산이어야 한다는 기존 원칙(`AuraBuffEffect`/`GlobalBuffEffect` 설계 근거와 동일) 위반을 피하기 위함.
- `GameManager`에 `BaseController.Instance`와 동일한 근거로 `static GameManager Instance` 싱글톤 추가 — `GoldMineEffect`가 씬의 매니저를 직접 참조해야 해서 필요.

### 씬/에셋 세팅 필요 (사용자 몫, 분량이 많아 체크리스트로 관리 권장)
각 신규 타워마다: ① 이펙트 SO 에셋(신규 5개만 새로 생성, 데이터 변형 7개는 기존 이펙트 에셋 재사용) → ② `TowerDefinition`(Attack/Skill/Power) 에셋 생성해 데이터+이펙트+스프라이트 연결 → ③ `UnlockTowerCard` 에셋 생성해 그 Definition 연결(기존 클래스 그대로 재사용, 새 코드 불필요) → ④ `TowerRoster`엔 등록하지 않음(추가 해금 방식이라 카드로만 획득) → ⑤ `CardRoster.cards`에 새 카드 12장 추가. 카드 총량이 기존 GDD 확정 15장에서 27장으로 늘어남 — 리포트 작성 시 "GDD 초안 대비 확장" 사항으로 언급할 것.

## 미해결/추후 확인 필요
- (현재 없음 — 새로 생기면 이 섹션에 추가)

## 카드 카테고리별 프레임 + 레벨업 중 건설 모드 차단/우클릭 취소 (2026-07-06)
{{user}} 발견: 레벨업 카드 선택 패널이 떠 있는 동안(`Time.timeScale=0`)에도 `TowerBuildController`/`TowerBuildPreview`의 `Update()`는 계속 돌아서(Time.timeScale은 deltaTime만 0으로 만들 뿐 Update 자체를 멈추지 않음) 패널 뒤에서 건설/철거/프리뷰가 그대로 동작하는 버그. 겸사겸사 건설 모드를 끄는 입력(우클릭)도 요청.

### 카드 프레임: CardEffectBase.Category 추가
- 카드 선택 UI(레벨업 3택)도 빌드 메뉴처럼 종류별 프레임(공격/스킬/파워)이 필요해짐.
- `CardEffectBase`에 `[SerializeField] private TowerKind category = TowerKind.Attack;` + `virtual TowerKind Category` 추가. 기본값 Attack — 플레이어/거점처럼 특정 타워 종류와 무관한 카드는 그대로 두면 "그 외" 프레임.
- `TowerSlotExpansionCard`/`UnlockTowerCard`는 이미 자기만의 `kind`/`unlockDefinition` 필드가 있어서, 그 값을 그대로 반환하도록 `Category`를 재정의 — 값을 두 군데(카테고리 필드 + 실제 로직 필드) 따로 관리하지 않게 함.
- `CardSelectionUI`에 `TowerBuildMenuUI.GetFrameSprite`와 동일한 규칙(파워→전용, 스킬→전용, 그 외→공격 프레임)으로 `cardFrameImages[i].sprite`를 설정하는 로직 추가.

### 건설 모드 차단: CardManager.IsChoosing
- `CardManager`에 `public bool IsChoosing => _isChoosing;` 추가.
- `TowerBuildController.Update()` 최상단에서 `cardManager.IsChoosing`이면 즉시 return — 선택 핫키/우클릭 취소/건설/철거 클릭 전부 차단.
- `TowerBuildPreview.Update()`도 같은 조건이면 스프라이트를 꺼서 프리뷰가 패널 위/뒤에 남아있지 않게 함.

### 우클릭 = 건설 모드 취소
- `TowerBuildController._selectedIndex`를 `int` → `int?`로 변경 (`null` = 건설 모드 꺼짐). `SelectedDefinition`도 이에 맞춰 null 반환하도록 수정 — 기존에도 `TowerBuildPreview`가 `definition == null`이면 자동으로 숨기는 로직이 이미 있어서 별도 분기 추가 없이 그대로 맞물림.
- `HandleCancelSelection()`(신규): 우클릭(`Mouse.current.rightButton.wasPressedThisFrame`) 시 `_selectedIndex = null`. 기존 숫자키 폴링과 동일하게 Input Action 에셋 대신 직접 폴링 방식 사용(이 스크립트의 기존 스타일과 통일).

### 씬 세팅 필요
- `TowerBuildController`/`TowerBuildPreview` 인스펙터에 새로 생긴 `Card Manager` 필드에 기존 CardManager 오브젝트 연결.
- `CardSelectionUI`에 `Card Frame Images`(배열, 카드 버튼 3개의 프레임 Image 순서대로) + `Attack/Skill/Power Frame Sprite` 3개 필드 연결 (빌드 메뉴에서 쓰는 것과 같은 스프라이트 재사용 가능).
- 스킬/파워 관련 카드 에셋들의 `Category`를 각각 Skill/Power로 설정 (해금/슬롯확장 카드는 자동 결정되므로 건드릴 필요 없음).

## 타워 SO 에셋 오염 방지: 세션당 1회 런타임 복제 (2026-07-06)
{{user}} 발견: 플레이할 때마다 `TowerRoster`/`TowerDefinition` 데이터가 "망가지는 느낌" — 정확한 원인 있음.

### 원인
카드(`AttackTowerDamageBoostCard` 등 5개 스탯강화 카드 + `GlobalBuffEffect.IncreaseAllGlobalBonuses`)가 `TowerDefinition.Data` 필드를 Play 모드 중에 직접 수정하고, `UnlockTowerCard.Apply`는 `TowerRoster.towers` 리스트에 직접 Add한다. 그런데 `TowerRoster`/`TowerDefinition`은 **프로젝트 에셋 그 자체**라서, 씬 오브젝트(플레이어/거점 등)와 달리 Play 모드를 꺼도 이 변경이 자동으로 원복되지 않는다 — 그래서 플레이 세션을 반복할수록 원본 에셋의 수치가 누적으로 오염되고, 해금한 타워도 그대로 로스터에 눌어붙어 있었음.

### 해결: 원본 대신 세션 전용 복제본을 참조
- `TowerDefinition`(추상 베이스)에 `CreateRuntimeInstance()` 추가: `[NonSerialized]` 캐시 필드에 `Instantiate(this)` 결과를 담아, 최초 호출 시 1번만 복제하고 이후 같은 복제본을 계속 반환. 캐시가 **원본** 쪽에 있어서, 어떤 경로로 호출하든(로스터 경유든 `UnlockTowerCard`가 직접 호출하든) 항상 동일한 복제본을 받는다.
- `TowerRoster`에 `GetRuntimeInstance()` 추가: 자신을 `Instantiate`하고, `towers` 리스트도 새로 만들어 각 원본 Definition의 `CreateRuntimeInstance()` 결과로 채운다.
- `TowerBuildController`/`CardManager`/`TowerBuildMenuUI` 셋 다 `Awake()`에서 `towerRoster = towerRoster.GetRuntimeInstance();`로 자기 필드를 복제본으로 바꿔치기. 세 스크립트의 `Awake()` 호출 순서는 보장되지 않지만, 캐시가 원본에 있어서 순서와 무관하게 전부 같은 복제본을 받아 안전함.
- `UnlockTowerCard`도 `unlockDefinition`(원본) 대신 `unlockDefinition.CreateRuntimeInstance()`로 Contains 판정/Add를 하도록 수정 — 안 그러면 카드로 새로 해금한 타워는 여전히 원본 그대로라 스탯강화 카드가 그 원본을 오염시키고, "이미 해금됨" 재추첨 방지 로직도 원본과 복제본이 달라 항상 실패했을 것.
- `CardContext.towerRoster`는 각 매니저가 이미 복제본으로 바꿔치기한 필드를 그대로 넘기므로, 카드 쪽 코드는 전혀 손대지 않아도 자동으로 복제본을 사용하게 됨.

### 스코프 확인 (다른 Roster/Data는 문제 없음)
- `EnemyRoster`/`CardRoster`는 런타임에 아무도 리스트를 수정하지 않아 대상 아님.
- `PlayerData`/`BaseController`(체력)/`GameManager`(골드/레벨/철거비율)는 전부 **씬 오브젝트의 필드**라서 Play 모드 종료 시 자동 원복됨 — 대상 아님.
- 순수하게 "카드가 직접 수정하는 프로젝트 에셋"인 `TowerRoster`+`TowerDefinition.Data`만 이 문제의 대상이었음.

### 확인 방법
Play 모드에서 스탯강화 카드를 몇 개 뽑은 뒤 정지 → 원본 `TowerDefinition` 에셋을 인스펙터에서 열어 수치가 그대로인지 확인. 신규 타워를 카드로 해금한 뒤 정지 → 원본 `TowerRoster` 에셋의 `Towers` 리스트에 추가된 항목이 없는지 확인.

## 플레이어 이동 방향 스프라이트 회전 (2026-07-06)
{{user}} 요청: 아트 교체 중 플레이어 스프라이트가 이동 방향을 바라보게.

### 왜 무조건 루트 Transform을 돌리지 않았나
타워 스프라이트 자동 피팅 때 스케일을 루트에 걸면 콜라이더까지 같이 커지는 버그를 겪었던 것과 같은 종류의 위험 — 회전도 루트 Transform에 걸면 몸통 `Collider2D`가 원형이 아닌 경우(박스/캡슐) 판정 모양이 같이 돌아가 버릴 수 있음. 그래서 스프라이트 전용 자식 오브젝트가 있으면 그것만 돌리고, 없으면(스프라이트가 루트에 바로 있는 지금 구조로 추정) 안전하게 루트를 그대로 돌리도록 두 경우 다 대응.

### 코드 구현 완료
- `PlayerController`에 `spriteTransform`(선택, 비워두면 루트 회전) / `spriteForwardOffsetDegrees`(기본 90 — 아트가 기본적으로 위쪽을 보고 그려졌다고 가정, 오른쪽 기준이면 0으로 조정) 필드 추가.
- `Move()`에서 입력이 있을 때만 `Mathf.Atan2`로 각도 계산해 회전 적용 — 입력이 0(정지)일 땐 마지막 방향을 그대로 유지(0으로 스냅되는 것 방지).

### 씬 세팅 필요
- 아트가 그려진 기본 방향에 따라 `Sprite Forward Offset Degrees` 조정 (오른쪽 기준 0 / 위쪽 기준 90 / 아래쪽 -90 / 왼쪽 180) — 플레이해서 이동 방향과 스프라이트가 맞는지 보고 90도 단위로 맞춰볼 것.
- 몸통 Collider2D가 원형이 아니라서 회전 시 판정이 이상해지면, 스프라이트만 담은 자식 오브젝트를 새로 만들어 `Sprite Transform` 필드에 연결할 것.

## 이동 잔상 트레일 (2026-07-06)
{{user}} 요청: 플레이어가 움직일 때 흰색 잔상이 뒤에 따라붙게.

### 한계 고지 (요청대로 미리 알림)
기본 Sprites/Default 셰이더로는 색을 완전한 흰색으로 강제할 수 없음 — 흰색을 곱해도 알파만 줄어들 뿐 원본 RGB는 그대로 유지됨. 그래서 지금 구현은 "원래 색을 유지한 채 알파만 서서히 0으로" 사라지는 반투명 잔상이고, 진짜 흰색 실루엣이 필요하면 별도 커스텀 셰이더가 필요함(요청 시 추가 가능).

### 코드 구현 완료
- `Runtime/MotionTrailGhost.cs`(신규, 순수 렌더러 프리팹): `Play(sprite, position, rotation, color, fadeDuration)`로 스폰 시점 스프라이트를 복사해 표시, `fadeDuration`에 걸쳐 알파 0까지 줄이며 자동 파괴.
- `Runtime/MotionTrailSpawner.cs`(신규): 자기 `transform` 위치 변화량만 관찰(`minMoveDistancePerSpawn` 이상 움직였을 때만) — 이동 로직과 완전히 분리된 순수 시각 컴포넌트라 PlayerController를 몰라도 되고, 스프라이트를 가진 오브젝트 아무데나 붙이면 동작함. `spawnInterval`(고스트 생성 주기)/`fadeDuration`/`ghostColor`/`minMoveDistancePerSpawn` 전부 Inspector 노출.
- 플레이어가 스프라이트를 회전 때문에 자식 오브젝트(`spriteTransform`)에 둘 수도 있어서, 이 컴포넌트는 "실제 SpriteRenderer가 있는 오브젝트"에 직접 붙이도록 설계(루트든 자식이든 무관).

### 씬 세팅 필요
- `MotionTrailGhost` 프리팹 1개 제작: 빈 GameObject + `SpriteRenderer`(재질은 기존 스프라이트용 그대로) + `MotionTrailGhost` 스크립트 → 프리팹화.
- 플레이어의 **SpriteRenderer가 실제로 붙어있는 오브젝트**(루트 또는 스프라이트 전용 자식)에 `MotionTrailSpawner` 스크립트 부착, `Ghost Prefab`에 위에서 만든 프리팹 연결.
- 기본값 그대로 재생해보고, 잔상 개수/수명/투명도는 `Spawn Interval`/`Fade Duration`/`Ghost Color`로 조정.

### 추가: 고정 크기 풀로 전환 (2026-07-06)
{{user}} 질문: 잔상도 풀링이 필요하지 않나 — 맞는 지적. `AttackFlash` 때는 "공격 쿨다운 걸린 저빈도 이벤트"라 풀링을 보류했지만, 이동 잔상은 초당 최대 20개(기본 `spawnInterval` 0.05초 기준)를 이동하는 내내 지속적으로 만들어내는 고빈도 패턴이라 Instantiate/Destroy 반복이 GC 부담으로 누적될 수 있어 판단이 다름.
- `MotionTrailSpawner.Awake()`에서 `poolSize`(기본 12, 동시 표시 가능한 고스트 수의 대략 2배 여유)개를 한 번만 `Instantiate`, 이후엔 배열을 순환하며 재사용.
- `MotionTrailGhost`는 더 이상 자기 자신을 `Destroy`하지 않고, 다 사라지면 알파 0 상태로 대기만 하다가 다음 `Play()` 호출에 재활용됨.
- 씬 세팅 변경 없음 — 프리팹은 이제 스포너가 시작 시 알아서 여러 개 복제해두므로, 기존에 만들어둔 프리팹 1개만 있으면 됨.

## AttackFlash 풀링 전환 (2026-07-06)
{{user}} 요청: 웨이브가 갈수록 타워 수·공속이 계속 늘어날 텐데 공격 이펙트도 풀링해야 하지 않겠냐 — MotionTrailGhost와 같은 이유로 타당한 지적이라 반영.
- `AttackFlash`를 prefab별 정적 대기열(`Dictionary<GameObject, Queue<AttackFlash>>`) 기반으로 전환: `Deactivate()`가 `Destroy` 대신 `LineRenderer.enabled = false` + 대기열 반납, `Spawn()`이 대기열에서 재사용 가능한 인스턴스를 먼저 찾고 없을 때만 `Instantiate`.
- **정적 `Spawn(prefab, from, to)` API는 그대로**라 호출부(`DamageEffect`/`PierceDamageEffect`/`PoisonDamageEffect`/`SplashDamageEffect`/`PlayerController`) 전부 코드 변경 없이 그대로 재사용됨.
- 서로 다른 카드/타워가 각자 다른 `attackFlashPrefab`을 물려뒀을 가능성을 감안해 prefab별로 풀을 분리 관리.
- 씬 세팅 변경 없음.

## 플레이어 자동공격 미발동 — 원인 조사 중 (2026-07-06)
{{user}} 발견: 적이 사거리 안에 있는데도 플레이어가 공격을 안 함.
- `PlayerController`/`AttackRangeTrigger`/`EnemyTargeting` 코드 로직 자체는 검토 결과 문제 없음 — 씬/프리팹 세팅(콜라이더 반경, Rigidbody2D, Is Trigger 등) 쪽 문제일 가능성이 높음. {{user}}가 자리를 비운 사이라 씬을 직접 확인할 수 없어, 원인을 좁히는 임시 디버그 로그를 추가해둠(`BaseController`/`EnemyInstance` 때와 동일한 방식).
- `HandleEnemyEnteredRange`/`HandleEnemyExitedRange`에 `[PlayerDebug] 사거리 진입/이탈` 로그 추가 — **이 로그 자체가 안 뜨면** `AttackRangeTrigger` 자식 오브젝트의 콜라이더/Rigidbody2D/Is Trigger 설정 문제(물리 이벤트 자체가 안 들어옴).
- `TryAutoAttack`에 "목록엔 있지만 attackRange 조건 통과 못 함" 로그 추가 — **진입 로그는 뜨는데 이 로그가 뜨면** `PlayerData.attackRange` 수치와 `AttackRangeTrigger`의 실제 콜라이더 반경이 어긋난 것(트리거 반경이 더 작아서 감지 자체가 attackRange보다 좁은 범위에서만 됨 등).
- 다음 액션: {{user}}가 Play 모드로 적을 사거리 안에 들여보내고 콘솔에 어떤 로그가 뜨는지(둘 다 안 뜸 / 진입 로그만 뜸 / 진입+조건통과실패 로그 둘 다 뜸) 확인 → 그 결과로 정확한 원인 특정 후 수정.
- **원인 확정 (2026-07-06)**: `EnemyView` 프리팹에 애초에 트리거용 Collider2D/Rigidbody2D 설정이 빠져 있었음 — 정확히는 물리 트리거 자체가 안 걸려있던 것. {{user}}가 씬에서 수정 완료. 위 디버그 로그(`[PlayerDebug]`)는 정상 작동 확인 후 제거 대상으로 남겨둠(다른 기존 디버그 로그 2개와 함께 나중에 일괄 정리).

## 적도 이동 방향으로 스프라이트 회전 (2026-07-06)
{{user}} 요청: 플레이어처럼 적도 이동 방향을 보게. 스프라이트는 기본적으로 위쪽을 봄.

### 코드 구현 완료
- `EnemyView`에 `spriteForwardOffsetDegrees`(기본 90, `PlayerController`와 동일 개념) 필드 추가.
- `EnemyInstance`(순수 C#)는 위치만 갖고 이동 방향 자체를 노출하지 않으므로, `EnemyView.LateUpdate()`가 매 프레임 `Instance.position`의 변화량(델타)으로 방향을 역산해 회전 — 이동 로직은 전혀 건드리지 않고 시각 표현 레이어에서만 처리(`PlayerController.UpdateFacing`과 같은 목적, 계산 방식만 다름 — Player는 입력 벡터, Enemy는 위치 델타).
- `Bind()` 시점에 `_previousPosition`을 스폰 위치로 초기화해, 첫 프레임에 (0,0) 기준 큰 델타로 잘못 회전하는 것 방지.

### 씬 세팅
- 기본값(90, 위쪽 기준) 그대로 두면 됨 — 스프라이트가 이미 위를 보고 그려졌다고 확인함.
- 플레이어 때와 동일한 주의사항: 적의 몸통 Collider2D가 원형이 아니면 회전 시 판정이 같이 돌아갈 수 있음 — 이상하면 스프라이트 전용 자식 오브젝트로 분리 필요.

### 추가 수정: 종류별 보정값 + 목적지 기준 방향 계산 (2026-07-06)
{{user}} 발견: 일부 적은 회전이 아예 안 먹고(스프라이트 원본 그대로), 일부는 거꾸로 보임 — 특정 종류에 국한되지 않음.

**원인 1 — 보정값이 전역 공용**: `spriteForwardOffsetDegrees`가 `EnemyView` 프리팹(전체 공용) 하나에 고정돼 있어서, 적마다 아트가 서로 다른 기본 방향(일부는 위, 일부는 아래 등)으로 그려졌을 때 하나의 값으로는 전부 못 맞춤 → "거꾸로 보임" 증상과 일치.
- 해결: `spriteForwardOffsetDegrees`를 `EnemyView`(공용 프리팹)에서 `EnemyDefinition`(종류별 데이터)으로 이동 — `sprite`와 같은 이유로 종류별 값이어야 함. `EnemyView.UpdateFacing`은 `Instance.definition.spriteForwardOffsetDegrees`를 참조.

**원인 2 — 델타 기반 방향 계산의 콜드스타트 문제**: {{user}} 확인: 스폰은 정확히 `path[0]`(첫 웨이포인트)에서 시작함(`WaveManager.SpawnOne`). 프레임 간 위치 변화량(델타)으로 방향을 추정하던 기존 방식은, 스폰 직후나 웨이포인트에 정확히 스냅되는 프레임처럼 실이동량이 0에 가까운 순간에 방향이 갱신되지 않아 "회전 자체가 안 먹는" 것처럼 보이는 문제가 있었음. {{user}} 제안: 다음 랠리포인트로 향하는 벡터를 직접 쓰자 — 채택.
- `EnemyInstance.CurrentTargetWaypoint`(신규, 읽기 전용 `Vector2?`): 지금 향하고 있는 웨이포인트를 노출 (경로 끝 도달 시 null).
- `EnemyView.UpdateFacing`을 "이전 프레임 대비 델타" 대신 "목표 웨이포인트 - 현재 위치" 벡터로 완전히 교체 — 실이동 여부와 무관하게 항상 의도된 방향을 즉시 반영, `_previousPosition` 필드 자체가 불필요해져 제거.

---

## 타워 사거리 표시 (RangeIndicator) (2026-07-06)
{{user}} 요청: 타워 클릭 시 공격/스킬 사거리를 빨간 테두리 원(내부 투명)으로 표시. 건설 프리뷰 단계부터 미리 보여주는 것도 고려.

### 코드 구현 완료
- `TowerData`에 `virtual DisplayRange`(기본 0) 추가, `AttackTowerData`(→attackRange)/`SkillTowerData`(→buffRange)가 재정의. 파워 타워는 기본값 0을 그대로 써서 "표시할 사거리 없음"으로 자연스럽게 처리됨. 기존 `TowerInstance.RefreshRangeCollider()`의 -1 sentinel 스위치 로직은 그대로 두고 건드리지 않음(콜라이더 반영과 순수 표시용은 별개 관심사로 분리, 리그레션 방지).
- `Runtime/RangeIndicator.cs`(신규, `[RequireComponent(LineRenderer)]`): `Show(center, radius)`가 LineRenderer로 원(loop) 좌표를 계산해 그리고, `radius <= 0`이면 자동으로 숨김. 별도 링 아트 없이 임의 반경에 대응.
- `TowerBuildController`: 클릭 처리 흐름을 살짝 재구성 — 셀을 한 번만 계산해 `HandleBuildClick(cell)`(건설 시도)과 `HandleRangeInspectClick(cell)`(그 셀에 이미 타워가 있으면 사거리 표시) 둘 다에 넘김. 건설 성공 직후에도 같은 프레임에 사거리가 바로 보이는 효과를 겸함 — {{user}}가 언급한 "설치할 때부터 표시" 아이디어와 자연스럽게 맞물림.
- `TowerBuildPreview`: 프리뷰 스프라이트가 보일 때 같은 `RangeIndicator`로 사거리 원도 같이 표시(파워 타워 선택 시엔 `DisplayRange=0`이라 자동으로 안 뜸), 프리뷰가 숨겨질 때 같이 숨김.
- `TowerBuildController`와 `TowerBuildPreview` 둘 다 같은 `RangeIndicator` 오브젝트 하나를 공유해도 되게 설계(동시에 두 사거리를 보여줄 필요는 없음).

### 씬 세팅 필요
- 빈 GameObject(예: `RangeIndicator`) 생성 → `LineRenderer` 추가(Width 얇게, Material은 `AttackFlash` 때와 같은 방식으로 색 있는 머티리얼 연결) + `RangeIndicator` 스크립트 부착.
- `TowerBuildController`와 `TowerBuildPreview` 양쪽의 `Range Indicator` 필드에 그 오브젝트 하나를 공통으로 연결.

### 버그 수정: 두 스크립트가 같은 인디케이터를 두고 매 프레임 충돌 (2026-07-06)
{{user}} 발견: 건설 프리뷰는 되는데 이미 지어진 타워 클릭은 전혀 반응 없음.

**원인**: `TowerBuildPreview.Update()`는 건설 모드가 꺼져 있어도(선택된 타워 없음) 매 프레임 `rangeIndicator.Hide()`를 호출하고 있었음. `TowerBuildController`는 클릭 "그 순간"에만 `Show()`를 호출하는데, 같은/다음 프레임에 `TowerBuildPreview`가 계속 `Hide()`로 덮어써서 사실상 안 보이는 것처럼 됐던 것 — 두 스크립트가 같은 `RangeIndicator`를 두고 매 프레임 충돌.

**해결**: `TowerBuildPreview`가 `definition == null`(건설 모드 완전히 꺼짐)일 땐 `rangeIndicator`를 아예 건드리지 않도록 수정 — 이 경우엔 `TowerBuildController`의 클릭 조회 표시가 그대로 유지됨. 대신 그 부작용으로, 우클릭(건설 모드 취소) 시 프리뷰가 보여주던 원이 안 지워지는 문제가 새로 생김 → `TowerBuildController.HandleCancelSelection()`이 취소 시점에 직접 `rangeIndicator.Hide()`를 호출하도록 추가.

**남은 알려진 동작(버그 아님)**: 타워 종류가 선택된 채로(건설 모드 켜짐) 기존 타워를 클릭하면, 프리뷰가 매 프레임 커서 위치에 사거리를 계속 그려서 클릭 조회 표시를 다시 덮어씀 — 사거리 "조회"만 하고 싶으면 먼저 우클릭으로 건설 모드를 끈 뒤 클릭할 것.

## 적 체력바 (2026-07-06)
{{user}} 제안, 트레이드오프 공유 후 진행 합의: 탱커형처럼 체력이 많은 적은 맞고 있는지 체감이 안 되니 작은 체력바 필요. 대신 잡몹까지 전부 떠서 화면이 지저분해지지 않도록 만피일 땐 숨기기로 함.

### 코드 구현 완료
- `Runtime/EnemyHealthBar.cs`(신규): UI Canvas 없이 `SpriteRenderer` 2개(배경+채움)만으로 구성 — `SetHealthPercent(percent)`가 채움 스프라이트의 `localScale.x`를 퍼센트로 조절하고, 만피(1.0) 또는 사망(0) 상태면 자동으로 `gameObject.SetActive(false)`. 채움 스프라이트는 **Pivot이 왼쪽(Left)** 이어야 오른쪽부터 줄어드는 자연스러운 바가 됨.
- `EnemyView`에 `healthBar`(선택) 필드 추가, `LateUpdate()`에서 `Instance.currentHealth / Instance.Data.maxHealth`로 매 프레임 갱신.
- **회전 상쇄 처리**: `EnemyView`가 이동방향으로 자기 자신(루트)을 매 프레임 회전시키는데, 체력바를 그 자식으로 두면 회전을 그대로 물려받아 같이 빙글빙글 돈다. `UpdateHealthBar()`가 `healthBar.transform.rotation = Quaternion.identity`로 매 프레임 되돌려 항상 수평 유지 — 이 처리가 자기 회전을 설정한 "다음 줄"에서 실행되므로(같은 LateUpdate 메서드 안, 실행 순서가 코드 순서로 보장됨) 스크립트 간 실행 순서 경쟁 없이 항상 정확히 상쇄됨.

### 씬 세팅 필요
- `EnemyView` 프리팹 아래에 자식 오브젝트 구성: `HealthBar`(빈 오브젝트, `EnemyHealthBar` 스크립트 부착) → 그 밑에 `Background`(SpriteRenderer, 회색/검정 막대 스프라이트) + `Fill`(SpriteRenderer, 빨강/초록 막대 스프라이트, **Pivot: Left**로 임포트 설정) 자식 2개.
- 위치는 적 스프라이트 기준 살짝 아래(또는 위)로 로컬 오프셋 조정 (예: `Local Position Y = -0.4` 정도, 스프라이트 크기에 맞춰 조절).
- `EnemyHealthBar`의 `Fill Transform` 필드에 `Fill` 오브젝트 연결.
- `EnemyView`의 `Health Bar` 필드에 `HealthBar` 오브젝트 연결.
- 종류별로 따로 만들 필요 없음 — 프리팹 1개 재사용 원칙 그대로 유지됨.

---

## 플레이어 스킬 재설계: 오버드라이브 모드 (2026-07-06)
{{user}}: 기존 1회성 즉발 AoE 스킬 대신, 이펙트 에셋 없이도 만족스러운 스킬을 원함 — "0.3초 간격 연속 공격 4회"로 재설계 제안.

### 왜 별도 VFX 없이도 되는가
매 타격이 기존 `EnemyView`의 피격 빨간 틴트(`HandleDamaged`)를 그대로 다시 트리거하므로, 4연타가 화면에서 자연스럽게 "번쩍번쩍" 터지는 것처럼 보인다 — 새 이펙트 에셋을 찾을 필요가 없어짐.

### 코드 구현 완료
- `PlayerController`에 `skillBurstCount`(기본 4)/`skillBurstInterval`(기본 0.3초) 필드 추가.
- `TrySkill()`을 상태 머신으로 재구성: 발동 시 `IsSkillActive=true` + 즉시 1타(딜레이 0) → 이후 `TickSkillBurst()`가 `skillBurstInterval`마다 `FireSkillPulse()`(기존 스킬 AoE 로직 그대로, 사거리 내 전체에게 `skillDamage`) 호출 → `skillBurstCount`번 다 쓰면 종료하고 그때부터 `skillCooldown` 카운트다운 시작.
- HUD 연동용 공개 프로퍼티 추가: `IsSkillActive`(사용 중), `IsSkillReady`(쿨다운 다 되고 사용 중도 아님), `SkillCooldownRemaining`/`SkillCooldownDuration`(게이지바용).
- `HudController`에 `skillReadyText`("준비" 텍스트, 준비 시에만 표시)/`overdriveText`("오버드라이브 모드" 텍스트, 사용 중에만 표시)/`skillGaugeBar`(Slider, `Duration - Remaining`으로 충전량 표시) 추가.

### 밸런싱 유의사항
`skillDamage`는 이제 "타당 데미지"라 총 데미지가 4배가 됨 — 기존 1회성 값 그대로 두면 과할 수 있으니 플레이테스트 시 낮출 것.

### 씬 세팅 필요
- HUD에 TMP 텍스트 2개("준비"/"오버드라이브 모드" 표시용) + Slider 1개(스킬 게이지) 추가 생성.
- `HudController`의 `Skill Ready Text`/`Overdrive Text`/`Skill Gauge Bar` 필드에 각각 연결. 게이지바는 체력바들과 마찬가지로 `Interactable` 끄고 `Fill Rect` 확인.

## 오퍼레이터(카시아) 대사 시스템 (2026-07-06)
{{user}} 요청: 상시 노출 오퍼레이터 초상화 + 말풍선. 8개 상황(게임 개시/스킬 사용/거점 피격/플레이어 피격(+체력 30% 이하 특수)/건설 실패 2종/플레이어 사망/거점 파괴)마다 대사 배열에서 랜덤 1줄 + 초상화 전환. 말풍선은 클릭 또는 4초 후 자동 페이드로 사라짐. "JSON으로 할까?" 질문에 SO 권장(스프라이트 참조가 어차피 Unity 에셋이라 JSON으론 절반만 처리되고 나머지는 SO로 와야 해서 — 프로젝트 전체의 SO+인스펙터 패턴과도 일관).

### 코드 구현 완료
- `UI/OperatorDialogueSet.cs`(신규, SO): `OperatorLineSet`(스프라이트 + 대사 배열) 9개 슬롯(idle 1 + 상황 8) 보유.
- `UI/OperatorDialogueUI.cs`(신규): 6개 이벤트(아래) 구독 + `Start()`에서 게임 개시 대사 1회 자동 출력. `ShowRandom`이 배열에서 무작위 선택 + 초상화 교체, `Update()`가 `displayDuration`(4초) 경과 후 `fadeDuration`(0.5초)에 걸쳐 알파 감소, `IPointerClickHandler`로 클릭 시 즉시 닫힘(페이드 없이). **CanvasGroup은 말풍선/텍스트에만 걸어야** 초상화가 계속 보이고 텍스트만 사라짐 — 씬 세팅 시 주의.
- 게임플레이 스크립트에 새 이벤트 추가(전부 UI가 구독하는 쪽, 반대 방향 참조 없음 — 계층 원칙 유지):
  - `PlayerController.Damaged(float)`/`Died`/`SkillUsed` — `TakeDamage`에 사망 가드(`_isDead`)와 체력 0 클램프도 같이 추가(기존엔 음수로 내려가고 사망 처리 자체가 없었음).
  - `BaseController.Damaged(float)` — 기존 `Defeated`는 그대로 재사용.
  - `TowerBuildController.BuildFailedInsufficientGold`/`BuildFailedNoSlot` — 기존 실패 분기에 이벤트 발생만 추가(로직 변경 없음).
- 대사 텍스트 8종 전체는 GDD Notion 페이지에 "오퍼레이터(카시아) 대사 스크립트" 섹션으로 정리해둠 — 에셋 채울 때 거기서 복붙.

### 알려진 한계
`Died`/`Defeated` 이벤트는 지금은 대사만 트리거함 — 실제 게임오버 화면/일시정지 등은 아직 없음(Day3 "승리/패배 결과 화면" 체크리스트 항목으로 별도 존재). `PlayerController`는 사망 후 `_isDead`로 추가 피해만 막아둔 상태.

### 씬 세팅 필요
- `OperatorDialogueSet` 에셋 생성(Create → RCCom → UI → Operator Dialogue Set), 9개 슬롯(대사 배열 + 초상화 스프라이트) 채우기.
- 오퍼레이터 UI 패널 구성: 초상화 `Image` 1개(페이드 그룹 밖) + 말풍선 영역(배경 Image + `dialogueText` TMP, 이 부분에만 `CanvasGroup` 부착) — 말풍선엔 클릭 감지가 되도록 raycast target이 켜진 Image가 있어야 함.
- 빈 오브젝트(또는 패널 루트)에 `OperatorDialogueUI` 부착 → `Dialogue Set`/`Portrait Image`/`Dialogue Text`/`Canvas Group`/`Player`/`Base Controller`/`Build Controller` 필드 연결.

---

## 버그 수정: 클릭 한 번에 건설 로직이 두 번 실행됨 (2026-07-06)
{{user}} 발견: 타워는 정상 설치되는데 "슬롯 부족" 오퍼레이터 대사가 매번 같이 뜸.

### 원인
`TowerBuildController`가 좌클릭 감지에 `clickAction`(Input Action "UI/Click")의 `WasPerformedThisFrame()`을 썼는데, 이 액션이 마우스를 누를 때/뗄 때 둘 다 Performed를 발생시키는 상호작용으로 되어 있었음 — 그래서 클릭 한 번에 `HandleBuildClick`이 2번 호출됨: 누르는 순간엔 슬롯이 비어있어 정상 건설, 떼는 순간엔 방금 그 셀이 이미 점유된 상태라 `CanBuild`가 실패해 `BuildFailedNoSlot` 이벤트가 추가로 발생.

### 해결
좌클릭 감지를 `clickAction.WasPerformedThisFrame()`에서 `Mouse.current.leftButton.wasPressedThisFrame`(직접 폴링)으로 교체 — 이 파일의 우클릭 취소/숫자키 선택이 이미 같은 방식(Input Action 대신 Mouse/Keyboard 직접 폴링)을 쓰고 있어서 스타일도 통일됨. 이제 물리적으로 누르는 순간에만 정확히 1번 반응. `clickAction` 필드와 `OnEnable`/`OnDisable`의 Enable/Disable 호출은 더 이상 안 쓰여서 함께 제거.

### 씬 세팅 필요
- `TowerBuildController`의 `Click Action` 필드가 사라졌으니, 인스펙터에 혹시 남아있던 참조는 자동으로 무시됨(에러 아님) — 별도 조치 불필요.

## 스킬 입력을 스페이스바로 분리 (2026-07-06)
{{user}} 발견(웃음): 위 클릭 수정 이후에도, 스킬이 "Attack" 액션(마우스 좌클릭 포함)에 물려 있어서 타워 건설 클릭과 스킬 발동 입력이 그대로 겹침.

### 해결
`PlayerController.skillAction`(InputActionReference, "Attack" 액션 재사용) 필드를 완전히 제거하고, `Keyboard.current.spaceKey.wasPressedThisFrame` 직접 폴링으로 교체 — `TowerBuildController`의 우클릭/숫자키와 동일한 스타일. `OnEnable`/`OnDisable`의 `skillAction.action.Enable/Disable()` 호출도 함께 제거.

### 씬 세팅 필요
- `PlayerController`의 `Skill Action` 필드가 사라짐 — 인스펙터에 남아있던 참조는 자동 무시(에러 아님).
- 이제 스킬은 **스페이스바**로 발동. 조작 안내 문구를 쓸 일이 있으면(Day3 튜토리얼 텍스트 등) 이 키로 반영할 것.

## 플레이어 피격/스킬 시각 피드백 (2026-07-06)
{{user}} 요청: 적처럼 플레이어도 피격 시 빨갛게, 스킬 사용 시 살짝 청색으로.

### 코드 구현 완료
- `PlayerController`에 `EnemyView`의 피격 틴트와 동일한 타이머 기반 방식 추가: `spriteRenderer`(수동 연결, 스프라이트가 루트/spriteTransform 자식 중 어디 있든 대응)/`hitFlashColor`(기본 빨강)/`hitFlashDuration`(0.15초)/`skillTintColor`(기본 옅은 청색).
- `UpdateSpriteTint()`가 매 프레임 우선순위대로 색 결정: **피격 직후(빨강) > 스킬 사용 중/오버드라이브 모드(청색) > 평상시(원래 색)** — 스킬 도중 맞으면 빨강이 우선 표시되고, 원래대로 돌아온 뒤 스킬이 아직 진행 중이면 다시 청색으로 복귀.
- `TakeDamage`에서 `_hitFlashRemaining` 타이머 설정, 스킬 틴트는 별도 타이머 없이 기존 `IsSkillActive` 상태를 그대로 참조(스킬 지속시간 내내 유지).

### 씬 세팅 필요
- `PlayerController`의 `Sprite Renderer` 필드에 실제 스프라이트가 붙어있는 오브젝트(루트 또는 `spriteTransform`으로 지정한 자식) 연결.
- `Hit Flash Color`/`Hit Flash Duration`/`Skill Tint Color`는 기본값 그대로 써도 되고 취향껏 조정.

## 게임 결과 화면 (Mission Result) + Retry 시 세션 캐시 초기화 (2026-07-06)
{{user}}: Day3 "승리/패배 결과 화면" 체크리스트 항목 구현 — 도달 웨이브/처치 수/획득 골드/생존 시간 표시, Retry(재시작)/Title(타이틀 씬 복귀) 버튼.

### 사전 작업: GameManager가 게임오버를 판정하도록 확장
지금까지 `PlayerController.Died`/`BaseController.Defeated` 이벤트는 있었지만 아무도 구독하지 않아 실제 게임오버 처리가 없었음(대사만 트리거). `GameManager`가 이 둘을 구독해 최초 1회만(`IsGameOver` 가드) `Time.timeScale = 0f` + `GameOver` 이벤트 발생 — `CardManager`가 레벨업 때 이미 쓰던 것과 같은 일시정지 메커니즘 재사용.
- `TowerBuildController`/`TowerBuildPreview`가 `cardManager.IsChoosing`과 같은 방식으로 `gameManager.IsGameOver`도 확인해 결과 화면이 떠 있는 동안 건설/프리뷰를 막도록 함께 수정.

### 중요 사전 작업: Retry(씬 재로드)가 실제로 "새 판"이 되도록 세션 캐시 초기화
Retry는 Editor Play 재시작과 달리 도메인 리로드가 없어(`SceneManager.LoadScene`은 일반 런타임 씬 전환일 뿐), 지금까지 "세션당 1회만 초기화되면 충분하다"고 가정했던 여러 static/캐시 상태가 그대로 남아 다음 판에 넘어가 버리는 문제를 이번에 발견하고 같이 고침:
- `TowerDefinition.ClearRuntimeInstance()`/`TowerRoster.ClearRuntimeInstance()`(신규): 카드 강화가 누적된 복제본 캐시를 초기화 — 안 하면 재시작해도 이전 판 스탯 강화가 그대로 이어짐.
- `UnlockTowerCard.ClearRuntimeCache()`(신규): 기본 로스터에 없는 해금형 타워의 복제본 캐시도 별도로 초기화 필요해서 추가.
- `AttackFlash.ClearPool()`(신규): 씬 재로드로 풀 안의 인스턴스는 파괴되는데 정적 대기열은 죽은 참조를 들고 있어서, 안 비우면 다음 판 첫 공격에서 `MissingReferenceException`.
- `GlobalTowerAuraRegistry.Auras.Clear()`: 파워 타워 전역 버프가 안 비워지면 재시작해도 이전 판 버프가 유지됨 (ARCHITECTURE.md 3단계에서 이미 예견했던 TODO였음 — 지금 처리).
- 이 초기화들은 전부 `GameManager.Awake()`에서 수행하는데, 다른 스크립트(`TowerBuildController` 등)의 `Awake()`가 먼저 실행돼 캐시를 재생성해버리면 늦으므로, `[DefaultExecutionOrder(-1000)]`로 `GameManager`가 씬의 모든 스크립트보다 먼저 `Awake()`하도록 강제함(Project Settings의 Script Execution Order 수동 설정 없이 코드만으로 순서 보장).

### 결과 화면 통계 구현
- `GameManager`에 `TotalGoldEarned`(누적 획득 골드, 소비해도 안 줄어듦 — `Gold`와 별개)/`EnemiesDefeated`/`SurvivalTime`(Time.deltaTime 누적, 카드 선택 일시정지 중엔 자동으로 멈춤) 추가.
- `WaveManager.GrantReward`가 처치마다 `gameManager.RecordEnemyDefeated()` 호출.

### UI 구현
- `UI/GameResultUI.cs`(신규): `GameManager.GameOver` 구독 → 4개 TMP 텍스트(도달 웨이브/처치 수/획득 골드/생존시간, `mm:ss` 포맷) 채우고 `CanvasGroup`으로 표시. `Retry` 버튼은 `Time.timeScale=1` 복원 후 현재 씬 재로드, `Title` 버튼은 같은 복원 후 지정 씬(기본 `"TitleScene"`) 로드 — 둘 다 타임스케일을 복원 안 하면 다음 씬도 멈춘 채로 시작해버림.

### 씬 세팅 필요
- `GameManager` 인스펙터에 새로 생긴 `Tower Roster`/`Card Roster`(기존 원본 에셋)/`Player`/`Base Controller` 필드 연결.
- 결과 화면 패널: `CanvasGroup` 부착 + `reachedWaveText`/`defeatedEnemiesText`/`earnedGoldText`/`survivalTimeText` TMP 4개 + `Retry`/`Title` 버튼 준비.
- 빈 오브젝트(또는 패널 루트)에 `GameResultUI` 부착 → `Game Manager`/`Wave Manager`/`Panel Group`/텍스트 4개/버튼 2개/`Title Scene Name`(실제 타이틀 씬 이름과 일치하는지 확인) 필드 연결.
- **File → Build Settings → Scenes In Build**에 현재 게임 씬과 타이틀 씬을 둘 다 추가해야 `SceneManager.LoadScene`이 정상 동작함 (둘 다 없으면 런타임 에러).

## 타워 슬롯 잔여 수 HUD 텍스트 (2026-07-06)
{{user}} 요청: 공격/스킬/파워 타워의 남은 설치 가능 수를 텍스트로 표시.
- `MapManager`에 `AttackSlotsRemaining`/`SkillSlotsRemaining`/`PowerSlotsRemaining`(각각 `max - 현재개수`) 프로퍼티 추가 — 기존 private 카운터/한도 필드는 그대로 두고 읽기 전용 노출만 추가.
- `HudController`에 `slotText1`(공격)/`slotText2`(스킬)/`slotText3`(파워) TMP 필드 추가, 매 프레임 남은 수만 텍스트로 표시.

### 씬 세팅 필요
- `HudController`의 새 `Map Manager` 필드에 기존 MapManager 오브젝트 연결.
- TMP 텍스트 3개 생성 후 `Slot Text 1`/`2`/`3` 필드에 각각 연결 (1=공격, 2=스킬, 3=파워).

## 게임오버 화면 어둡게 처리 배경 (2026-07-06)
{{user}} 발견: `GameOverBackground`가 화면 전체가 아니라 결과 카드 자체(779×535, 중앙 고정)라서, 그 자식으로 `DimBackground`를 넣으면 카드 크기 안에서만 어두워짐 — 화면 전체를 덮으려면 형제(같은 부모, 카드보다 앞 순서)로 빼야 하는데, 그러면 카드의 `CanvasGroup`(`panelGroup`)에 안 딸려서 따로 켜고 꺼야 함.

### 해결
`GameResultUI`에 `dimBackgroundGroup`(별도 `CanvasGroup`) 필드 추가, `Show()`/`Hide()`가 `panelGroup`과 `dimBackgroundGroup` 둘 다 같이 제어하도록 수정.

### 씬 세팅 필요
- `DimBackground`를 `GameOverBackground` 자식에서 빼서, `GameOverBackground`와 **같은 부모**(Canvas 등) 아래 **형제**로 이동. Hierarchy 순서는 `GameOverBackground`보다 **앞**에 둘 것(뒤에 그려지게).
- 앵커는 그대로 Stretch(0,0)~(1,1), 오프셋 전부 0 유지 — 이번엔 부모가 전체 화면 Canvas라 실제로 전체를 덮게 됨.
- `DimBackground`에 `Canvas Group` 컴포넌트 추가 (없으면).
- `GameResultUI`의 새 `Dim Background Group` 필드에 그 `CanvasGroup` 연결.

## 버튼 호버 확대 효과 (2026-07-06)
{{user}} 요청: Retry/Title 버튼 호버 시 살짝 커지게.
- `UI/UIHoverScale.cs`(신규, 범용 컴포넌트): `IPointerEnterHandler`/`IPointerExitHandler`로 호버 시 목표 스케일을 `hoverScale`(기본 1.1배)로, 벗어나면 원래대로 설정하고 `Update()`에서 `Vector3.Lerp`로 부드럽게 따라감.
- **`Time.unscaledDeltaTime` 사용** — 게임오버 화면은 `Time.timeScale=0`(일시정지) 중에 뜨는데, 일반 `Time.deltaTime`을 쓰면 그동안 값이 0이라 애니메이션이 안 움직임.

### 씬 세팅 필요
- `Retry`/`Title` 버튼 오브젝트 각각에 `Add Component → UI Hover Scale` 추가만 하면 됨 (별도 필드 연결 불필요, 자기 자신의 Transform만 사용). 필요하면 `Hover Scale`/`Transition Speed` 값 조정.

## 튜토리얼 시스템 (2026-07-06)
{{user}} 요청: 게임 시작 시 페이지 넘김형 튜토리얼(일련번호/주제/삽화/설명/Next 버튼). "SO로 관리하는 게 낫겠지?"에 확인 — 오퍼레이터 대사와 완전히 같은 이유(페이지마다 삽화 에셋을 들고 있어야 해서 JSON으론 절반만 처리됨).

### 코드 구현 완료
- `UI/TutorialSet.cs`(신규, SO): `TutorialPage`(주제/설명/삽화) 리스트.
- `UI/TutorialUI.cs`(신규): `Awake()`에서 페이지가 있으면 자동으로 첫 페이지 표시 + `Time.timeScale = 0f`(읽는 동안 게임 진행 방지 — CardManager의 레벨업 패널과 동일한 메커니즘). `Next` 버튼이 다음 페이지로, 마지막 페이지에서 누르면 `Time.timeScale = 1f` 복원 후 패널 닫힘.
- **알려진 경미한 한계**: `TowerBuildController`는 `cardManager.IsChoosing`/`gameManager.IsGameOver`만 확인하고 튜토리얼 표시 여부는 모른다(UI→게임플레이 역참조를 피하려고 의도적으로 안 엮음) — `Time.timeScale=0`이 대부분의 게임 로직(웨이브/타워 틱/이동)은 막아주지만, 이론상 튜토리얼이 떠 있는 아주 짧은 시간(게임 시작 직후, 골드도 거의 없을 시점) 동안 클릭으로 건설을 시도하는 것 자체는 막히지 않음 — 리스크가 매우 낮아 이번엔 그대로 둠.

### 씬 세팅 필요
- `TutorialSet` 에셋 생성(Create → RCCom → UI → Tutorial Set), 페이지 추가하며 주제/설명/삽화 채우기(내용은 사용자가 채울 것 — 예: 이동/공격, 스킬-오버드라이브 모드, 타워 설치, 카드 업그레이드, 웨이브 대기시간 등).
- 스크린샷 프레임 안에 `TutorialIndexText`/`TutorialTopic`/`TutorialImage`/`DescriptionText`(TMP 3개 + Image 1개) + `Next` 버튼 배치, 전체를 감싸는 오브젝트에 `CanvasGroup` 부착.
- 빈 오브젝트(또는 패널 루트)에 `TutorialUI` 부착 → `Tutorial Set`/`Panel Group`/`Index Text`/`Topic Text`/`Tutorial Image`/`Description Text`/`Next Button` 필드 연결.

---

## 건설 차단 조건 단순화 + 숫자키 선택 제거 (2026-07-06)
{{user}}: 튜토리얼 중에도 건설이 막혀야 할 것 같다는 지적 + 숫자키(1~6) 타워 선택 제거 요청.

### 건설 차단: 개별 매니저 상태 대신 Time.timeScale 하나로 통일
카드 선택(`CardManager`)/게임오버(`GameManager`)/튜토리얼(`TutorialUI`) 전부 공통적으로 `Time.timeScale = 0f`을 쓰고 있다는 점에 착안 — `TowerBuildController`/`TowerBuildPreview`의 차단 조건을 `cardManager.IsChoosing || gameManager.IsGameOver` 개별 확인에서 **`Time.timeScale <= 0f`** 단일 확인으로 교체. 튜토리얼처럼 새로운 일시정지 화면이 늘어나도 이 코드를 안 건드려도 되고, `TowerBuildPreview`에서 `cardManager`/`gameManager` 참조 자체가 필요 없어져 제거됨.

### 숫자키 타워 선택 제거
`TowerBuildController.HandleSelectionHotkeys()`(digit1~6Key) 삭제 — 스크롤 빌드 메뉴(`TowerBuildMenuUI`) 버튼 클릭이 이미 완전히 대체하고 있었고, 숫자키 입력이 다른 기능과 계속 겹치는 문제가 반복됐음. `SelectTower(index)`는 그대로 유지(빌드 메뉴 버튼이 호출).

### 씬 세팅 필요
- `TowerBuildController`/`TowerBuildPreview`에서 `Card Manager` 필드가 사라짐 — 기존에 연결돼 있었다면 자동으로 무시됨(에러 아님).
- 별도 조치 불필요, 숫자키는 이제 아무 반응 없음(빌드 메뉴 버튼으로만 선택).

## 버그 수정: 일시정지 중에도 플레이어 방향 전환/스킬 발동이 새어나감 + 스킬 이동속도 버프 (2026-07-06)
{{user}} 발견: 레벨업 카드 패널이 떠서 다 멈춘 것처럼 보여도 플레이어가 방향을 마구 바꿀 수 있음.

### 원인 — 같은 부류의 버그 (건설 차단 때와 동일)
`PlayerController.Update()`는 `Time.timeScale=0`이어도 계속 돈다. 이동 자체는 `deltaTime`이 0이라 위치가 안 바뀌어 "멈춘 것처럼" 보이지만, `UpdateFacing()`은 `deltaTime`과 무관하게 그 프레임의 입력 방향만 보고 즉시 회전시키고, `TrySkill()`의 스페이스바 감지도 `deltaTime`과 무관한 키 폴링이라 — 카드 패널이 떠 있는 동안에도 방향 전환/스킬 발동이 그대로 새어나가고 있었음.

### 해결
`PlayerController.Update()` 최상단에 `Time.timeScale <= 0f`면 즉시 `return`하는 게이트 추가 — `TowerBuildController`/`TowerBuildPreview`에 적용한 것과 완전히 같은 원칙. 이제 카드 선택/게임오버/튜토리얼 중엔 이동/회전/공격/스킬 전부 완전히 멈춤.

### 스킬 사용 중 이동속도 증가
{{user}} 요청: 오버드라이브 모드 중 이동속도 1.5배. `skillMoveSpeedMultiplier`(기본 1.5) 필드 추가, `Move()`가 `IsSkillActive`일 때만 `data.moveSpeed`에 곱해 적용 — 스킬 종료 시 자동으로 원래 속도로 복귀(별도 복원 로직 불필요, 매 프레임 다시 계산하는 방식이라).

### 씬 세팅 필요
- 없음. `Skill Move Speed Multiplier` 필드가 새로 보이면 기본값(1.5) 그대로 써도 되고 취향껏 조정.

---

## 사운드 매니저 (2026-07-06)
{{user}} 요청: 공격 타워 명중음(플레이어 사거리 내에서만 청취)/BGM 랜덤 재생/플레이어 공격음/버튼 클릭음(게임오버·튜토리얼)/스킬 사용음, 전부 AI 생성 음원이라 0.5초 강제 컷오프.

### 5번째 매니저 도입 (의도적 예외)
`SoundManager`를 `Game/Map/Wave/CardManager` 4개 원칙과는 별개로 추가 — 오디오 재생은 타워 이펙트/플레이어/UI 버튼이 공통으로 호출해야 하는 횡단 관심사라 기존 4개 중 어디에도 억지로 끼워 넣지 않는 게 낫다고 판단(“오브젝트 타입별 매니저 금지” 원칙 위반이 아니라, 애초에 “게임 흐름 단계”로 분류가 안 되는 별도 서비스 성격). `BaseController.Instance`/`GameManager.Instance`와 같은 근거로 정적 싱글톤 노출.

### 코드 구현 완료
- `Managers/SoundManager.cs`(신규): `AudioSource` 풀링(재생 중 아닌 소스 재사용, 없으면 `AddComponent`로 생성) + 활성 재생 목록을 매 프레임 `Time.unscaledDeltaTime`으로 감산해 `effectCutoffDuration`(기본 0.5초) 지나면 강제 `Stop()`. **unscaledDeltaTime을 쓴 이유**: 버튼 클릭음이 게임오버/튜토리얼처럼 `Time.timeScale=0`인 화면에서도 재생되는데, 일반 deltaTime이면 그동안 컷오프 자체가 멈춰서 원본 클립 길이(최대 2초)까지 그대로 재생돼버림.
- `PlayTowerAttack(Vector2 towerPosition)`: 플레이어와의 거리가 `player.data.attackRange` 이내일 때만 재생 — 포탑이 아무리 많아져도 소리가 다 겹쳐 감당 안 되는 문제 방지. `DamageEffect`/`PierceDamageEffect`/`PoisonDamageEffect`/`SplashDamageEffect` 전부 명중 처리 직후 호출.
- `PlayPlayerAttack()`: 항상 재생, `PlayerController.TryAutoAttack()`에서 호출.
- `PlaySkill()`: 오버드라이브 모드 발동 시작 시 1회, `PlayerController.TrySkill()`에서 `SkillUsed` 발생 지점과 함께 호출.
- `PlayButtonClick()`: `TutorialUI.HandleNext()`, `GameResultUI`의 Retry/Title 처리부에서 호출.
- BGM: `bgmPlaylist` 배열에서 매번 랜덤 선택하되 직전 곡과 같은 인덱스는 재추첨해 연속 반복 방지, `Update()`에서 `bgmSource.isPlaying`이 꺼지면 자동으로 다음 곡.

### 부수 발견: 게임오버 화면의 씬 전환이 클릭음을 잘라먹는 문제
`SceneManager.LoadScene`은 현재 씬을 즉시 파괴하므로, 클릭 즉시 전환하면 클릭음이 거의 안 들림. `GameResultUI`에 `sceneChangeDelay`(기본 0.15초) 수동 타이머를 추가해, 클릭음을 먼저 재생하고 그 시간만큼 지난 뒤에야 실제로 씬을 전환하도록 함 — `Invoke()`가 `Time.timeScale`에 영향받는지 확신이 안 서서(게임오버 화면은 timeScale=0 상태), 이 프로젝트에서 계속 검증되어 온 "수동 타이머 + `Time.unscaledDeltaTime`" 패턴을 그대로 재사용.

### 씬 세팅 필요
- 빈 GameObject(예: `SoundManager`)에 `SoundManager` 스크립트 부착, `Player` 필드에 플레이어 오브젝트 연결.
- `Tower Attack Clip`/`Player Attack Clip`/`Skill Clip`/`Button Click Clip` 각각에 AI로 생성한 음원 연결.
- `Bgm Source`에 별도 `AudioSource` 컴포넌트(같은 오브젝트에 추가) 연결, `Bgm Playlist` 배열에 BGM 곡들 등록.
- `Effect Cutoff Duration`(기본 0.5초)/`Effect Volume`은 기본값 또는 취향껏 조정.
- `GameResultUI`/`TutorialUI` 쪽은 별도 필드 연결 불필요 — `SoundManager.Instance`를 코드에서 자동으로 찾아 호출.

---

## 오디오 볼륨 설정(Configuration 화면) + 타이틀 씬 사운드 확장 (2026-07-06)
{{user}}: 타이틀 씬에 Master/BGM/SFX 볼륨 슬라이더 + 화면설정이 있는 Configuration 화면을 발견 — SoundManager를 DontDestroyOnLoad로 처리해야 하는지, 이펙트/BGM에 "태그"를 붙여야 하는지 질문.

### 답변 반영: DontDestroyOnLoad 대신 AudioMixer
`SoundManager` 인스턴스는 계속 씬 전용으로 유지(DontDestroyOnLoad 불필요) — 대신 **AudioMixer** 에셋(프로젝트 에셋이라 씬에 종속 안 됨)의 노출 파라미터로 볼륨을 제어하면, 한 번 `SetFloat`한 값이 씬 전환/인스턴스 재생성과 무관하게 세션 내내 유지된다. "태그"라고 부른 개념은 Unity GameObject 태그가 아니라 **AudioMixer 그룹**(Master 아래 BGM/SFX 자식 그룹) — 각 AudioSource의 Output 필드에 해당 그룹을 연결하면 그 그룹 볼륨의 영향을 받는다.

### 코드 구현 완료
- `SoundManager`에 `AudioMixer`/`AudioMixerGroup(bgm)`/`AudioMixerGroup(sfx)` 참조 추가. `SetMasterVolume`/`SetBgmVolume`/`SetSfxVolume`(0~1 슬라이더 값 → `Mathf.Log10(value)×20` 데시벨 변환 → `AudioMixer.SetFloat`) + `PlayerPrefs` 저장(키: `Audio_MasterVolume` 등). `Awake()`에서 `ApplyPersistedVolumes()`로 세션 시작 시(씬 로드마다) 저장된 값을 다시 적용 — 앱을 껐다 켜도 유지됨.
- 슬라이더 조작 중엔 `PlayerPrefs.SetFloat`(메모리에만 반영, 빠름)만 하고, 실제 디스크 쓰기(`PlayerPrefs.Save()`)는 `SaveVolumeSettings()`로 분리해 **APPLY 버튼을 눌렀을 때만** 확정 — 드래그마다 디스크 I/O가 발생하지 않도록.
- 동적으로 생성되는 SFX 풀 AudioSource는 생성 시점에 `sfxMixerGroup`을 코드로 할당(디자인 타임에 미리 설정할 방법이 없어서), `bgmSource`/`ambienceSource`는 `Awake()`에서 `bgmMixerGroup`으로 자동 할당.
- 타이틀 씬용 클립 3종 추가: `titleClickClip`/`mainMenuClickClip`/`settingsButtonClickClip` (+ 각각 `PlayTitleClick()`/`PlayMainMenuClick()`/`PlaySettingsButtonClick()`). 기존 게임플레이용 `buttonClickClip`/`PlayButtonClick()`은 그대로 유지(게임오버/튜토리얼 버튼 전용).
- 배경 앰비언스(바람소리 등) 지원: `ambienceSource`/`ambienceClip` — `Start()`에서 루프 재생 시작, BGM 믹서 그룹 공유(전용 슬라이더가 없어서 BGM 볼륨에 같이 묶임 — 필요하면 나중에 전용 그룹/슬라이더로 분리 가능).
- `UI/AudioSettingsUI.cs`(신규): Configuration 화면의 슬라이더 3개를 `SoundManager`의 볼륨 프로퍼티/메서드에 연결. `Apply` 버튼 클릭 시 클릭음 재생 + `SaveVolumeSettings()` 호출.
- 타이틀씬 클릭음 배선 완료: `TitleSceneController.BeginTransition()`(타이틀 화면 첫 클릭/입력) → `PlayTitleClick()`, `TitleMenuTextButton.InvokeAction()`(새 게임/설정/뒤로가기 등 메인 메뉴 버튼) → `PlayMainMenuClick()`, `TitleConfigurationButton.Invoke()`(해상도/화면모드/VSync/Apply/Back 등 설정 화면 버튼) → `PlaySettingsButtonClick()`. 전부 `SoundManager.Instance != null` 가드 후 호출 — 씬에 SoundManager가 아직 없어도(또는 파괴된 뒤) NRE 없이 조용히 무시됨.

### 씬 세팅 필요
- **AudioMixer 에셋 생성**: Project 창 → `Create → Audio Mixer` (이름 예: `MainMixer`). 믹서 창 열어서 `BGM`/`SFX` 그룹 2개를 Master의 자식으로 생성. 각 그룹 선택 → Volume 슬라이더 우클릭 → `Expose "Volume (of OOO)" to script` → 하단 `Exposed Parameters`에서 이름을 각각 `MasterVolume`(Master 그룹 것)/`BGMVolume`/`SFXVolume`으로 정확히 변경(코드의 상수 문자열과 일치해야 함).
- **양쪽 씬의 `SoundManager`**: `Audio Mixer` 필드에 그 에셋 연결, `Bgm Mixer Group`/`Sfx Mixer Group`에 각각 방금 만든 그룹 드래그.
- **게임 씬**: 기존 필드(Tower/Player Attack Clip 등) 그대로 두고, 새 필드는 필요 없으면 비워둠.
- **타이틀 씬**: 새 `SoundManager` 오브젝트 생성 → `Title Click Clip`/`Main Menu Click Clip`/`Settings Button Click Clip`/`Ambience Source`(새 AudioSource)+`Ambience Clip`(바람소리)/`Bgm Source`+`Bgm Playlist`(타이틀 BGM) 채우기. `Player`나 게임플레이 전용 클립은 비워둘 것.
- **Configuration 화면**: 빈 오브젝트에 `AudioSettingsUI` 부착 → `Master/Bgm/Sfx Volume Slider` 3개 + `Apply Button` 연결. 슬라이더의 `Min/Max Value`는 0~1로 맞출 것.
- ~~타이틀/메인메뉴 버튼들의 `OnClick()`에서 `SoundManager.Instance.PlayTitleClick()`/`PlayMainMenuClick()`를 직접 호출하도록 연결~~ → 코드 배선 완료 (`TitleSceneController`/`TitleMenuTextButton`/`TitleConfigurationButton`), 남은 건 각 클립 에셋 채우기뿐.

---

## 볼륨 슬라이더가 BGM/SFX에 반영 안 되던 버그 수정 (2026-07-06)
{{user}} 발견: Configurator에서 슬라이더를 내려도 BGM이 작아지지 않음.

### 원인
실제 Configuration 화면(해상도/화면모드/VSync까지 함께 관리)은 우리 `AudioSettingsUI`가 아니라 별도로 작성된 `TitleConfigurationController`가 담당하고 있었음 — 이 스크립트가 자기만의 볼륨 처리를 갖고 있었는데, `Apply()`에서 Master는 `AudioListener.volume`(유니티 전역 출력 스칼라)에 반영해 그나마 효과가 있었지만, BGM/SFX는 자체 PlayerPrefs 키(`Settings.BGMVolume` 등, `SoundManager`의 `Audio_BgmVolume`과는 별개)에 값만 저장하고 실제로 어디에도 적용하지 않았음 — `SoundManager`/`AudioMixer`를 전혀 호출하지 않는 완전히 분리된 반쪽짜리 병렬 시스템이었음.

### 수정
- `TitleConfigurationController`의 슬라이더 초기화/적용 로직을 `SoundManager`에 완전히 위임하도록 교체: `LoadSettings()`에서 `SoundManager.Instance.MasterVolume`/`BgmVolume`/`SfxVolume`로 슬라이더 초기값을 채우고, 각 슬라이더의 `onValueChanged`를 `SoundManager.Instance.SetMasterVolume`/`SetBgmVolume`/`SetSfxVolume`에 직접 연결(드래그 즉시 실시간 미리듣기). `Apply()`는 이제 `SoundManager.Instance.SaveVolumeSettings()`만 호출 — 화면모드/VSync는 기존 그대로 자체 PlayerPrefs 유지(오디오와 무관한 설정이라 그대로 둠).
- `AudioListener.volume` 조작과 자체 오디오 PlayerPrefs 키(`Settings.MasterVolume` 등) 전부 제거.
- **`AudioSettingsUI.cs` 삭제**: `TitleConfigurationController`와 기능이 완전히 겹치는 데다, 씬/프리팹 어디에도 실제로 붙어있지 않았음(스크립트 GUID로 프로젝트 전체 검색해 확인) — 죽은 코드였으므로 유지할 이유 없음.

### 교훈
같은 역할(볼륨 설정)을 하는 스크립트가 두 번 따로 작성되면 이런 "둘 다 있지만 실제로 쓰이는 건 하나뿐이고 그게 하필 미완성"인 상황이 생길 수 있음 — 다른 주체(사람이든 AI든)가 UI를 붙일 때는 기존에 이미 있는 매니저/서비스가 있는지 먼저 확인하고 거기 위임하는 게 안전.

---

## 타워 설치/철거 사운드 추가 (2026-07-06)
`SoundManager`에 `towerBuildClip`/`towerDemolishClip` + `PlayTowerBuild()`/`PlayTowerDemolish()` 추가. `TowerBuildController.HandleBuildClick()`은 `mapManager.Build()` 성공 직후, `HandleDemolishClick()`은 `mapManager.RemoveTower()` 성공 직후에만 각각 호출 — 슬롯 부족/골드 부족으로 실패하는 경로에서는 호출되지 않음(실패는 오퍼레이터 대사 쪽 책임).

### 씬 세팅 필요
- 게임 씬 `SoundManager`의 `Tower Build Clip`/`Tower Demolish Clip` 필드에 각각 음원 연결.

---

## 타이틀 Configuration 화면: 해상도 변경 실제 구현 (2026-07-06)
{{user}} 결정: 코덱스가 만든 `TitleConfigurationController.PreviousResolution()`/`NextResolution()`이 라벨만 새로고침하고 실제로 아무 동작도 안 하는 빈 껍데기였음(당장은 손대지 말라고 지시했었는데, 최종 빌드 전 시간이 남아 마저 구현하기로 함).

### 구현
- `BuildResolutionList()`(신규): `Screen.resolutions`에서 너비×높이 기준으로만 중복 제거한 목록 생성 (갱신주파수별 중복 항목 제거 목적). 목록이 비면(이론상 발생 안 하지만 방어적으로) 현재 `Screen.width/height` 하나만 넣음.
- `LoadSettings()`: 저장된 해상도(`Settings.ResolutionWidth/Height`)를 목록에서 찾아 `pendingResolutionIndex` 복원, 없으면(다른 모니터에서 저장된 값 등) 목록의 마지막(가장 높은 해상도, 보통 네이티브)으로 대체.
- `PreviousResolution()`/`NextResolution()`: 이제 `pendingResolutionIndex`를 목록 범위 안에서 실제로 -1/+1 이동.
- `RefreshLabels()`의 해상도 텍스트: `Screen.width/height`(현재 실제 해상도) 대신 `availableResolutions[pendingResolutionIndex]`(대기 중인 선택값)을 표시하도록 변경 — Apply 전에도 버튼 클릭에 따라 미리보기 텍스트가 바뀌어야 하므로.
- `Apply()`: `Screen.fullScreenMode`/`Screen.fullScreen`을 따로 설정하던 걸 `Screen.SetResolution(width, height, fullScreenMode)` 한 번 호출로 교체 — 해상도와 창모드를 동시에 안 넘기면 창모드 전환 시 해상도가 안 맞아 화면이 어긋나는 경우가 있어(유니티 공식 권장 방식이 이 조합 호출), 겸사겸사 정리. 선택한 해상도도 `Settings.ResolutionWidth/Height` 키로 같이 저장.

### 씬 세팅 필요 없음
기존에 이미 배선된 `PreviousResolution`/`NextResolution` 버튼 OnClick 그대로 재사용 — 스크립트 로직만 채운 것이라 인스펙터 작업 불필요.

---

## WebGL에서 타이틀 영상이 안 나오는 문제 → StreamingAssets URL 재생으로 전환 (2026-07-06)
{{user}} 발견: 타이틀 실루엣 안 영상(VideoPlayer + RenderTexture)이 데스크톱에선 잘 나오는데 Web 빌드에선 아예 재생 안 됨.

### 원인
WebGL 플랫폼의 알려진 제약 — VideoPlayer가 **VideoClip 에셋 재생을 지원하지 않고 URL 소스만 지원**함. 에러도 없이 조용히 안 나오는 게 특징.

### 해결: `UI/StreamingVideoSource.cs` (신규)
- 영상 파일을 `Assets/StreamingAssets/`에 두면 빌드에 원본 그대로 복사되고 WebGL에선 URL로 서빙됨 — `Awake()`에서 `VideoPlayer.source = Url` + `Application.streamingAssetsPath` 기반 URL을 배선.
- 에디터/데스크톱에서도 같은 경로가 로컬 파일로 동작하므로 플랫폼 분기 없이 이 방식 하나로 통일.
- 오디오 출력 모드가 '없음'이라 브라우저 자동재생 정책(음소거 아니면 자동재생 차단)에도 안 걸림.

### 씬/에셋 세팅 필요
- `Assets/StreamingAssets` 폴더 생성(정확히 이 이름), 영상 원본 파일(mp4, H.264 코덱 권장)을 그 안에 복사.
- Video Player 오브젝트에 `StreamingVideoSource` 부착, `File Name`에 파일명 입력.
- 기존 `비디오 클립` 필드는 비워도 됨(스크립트가 URL 모드로 덮어씀). 임포트된 VideoClip 에셋은 이제 빌드에 불필요하므로 용량 아끼려면 삭제 가능.

---

## 진행 로그

### 2026-07-04 — 데이터 컨테이너 1차 작업
- `Assets/Scripts/Data/EnemyData.cs`, `TowerData.cs`, `PlayerData.cs` 작성.
- 확인 완료: 플레이어도 체력/피격 있음 (거점 체력과 별개의 값).
- 다음 단계: 사용자가 Unity 에디터에서 컴파일 에러 없는지 확인 → 이후 SO(Ability) 레이어로 진행.

### 2026-07-04 — 효과 훅 레이어 (Tower/Enemy)
- GDD 기술 노트 반영해 `ITowerEffect`/`TowerEffectBase`/`ITowerAura`, `IEnemyEffect`/`EnemyEffectBase`, `StatusType`, `IDamageable`, `TowerInstance`/`TowerContext`, `EnemyInstance`/`EnemyContext` 작성.
- 확인 완료: StatusType은 새로 정의, Enemy도 Tower와 동일 훅 패턴 적용.
- 다음 단계: Definition SO 래퍼 + 구체 효과 구현체 (사용자 확인 후 진행).
