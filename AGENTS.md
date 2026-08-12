# AGENTS.md — R&C Company (TVD)

> 이 저장소에서 작업하는 AI 에이전트를 위한 상시 컨텍스트. **작업 시작 전 이 문서 전체를 읽을 것.**
> 여기 적힌 규칙은 제안이 아니라 제약이다. 어기면 빌드가 깨지거나 이미 만들어진 에셋이 유실된다.

---

## 1. 프로젝트 개요

**R&C Company** — Unity 6 (URP 2D) 기반 탑뷰 하이브리드 디펜스 게임.
플레이어가 직접 조작하며 싸우는 동시에, 자원으로 타워를 설치해 거점을 방어한다. 레벨업 시 랜덤 3택 카드로 성장하는 로그라이트 구조.

| 항목 | 값 |
|---|---|
| Unity 버전 | `6000.3.13f1` (변경 금지) |
| 렌더 파이프라인 | URP 2D |
| 입력 | 신규 Input System (`UnityEngine.InputSystem`) |
| UI | TextMeshPro + 레거시 `UnityEngine.UI` |
| 빌드 타깃 | **WebGL(Web) 필수** + Windows Standalone |
| 씬 | `Assets/Scenes/TitleScene.unity`, `Assets/Scenes/DefenseScene.unity` |
| 스크립트 | `Assets/Scripts/` 하위 92개 (2026.08 기준), 전부 `Assembly-CSharp` |

### 현재 구현되어 있는 것
플레이어 컨트롤러(이동/자동공격/오버드라이브 스킬) · 타워 18종(공격·스킬·파워) · 적 3종 · 예산 기반 웨이브 절차적 생성 · 업그레이드 카드 27장 · 오퍼레이터 대사 시스템 · 튜토리얼 · 결과 화면 · 타이틀 씬 · AudioMixer 사운드 시스템.

이 코드베이스는 2026.07 넥슨 게임잼 제출작으로 3일 만에 만들어졌고, **모든 구조적 결정과 그 이유가 `Assets/Docs/ARCHITECTURE.md`(약 1,100줄)에 기록되어 있다.**

---

## 2. 이번 작업의 목표

**OpenAI Game Builders Seoul — Track 1** 출품을 위한 확장 개발. (접수 마감 2026.08.26)

기존 게임 위에 아래를 얹는다. 상세는 `Assets/Docs/EXPANSION_PLAN.md` 참조.

1. **오퍼레이터 로드아웃 시스템** — 오퍼레이터를 연출용 캐릭터에서 "플레이 스타일 패키지"(플레이어 스탯 보정 + 사용 가능 타워 풀 + 카드 풀 + 유닛 로스터 + 대사 + 아트)로 승격
2. **아군 유닛 시스템** — 자원 소비 → 아군 소환 → 자동 진격 → 적과 조우 시 교전
3. **PlayerProfile 영속 데이터** — 세션 데이터와 분리된 계정 데이터, 저장소는 인터페이스로 추상화
4. **Addressable 패키징 + 원격 로딩** — 오퍼레이터 1명 = Addressable 그룹 1개. 빌드에 없는 오퍼레이터를 실서버에서 라이브로 내려받아 즉시 플레이

**심사 기준상 가장 중요한 것**: 이 확장이 *"전투 코드를 한 줄도 고치지 않고 데이터만으로 신규 오퍼레이터를 추가할 수 있는 구조"* 임을 실증하는 것. 따라서 **오퍼레이터를 추가할 때마다 새 C# 클래스가 필요해지는 설계는 그 자체로 실패한 설계다.** 기존 효과 훅 SO 패턴(`ITowerEffect`/`ICardEffect`)을 그대로 확장해, 신규 오퍼레이터가 기존 효과 에셋의 **조립만으로** 만들어지도록 설계할 것.

---

## 3. 실행 환경 — 너와 도구 체인 ★가장 중요★

### 3-1. 이 프로젝트는 AI 최대 활용을 전제로 굴러간다

| 주체 | 역할 |
|---|---|
| **Codex (로컬)** | **주 개발 주체.** 런타임 코드 + 에디터 자동화 코드 + 빌드 스크립트 작성, **그리고 `unity` CLI를 직접 호출해 실행·검증까지** |
| **`unity` CLI** | 네가 쓰는 도구. 에디터 제어(Pipeline `eval`) · 헤드리스 빌드 · 컴파일 검증 |
| **Claude Code (로컬)** | 통합·리뷰·보조 |
| **사람** | 방향 결정, 아트 생성, 최종 검수 |

**핵심**: 너는 로컬에서 이 저장소를 직접 보고 작업하며, **Unity 에디터에 `unity` CLI를 통해 실제로 도달할 수 있다.** 즉 "코드를 쓰고 사람에게 넘기는" 구조가 아니라, **네가 코드를 쓰고 → 컴파일을 확인하고 → 에셋을 생성하고 → 빌드까지 돌리는** 구조다. 사람에게 넘기는 것은 그 경로로 불가능한 것(아트 생성, 방향 결정)뿐이다.

### 3-2. 네가 직접 할 수 있는 것

| 대상 | 방식 |
|---|---|
| `.cs` 파일 (런타임) | 직접 작성 |
| `.cs` 파일 (에디터 툴, `Assets/Editor/`) | 직접 작성 — **이게 너의 최대 레버리지다** |
| `.md`, `.json` | 직접 작성 |
| 컴파일 검증 | `unity command recompile` → `console` (§3-4) |
| SO 에셋(`.asset`) 생성 | 에디터 툴을 쓰고 CLI로 실행 (§3-3) |
| 프리팹 조립 / 필드 연결 | 동일 |
| 씬 배치 | 동일 — **단 `.unity` 파일 텍스트 직접 편집은 금지** |
| 헤드리스 빌드 | `unity build --target WebGL --execute-method ...` |

**절대 하지 말 것**
- `.unity` / `.prefab` / `.asset` 파일을 **텍스트로 직접 편집** — YAML 구조가 깨지면 씬 전체가 유실된다. 이런 파일은 반드시 에디터 API(`eval` 또는 에디터 툴) 경유로만 수정할 것
- **`.meta` 파일 직접 생성** — Unity가 자동 생성한다. 임의 생성 시 GUID가 충돌해 기존 에셋 참조가 끊긴다
- `Library/` `Temp/` `Logs/` `Build*/` 를 읽거나 수정 — gitignore 대상이고 용량만 크다
- **`Assets/Gutty Kreum/` 을 커밋하거나 새로 참조** — 유료 에셋이라 로컬 디스크에는 있지만 gitignore로 저장소에서 제외되어 있다. 라이선스상 재배포 불가이므로 절대 커밋하지 말고, 신규 코드가 이 경로에 의존하게 만들지도 마라

### 3-2-1. 사람에게 넘겨야 하는 것 (이것만)
- **아트 에셋 생성** (신규 오퍼레이터 초상화, 유닛 스프라이트)
- **방향 결정** — `EXPANSION_PLAN.md` §5 미확정 항목
- **외부 인프라** — 배포 서버 CORS 설정, 패키지 설치 승인
- **되돌리기 어려운 조작 승인** — §3-5 참조

### 3-3. Unity 에디터를 조작하는 방법

에셋 생성·프리팹 조립·필드 연결이 필요하면 아래 둘 중 하나로 직접 처리한다. 위쪽이 더 좋다.

**① 커밋되는 에디터 툴** — 반복 실행되거나 나중에 또 쓸 작업
```csharp
// Assets/Editor/OperatorAssetBuilder.cs
public static class OperatorAssetBuilder
{
    [MenuItem("RCCom/Build Operator Assets")]
    public static void BuildAll() { /* ... */ }
}
```
→ 실행: `unity command eval "UnityEditor.EditorApplication.ExecuteMenuItem(\"RCCom/Build Operator Assets\")" --project-path .`

**② 일회성 eval 스크립트** — 한 번 쓰고 버릴 작업
`Tools/eval/` 아래 `.cs`로 작성 → `unity command eval_file Tools/eval/xxx.cs --project-path .`

**eval 작성 시 반드시 지킬 것** (실패의 대부분이 여기서 나온다)
- **완전한 타입명**을 쓸 것: `UnityEditor.AssetDatabase`, `UnityEngine.GameObject`. eval 스니펫은 프로젝트의 `using`을 상속하지 않는다
- 에셋을 건드렸으면 **끝에 `UnityEditor.AssetDatabase.SaveAssets()` + `.Refresh()`** 호출. 없으면 메모리에만 반영되고 디스크에 저장되지 않는다
- **한 번의 eval에 묶어서 처리하라.** 필드 하나당 한 번씩 호출하면 왕복 오버헤드만 늘어난다
- **여러 줄 C#을 셸 인자로 인라인하지 마라.** Windows 환경이라 따옴표 처리가 지옥이다. 두 줄 넘어가면 파일로 써서 `eval_file`을 쓸 것
- `unity list --project-path . --format json`으로 **실제 사용 가능한 명령 이름을 먼저 확인**하라. 명령 집합은 프로젝트에 설치된 Pipeline 패키지 버전이 정하는 것이지 고정된 게 아니다. 인자는 `key=value`가 아니라 **위치 인자**다

### 3-4. 검증 루프 — 네 손으로 닫아라

파일을 저장하는 것은 끝이 아니다. **스크립트를 건드렸으면 컴파일 확인까지가 한 작업이다.**
```
unity command recompile --project-path .
unity command recompile_status --project-path .    # 완료까지 폴링
unity command console --project-path .             # 컴파일 에러/경고 확인
```

- **"코드를 작성했다"가 아니라 "작성했고 컴파일을 확인했다"까지 가라.** 사람에게 "Unity 콘솔을 확인해 주세요"라고 미루지 마라 — 네가 직접 확인할 수 있다
- **컴파일 에러를 두려워해 소극적으로 쓰지 마라.** 에러가 나면 즉시 보인다. 고치면 된다
- 다만 **확신 없는 API를 추측하지는 마라.** 이 저장소에 이미 있는 패턴을 찾아 그대로 따르는 것이 항상 우선이다
- 큰 변경일수록 **작게 나눠 각각 컴파일을 확인**하라. 한 번에 크게 쓰면 어느 부분이 깨졌는지 특정하기 어렵다
- 에디터가 실행 중이 아니면 라이브 명령이 실패한다. `unity status`로 먼저 확인할 것

### 3-5. 안전 — eval은 임의 코드 실행이다

`unity command eval`은 실행 중인 에디터 안에서 **임의의 C#을 실행**한다. 즉 프로젝트의 어떤 에셋이든 만들고 고치고 지울 수 있으며, **대부분 Undo가 되지 않는다.** 다음을 지켜라.

- **작업 단위로 한 번 합의하고, 그 안에서는 매 호출마다 묻지 마라.** "오퍼레이터 SO 3개 생성"에 합의했으면 그걸 하는 데 필요한 호출은 다 해도 된다
- **범위를 벗어나면 멈추고 물어라** — 합의된 대상 밖의 파일/에셋을 건드리게 될 때, 또는 **이번 작업에서 네가 만든 게 아닌 기존 에셋을 덮어쓰거나 지우게 될 때**
- 읽기 전용 조회(`status`, `list`, `console`)는 언제든 확인 없이 해도 된다
- 씬을 수정했다면 저장까지 확인하라. 저장되지 않은 씬 변경은 에디터가 닫히면 사라진다

### 3-6. 네가 우선적으로 만들어야 할 인프라

아래는 만들어두면 이후 전 작업의 자동화 수준이 올라간다. 기회가 되면 먼저 처리하라.

1. **`Assets/Editor/BuildScript.cs`** — `unity build --execute-method`로 호출할 정적 빌드 메서드. WebGL / StandaloneWindows64 각각.
   현재 이게 없어서 CLI 빌드가 아예 불가능하다. 우선순위 높음.
   ```
   unity build --target WebGL --execute-method BuildScript.BuildWebGL
   ```
2. **에셋 생성 도구** — `OperatorDefinition` 등 SO를 데이터로부터 일괄 생성. 오퍼레이터를 수 명 만들 예정이므로 손으로 만들 이유가 없다.
3. **검증 도구** — Roster에 등록되지 않은 Definition, 미연결 인스펙터 필드 등을 찾아 경고하는 메뉴 항목. 사람의 수작업 실수를 코드로 막는다.

**단, 테스트 도입은 먼저 질문하라.** 현재 92개 스크립트가 전부 asmdef 없이 `Assembly-CSharp`에 있어서, 테스트 어셈블리를 붙이려면 asmdef 구조 결정이 선행되어야 하고 이는 기존 전체 참조에 영향을 줄 수 있다. 마감이 촉박하므로 **임의로 asmdef를 도입하지 마라.**

---

## 4. 아키텍처 (반드시 따를 것)

### 4계층 구조
1. **데이터 컨테이너** (`Assets/Scripts/Data/`) — 순수 C# `[Serializable]` 클래스. 스탯 수치의 "모양"만 정의
2. **데이터 기반 SO** (`Assets/Scripts/Definitions/`, `Assets/Scripts/Effects/`) — Data를 감싸거나 효과 로직을 담는 ScriptableObject. 인스펙터 조립 지점
3. **렌더러 프리팹** — 시각 표현만. 게임 로직 없음
4. **매니저** (`Assets/Scripts/Managers/`) — 흐름 조율

### 매니저 원칙 (위반 금지)
> **매니저는 "게임 흐름 단계"당 1개만 둔다. "오브젝트 타입"별 매니저(EnemyManager, TowerManager, UnitManager 등)는 만들지 않는다.**

현재 매니저는 5개뿐: `GameManager` / `MapManager` / `WaveManager` / `CardManager` / `SoundManager`(횡단 관심사 예외).
개별 오브젝트가 스스로 상태를 들고 이벤트로 알리며, 매니저는 리스트 순회나 흐름 조율만 한다.

**새 매니저를 만들고 싶어지면 먼저 질문하라.** 아군 유닛 추가를 이유로 `AllyUnitManager`를 만드는 것은 이 원칙 위반이다 — `EXPANSION_PLAN.md` §2-5의 `UnitDeployController` 결정을 따를 것.

### 개체 처리 방식 (개체 수에 따라 다름)
- **다수·고빈도 개체 = 순수 C# 인스턴스 + View 분리** — `EnemyInstance`(순수 C#, `Tick(dt)`/이벤트 보유) + `EnemyView`(MonoBehaviour, 위치만 반영). `MonoBehaviour.Update()` 오버헤드 회피 목적
  → **아군 유닛도 반드시 이 패턴을 따를 것**: `AllyUnitInstance`(순수 C#) + `AllyUnitView`(MonoBehaviour)
- **소수 개체 = 일반 MonoBehaviour** — 타워(슬롯 제한으로 6~8개), 플레이어, 거점

### 프리팹 1개 + Definition 주입 원칙
적도 타워도 **프리팹은 각 1개뿐**이고, 종류별 차이(스탯·스프라이트·효과)는 전부 Definition SO 데이터로 주입한다. 종류를 추가할 때 프리팹을 복제하지 마라.
→ 아군 유닛도 **`AllyUnitView` 프리팹 1개 + `AllyUnitDefinition` 주입**으로 설계할 것.

주의: `Instantiate()`는 그 자리에서 즉시 `Awake()`를 실행한다. 그래서 `TowerInstance`는 `Awake()`에서 로직을 돌리지 않고, `MapManager`가 `Instantiate` 직후 명시적으로 호출하는 `Build(definition)`에서 초기화한다. (`EnemyView.Bind()`도 동일 패턴.) 신규 개체도 이 패턴을 따를 것.

---

## 5. 코딩 규칙 (하드 룰)

전부 **실제로 버그를 냈던 사례**에서 나왔다. 상세 경위는 `ARCHITECTURE.md`에 있다.

1. **파일 1개 = 타입 1개, 파일명 = 타입명.** SO/MonoBehaviour 상속 타입은 예외 없이. (한 파일에 여러 SO를 넣으면 이름 변경 시 기존 `.asset`이 Missing Script가 된다.)

2. **`UnityEditor` 네임스페이스를 쓰는 코드는 반드시 `Assets/Editor/` 아래에 둔다.** 런타임 스크립트에 섞이면 플레이어 빌드가 컴파일 에러로 실패한다. 런타임 파일에서 불가피하게 필요하면 `#if UNITY_EDITOR`로 감쌀 것.

3. **입력을 폴링하는 `Update()`는 최상단에 일시정지 게이트를 둔다.**
   ```csharp
   private void Update()
   {
       if (Time.timeScale <= 0f) { return; }
       // ...
   }
   ```
   `Time.timeScale = 0`은 `deltaTime`만 0으로 만들 뿐 `Update()`와 입력 폴링은 계속 돈다. 이 게이트가 없으면 카드 선택/게임오버/튜토리얼 화면 뒤에서 조작이 새어나간다.

4. **일시정지 중에도 흘러야 하는 타이머는 `Time.unscaledDeltaTime`.** `Invoke()`/코루틴 대신 이 프로젝트의 기존 패턴인 **수동 타이머 필드 + `Update()` 감산**을 따를 것.

5. **ScriptableObject 원본을 런타임에 직접 수정하지 마라.** SO는 프로젝트 에셋 그 자체라 Play 모드를 꺼도 변경이 원복되지 않는다. `CreateRuntimeInstance()`/`GetRuntimeInstance()`로 세션 전용 복제본을 얻어 수정할 것. (`TowerDefinition`/`TowerRoster` 참고.)

6. **씬 재로드로 초기화되지 않는 static/캐시를 만들었다면 `GameManager.Awake()`의 초기화 목록에 반드시 추가하라.** `SceneManager.LoadScene`은 도메인 리로드가 아니라서 static이 살아남는다. `GameManager`는 `[DefaultExecutionOrder(-1000)]`로 먼저 실행된다.

7. **Unity 오브젝트의 null 체크는 `?.`가 아니라 명시적 `!= null`로.** Unity의 오버로드된 `==`는 파괴된 오브젝트를 null로 취급하는데 `?.`는 이를 우회한다.
   ```csharp
   if (SoundManager.Instance != null) { SoundManager.Instance.PlayX(); }   // O
   SoundManager.Instance?.PlayX();                                          // X
   ```

8. **효과 SO는 상태를 갖지 않는다.** 여러 인스턴스가 공유하는 자산이므로 인스턴스별로 달라지는 값(쿨다운 잔여 등)은 런타임 인스턴스 쪽에 둘 것.

9. **SO에 씬 오브젝트 참조를 넣지 마라.** 필요하면 기존 싱글톤(`BaseController.Instance`, `GameManager.Instance`)이나 컨텍스트 객체(`TowerContext`/`CardContext`)를 통할 것.

10. **주석은 한국어로.** 코드베이스 전체가 한국어 주석이다. 그리고 **"무엇을"이 아니라 "왜"를 적어라** — 특히 직관에 반하는 선택을 했을 때. 이 프로젝트의 주석은 그 자체가 설계 문서다.

11. **UI→게임플레이 단방향 참조.** UI가 게임플레이 이벤트를 구독하는 것은 O, 게임플레이가 UI를 참조하는 것은 X.

12. **WebGL을 항상 의심하라.** 제출 필수 타깃이 WebGL이고, 이 프로젝트는 이미 한 번 물렸다 — WebGL에서 `VideoPlayer`가 `VideoClip` 에셋 재생을 지원하지 않아 `StreamingVideoSource.cs`로 우회했다. 파일 I/O·스레딩·동기 네트워킹·`System.IO` 직접 사용은 WebGL에서 동작이 다르거나 아예 안 된다.

---

## 6. 작업 방식

해당 규칙을 따라 개발한다.

- **기획이 불명확하면 개발을 중단하고 질문하라.** 임의로 확정하지 마라. 실제로 이 질문 루프에서 다수의 설계 결정이 확정됐다. `EXPANSION_PLAN.md` §5에 미확정 항목이 정리되어 있다.
- **한 번에 한 모듈, 혹은 그보다 작은 단위로.** 컴파일 피드백이 비동기라 크게 쓰면 원인 특정이 어렵다.
- **기존 코드를 먼저 읽어라.** 비슷한 것이 이미 있는데 새로 만드는 것이 이 저장소에서 실제로 사고를 냈다 — 볼륨 설정이 두 곳에 중복 구현되어 하나가 미완성인 채 방치됐고, 그게 "슬라이더를 내려도 소리가 안 줄어드는" 버그로 나타났다. 새 시스템 전에 유사 기능의 존재를 먼저 확인하라.
- **자동화할 수 있으면 자동화하라.** 같은 수작업이 3번 이상 반복될 것 같으면 에디터 툴을 만드는 쪽이 빠르다. 오퍼레이터 3명 × (SO 여러 개 + 프리팹 + 연결)은 명백히 그 경우다.
- **구조적 결정과 그 이유를 `Assets/Docs/EXPANSION_LOG.md`에 기록하라.** 무엇을 만들었는지가 아니라 **왜 그렇게 했고 무엇을 의도적으로 하지 않았는지**를 적을 것. 이 문서는 대회 제출물(Codex 활용 설명 문서)의 원본이 되므로, 기록의 질이 곧 심사 점수다.
- **최적화를 미리 하지 마라.** 오브젝트 풀링은 실측상 고빈도인 것(이동 잔상, 공격 플래시)에만 적용되어 있다.

---

## 7. 문서 인덱스 — 언제 무엇을 읽는가

전부 읽으려 하지 마라. 필요할 때 해당 부분만 찾아 읽어라.

| 문서 | 언제 읽는가 |
|---|---|
| `Assets/Docs/EXPANSION_PLAN.md` | **작업 시작 전 필수.** 목표·마일스톤·확정 설계·미확정 항목 |
| `Assets/Docs/EXPANSION_LOG.md` | 이번 확장의 기존 결정 확인. **작업 후 여기에 기록** |
| `Assets/Docs/ARCHITECTURE.md` | 기존 시스템을 건드리기 전. 약 1,100줄이므로 **관련 섹션만 검색해서** 읽을 것 |
| `Assets/Docs/GDD.md` | 기획 의도·시스템 명세·밸런싱 공식 |
| `Assets/Docs/구현_체크리스트.md` | 기존 개발 진행 순서와 각 단계의 판단 근거 |
| `Assets/Docs/제작과정.md` | AI 협업 이력 (참고용) |
| `보고서.md` | 넥슨 제출 리포트. 프로젝트 전체 요약이 필요할 때 가장 빠른 진입점 |

**코드 탐색 힌트**
```
Assets/Scripts/Data/          스탯 컨테이너 (TowerData, EnemyData, PlayerData)
Assets/Scripts/Definitions/   Definition SO + Roster SO (Tower/Enemy/Card)
Assets/Scripts/Effects/       효과 훅 SO (Tower/Enemy/Card, 각 Concrete/ 하위에 구현체)
Assets/Scripts/Runtime/       런타임 인스턴스·컨트롤러 (PlayerController, EnemyInstance, TowerInstance, ...)
Assets/Scripts/Managers/      Game/Map/Wave/Card/Sound
Assets/Scripts/UI/            HUD·카드선택·튜토리얼·결과화면·타이틀
Assets/Scripts/Core/          공용 계약·유틸 (IDamageable, EnemyTargeting)
Assets/Editor/                (아직 없음 — 네가 만들 에디터 자동화 도구가 들어갈 곳)
```

---

## 8. 커밋 규칙

로컬 작업이므로 PR 기반이 아니다. `main`에서 직접 작업하되 커밋 위생을 지킨다.

- **작업 단위마다 커밋을 분리하라.** 대회 제출 시 "챌린지 기간 중 신규 개발한 내용"을 명시해야 하는데 커밋 히스토리가 그 증빙이 된다. 기준 태그는 `nexon-final`이며 **그 이후 모든 커밋이 챌린지 신규 개발분**이다.
- 커밋 메시지는 한국어로. 무엇을 왜 했는지 한 줄 요약 + 필요시 본문.
- **커밋 전에 컴파일이 통과하는지 확인하라** (§3-4). 깨진 상태를 커밋하지 마라.
- **`.meta` 파일은 Unity가 생성한 것을 반드시 함께 커밋하라.** `.meta`가 없으면 다른 환경에서 에셋 참조가 전부 끊긴다. 다만 **직접 손으로 작성하지는 마라** — 스크립트를 만든 뒤 Unity가 생성하도록 두고, 생성된 것을 커밋한다.
- **`Assets/Gutty Kreum/` 이 스테이징에 올라오지 않았는지 확인하라.** 유료 에셋이라 재배포 불가다.
- 에디터 툴·빌드 스크립트·`Tools/eval/` 스니펫도 정식 산출물이니 전부 커밋하라 — **재현 가능성 자체가 심사 자료**다(Codex Collaboration).
- 여러 작업이 병렬로 진행될 수 있으니, 공유 파일(`GameManager.cs`, `CardContext.cs`, `HudController.cs`, `GameResultUI.cs`)을 수정할 때는 커밋 본문에 명시하라.

### 작업 하나를 끝냈다고 말하기 전에 확인할 것
1. 컴파일이 통과하는가 (`unity command console`로 확인)
2. 에셋/프리팹을 생성했다면 실제로 디스크에 저장되었는가 (`SaveAssets` + `Refresh`)
3. `EXPANSION_LOG.md`에 결정과 근거를 기록했는가
4. 사람이 해야 할 일이 남았다면 무엇인지 명시했는가 (§3-2-1의 범주만 해당)

---

## 9. 지금 이 프로젝트의 상태 (최종 갱신: 2026-08-13)

- `main` 브랜치, 원격은 `origin` = `SkylightStudio07/Rebecca-Cecylia` **하나뿐**
- 넥슨 제출본은 별도 저장소에 보존되어 있으며 **이 저장소에서는 건드리지 않는다**
- 기준 태그 `nexon-final`이 넥슨 최종 커밋(`4803059`)에 부착됨
- **Addressables 패키지 미설치** (`Packages/manifest.json` 확인)
- **`Assets/Editor/` 및 빌드 스크립트 부재** — 현재 `unity build`가 불가능한 상태다. Phase 0의 첫 작업(P0-0)
- WebGL 빌드는 검증 완료 (수동 빌드 기준)
- 개발 환경은 **Windows** — 셸 따옴표 처리에 주의(§3-3), 경로에 공백이 있으면 인용할 것

### 시작할 때 먼저 확인하면 좋은 것
```
unity doctor                    # CLI 환경 상태
unity editors -i                # 설치된 에디터 (6000.3.13f1 필요)
unity status                    # 실행 중인 에디터 인스턴스 — 비어 있으면 라이브 명령 불가
unity list --project-path . --format json   # 이 프로젝트에서 쓸 수 있는 실제 명령 목록
```
`unity status`가 비어 있으면 Pipeline 라이브 제어가 안 된다. 사람에게 에디터를 열어달라고 요청하거나, 라이브 제어가 필요 없는 작업(코드 작성)부터 진행하라.
Pipeline 패키지가 아직 설치되지 않았다면 `unity pipeline install --project-path .` 이 필요하며, 이는 패키지 매니페스트를 수정하고 도메인 리로드를 유발하므로 **사람에게 먼저 알릴 것**.
