# Handoff — 아군 유닛 · 적 데이터 Addressables 전환

## 이 작업이 무엇인가

오퍼레이터는 이미 **JSON 레시피 → Builder → Definition SO + Catalog + Addressables 그룹**
파이프라인을 갖추고 있다. 아군 유닛과 적을 같은 파이프라인으로 통일하고, 각각 독립
Addressable 그룹으로 분리해 개별 Local/Remote 서빙이 가능하게 만드는 것이 목표다.
전투 코드(`EnemyInstance`, `AllyUnitInstance`, `EnemyView`, `AllyUnitView`, 효과 SO)는 건드리지 않는다.

**전체 계획서**: `C:\Users\admin\.claude\plans\floofy-jumping-snowflake.md` — 먼저 읽을 것.
**저장소 규약**: `AGENTS.md` — 하드 룰. 반드시 정독.

## 확정된 방향 (사용자 승인 완료)

| 항목 | 결정 |
|---|---|
| 범위 | 적 + 아군 **전면 전환** |
| 아군 원본 | **JSON 레시피로 전환** — 기존 "SO가 단일 원본" 결정을 의도적으로 뒤집음 |
| 리모트 단위 | 아군·적 **모두 개별 리모트** 지원 |

## 지금까지 완료된 것 (브랜치 `feature/enemy-studio`, 커밋 2개)

- `84b5b5a` 적 파이프라인 코드 골격 + 마이그레이터
- `5689565` 적 3종 마이그레이션 실행 결과

구체적으로:
- **Phase 0**: `AddressablesRemoteProfileConfigurator`가 원격 카탈로그를 코드로 활성화
  (`BuildRemoteCatalog=true` + RemoteCatalog 경로를 Remote 프로파일 변수에 연결).
  단 `ConfigureActiveProfile` 안에서만 호출되므로 **환경변수를 세팅하고
  `RCCom/Addressables/Configure Active Remote Profile From Environment`를 실행해야 실제로 켜진다.**
- **Phase 1 전체**: `EnemyCatalog`/`EnemyCatalogEntry`(런타임), `EnemyAssetRecipe`/`EnemyAssetBuilder`/
  `EnemyCatalogBuilder`/`EnemyAssetValidator`/`EnemyRecipeMigrator`(에디터) 신규.
  `EnemyRoster`는 `enemyIds`(직렬화) + `[NonSerialized] enemies`로 전환.
  `StageEnemySpawn.enemy` → `enemyId`, `WaveManager` 2곳 수정.
  적 3종을 `enemy-normal`/`enemy-rusher`/`enemy-tanker` 슬러그로 재부여하고
  `Assets/Data/Enemies/{slug}/EnemyDefinition.asset`로 `MoveAsset`(GUID 보존).
  스테이지 7종 spawn 41건을 id 문자열로 변환. `Enemy-{id}-Local` 그룹 3개 + `EnemyCatalog` 생성.

**검증 상태**: 컴파일 통과. 적/스테이지/오퍼레이터 검증기 + WebGL 빌드 사전 검증 전부 오류 0.
기존 경고만 잔존(`축적.asset` 미등록 1건, 스테이지 설명·보상 누락 14건) — 둘 다 이번 작업과 무관.

## 남은 작업

### Step 3 — `EnemyStudioWindow` 신규 (계획서 Phase 2)
`Assets/Editor/EnemyStudioWindow.cs`, 메뉴 `RCCom/Enemies/Open Enemy Studio`.
`OperatorStudioWindow`의 좌측 목록 + 탭 골격을 그대로 차용.
탭: Identity(id·catalogOrder·displayName·kind·remoteContent) / Combat(EnemyData) /
Presentation(스프라이트 경로·`spriteForwardOffsetDegrees`·효과 경로 목록) /
Package(주소·그룹명 표시 + Save·Validate·Build 버튼).

### Step 4 — 아군 유닛 파이프라인 + Studio 재작업 (계획서 Phase 3)
적 쪽을 그대로 미러링. 마이그레이션 대상은 `cassia-guard`, `cassia-vanguard`,
`TestRifleman`, `TestGuard` 4개 + AllyUnitRoster 5개.
`OperatorAssetBuilder`의 아군 Roster 복제 로직을 ID 목록 복제로 변경해야 한다.
`AllyUnitStudioWindow`(1,314줄)는 Units 탭만 레시피 편집으로 교체하고
**Rosters/Audit 탭과 `RCCom.GeneratedOperator` 읽기 전용 잠금은 유지**할 것.

### Step 5 — 프리로드 연동 (계획서 Phase 4)
`Assets/Scripts/Runtime/BattleContentCache.cs` 신규.
`UILoadingTransition.Run(IEnumerator)`가 **화면이 덮인 구간에서 코루틴을 돌리는 훅으로
이미 존재하고 호출자가 0건**이다 — 이것이 프리로드 자리다.
`WaveManager`에 남겨둔 TODO 주석이 이 캐시를 가리킨다.
⚠️ `AllyUnitFoundationVerifier.cs:121`이 `ResolveAllyUnitRoster()`의 **참조 동일성**을
단언한다. 런타임 클론을 반환하게 되므로 ID 집합 비교로 **반드시** 고쳐야 한다
(미수정 시 `BuildScript` 플레이어 빌드가 막힌다).

### Step 6 — 문서 (계획서 Phase 5)
`EXPANSION_LOG.md`에 결정과 근거 기록. 특히 아군 SO 단일 원본 결정을 뒤집은 이유와
그 대가(경로 문자열 참조가 에셋 이동 시 끊김)를 명시.
`AGENTS.md` §9의 "Addressables 미설치" 기술 갱신 + 아래 CLI 정정 반영.

## ⚠️ 함정 — 시간을 크게 아껴줄 것들

### 1. `unity` CLI 호출법 (이 세션에서 확인한 beta.5 기준)
이 세션에 연결된 CLI 버전 `1.0.0-beta.5`에서는 다음 형식이 동작했다:
- **라이브 명령에는 `--project-path`를 붙이지 않는다.** 이 연결형 CLI에서는 붙이면 실행 중인
  에디터를 못 찾는다. `unity status` (O) / `unity status --project-path .` (이 환경에서는 빈 테이블)
- **메뉴는 전용 `menu` 명령과 `--path` 인자를 사용한다.**
  ```
  unity command menu --path "RCCom/Enemies/Validate Enemy Assets"     # 이 환경에서 O
  unity command menu "RCCom/..."                                       # 이 환경에서는 메뉴 목록만 덤프
  unity command menu path="RCCom/..."                                  # 이 환경에서는 동일
  ```
  `unity command menu`를 인자 없이 부르면 등록된 전체 메뉴 경로를 덤프한다 — 메뉴가
  실제로 등록됐는지 확인할 때 유용하다.
- 사용 가능한 명령과 파라미터는 이 환경에서는 `unity list --format json`, 프로젝트 지정형
  CLI에서는 `unity list --project-path . --format json`으로 확인할 것.
- 다른 협업자 CLI에서 프로젝트 경로 인자를 요구하거나 `eval` 메뉴 실행을 제공한다면 그 형식을
  삭제하지 말고 함께 사용한다. 예: `unity command eval "UnityEditor.EditorApplication.ExecuteMenuItem(\"RCCom/Enemies/Validate Enemy Assets\")" --project-path .`

### 2. `unity command console`은 지금 매우 크다 (70KB+)
`PlayerController.TryAutoAttack`이 매 프레임 `[PlayerDebug]` 로그를 뱉는다.
통째로 컨텍스트에 넣지 말고 반드시 필터링할 것. 예:
```
unity command console 2>&1 | grep -oE '"level":"(error|exception)"' | sort | uniq -c
```

### 3. 에디터가 Play 모드면 리컴파일이 안 끝난다
`unity command recompile` → `recompile_status`가 `completed`로 안 가면 Play 중인지 의심할 것.
**Play 모드를 직접 끄거나 게임을 조작하지 마라 — 사용자에게 요청할 것.**

### 4. 검증기가 플레이어 빌드 경로에 있다
`BuildScript` → `AddressablesBuildValidator.ValidateOrThrow` → `OperatorAssetValidator.ValidateAll`.
Roster 스키마를 바꾸면 검증기를 **같은 커밋에서** 함께 갱신해야 빌드가 안 막힌다.

### 5. 마이그레이션은 항상 Dry Run 먼저
`MoveAsset`은 GUID를 보존하지만 Unity 작업은 대부분 Undo가 안 된다.
실행 전 커밋해 복구 지점을 만들 것. 롤백은 `git checkout -- Assets/Data` + `git clean`.

### 6. `git stash`를 Unity가 켜진 상태에서 쓰지 마라
이번에 한 번 썼고 운 좋게 복구됐지만, 에디터가 에셋을 잡고 있는 동안 작업 트리를 통째로
갈아치우는 것은 위험하다. 비교가 필요하면 `git show HEAD:path`로 읽어라.

## 알아둘 설계 사실

- **`FindById`는 런타임에서 호출되지 않는다.** 모든 해석이 직접 객체 참조 또는 리스트
  인덱스다. "FindById 내부만 캐시로 바꾸면 된다"는 접근은 통하지 않는다.
- **적 3종의 `attackRange 0` / `attackInterval 1`은 정상이다.** 근접 전용 설계이고
  `ContactDamageEffect`로만 피해를 준다. 필드가 에셋 저작 이후에 추가돼 YAML에 키가 없을 뿐.
  검증기에서 "미설정"으로 잡아 임의 보정하지 마라.
- **`EnemyData.kind`는 런타임에서 읽히지 않는다.** 카탈로그 표시용 메타로만 쓴다.
- 리모트 콘텐츠의 스프라이트 참조는 카탈로그에서 **반드시 null로 비운다**. `Color tint`는
  값 타입이라 의존성을 만들지 않으므로 항상 복사해 다운로드 전 색 스와치를 그린다.

## 작업 방식

사용자 지시: **구현은 Sonnet 단일 에이전트에 단계별로 위임하고, 메인은 검수만 한다.**
실 플레이 테스트는 **사용자에게 요청**한다 — 에디터를 붙잡고 Play를 누르며 스크린샷을
찍는 식으로 억지로 검증하지 말 것.

## 최종 플레이 검증 항목 (전부 끝난 뒤 사용자에게 요청)

1. 스테이지 모드 — 스테이지 선택 → 로딩 중 적 프리로드 → 전투에서 정상 스폰
2. 무한 모드 — 웨이브 진행에 따라 적 3종이 모두 등장
3. 아군 배치 — 오퍼레이터 선택 후 배치 메뉴에 유닛 표시, 배치·교전 정상
4. **재시도 경로** — 결과 화면 → 재시도. `GameResultUI`가 `UILoadingTransition`을 우회해
   `SceneManager.LoadScene`을 직접 부르므로 캐시 생존 여부가 걸린다. **가장 깨지기 쉽다.**

## 2026-08-24 이어받은 결과

- Step 3 `EnemyStudioWindow`를 구현하고 `7b20db0`에 커밋했다.
- Step 4 아군 유닛 4종을 JSON 레시피로 마이그레이션했다. Definition 4종, `AllyUnitCatalog`, 유닛별 Addressables Local 그룹 4개, Roster 5개의 `unitIds`를 Unity Editor API로 생성·갱신했다. 이전 Roster의 Definition 참조는 `ForceReserializeAssets`로 제거했다.
- `AllyUnitStudioWindow` Units 탭을 레시피 편집으로 전환했고, Package 탭에서 저장·검증·전체 빌드를 실행할 수 있다. 오퍼레이터 카탈로그 미리보기는 `AllyUnitCatalogEntry`를 사용한다.
- Step 5 `BattleContentCache`를 추가했다. 오퍼레이터 선택 뒤 아군 유닛을, Stage/Endless 출격 뒤 적을 프리로드하며, `WaveManager`와 `OperatorLoadoutSession`은 런타임 Roster 클론과 캐시를 사용한다. Retry 씬 재로드에서는 캐시 핸들을 유지한다.
- `AllyUnitFoundationVerifier`는 참조 동일성 대신 런타임 클론의 ID/Definition 계약을 검증한다.
- 컴파일, Foundation 계약, AllyUnit/Operator/Stage 검증, WebGL Addressables 사전 검증을 통과했다. 남은 기존 경고는 미등록 `축적.asset` 타워 1건이다.
- 아직 하지 않은 것: 최종 WebGL 플레이어 빌드, 사용자 수동 Play 검증 4개 항목, 실제 CDN URL을 반영한 Remote Profile 설정. 이 세션의 연결형 beta.5에서는 Unity 라이브 명령을 `--project-path` 없이, 메뉴를 `unity command menu --path "..."`로 실행했다. 다른 협업자 CLI는 `unity list --format json` 또는 `unity list --project-path . --format json`으로 지원 문법을 확인하고, 필요한 경우 `--project-path .`·`eval` 형식을 병기해 사용한다.
