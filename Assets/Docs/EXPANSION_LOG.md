# 확장 개발 결정 로그

> OpenAI Game Builders Seoul Track 1 확장 개발(2026.08.13~08.26)의 구조적 결정과 그 이유를 시간순으로 기록한다.
> 기존 개발분의 결정 로그는 `ARCHITECTURE.md`에 있으며, **이 문서는 챌린지 기간 신규 개발분만** 다룬다.
> 이 분리 자체가 대회 요건(기존 프로젝트 활용 시 신규 개발분 명시)에 대한 증빙이자, Codex 활용 설명 문서의 원본이다.

## 기록 규칙

각 항목은 아래 형식을 따른다. **무엇을 만들었는지가 아니라 왜 그렇게 했는지, 그리고 무엇을 의도적으로 하지 않았는지**를 적는다.

```markdown
## [날짜] 제목
**맥락** — 왜 이 작업이 필요했는가
**결정** — 무엇을 어떻게 하기로 했는가
**근거** — 왜 그 선택인가. 검토했다가 버린 대안이 있으면 그것도
**의도적으로 하지 않은 것** — 범위에서 뺀 것과 그 이유
**사람 액션** — Unity 에디터에서 해야 할 작업
```

버그를 고쳤다면 **증상 → 원인 → 해결**을 남긴다. 원인 분석이 결과보다 가치 있다.

---

## 2026-08-13 — 확장 개발 착수 및 AI 협업 구조 전환

**맥락**
넥슨 게임잼(2026.07) 제출작 R&C Company를 OpenAI Game Builders Seoul Track 1에 확장 출품하기로 결정. 대회 심사 기준에 **Codex Collaboration**이 포함되어 있어, 기존 개발에서 Claude Code가 담당하던 게임플레이 코드 작성을 **Codex 주도로 전환**한다.

**결정**
1. 저장소를 분리한다. 넥슨 제출본(`SkylightStudio07/RCCompany`)은 동결하고, 확장 개발은 신규 저장소(`SkylightStudio07/Rebecca-Cecylia`)에서 진행한다. 커밋 히스토리는 유지한다.
2. 넥슨 최종 커밋에 `nexon-final` 태그를 박아 챌린지 신규 개발분의 기준점으로 삼는다.
3. `AGENTS.md`(Codex 상시 컨텍스트) / `EXPANSION_PLAN.md`(작업 계획) / `EXPANSION_LOG.md`(이 문서) 3종을 핸드오프 문서로 작성한다.

**근거**
- 히스토리를 유지한 이유: 심사에서 "기존 프로젝트를 어떻게 확장했는가"가 오히려 서사가 된다. 스쿼시하면 그 맥락이 사라진다.
- 태그로 기준점을 박은 이유: 대회 요건인 "챌린지 기간 신규 개발분 명시"를 문서 주장이 아니라 **커밋 히스토리로 검증 가능한 형태**로 만들기 위함.
- 문서를 3종으로 나눈 이유: 기존 `ARCHITECTURE.md`가 약 1,100줄이라 Codex가 매번 전체를 읽을 수 없다. 상시 컨텍스트(짧고 규칙 중심) / 작업 계획(무엇을 할지) / 결정 로그(무엇을 했고 왜)로 역할을 분리해야 컨텍스트 예산 안에서 동작한다.

**의도적으로 하지 않은 것**
- 기존 `ARCHITECTURE.md`에 확장분을 이어 쓰지 않았다. 신규 개발분이 기존 기록과 섞이면 대회 고지 요건을 만족시키기 어려워진다.
- 원격을 두 개(`origin` + 신규) 유지하지 않고 `origin` 하나만 신규 저장소로 재지정했다. 여러 AI 도구가 붙는 환경에서 원격이 둘이면 오발송 위험이 있다.

**사람 액션**
- 완료: 신규 저장소 생성 및 `origin` 재지정, `nexon-final` 태그 부착
- 필요: Unity Package Manager에서 Addressables 패키지 설치 (`com.unity.addressables`)

---

## 2026-08-13 — AI 최대 활용을 전제로 한 협업 구조 설계

**맥락**
넥슨 개발 시에는 Claude Code가 로컬에서 동작해 Unity 에디터와 같은 머신에 있었고, 그럼에도 SO 에셋 생성·프리팹 조립·인스펙터 연결은 전부 사람의 수작업이었다. 자동화 경로가 없었기 때문이다. 실제로 그 수작업이 개발 병목이었다.

그 사이에 `unity` CLI가 확보되었다. 이 CLI는 헤드리스 빌드/테스트뿐 아니라 **Pipeline 패키지의 `eval`로 실행 중인 에디터 안에서 임의 C#을 실행**할 수 있다. 그리고 이번 확장의 주 개발 주체인 **Codex는 로컬에서 저장소를 직접 보며 작업**하므로, 이 CLI를 Codex 자신이 호출할 수 있다.

**결정**
협업 구조를 "AI가 코드를 쓰고 사람이 손으로 조립한다"가 아니라 **"AI가 코드를 쓰고, 컴파일을 확인하고, 에셋을 생성하고, 빌드까지 돌린다"**로 설계했다.

- Codex의 산출물은 런타임 코드에 한정되지 않는다. **에디터 자동화 도구(`Assets/Editor/`), 헤드리스 빌드 스크립트, 검증 툴**까지 Codex가 작성한다
- Unity 에디터 조작(에셋 생성·프리팹 조립·필드 연결)은 사람에게 넘기지 않고 **Codex가 에디터 툴 또는 `Tools/eval/` 스니펫을 작성해 CLI로 직접 실행**한다
- **검증 루프를 Codex 손에서 닫도록 했다.** `unity command recompile` → `console`로 컴파일 결과를 스스로 확인한다. "코드를 작성했다"가 아니라 "작성했고 컴파일을 확인했다"까지가 한 작업이다
- 사람에게 남는 것은 **아트 생성 · 방향 결정 · 외부 인프라(CORS·패키지 설치) · 되돌리기 어려운 조작 승인** 넷뿐이다
- 다만 `.unity`/`.prefab`/`.asset` 직접 텍스트 편집과 **`.meta` 직접 생성은 금지**로 남겼다 (반드시 에디터 API 경유)
- `eval`이 임의 코드 실행이고 대부분 Undo가 안 된다는 점을 감안해, **작업 단위로 한 번 합의하고 그 안에서는 재확인하지 않되, 범위를 벗어나거나 기존 에셋을 덮어쓸 때는 멈추고 묻는** 규칙을 뒀다

**근거**
1. **심사 기준 대응**: Codex Collaboration이 평가 항목이다. Codex가 코드만 뱉고 사람이 손으로 조립한 구조보다, Codex가 자동화 파이프라인까지 만든 구조가 실증으로서 훨씬 강하다.
2. **일정**: 13일에 오퍼레이터 3명 × (SO 다수 + 프리팹 + 연결)을 손으로 만들 여유가 없다. 넥슨 개발에서 인스펙터 수작업이 실제 병목이었고, 그때는 자동화 경로가 없어서 감수했을 뿐이다.
3. **재현성**: 자동화 스크립트가 커밋되면 "어떻게 만들었는가"가 그대로 남는다. 수작업은 기록이 남지 않는다.

`.meta` 금지 규칙만 예외적으로 강하게 남긴 이유는 이 프로젝트가 이미 유사한 사고를 겪을 뻔했기 때문이다 — `ARCHITECTURE.md` 9단계에 "SO/MonoBehaviour를 파일명과 다른 이름으로 한 파일에 몰아두면 이름 변경 시 기존 에셋이 Missing Script가 된다"는 기록이 있다. GUID 기반 참조가 이 프로젝트의 실제 위험 요소임이 확인되어 있다.

**의도적으로 하지 않은 것**
- Codex 웹앱에서 로컬 Unity 에디터로 직접 도달하는 경로(MCP 터널링 등)를 만들지 않았다. 클라우드→로컬 경로를 억지로 뚫는 것 자체가 개발 기간을 잡아먹고, CLI 경유로 이미 충분하다.
- **테스트 인프라 도입을 보류**했다. 현재 92개 스크립트가 전부 asmdef 없이 `Assembly-CSharp`에 있어, 테스트 어셈블리를 붙이려면 asmdef 구조 결정이 선행되어야 하고 이는 기존 전체 참조에 영향을 준다. 순수 C# 인스턴스 패턴 덕에 로직 테스트 가치는 높지만, 마감 13일 시점에 착수할 작업은 아니라고 판단했다. `AGENTS.md`에 "임의 도입 금지, 먼저 질문"으로 명시.

**사람 액션 / 인프라 부채**
현재 `Assets/Editor/` 폴더 자체가 없고 **헤드리스 빌드 스크립트가 없어 `unity build`가 아예 불가능한 상태**다. 이것이 Phase 0의 첫 작업(P0-0)이 되었다 — 이게 없으면 이후 모든 빌드 검증이 수작업으로 되돌아간다.

---

## 2026-08-13 — 확장 설계의 핵심 결정 7건

`EXPANSION_PLAN.md` §2에 확정 기록. 요약과 근거만 남긴다.

| # | 결정 | 핵심 근거 |
|---|---|---|
| 1 | 오퍼레이터 3명, 그중 **1명은 빌드 미포함 원격 전용** | 빌드에 없는 것을 내려받아야 라이브 드랍 시연이 진짜가 된다 |
| 2 | 유닛 자원을 골드와 **분리** ("지휘 포인트", 자동 회복) | 골드 공유 시 소모성(유닛)과 영구성(타워)의 밸런싱이 붕괴. 자원 체계 차이 자체가 로드아웃 차별화의 실증이 됨 |
| 3 | Addressable 로딩을 **오퍼레이터 선택 시점 개별 로딩**으로 | 타이틀 일괄 로딩은 대기만 길고 "받아오는 것"이 안 보임. 선택 화면 진행률 UI가 시연에서 강력 |
| 4 | 아군 유닛도 **순수 C# 인스턴스 + View 분리** | 기존 `EnemyInstance`/`EnemyView` 패턴 미러링. 다수 개체 성능 구조가 이미 검증됨 |
| 5 | 소환/순회 주체를 `UnitDeployController`로 (**새 매니저 금지**) | "매니저는 게임 흐름 단계당 1개" 원칙 유지. `TowerBuildController`와 대칭 구조 |
| 6 | 유닛 효과도 **SO 훅 패턴**(`IAllyUnitEffect`) | 신규 오퍼레이터마다 새 C# 클래스가 필요하면 "코드 수정 없는 데이터 드랍" 주장이 무너진다 |
| 7 | 해금 조건 = 누적 최고 도달 웨이브, **임계치는 낮게** | 심사위원은 오래 플레이하지 않는다. 디버그 해금 경로도 유지 |

**특히 6번이 이번 확장의 설계 축이다.** Release Potential 논거 전체가 "전투 코드를 고치지 않고 데이터만으로 신규 오퍼레이터를 추가할 수 있다"에 걸려 있으므로, **오퍼레이터를 추가할 때 새 클래스가 필요해지는 설계는 그 자체로 실패**다. 신규 유닛/타워/카드는 기존 효과 SO의 조립만으로 만들어져야 한다.

**의도적으로 하지 않은 것 (기획 단계에서 배제)**
- Hive SDK 실연동 — SDK가 WebGL을 지원하지 않는 것으로 확인되었고, 대회도 필수로 걸지 않았다. 목표는 "붙였다"가 아니라 **"붙일 수 있는 구조로 설계했다"**이다. (`Hive_호환성_검토` 참조)
- 재화 시스템·영구 강화 트리 — 13일 일정에 부담이며 피치에서 얻는 근거는 늘지 않는다. "계정 데이터가 존재하고 저장소 교체 지점이 명확하다"까지가 목표.

---

<!-- 이후 작업은 이 아래에 시간순으로 추가 -->

## 2026-08-13 — Unity CLI Pipeline 및 Addressables 설치

**맥락**
Phase 0 자동화 경로를 실제로 열고, 이후 오퍼레이터별 원격 콘텐츠를 구성하기 위한 패키지 기반이 필요했다.

**결정**
- 프로젝트에 Unity Pipeline `0.5.0-exp.1`을 설치했다.
- Unity 6.0용 Addressables `2.7.6`을 설치했다.
- 설치 직후 실행 중인 Unity `6000.3.13f1` 에디터에서 Pipeline 연결 상태와 명령 목록을 확인하고 재컴파일 및 콘솔 조회까지 수행했다.

**근거**
- Pipeline은 Codex가 에디터 내부의 컴파일·에셋 생성·검증을 직접 수행하는 자동화 경로다.
- Addressables는 "오퍼레이터 1명 = Addressable 그룹 1개" 및 빌드 미포함 오퍼레이터의 원격 배포를 구현하는 전제다.
- 패키지 버전을 `manifest.json`과 Unity가 해석한 `packages-lock.json`에 함께 고정해 다른 환경에서도 같은 의존성을 재현할 수 있게 했다.

**검증**
- Pipeline 서버: `ready` (`127.0.0.1:7800`)
- 라이브 명령: `eval`, `eval_file`, `recompile`, `recompile_status`, `console` 노출 확인
- 재컴파일: `up_to_date`, 컴파일 오류 없음
- 콘솔: 패키지 오류 없음. 에디터가 `-automated` 없이 실행되었다는 Pipeline 주의 경고 1건만 확인

**의도적으로 하지 않은 것**
- Addressables Settings, 그룹, 프로필 및 원격 경로 에셋은 아직 생성하지 않았다. 패키지 설치와 콘텐츠 구조 결정은 분리해, 다음 작업에서 에디터 API를 통해 재현 가능하게 생성한다.
- WebGL 빌드는 이번 설치 검증 범위에서 실행하지 않았다. 최종 빌드는 사람이 수행한다는 현재 작업 합의를 따른다.

---

## 2026-08-13 — OperatorDefinition SO 골격 정의

**맥락**
오퍼레이터를 연출용 캐릭터에서 플레이 스타일 패키지로 승격하려면, 기존 데이터 에셋을 한 곳에서 조립하는 최상위 Definition 계약이 먼저 필요했다.

**결정**
- `OperatorDefinition`을 `Definitions/Operator/`에 별도 파일로 추가했다.
- 영구 식별자와 선택 화면 표시 정보, `PlayerData`, `TowerRoster`, `CardRoster`, 기존 `OperatorDialogueSet`, 최고 도달 웨이브 해금 조건을 필드로 둔다.
- 선택 화면 초상화와 전투 중 상황별 초상화의 책임을 분리했다. 전자는 Definition이, 후자는 기존 DialogueSet이 맡는다.
- `operatorId`를 표시 이름과 분리해 이름을 바꿔도 저장 데이터와 Addressable 식별 경로가 깨지지 않게 했다.

**근거**
- 기존 Tower/Card/Dialogue SO를 직접 참조하면 신규 오퍼레이터는 전투 클래스 추가 없이 에셋 조립만으로 구성할 수 있다.
- 원격 여부와 다운로드 주소는 `OperatorDefinition`을 받기 전에도 선택 화면이 알아야 한다. 따라서 Definition 안에 순환적으로 넣지 않고 후속 `OperatorCatalog`의 로컬/원격 메타데이터 책임으로 남겼다.
- `PlayerData`는 SO 안에 포함되면 원본 에셋의 일부가 되므로, P1 적용 파이프라인에서는 반드시 세션용 값으로 복제한 뒤 플레이어에 적용해야 한다.

**의도적으로 하지 않은 것**
- 적용 로직, 선택 UI, SO 에셋 생성과 씬 연결은 P1/P0-6 범위라 추가하지 않았다.
- 아군 유닛 로스터는 아직 `AllyUnitDefinition` 계약이 없으므로 느슨한 `ScriptableObject` 참조나 임시 타입으로 넣지 않았다. P1-B1에서 강타입 계약이 생기면 필드를 추가한다.
- 원격 배포 여부, Addressable 키와 URL은 `OperatorCatalog`에 둘 정보라 제외했다.

**검증**
- Unity `6000.3.13f1` 재컴파일 완료, 컴파일 오류 없음.
- Unity가 폴더 및 스크립트 `.meta`를 자동 생성한 것을 확인했다.

---

## 2026-08-13 — PlayerProfile 영속 데이터와 저장소 추상화

**맥락**
오퍼레이터 선택과 최고 도달 웨이브는 씬 재시작으로 초기화되는 전투 세션이 아니라 계정 단위로 남아야 한다. 동시에 로컬 저장 방식이 향후 외부 SDK 연동 코드와 게임 로직에 직접 섞이면 저장 백엔드를 바꾸기 어렵다.

**결정**
- 순수 데이터 컨테이너 `PlayerProfile`에 스키마 버전, 누적 최고 도달 웨이브, 마지막 선택 오퍼레이터 ID를 둔다.
- `IProfileStorage`는 `Load`/`Save`만 노출하는 최소 계약으로 정의했다.
- `PlayerPrefsProfileStorage`는 프로필 전체를 JSON 한 덩어리로 직렬화해 `RCCom.PlayerProfile` 키에 저장한다.
- 저장이 없거나 값이 비어 있거나 JSON이 손상된 경우 게임 진입을 막지 않고 기본 프로필을 반환한다.
- 저장 시점마다 현재 스키마 버전을 기록하고 `PlayerPrefs.Save()`로 즉시 확정한다.

**근거**
- 해금 여부는 `bestWave >= OperatorDefinition.requiredBestWave`로 결정되므로 해금 ID 목록을 함께 저장하면 동일 상태가 두 군데 존재한다. 파생 상태는 저장하지 않아 모순 가능성을 없앴다.
- 필드마다 PlayerPrefs 키를 만들지 않고 JSON 하나를 쓰면 프로필 필드가 늘어날 때 저장 키가 흩어지지 않고 스키마 버전 기준으로 마이그레이션할 수 있다.
- `System.IO` 대신 Unity의 PlayerPrefs/JsonUtility만 사용해 WebGL의 IndexedDB 저장 경로와 호환되게 했다.
- 외부 SDK의 비동기 형태가 정해지지 않은 상태에서 추측성 비동기 API를 만들지 않았다. 실제 백엔드를 붙일 때 `IProfileStorage` 구현 또는 호출 경계만 조정한다.

**의도적으로 하지 않은 것**
- `GameManager`와 `GameResultUI`에 최고 웨이브 갱신을 연결하지 않았다. 해금 판정과 실제 저장 연결은 계획상 P2-4 작업이다.
- 영구 강화, 재화, 명시적 해금 목록은 이번 출품 범위가 아니므로 프로필에 추가하지 않았다.
- 별도 테스트 asmdef나 Unity Test Framework 구조를 만들지 않았다. 기존 전체가 `Assembly-CSharp`인 상태를 유지했다.

**검증**
- Unity `6000.3.13f1` 재컴파일 완료, 컴파일 오류 없음.
- `Tools/eval/VerifyPlayerProfileStorage.cs`를 Pipeline으로 실행해 기본값과 저장/재로딩 값, 스키마 버전 고정을 왕복 검증했다.
- 검증에는 전용 임시 키를 사용했고 종료 시 삭제해 실제 사용자 프로필을 건드리지 않았다.

**사람 액션**
- 없음. 이 단계는 코드 계약만으로 완결되며 신규 아트/SO/씬 연결이 필요하지 않다.

---

## 2026-08-13 — 오퍼레이터 에셋 생성 및 검증 자동화

**맥락**
오퍼레이터 3명마다 Definition, TowerRoster, CardRoster와 참조 연결을 손으로 반복하면 시간이 오래 걸릴 뿐 아니라 누락과 잘못된 연결이 재현되지 않는 수작업으로 남는다. 신규 오퍼레이터가 C# 클래스 추가 없이 데이터 조립만으로 만들어진다는 핵심 주장도 실제 생성 경로로 증명할 필요가 있었다.

**결정**
- `Assets/Editor/OperatorRecipes/`의 JSON 레시피를 읽어 오퍼레이터별 전용 `OperatorDefinition`, `TowerRoster`, `CardRoster`를 일괄 생성하는 `OperatorAssetBuilder`를 추가했다.
- 레시피는 식별/표시 정보, 플레이어 수치, 기존 Roster/DialogueSet/초상화 경로만 가진다. Unity 에셋 경로는 에디터에서 강타입 참조로 변환한다.
- 자동 생성물에는 `RCCom.GeneratedOperator` 라벨을 붙인다. 같은 경로에 라벨 없는 기존 에셋이 있으면 덮어쓰지 않고 실패하도록 했다.
- `OperatorAssetValidator`는 오퍼레이터의 필수 참조·유효 수치·ID 중복, Roster의 null/중복 항목, 미등록 Tower/Enemy Definition을 검사한다.
- 필수 참조 오류는 생성 실패로, 미등록 Definition은 기존 데이터 부채를 발견하는 경고로 구분했다.

**근거**
- JSON 레시피는 신규 오퍼레이터마다 새 C# 클래스를 요구하지 않으면서도 Git diff로 콘텐츠 구성을 검토할 수 있다.
- 오퍼레이터별 Roster는 기존 목록을 복사한 독립 SO로 생성한다. 이후 각 오퍼레이터의 풀을 조정해도 원본 공용 Roster나 다른 오퍼레이터가 함께 바뀌지 않는다.
- 자동 생성 라벨은 재실행의 멱등성을 유지하면서, 도구가 만들지 않은 사람 소유 에셋을 실수로 덮어쓰는 것을 막는 소유권 경계다.
- 미등록 Definition은 삭제 예정·실험 자산일 수도 있어 자동 생성 전체를 막기보다 경고하는 편이 안전하다. 반면 null 참조나 빈 필수 Roster는 즉시 런타임 장애로 이어지므로 오류로 유지했다.

**실행 결과**
- DefenseScene을 저장 없이 추가 로드해 기존 카시아의 PlayerData와 Tower/Card Roster, DialogueSet, 기본 초상화 참조를 읽고 원래 TitleScene만 열린 상태로 복원했다.
- `Cassia.json` 레시피로 `Assets/Data/Operators/cassia/` 아래 에셋 3개를 생성했다.
- 생성기를 두 번 실행해 최초 생성과 재실행 갱신이 모두 성공하는 것을 확인했다.
- 카시아 전용 TowerRoster 3종, CardRoster 27장, Definition 상호 참조와 자동 생성 라벨을 별도 Pipeline 스니펫으로 검증했다.
- 기존 `Assets/Data/Definition/Tower/축적.asset`이 어느 TowerRoster와 UnlockTowerCard에도 연결되지 않은 고아 Definition임을 발견했다. 이번 범위에서는 기존 에셋을 임의 수정하지 않고 경고로 기록했다.

**의도적으로 하지 않은 것**
- 기존 Tower/Card/Dialogue 에셋과 DefenseScene은 수정하지 않았다.
- 신규 오퍼레이터 2·3번 레시피와 에셋은 이름·컨셉·아트가 확정되지 않아 만들지 않았다.
- Addressable 그룹 생성과 원격 프로필 설정은 원격 로딩 구조 작업으로 분리했다.

**검증**
- Unity `6000.3.13f1` 에디터 스크립트 재컴파일 완료, 컴파일 오류 없음.
- 자동 생성·독립 검증 모두 성공. 알려진 기존 고아 Definition 경고 1건만 남았다.
- `AssetDatabase.SaveAssets()`와 `Refresh()` 이후 생성 에셋과 Unity 자동 생성 `.meta` 파일이 디스크에 존재함을 확인했다.
- TitleScene은 유일하게 열린 씬이며 dirty 상태가 아님을 확인했다.

**사람 액션**
- 신규 오퍼레이터 레시피를 완성하려면 2·3번의 이름/컨셉/대사 톤과 초상화가 필요하다. 유닛 스프라이트는 이후 유닛 Definition 에셋 생성 시 필요하다.

---

## 2026-08-13 — 선택 오퍼레이터의 전투 세션 로드아웃 적용

**맥락**
`OperatorDefinition`과 카시아 에셋은 존재하지만, 기존 DefenseScene의 각 컴포넌트는 여전히 인스펙터에 직접 연결된 PlayerData, TowerRoster, CardRoster, DialogueSet을 따로 사용하고 있었다. 선택 화면에서 Definition 하나를 골라도 모든 소비자가 같은 패키지를 사용하도록 만드는 런타임 경계가 필요했다.

**결정**
- `OperatorLoadoutSession`이 타이틀에서 고른 `OperatorDefinition`을 DefenseScene으로 전달한다.
- `[DefaultExecutionOrder(-1000)]`인 `GameManager.Awake()`가 선택 Definition을 가장 먼저 검증하고, 선택된 Tower/Card Roster의 세션 캐시를 초기화한다.
- `PlayerController`, `TowerBuildController`, `TowerBuildMenuUI`, `CardManager`, `OperatorDialogueUI`는 각자의 Awake에서 동일한 세션을 통해 PlayerData, TowerRoster, CardRoster, DialogueSet을 해석한다.
- PlayerData는 필드 단위로 새 값 객체를 만들어 적용하며, 공격 사거리 트리거의 실제 반경도 선택 데이터에 맞춰 갱신한다.
- 선택 상태는 Retry 씬 재로드에서는 유지하지만 새 애플리케이션/Play 실행에서는 `SubsystemRegistration` 시점에 초기화한다.
- 선택이 없는 채 DefenseScene을 직접 실행할 때는 기존 인스펙터 연결을 fallback으로 사용한다.

**근거**
- 한 Definition을 세션의 유일한 진입점으로 삼아야 오퍼레이터별 데이터가 소비자마다 섞이는 것을 막을 수 있다.
- 별도 OperatorManager를 만들지 않고 정적 세션 경계와 기존 GameManager 초기화 단계만 사용해 "오브젝트 타입별 매니저 금지" 원칙을 유지했다.
- 플레이어 카드 효과가 `PlayerController.data`를 직접 수정하므로 Definition 안의 PlayerData를 그대로 넘기면 프로젝트 에셋 원본이 오염된다. 매 DefenseScene마다 깊은 값 복제를 만드는 이유다.
- 공격 수치만 바꾸고 CircleCollider2D 반경을 그대로 두면 표시 수치와 실제 타겟 감지가 달라진다. 로드아웃 적용 시 두 값을 같은 경로에서 동기화했다.
- 선택 없음 fallback은 선택 UI가 아직 없는 현재 개발 단계와 개별 DefenseScene 디버깅을 모두 보존한다.

**의도적으로 하지 않은 것**
- TitleScene 선택 UI와 PlayerProfile의 마지막 선택 저장은 아직 연결하지 않았다. 이는 P1-A2의 책임이다.
- `OperatorLoadoutSession`이 런타임 Roster 복제본을 직접 소유하지 않게 했다. 기존 `TowerRoster.GetRuntimeInstance()` 캐시 계약을 그대로 사용해 중복 상태를 만들지 않았다.
- 기존 DefenseScene이나 프리팹 YAML을 수정하지 않았다. 현재 인스펙터 참조는 fallback으로 계속 유효하다.

**검증**
- Unity `6000.3.13f1` 재컴파일 완료, 컴파일 오류 없음.
- 실제 카시아 Definition을 선택해 TowerRoster, CardRoster, DialogueSet이 모두 같은 Definition의 참조로 해석되는 것을 Pipeline 스니펫으로 확인했다.
- 런타임 PlayerData가 별도 객체이며 이를 수정해도 Definition 원본 수치가 변하지 않는 것을 확인했다.
- 검증 종료 시 정적 선택을 비웠고, 현재 Unity 콘솔에는 성공 로그 1건만 존재한다.

**사람 액션**
- 없음. 선택 화면을 완성하는 다음 단계에서 신규 오퍼레이터 이름·컨셉·초상화가 필요하다.

---

## 2026-08-13 — 오퍼레이터 선택 UI·프로필 복원·Addressables 로딩 기반

**맥락**
선택된 Definition을 전투에 적용하는 경계는 생겼지만 TitleScene에는 선택 과정이 없었고, New Game은 DefenseScene을 즉시 열었다. 또한 원격 Definition을 받기 전에도 목록·잠금·다운로드 여부를 보여줄 로컬 메타데이터와 실제 Addressables 주소가 필요했다. 신규 이미지가 아직 없으므로 아트 교체를 기다리지 않고 코드와 배선을 먼저 완성할 수 있어야 했다.

**결정**
- 빌드에 항상 포함되는 `OperatorCatalog`/`OperatorCatalogEntry`를 추가했다. 표시 이름, 설명, 선택용 미리보기, Addressables 주소, 원격 표시, 최고 웨이브 해금 조건만 담고 실제 전투 데이터는 Definition에 남겼다.
- TitleScene의 New Game을 오퍼레이터 선택 패널 진입으로 바꿨다. 좌우 순회, 잠금 안내, 다운로드 상태·진행률, 선택, 뒤로가기를 제공한다.
- 선택 패널은 `PlayerProfile.selectedOperatorId`를 복원한다. 저장된 ID가 없거나 잠겼으면 첫 해금 오퍼레이터로 안전하게 돌아가며, 선택을 확정할 때만 프로필을 저장한다.
- 선택 확정 시 `InitializeAsync` → 다운로드 크기 확인 → 필요할 때만 의존성 다운로드 → Definition 로드 순서로 처리한다. 실패하면 씬을 넘기지 않고 패널에 오류를 표시해 같은 버튼으로 재시도할 수 있다.
- Addressables 로드 핸들의 소유권을 `OperatorLoadoutSession`으로 넘겨 DefenseScene에서도 Definition과 의존 에셋이 유지되게 했다. 새 선택 또는 새 애플리케이션 실행 시 기존 핸들을 해제한다.
- JSON 레시피 하나에서 OperatorCatalog와 "오퍼레이터 1명 = 그룹 1개" Addressables 구성을 함께 만드는 `OperatorCatalogBuilder`를 추가했다. `remoteContent` 값으로 Local/Remote Build·Load 경로를 선택한다.
- `OperatorSelectionSetup`이 TitleScene의 1920×1080 Canvas에 선택 UI를 생성하고 모든 필드·버튼·New Game 참조를 에디터 API로 배선한다.
- 선택 초상화는 선택 사항으로 낮췄다. 이미지가 없으면 UI가 초상화 영역만 숨기고 계속 동작하며 검증기는 제출 전 확인용 경고만 낸다.

**근거**
- 원격 Definition 자체에 목록 정보를 넣으면 그 에셋을 받기 전에는 선택 화면을 만들 수 없다. 따라서 작은 카탈로그는 로컬, 실제 플레이 패키지는 Addressables 그룹으로 책임을 분리했다.
- 다운로드를 선택 확정 시점에만 수행하면 타이틀 진입 지연을 피하면서 심사 시 실제 라이브 콘텐츠 로딩 과정을 진행률로 보여줄 수 있다.
- 비동기 핸들을 씬의 MonoBehaviour가 소유하면 TitleScene 파괴와 함께 해제될 위험이 있다. 전투 세션 경계가 핸들을 보유해야 씬 전환 뒤 참조 수명이 명확하다.
- 카탈로그·그룹·씬 배선을 별도의 수작업으로 두면 신규 오퍼레이터마다 주소 오타와 그룹 누락 가능성이 생긴다. 한 레시피에서 생성하고 검증하는 것이 "데이터만으로 콘텐츠 추가" 주장과 재현성을 함께 강화한다.
- 아직 없는 이미지를 필수 오류로 막으면 아트와 코드가 직렬 작업이 된다. 빈 초상화를 명시적으로 지원해 UI·로딩·저장 개발을 병렬화했다.

**의도적으로 하지 않은 것**
- 신규 오퍼레이터 2·3번의 임시 Definition을 만들지 않았다. 이름·컨셉·수치·대사 톤이 미확정인 상태에서 가짜 영구 ID를 커밋하면 저장 데이터와 배포 주소가 나중에 흔들린다.
- 실제 원격 서버 URL, CORS, 원격 콘텐츠 빌드는 설정하지 않았다. Remote 그룹과 경로 전환은 준비했지만 배포 서버 결정은 외부 인프라 작업이다.
- Addressables 콘텐츠 빌드와 WebGL 플레이어 빌드는 실행하지 않았다. 최종 WebGL 빌드는 사람이 수행한다는 작업 합의를 유지했다.
- 선택 화면에 별도 프리팹이나 이미지 에셋을 만들지 않았다. 현재는 코드 생성 UI와 기존 Cassia 초상화만 사용한다.

**검증**
- Unity `6000.3.13f1` 재컴파일 완료, 컴파일 오류 없음.
- 카탈로그 Cassia 1명, 주소 `operator/cassia`, 그룹 `Operator-cassia-Local`, TitleScene 필수 필드와 New Game 연결, 저장된 씬의 비활성 초기 패널 상태를 검증했다.
- `operator/cassia` 주소로 실제 Addressables 초기화·Definition 로드·릴리스를 성공했다.
- Play Mode에서 Cassia 이름·설명, 단일 항목 탐색 버튼 비활성, 선택 버튼 활성, 뒤로가기와 메인 메뉴 입력 복원을 검증했다.
- TMP에 없던 꺾쇠 기호를 ASCII 기호로 교체한 뒤 Play Mode 콘솔 경고가 없는 것을 재확인했다.
- 기존 고아 `Assets/Data/Definition/Tower/축적.asset` 경고 1건은 이전과 동일하며 이번 작업에서 기존 에셋을 수정하지 않았다.

**사람 액션**
- 신규 오퍼레이터 레시피를 만들 때 영구 ID·이름·컨셉·수치·대사 톤을 확정해야 한다. 선택 초상화는 나중에 연결해도 된다.
- 원격 전용 오퍼레이터를 배포할 때 실제 Remote Load Path와 서버 CORS를 설정해야 한다.
## 2026-08-13 — 최고 도달 웨이브 저장과 해금 진행도 연결

### 구현
- `PlayerProfile.TryRecordBestWave`가 음수 입력을 0으로 정규화하고 기존 최고 기록보다 높을 때만 값을 갱신하도록 했다.
- `GameResultUI`가 게임오버 결과를 확정할 때 현재 웨이브를 프로필에 반영하고, 실제 최고 기록이 바뀐 경우에만 `PlayerPrefsProfileStorage.Save`를 호출하도록 연결했다.

### 판단 근거
- 결과 화면은 이미 `GameManager.GameOver`를 구독하고 `WaveManager.CurrentWave`로 세션 통계를 확정하는 경계다. 같은 시점에 영속 진행도를 기록하면 전투 중간의 불완전한 상태를 저장하지 않으면서 별도 매니저를 추가하지 않아도 된다.
- 최고 기록의 단조 증가 규칙을 UI에 직접 풀어 쓰지 않고 `PlayerProfile`에 두어, 이후 다른 결과 처리 경로가 생겨도 동일한 규칙을 재사용할 수 있게 했다.
- 최고 기록이 오르지 않은 재도전에서는 저장하지 않아 WebGL의 PlayerPrefs 확정 비용을 불필요하게 반복하지 않는다.

### 검증
- `VerifyPlayerProfileProgress.cs`로 최고 기록의 최초 갱신, 낮은 값/음수 무시, PlayerPrefs 저장 왕복을 임시 키에서 검증했다.

## 2026-08-13 — 오퍼레이터 선택 화면 키보드·게임패드 입력

### 구현
- 선택 패널이 열려 있을 때 키보드 `←/A`, `→/D`, `Enter/Space`, `Esc`로 이전·다음·확정·뒤로가기를 조작할 수 있게 했다.
- 게임패드는 D-pad/숄더 버튼으로 이동하고 South 버튼으로 확정, East 버튼으로 뒤로가기를 수행한다.
- 패널을 열거나 항목을 이동한 뒤에는 현재 상태에서 누를 수 있는 기본 버튼에 EventSystem 포커스를 맞춰, 시각적 선택 상태와 실제 제출 대상이 어긋나지 않게 했다.

### 판단 근거
- 선택 화면 하나를 위해 별도 Input Action 에셋이나 씬 배선을 추가하면 작은 UI 동작이 전역 입력 구성에 결합된다. 현재 프로젝트가 쓰는 신규 Input System의 장치 상태를 패널 내부에서만 읽어 기존 마우스 버튼 경로와 같은 공개 메서드를 호출하도록 했다.
- 아날로그 스틱 임계값 폴링은 한 번 기울였을 때 매 프레임 항목이 넘어가는 반복 입력 문제가 있어 제외했다. D-pad와 숄더 버튼은 `wasPressedThisFrame`으로 한 입력당 한 칸만 이동한다.

### 검증
- TitleScene Play Mode에서 선택 화면을 열어 확정 버튼 기본 포커스, 버튼 상태, 패널을 닫았을 때 포커스 및 메인 메뉴 입력 복원을 확인했다.

## 2026-08-13 — Addressables 그룹 정리·원격 프로필·빌드 사전 검증 강화

### 구현
- `OperatorCatalogBuilder`가 레시피 ID 중복을 에셋 변경 전에 차단하고, 현재 레시피에 대응하지 않는 `Operator-*` 빈 그룹을 정리하도록 했다. 항목이 남은 이전 그룹은 자동 삭제하지 않고 오류로 중단한다.
- 검증기가 카탈로그 ID뿐 아니라 Addressables 주소 중복/명명 규칙, 필수 라벨, 그룹당 명시적 Definition 1개, Local/Remote Build·Load 경로 유형, 미등록 Definition과 이전 그룹 잔존을 검사한다.
- `AddressablesRemoteProfileConfigurator`가 `RCCOM_REMOTE_LOAD_PATH`와 선택적 `RCCOM_REMOTE_BUILD_PATH` 환경 변수로 활성 프로필을 재현 가능하게 설정한다. 최종 WebGL에 안전한 HTTPS를 기본 계약으로 하고 localhost HTTP는 로컬 스파이크에서만 허용한다.
- `AddressablesBuildValidator`가 플레이어 빌드 전에 오퍼레이터 에셋 전체 검증, 설치된 빌드 모듈, 활성 프로필, 원격 콘텐츠의 실제 로드 주소를 한 번에 확인한다.

### 판단 근거
- `remoteContent`를 Local↔Remote로 바꾸면 Addressables가 새 그룹으로 엔트리를 이동시키지만 예전 빈 그룹은 남는다. 자동화 소유 이름과 빈 상태를 모두 확인한 경우만 제거해 반복 실행의 멱등성과 기존 콘텐츠 보호를 같이 유지했다.
- 원격 서버 URL은 배포 환경마다 달라지고 저장소에 고정할 값이 아니다. 환경 변수 주입 도구로 코드/에셋 구조와 외부 인프라 값을 분리했다.
- 잘못된 주소나 그룹 배선은 런타임 다운로드 때 늦게 드러난다. 플레이어 빌드 진입 전에 실패하도록 검증 경계를 앞당겼다.

### 검증
- 카탈로그/그룹 생성기를 다시 실행해 반복 실행이 현재 에셋을 손상시키지 않는 것을 확인했다.
- 실제 설치된 WebGL 모듈과 현재 카탈로그·Addressables 구성을 대상으로 빌드 사전 검증을 통과했다.
- 검증 스니펫으로 HTTPS 주소 정규화와 비-localhost HTTP 거부 계약을 확인했다. Addressables 콘텐츠 및 플레이어 빌드는 실행하지 않았다.

## 2026-08-13 — Unity CLI용 WebGL·Windows 빌드 진입점

### 구현
- `Assets/Editor/BuildScript.cs`에 `BuildWebGL`, `BuildWindows`/`BuildStandaloneWindows64` 정적 메서드를 추가했다.
- 빌드 전 오퍼레이터·Addressables 구성, 설치 모듈, 활성 필수 씬과 중복 씬을 검증한다.
- Addressables 콘텐츠를 먼저 명시적으로 한 번 빌드하고, 이어지는 플레이어 빌드 동안 자동 Addressables 생성을 임시로 끈 뒤 원래 설정으로 복원한다. 콘텐츠 실패와 플레이어 실패를 각각 즉시 예외로 보고한다.
- 실제 산출물 없이 확인할 수 있는 `ValidateWebGL`과 `ValidateWindows` 진입점을 함께 제공한다.

### 판단 근거
- Addressables Player Build 설정에만 의존하면 개발자 환경의 Preferences/프로젝트 옵션에 따라 콘텐츠가 생략되거나 중복 생성될 수 있다. CLI 진입점이 콘텐츠 생성 순서를 소유해 동일한 결과를 보장한다.
- 활성 타깃과 요청 타깃이 다른 상태에서 Addressables를 만들면 잘못된 플랫폼용 번들이 생성될 수 있다. 빌드 메서드는 CLI `--target` 전환을 요구하고 내부에서 암묵적 타깃 전환을 하지 않는다.
- 빌드 산출 경로는 `Builds/WebGL`, `Builds/Windows/RCCom.exe`로 고정해 로컬과 자동화가 같은 위치를 사용한다.

### 실행 경로
- WebGL: `unity build --target WebGL --execute-method BuildScript.BuildWebGL`
- Windows: `unity build --target StandaloneWindows64 --execute-method BuildScript.BuildWindows`
- 산출물 없는 사전 확인: Pipeline `eval`로 `BuildScript.ValidateWebGL()` 또는 `BuildScript.ValidateWindows()` 호출

### 검증
- 두 플랫폼의 사전 검증 메서드를 실행해 설치 모듈, 필수 씬, 현재 오퍼레이터/Addressables 구성이 모두 통과하는 것을 확인했다.
- 작업 합의대로 Addressables 콘텐츠 빌드와 WebGL/Windows 플레이어 빌드는 실행하지 않았다.

## 2026-08-13 — 두 클라이언트 병렬 개발용 아군 유닛 공통 기반

### 확정한 기획
- 아군 유닛은 `MapManager.Waypoints`의 같은 경로 원본을 받아 끝점에서 시작해 역방향으로 진격한다.
- 공격 범위 내에서 적을 만나면 `Advancing`에서 `Engaging`으로 전환해 정지·발포하고, 대상이 사라지면 다시 진격한다.
- 오퍼레이터당 기본 유닛 2종을 우선하고 일정 여유가 있을 때 지원/특수형 3종째를 추가한다.

### 구현
- 순수 데이터 `AllyUnitData`와 `AllyUnitState`, 데이터·효과·스프라이트를 조립하는 `AllyUnitDefinition`, 오퍼레이터별 목록인 `AllyUnitRoster` 계약을 추가했다.
- 상태 없는 효과 SO 계약 `IAllyUnitEffect`/`AllyUnitEffectBase`와 적·아군 후보를 전달하는 `AllyUnitContext`를 추가했다.
- 순수 C# `AllyUnitInstance`에 스폰, 역방향 경로 인덱스, 상태·타깃 전이, 공격 훅, 피해·사망 이벤트의 공통 API를 만들었다. 이동·탐색·발포 알고리즘은 클라이언트 A의 후속 구현 범위로 의도적으로 남겼다.
- 공용 프리팹이 사용할 `AllyUnitView.Bind` 골격과 위치·방향 동기화를 추가했다. 프리팹과 아트는 아직 생성하지 않았다.
- `WaveManager.ActiveEnemies`를 읽기 전용으로 노출해 UnitDeployController가 별도 EnemyManager 없이 아군 Tick에 적 후보를 전달할 수 있게 했다.
- `OperatorDefinition`/`OperatorLoadoutSession`에 선택적 `AllyUnitRoster` 경계를 추가했다. 타워 전용 오퍼레이터는 null이 정상이며 이때 후속 배치 UI가 숨겨지는 계약이다.
- 오퍼레이터 JSON 레시피와 에셋 빌더가 선택적 유닛 로스터를 복제·연결하도록 확장하고, 검증기에 빈 로스터·중복 ID·잘못된 수치 검사를 추가했다.

### 판단 근거
- 두 클라이언트가 병렬로 작업하려면 전투 구현과 소환/UI가 함께 의존할 타입·메서드 모양이 먼저 고정되어야 한다. 이 커밋은 그 경계만 제공하고 양쪽 기능을 선점하지 않는다.
- 로스터와 Definition은 읽기 전용으로 소비하며 인스턴스별 체력·쿨다운·상태는 `AllyUnitInstance`에 둔다. 아직 유닛 해금 카드처럼 로스터 자체를 변경하는 요구가 없어 TowerRoster의 런타임 복제 캐시는 선제 도입하지 않았다.
- WaveManager의 실제 리스트를 `IReadOnlyList`로만 노출해 적 생명주기 소유권은 기존 WaveManager에 유지했다.
- 유닛 로스터를 필수로 만들면 기존 타워형 카시아가 의미 없는 빈 에셋을 가져야 한다. 선택적 계약으로 두어 유닛형 오퍼레이터에서만 배치 시스템을 활성화한다.

### 후속 작업 분리
- 클라이언트 A: `AllyUnitInstance` 이동·탐색·발포, `EnemyInstance`의 아군 조우·교전 최소 변경, 근접/원거리 효과.
- 클라이언트 B: `UnitDeployController`, 지휘 포인트, 선택·소환 UI/HUD, 프리팹·씬·에셋 자동 배선.
- 디렉터/통합: 공유 파일 변경 검수, 수치·콘텐츠 결정, 양쪽 수직 슬라이스 통합.

### 의도적으로 하지 않은 것
- `AllyUnitManager`를 추가하지 않았다.
- `EnemyInstance` 전투 동작, 지휘 포인트, 소환 입력과 UI를 구현하지 않았다.
- 아군 유닛 Definition/Roster 에셋이나 프리팹을 임의의 임시 데이터로 만들지 않았다.

### 검증 경로
- `AllyUnitFoundationVerifier`가 메모리 임시 SO만 사용해 역방향 경로 스폰, 진격↔교전 상태 전이, 피해·사망, Roster ID 조회, OperatorLoadoutSession 로스터 해석을 검사한다.
- 기존 전 스크립트가 asmdef 없는 `Assembly-CSharp` 구조이므로 테스트 어셈블리는 도입하지 않고, Unity 메뉴와 배치 `-executeMethod RCCom.EditorTools.AllyUnitFoundationVerifier.Verify` 양쪽에서 같은 검증을 재사용한다.
- Unity `6000.3.13f1` 배치 모드에서 전체 스크립트 컴파일과 기반 계약 검증을 통과했다.
- 기존 카시아의 유닛 로스터가 비어 있는 상태에서도 전체 OperatorAssetValidator를 통과했다. 기존 `축적.asset` 미등록 경고 1건은 동일하다.

## 2026-08-14 — 아군 유닛 이동·전열 교전 코어

**맥락**
아군 유닛의 공통 계약은 앞선 작업에서 고정됐지만 실제 진격·교전·적의 역교전은 비어 있었다. 이번 수직 슬라이스는 `UnitDeployController`와 씬 배선에 의존하지 않고, 순수 C# 인스턴스가 기존 `MapManager.Waypoints`를 공유해 이동과 전투를 완결하는 것을 목표로 했다.

**결정**
- `UnitCombatSettings` SO에 `contactRange`와 `separationMargin`만 두고, `AllyUnitInstance.Spawn(definition, path, settings)`에서 유효한 값만 해석해 인스턴스에 보관한다. 기존 `Spawn(definition, path)`는 0.75/0.05 안전 기본값으로 유지했다.
- `attackRange`와 `contactRange`를 분리했다. `attackRange`는 공격 가능 거리이고 `contactRange`는 양 진영이 이동을 멈추는 거리이므로, 원거리 유닛이 공격을 시작했다고 바로 멈추지 않고 실제 접촉 전까지 진격할 수 있다. 적의 잘못된 `attackRange`는 상대의 `contactRange` 이상으로 보정하고, `attackInterval`의 잘못된 값은 1초로 보정한다.
- 아군은 경로 끝점에서 시작해 웨이포인트 인덱스를 감소시키며 이동한다. 적 생성점부터 누적 경로 거리로 `contactRange + separationMargin`을 계산해 최종 대기점을 만들고, 첫 선분이 짧아도 전체 폴리라인을 따라 대기점을 찾는다.
- `AllyUnitTargeting`은 월드 거리 필터 후 연속 경로 진행도를 비교한다. 적은 0→1, 아군은 1→0인 같은 좌표계를 사용해, 단순 웨이포인트 인덱스가 아니라 현재 선분 내부 이동량까지 전열 판단에 포함했다.
- 아군이 매 Tick 각 적에게 자신을 후보로 제시하게 했다. 적이 전체 아군 목록을 저장하거나 `WaveManager`가 목록을 전달하면 오브젝트 타입별 매니저와 흐름 매니저의 결합이 생기므로, 적은 제시된 후보 중 생성점 방향으로 가장 전진한 아군만 교체·유지한다. 같은 진행도에서는 거리, 완전 동률에서는 먼저 제시된 대상을 유지해 여러 아군/적이 같은 전열을 집중 공격한다.
- `EnemyInstance`에는 별도의 `EnemyState` 전체 상태 머신을 도입하지 않았다. 기존 웨이포인트 이동·둔화·독·취약·효과 훅을 유지한 채 현재 아군 타깃, 공격 쿨다운, 거점 도달 여부, 생존 상태만 추가하고, 접촉 거리 안일 때만 기존 이동 호출을 건너뛰도록 최소 변경했다.
- `BasicAttackEffect`는 `AllyUnitEffectBase`를 상속하고 `OnAttack`에서 살아 있는 대상에게 `ctx.self.Data.attackDamage`만 적용한다. 쿨다운·타깃·목록은 여러 인스턴스가 공유하는 SO가 아니라 `AllyUnitInstance`가 소유한다. 따라서 근접/원거리 유닛 모두 같은 효과 SO를 조립할 수 있다.
- `AllyUnitView`는 `Damaged`/`Died`를 구독해 코루틴 없는 잔여 타이머 틴트를 적용하고, 공격 가능한 타깃 또는 다음 웨이포인트 방향을 표현한다.

**근거**
- 공격 사거리와 정지 사거리를 하나로 합치면 원거리 공격 시작 순간에 이동이 멈춰 전열이 지나치게 앞에서 고정된다. 두 거리를 분리해야 공격 중 진격과 접촉 시 정지가 동시에 성립한다.
- 연속 진행도를 사용한 이유는 긴 웨이포인트 선분 안에서 조금 더 전진한 적/아군을 인덱스만으로 구분할 수 없기 때문이다. 진행도 비교는 적·아군 모두 같은 경로 의미를 공유하면서도 방향별 우선순위를 대칭적으로 표현한다.
- 아군 후보 제시 방식은 `WaveManager`에 아군 목록을 추가하지 않기 위한 선택이다. 유닛 순회 주체가 나중에 바뀌어도 적은 공개 후보 메서드 계약만 소비한다.
- 적에 전체 상태 머신을 넣지 않은 이유는 기존 이동과 효과 훅의 회귀 범위를 키우지 않기 위해서다. 교전 여부는 현재 타깃의 유효성·공격 범위·접촉 범위로 충분히 표현되며, 사망/거점 도달 뒤에는 Tick 자체를 중지한다.
- SO에 설정만 두고 런타임 상태를 두지 않은 이유는 동일한 Definition/Effect를 여러 유닛이 공유하기 때문이다. 타깃과 쿨다운을 SO에 넣으면 한 유닛의 공격이 다른 유닛의 상태를 오염시키므로, 인스턴스별 상태를 순수 C# 런타임 객체에 한정했다.

**의도적으로 하지 않은 것**
- `UnitDeployController`, `WaveManager`, 지휘 포인트, UI/HUD, 씬, 프리팹, 기존 `.asset`은 수정하지 않았다. 실제 `UnitCombatSettings`/`BasicAttackEffect` 에셋 생성과 컨트롤러 주입은 통합 작업으로 남겼다.
- 투사체·비행 로직·체력바·사운드는 이 코어의 범위에서 제외했다.
- 신규 매니저, asmdef, 테스트 어셈블리를 추가하지 않았다. 검증은 기존 프로젝트 구조에 맞춰 Editor 메뉴와 메모리 임시 SO로 수행했다.

**검증**
- Unity `6000.3.13f1`에서 단계별 `recompile`/`recompile_status`를 반복 실행했고 최종 `failed=false`를 확인했다. 최종 컴파일 이후 신규 콘솔 오류는 0건이었다. 기존 `OperatorSelectionSetup.cs`의 obsolete 경고와 Pipeline 자동화 모드 경고는 기존 경고다.
- `AllyUnitFoundationVerifier`와 신규 `AllyUnitCombatVerifier`를 Unity Pipeline `eval`로 실행했다. 신규 검증기는 끝점 스폰, 연속 진행도 감소, 짧은 첫 선분을 포함한 최종 대기점 0.8, 이동 중 공격, 양측 contactRange 정지, 즉시 첫 공격/쿨다운, 양측 사망 후 진격 재개, 양측 전열 집중포화, 죽은 대상 재공격 방지, 범위 이탈 해제, 교전 없는 기존 적 이동, 거점 피해/`ReachedGoal` 1회, 기본 공격 피해, 기존 `ContactDamageEffect` 경로 등 18개 시나리오를 모두 통과했다.
- 검증기는 종료 시 생성한 임시 SO를 모두 `DestroyImmediate`로 정리했으며 프로젝트 `.asset`/프리팹/씬에는 변경이 없다.

**사람 액션**
- 없음. 통합 단계에서 실제 `UnitCombatSettings` 에셋을 만들고 `UnitDeployController`가 새 Spawn 오버로드에 전달하면 된다. 이번 작업에서는 해당 파일이 없는 브랜치 계약을 보존하기 위해 컨트롤러를 건드리지 않았다.

## 2026-08-14 전투 코어 검토 보완

### 보완 내용
- 큰 프레임에서도 아군과 적이 접촉선을 관통하지 않도록, 각 이동 선분과 `contactRange` 원의 첫 교차점까지만 이동을 허용했다. 범위 밖에서 접촉 범위로 진입하는 프레임에 양쪽이 즉시 `Engaging`이 되는 이유는 실제 프레임 순서에서 한 번도 접촉 상태를 놓치지 않게 하기 위해서다.
- 진행도는 위치에서 가장 가까운 선분을 추측하지 않고 각 인스턴스가 가진 현재 웨이포인트 인덱스와 해당 선분의 보간값으로 계산한다. 교차·되감기 경로에서도 전열이 다른 구간으로 순간 이동하지 않으며, 최종 대기점도 같은 누적 경로 길이 계산을 사용한다.
- `SetEngagementTarget`은 유효하게 스폰된 대상이 `contactRange` 안에 있을 때만 `Engaging`으로 전환한다. 공격 범위 안이지만 접촉 범위 밖인 원거리 유닛은 계속 `Advancing`할 수 있다.
- 적 전열 집중 공격 검증은 서로 다른 진행도를 가진 전방·후방 아군을 함께 배치하고 두 적이 전방 아군을 선택하는지 확인하도록 보강했다.
- 적 최초 조우의 이동 순서는 모든 `AllyUnitInstance.Tick`을 먼저 끝낸 뒤 `EnemyInstance.Tick`을 호출하는 단계 계약으로 명시했다. 각 아군은 이동을 마친 최신 위치에서 Tick 마지막에 후보를 제시하므로, 적이 전체 아군 목록을 보유하지 않으면서도 같은 프레임 위치 변화로 접촉 경계 제한이 빠지지 않는다.
- `attackRange` 밖의 아군은 공격 타깃으로는 계속 거부하되, 현재 프레임 이동 선분이 그 아군의 `contactRange`와 만날 경우 별도의 이동 차단 타깃으로 기록한다. 따라서 최초 후보 제시 시점에 공격 사거리를 벗어나 있어도 긴 프레임 이동으로 접촉선을 관통하지 않는다.
- `OfferAttackCandidates`는 아군 이동과 효과 Tick이 끝난 뒤 한 번만 실행한다. 통합 컨트롤러가 별도 후보 선행 순회를 하지 않아 아군×적 후보 비교가 중복되지 않는다.

### 검증 결과
- `AllyUnitFoundationVerifier`와 `AllyUnitCombatVerifier`를 Unity Pipeline `eval`로 다시 실행했다. 컴파일 `failed=false`, 신규 콘솔 오류 없음, 전투 검증기 19개 시나리오 통과를 확인했다.
- 큰 프레임 양방향 진입, 초기 거리 11에서 아군이 먼저 2만큼 이동한 뒤 `attackRange` 3인 적이 10만큼 이동하는 최초 조우, 최신 위치 후보 제시 후 적 이동 순서, 접촉 범위 정지, 교차·되감기 경로의 실제 구간 진행도, `SetEngagementTarget`의 원거리 이동 의미, 전·후방 아군을 둔 적 집중 공격을 추가로 검증했다.

### 통합 시 남은 순서 계약
- `WaveManager`는 이번 작업에서 수정하지 않았다. `UnitDeployController` 통합 시 같은 프레임의 모든 아군 `Tick(deltaTime, activeEnemies, activeAllies)`을 먼저 완료하고, 그 뒤 기존 `WaveManager.Update`가 `EnemyInstance.Tick(deltaTime)`을 실행하도록 명시적인 실행 순서를 부여해야 한다. 아군 Tick이 최신 위치 후보 제시까지 소유하므로 별도의 `OfferAttackCandidates` 선행 호출은 하지 않는다.

## 2026-08-14 — Manager가 아닌 UnitDeployController 배치 경계

### 맥락
- 아군 유닛은 순수 C# `AllyUnitInstance`라서 MonoBehaviour `Update`를 스스로 가질 수 없지만, 개체 타입별 `AllyUnitManager`를 추가하면 기존 매니저 원칙을 위반한다.
- 타워 건설과 대칭으로, 플레이어의 "유닛 배치"라는 한 입력 흐름과 그 흐름이 만든 인스턴스만 소유하는 일반 Controller가 필요했다.

### 결정
- `Runtime/UnitDeployController.cs`를 MonoBehaviour로 추가했다. 별도 Manager나 싱글톤은 만들지 않았다.
- 선택된 오퍼레이터의 선택적 `AllyUnitRoster`를 `OperatorLoadoutSession`에서 해석한다. 타워형 오퍼레이터처럼 로스터가 없으면 오류나 암묵적 기본 로스터 없이 안전하게 동작을 멈춘다.
- UI가 호출할 `SelectUnit`, `ClearSelection`, `TryDeploySelected`, `TryDeploy` API를 제공한다.
- 배치 시 `AllyUnitInstance.Spawn` 후 공용 `AllyUnitView` 프리팹 하나를 생성해 `Bind`한다. 유닛 종류별 프리팹이나 C# 타입은 추가하지 않는다.
- Controller가 자신이 배치한 `List<AllyUnitInstance>`만 소유하고 역순으로 Tick한다. 적 후보는 `WaveManager.ActiveEnemies`를 읽기 전용으로 전달하며 적 목록의 소유권을 가져오지 않는다.
- 사망 이벤트로 목록을 제거하고, UI가 단방향으로 상태를 관찰할 수 있도록 선택·배치·제거 이벤트를 노출한다.

### 판단 근거
- Manager와 Controller의 차이는 이름이 아니라 책임 범위다. 전자는 유닛 타입 전체의 전역 시스템이 되지만, 후자는 TowerBuildController와 마찬가지로 한 플레이어 행동 흐름만 조율한다.
- 인스턴스 목록 수정 권한을 Controller 하나에 모으면 향후 적과의 상호 전투가 추가되어도 WaveManager와 서로 상대 목록을 직접 수정하지 않는다.
- 로스터 선택과 View 주입을 공개 배치 API 안에서 닫아 후속 UI가 런타임 인스턴스 생성 순서를 중복 구현하지 않게 했다.

### 의도적으로 하지 않은 것
- 지휘 포인트 최대치·시작치·회복량과 유닛 비용 차감은 기획 수치가 미확정이라 임의 구현하지 않았다.
- 키보드·게임패드의 구체 배치 키도 정하지 않았다. 후속 선택 UI가 공개 API를 호출하도록 경계만 만들었다.
- DefenseScene 배치, 공용 View 프리팹 생성, 인스펙터 연결은 이번 Controller 코드 범위에 포함하지 않았다.
- `AllyUnitManager`, 전역 static 목록, 종류별 유닛 프리팹을 만들지 않았다.

### 검증
- 실행 중인 Unity `6000.3.13f1` Pipeline에서 재컴파일 완료, 컴파일 오류 없음.
- 기존 `AllyUnitFoundationVerifier`를 다시 실행해 역주행 스폰·상태·피해·Roster·Loadout 계약 통과를 확인했다.
- Unity가 `UnitDeployController.cs.meta`를 자동 생성한 것을 확인했다.
- 콘솔에는 이번 변경과 무관한 기존 TMP obsolete 경고 1건과 Pipeline 자동화 모드 주의만 남아 있다.

## 2026-08-14 — 유닛 로스터 가용성에 따른 배치 입력·UI 차단

### 결정
- `UnitDeployController.IsDeployInputEnabled`를 유닛 배치 입력의 단일 게이트로 두고, 일시정지 중이거나 선택된 오퍼레이터의 `AllyUnitRoster`가 null·빈 목록이면 선택과 배치 요청을 거부한다.
- `UnitDeployMenuUI`는 `UnitDeployController.IsAvailable`을 읽어 유닛 패널의 `CanvasGroup`을 표시·숨김 처리한다. 로스터가 없는 타워형 오퍼레이터는 오류가 아니라 정상 비활성 상태로 취급한다.
- 패널은 `GameObject.SetActive`로 끄지 않고 `alpha`, `interactable`, `blocksRaycasts`를 함께 변경한다.

### 판단 근거
- UI를 숨기는 것만으로는 단축키나 다른 호출 경로가 배치 API를 직접 실행할 수 있으므로 실제 생성 책임자인 Controller에서도 입력을 거부해야 한다.
- 게임플레이가 UI를 직접 찾아 끄지 않고 UI가 공개 가용성 상태를 읽게 해 UI→게임플레이 단방향 참조 원칙을 유지한다.
- `GameObject.SetActive(false)`로 UI 자신을 끄면 후속 이벤트 구독이나 갱신 경로까지 사라졌던 기존 `CardSelectionUI` 사례가 있어 같은 `CanvasGroup` 계약을 재사용한다.

### 의도적으로 하지 않은 것
- 키보드·게임패드의 구체 배치 키는 아직 기획이 확정되지 않아 추가하지 않았다.
- 실제 유닛 패널과 `UnitDeployController`가 DefenseScene에 아직 없으므로 기존 씬이나 임시 UI 에셋을 만들지 않았다. 신규 오퍼레이터·유닛 UI가 준비되면 `UnitDeployMenuUI`의 Controller와 CanvasGroup만 연결한다.

### 검증
- Unity `6000.3.13f1` Pipeline 재컴파일을 완료했고 컴파일 오류가 없음을 확인했다.
- `AllyUnitFoundationVerifier`에서 null 로스터일 때 선택 요청 거부와 패널 비표시·비상호작용, 유효 로스터일 때 입력 허용과 패널 표시·상호작용 계약을 모두 검증했다.

## 2026-08-14 — AllyUnitRoster 기반 동적 Definition 선택 UI

### 구현
- `UnitDeployMenuUI`가 선택된 오퍼레이터의 `AllyUnitRoster.units`를 순회해 공용 `UnitDeployButton` 프리팹을 동적으로 생성한다.
- 각 버튼은 `AllyUnitDefinition`의 스프라이트, 표시 이름, 배치 비용을 표시하고 로스터 인덱스를 캡처해 `UnitDeployController.SelectUnit`을 호출한다.
- `UnitDeployButton`은 자신이 표시하는 Definition만 보유하며 로스터나 Controller를 직접 참조하지 않는다. 선택 인덱스와 생성 책임은 메뉴에 남겼다.
- 메뉴는 `SelectionChanged` 이벤트를 구독해 현재 Definition과 일치하는 버튼에만 선택 표시를 켠다. `ClearSelection`이 호출되면 모든 표시가 꺼진다.

### 판단 근거
- 버튼 개수를 고정하면 오퍼레이터마다 유닛 수가 달라질 때 씬이나 코드를 다시 수정해야 한다. Roster 기반 동적 생성은 신규 오퍼레이터를 에셋 조립만으로 추가한다는 확장 목표를 유지한다.
- 버튼이 로스터 인덱스를 직접 계산하거나 Controller를 소유하게 하지 않고 콜백만 받게 해 기존 `TowerBuildButton`/`TowerBuildMenuUI`의 책임 분리를 따른다.
- 자동 기본 선택은 기획으로 확정되지 않았으므로 넣지 않았다. 플레이어가 버튼을 누른 Definition만 선택 상태가 된다.

### 의도적으로 하지 않은 것
- 실제 버튼 프리팹과 DefenseScene의 스크롤 Content 배치는 유닛 아트와 UI 레이아웃이 아직 없어 생성하지 않았다.
- 배치 비용은 표시만 하며 지휘 포인트 차감은 별도 자원 작업에서 구현한다.

### 검증
- Unity `6000.3.13f1`에서 전체 스크립트 재컴파일을 완료했고 오류가 없었다.
- `AllyUnitFoundationVerifier`가 메모리 임시 버튼 템플릿으로 로스터 수만큼 생성, Definition 연결, 이름·비용 표시, 클릭 콜백 선택, 선택 표시와 해제를 검증했다.

## 2026-08-15 — 유닛 배치 지휘 포인트 비용 확인·소비

### 구현
- `UnitDeployController`에 시작 지휘 포인트와 세션 중 현재 잔액을 추가하고 `CommandPoints`, `CanSpendCommandPoints`, `CanAfford`, `TrySpendCommandPoints`, `AddCommandPoints`를 공개했다.
- 모든 배치 호출 경로가 합류하는 `TryDeploy`에서 `AllyUnitData.deployCost`를 검사한다. 잔액이 부족하면 생성 없이 실패하고 `DeployFailedInsufficientCommandPoints`를 알린다.
- 순수 인스턴스 스폰과 공용 View 바인딩까지 성공한 뒤 비용을 소비하고, 잔액 변경은 `CommandPointsChanged`로 알린다.
- 음수 비용은 잘못된 Definition으로 보고 배치를 거부하며, 비용 0은 정상적인 무료 배치로 처리한다.

### 판단 근거
- UI에서만 비용을 확인하면 단축키나 후속 자동 배치 경로가 검증을 우회할 수 있으므로 실제 생성 책임자인 Controller를 단일 소비 경계로 삼았다.
- 스폰 효과 중 즉시 사망하거나 View 생성 준비가 실패한 배치에서는 지휘 포인트가 빠지지 않아야 하므로, 성공 조건을 확인한 뒤 소비한다.
- 지휘 포인트는 유닛 배치 흐름 전용 상태라 별도 Manager나 `GameManager` 확장 없이 `UnitDeployController`가 소유한다.

### 의도적으로 하지 않은 것
- 최대 지휘 포인트, 자동 회복 속도, 오퍼레이터별 시작값은 `EXPANSION_PLAN.md` §5 미확정 수치이므로 정하지 않았다. 시작 잔액만 인스펙터 입력으로 두고 현재 구현에는 상한을 넣지 않았다.
- HUD 표시와 부족 피드백 연출은 공개 값과 이벤트만 준비하고 이번 범위에는 포함하지 않았다.

### 검증
- Unity `6000.3.13f1` Pipeline에서 전체 스크립트 재컴파일을 완료했고 오류가 없었다.
- `AllyUnitFoundationVerifier`에서 5포인트 지급, 비용 3의 구매 가능 판정과 소비 후 잔액 2, 다시 비용 3을 소비하려 할 때 거부되고 잔액과 변경 이벤트 값이 유지되는 계약을 검증했다.

## 2026-08-15 — 공용 AllyUnitView 프리팹 생성

### 구현
- `AllyUnitViewPrefabBuilder`를 추가해 `Assets/Data/Prefabs/AllyUnitView.prefab`을 Unity Editor API로 반복 생성할 수 있게 했다.
- 공용 프리팹은 루트 하나에 `SpriteRenderer`와 `AllyUnitView`만 둔다. 스프라이트는 비워 두며 `UnitDeployController`가 생성 직후 호출하는 `Bind`에서 `AllyUnitDefinition.sprite`를 주입한다.
- 기존 EnemyView와 같은 기본 Sorting Layer와 Order 2를 사용하고, `AllyUnitView.targetVisualSize` 기본값 0.9를 유지한다.
- 빌더가 저장 직후 프리팹 존재 여부, 필수 컴포넌트, 빈 기본 스프라이트, 단일 `AllyUnitView` 계약을 검증한다.

### 판단 근거
- 유닛별 프리팹에 스프라이트를 고정하면 신규 오퍼레이터마다 프리팹 복제가 필요해져 Definition 주입 원칙과 데이터 드랍 목표가 깨진다.
- 현재 아군 교전은 순수 C# Instance가 담당하고 `AllyUnitView`에는 물리 충돌이나 체력바 계약이 없으므로 Collider·Rigidbody2D·체력바를 선제 추가하지 않았다.
- 같은 에셋을 다시 만들 일이 생겨도 YAML을 직접 편집하지 않도록 커밋 가능한 에디터 빌더를 생성 경로로 남겼다.

### 의도적으로 하지 않은 것
- `UnitDeployController`와 공용 프리팹의 DefenseScene 배선은 Controller 오브젝트와 유닛 로스터가 아직 씬에 없어 이번 범위에서 수정하지 않았다.
- 유닛별 스프라이트는 신규 오퍼레이터 아트가 준비된 뒤 각 `AllyUnitDefinition`에 연결한다.

### 검증
- Unity `6000.3.13f1` Pipeline에서 빌더 컴파일을 완료했고 오류가 없었다.
- 빌더를 실제 실행해 프리팹과 Unity 생성 `.meta`가 디스크에 저장됐으며, 생성 직후 구조 검증을 통과했다.

## 2026-08-17 — 유닛 배치와 전열 교전 실행 순서 통합

### 맥락
- KIM의 배치 Controller는 아군 목록을 직접 Tick하도록 만들어졌고, CLIENT-1의 전열 교전은 모든 아군이 최신 위치에서 후보를 제시한 뒤 적이 Tick해야 한다는 순서 계약을 추가했다.
- 두 브랜치를 단순히 함께 두면 MonoBehaviour `Update` 순서가 보장되지 않으며, 특히 같은 프레임에 생성된 적이 아군 후보를 받기 전에 이동할 수 있다.

### 결정
- `WaveManager`가 스폰·빌드 단계 처리 후, 적 Tick 직전에 `BeforeEnemiesTick` 이벤트를 발생시킨다.
- `UnitDeployController`는 이 이벤트를 구독해 자신이 소유한 모든 아군을 Tick한다. `WaveManager`는 아군 목록이나 Controller를 참조하지 않고 기존 적 목록 소유권을 유지한다.
- 일시정지 게이트는 이벤트 핸들러 첫 단계에 유지해 `Time.timeScale == 0`일 때 아군 상태가 진행되지 않게 했다.
- 배치 시 CLIENT-1이 추가한 `UnitCombatSettings`를 `AllyUnitInstance.Spawn`에 주입해 접촉 거리와 최종 대기점 설정이 실제 플레이 경로에서도 적용되게 했다. 설정이 비어 있으면 인스턴스의 안전 기본값을 사용한다.

### 의도적으로 하지 않은 것
- `WaveManager`에 아군 목록이나 `UnitDeployController` 참조를 추가하지 않았다.
- 별도 `AllyUnitManager`나 전역 실행 순서 설정을 만들지 않았다.

### 검증 결과
- Unity `6000.3.13f1` 배치 모드에서 전체 스크립트 컴파일을 통과했다. 기존 `OperatorSelectionSetup`의 TMP obsolete 경고 1건만 동일하게 남았다.
- `AllyUnitFoundationVerifier`의 역주행 기반·배치 가용성·Definition 선택·지휘 포인트 계약을 통과했다.
- `AllyUnitCombatVerifier`의 이동·접촉 경계·공격·타깃 해제·전열 집중 19개 시나리오를 통과했다.
- `AllyUnitViewPrefabBuilder.Validate`로 공용 View 프리팹 구조와 Definition 스프라이트 주입 전제를 확인했다.
- `OperatorAssetValidator`의 필수 참조 검증을 통과했다. 기존 `축적.asset` 미등록 경고 1건은 동일하다.

## 2026-08-17 — Cassia 임시 아군 수직 슬라이스 배선

### 맥락
- 전투 코어와 배치 Controller는 각각 검증됐지만, 실제 Definition·Roster·지휘 포인트 회복·UI·DefenseScene 참조가 비어 있어 게임 안에서 한 흐름으로 확인할 수 없었다.
- 신규 유닛 아트와 오퍼레이터 2·3의 최종 콘셉트는 아직 없으므로, 그 결정을 기다리지 않고 데이터 조립 구조를 실증할 수 있는 회색상자 기준이 필요했다.

### 결정
- Cassia에 임시 `AllyUnitRoster`를 연결했다. `전진 사수`는 비용 25, 체력 40, 이동 2.7, 피해 6/0.55초, 사거리 3.8의 저비용 원거리 지원이고, `방호 요원`은 비용 55, 체력 110, 이동 1.6, 피해 9/0.9초, 사거리 1.1의 전열 유지 역할이다. 둘 다 상태 없는 `BasicAttackEffect` 하나만 조립한다.
- 지휘 포인트는 최대 100, 시작 40, 전투 중 초당 4로 둔다. 빌드 페이즈에서 회복하면 첫 웨이브 전에 자원이 쌓여 선택이 사라지므로, `WaveManager.IsWaitingForNextWave`일 때는 회복하지 않는다. 일시정지는 기존 `Time.timeScale` 게이트로 함께 멈춘다.
- `UnitDeployMenuUI`는 로스터 버튼으로 유닛을 선택하고 별도 `소환` 버튼으로 Controller의 `TryDeploySelected`만 호출한다. 현재/최대 CP를 표시하며 잔액이 부족하면 소환 버튼을 비활성화한다. UI는 게임플레이 상태를 읽고 이벤트를 구독할 뿐 역참조하지 않는다.
- 정식 스프라이트가 없는 동안 `AllyUnitView`는 런타임 흰 사각형 마커를 만들고 Definition의 `tint`를 적용한다. 이는 이미지 에셋을 대신하는 회색상자 표현이며, Definition에 스프라이트가 연결되면 자동으로 실제 스프라이트 경로를 사용한다.
- `AllyUnitVerticalSliceBuilder`가 임시 SO 2개, 공용 공격 효과, Roster, CombatSettings, UnitDeployButton 프리팹과 DefenseScene 연결을 반복 생성한다. 자동 생성 라벨이 없는 기존 에셋은 덮어쓰지 않는다.

### 의도적으로 하지 않은 것
- 키보드·게임패드의 배치 단축키, 유닛별 해금 UI, 부족 시 사운드/대사 연출은 아직 추가하지 않았다.
- 오퍼레이터 2·3, 최종 밸런스, 정식 유닛 스프라이트는 이 임시 Cassia 수직 슬라이스에 포함하지 않았다.

### 검증
- 실행 중 Unity `6000.3.13f1`에서 컴파일 오류 없이 빌더를 실행했고, 생성 에셋·Cassia Roster·DefenseScene Controller/UI 참조 검증을 통과했다.
- `OperatorAssetValidator`, `AllyUnitFoundationVerifier`, `AllyUnitCombatVerifier`를 다시 실행해 각각 필수 참조, 배치/지휘 포인트 계약, 전투 19개 시나리오 통과를 확인했다. 기존 `축적.asset` 미등록 경고와 TMP obsolete 경고는 동일하다.

## 2026-08-18 — uGUI 회색상자 선택·유닛 배치 화면 정리

### 맥락
- 오퍼레이터 선택과 유닛 소환의 기능 경로는 이미 있었지만, 선택 화면은 단일 초상화·좌우 이동 중심이고 DefenseScene 패널은 우측 기준점 밖으로 밀릴 수 있는 임시 RectTransform 값이었다.
- 신규 아트와 오퍼레이터 2·3의 최종 데이터는 아직 없으므로, 이미지 제작을 기다리는 대신 데이터 수에 따라 늘어나는 uGUI 구조를 먼저 확정할 필요가 있었다.

### 결정
- `OperatorSelectionUI`가 카탈로그 항목마다 공용 `OperatorSelectionCard`를 동적으로 만들도록 바꿨다. 카드는 로컬/원격, 잠김/사용 가능 상태와 선택 테두리만 표현하고, 선택·다운로드·씬 전환의 책임은 기존 UI Controller에 유지했다.
- `OperatorSelectionSetup`은 카드 프리팹과 TitleScene의 카드 행·상세 패널·출격 버튼을 에디터 API로 생성한다. 카탈로그에 새 항목이 들어오면 별도 UI 프리팹 복제 없이 같은 행에 표시된다.
- 유닛 배치 패널은 우측 하단 앵커와 음수 여백으로 고정해 화면 바깥으로 밀리지 않게 했고, 현재 CP·유닛 비용·선택 유닛 출격 버튼·마지막 웨이포인트 출격 안내를 함께 표시한다.
- 유닛 버튼은 스프라이트가 비어 있으면 Definition의 `tint`를 임시 아이콘 색으로 사용한다. 정식 스프라이트가 연결되면 자동으로 원본 색을 사용한다.

### 의도적으로 하지 않은 것
- 오퍼레이터 2·3의 임시 영구 ID·콘텐츠 데이터를 만들지 않았다. 현재 카탈로그가 Cassia 한 명인 것은 기획 미확정 상태를 보존하기 위한 것이며, 카드 UI는 후속 데이터가 들어올 때 자동 확장된다.
- 별도의 UI 테마 SO, 맵 클릭 배치, 유닛 단축키, 아트 에셋은 추가하지 않았다. 마지막 웨이포인트에서의 즉시 출격은 기존 `AllyUnitInstance.Spawn` 계약을 그대로 사용한다.

### 검증
- Unity `6000.3.13f1` 재컴파일을 통과했다.
- `OperatorSelectionSetup.ValidateTitleSelectionUI`로 카드 프리팹, TitleScene 필수 참조, New Game 연결을 검증했다.
- `AllyUnitVerticalSliceBuilder.Validate`로 DefenseScene의 Controller·메뉴·지휘 포인트 참조를 다시 검증했다.

## 2026-08-18 — 타이틀 로비 오퍼레이터 클릭 대사 분리

### 맥락
- 기존 `OperatorDialogueUI`는 전투 중 플레이어·거점·건설 이벤트를 구독하므로 TitleScene에 그대로 재사용하면 필수 참조가 없는 로비에서 null 참조가 발생한다.
- 로비 캐릭터 아트는 아직 확정 전이지만, 캐릭터와 클릭 영역을 분리하면 후속 아트 교체를 기다리지 않고 상호작용 배선을 먼저 닫을 수 있다.

### 결정
- `LobbyOperatorDialogueUI`를 추가해 오퍼레이터 클릭, 랜덤 한 줄 표시, 대사창 클릭 닫기, 4초 유지 후 페이드만 담당하게 했다. 로비 타이머는 게임플레이 일시정지와 무관해야 하므로 `Time.unscaledDeltaTime`을 사용한다.
- `OperatorDialogueSet`에 선택적인 `lobbyInteraction` 슬롯을 추가했다. 최종 로비 대사가 비어 있는 동안에는 기존 `gameStart` 배열을 폴백으로 사용해 현재 Cassia 데이터만으로 즉시 동작한다.
- `LobbyOperatorDialogueSetup`이 TitleScene의 `MainMenuBackground` 아래에 왼쪽 오퍼레이터용 투명 클릭 영역과 하단 대사창을 생성하고 모든 필드를 연결한다. 실제 캐릭터 Image, 투명 Button, TMP 텍스트를 분리해 아트 교체가 클릭·대사 로직을 변경하지 않게 했다.

### 의도적으로 하지 않은 것
- 전투용 `OperatorDialogueUI`에 TitleScene 예외 분기를 넣지 않았다. 전투 이벤트 구독 책임과 로비 클릭 책임을 한 컴포넌트에 섞으면 두 씬 모두 불필요한 참조를 갖게 된다.
- Cassia의 최종 로비 전용 대사는 창작 방향이 확정되지 않아 임의 작성하지 않았다. `lobbyInteraction` 데이터만 채우면 코드나 씬 변경 없이 교체된다.
- 오퍼레이터 캐릭터 스프라이트와 최종 대사창 아트는 아직 연결하지 않았다. 현재 배선은 후속 로비 아트 위에 그대로 유지되는 회색상자다.

### 검증
- Unity `6000.3.13f1` Pipeline 재컴파일에서 `failed=false`를 확인했다.
- `LobbyOperatorDialogueSetup.Build`와 `Validate`를 실행해 TitleScene 씬 저장, 대사 데이터·클릭 버튼·닫기 버튼·TMP·CanvasGroup 참조 연결을 확인했다.
- 저장된 TitleScene 계층에서 `LobbyOperatorDialogueSystem/OperatorClickTarget`과 `DialogueBubble/DialogueText` 생성을 확인했다.

## 2026-08-18 — 2.5D 커맨드 로비 메뉴 배치와 기존 화면 흐름 통합

### 맥락
- 사용자가 `HomeBackground`에 새 로비 배경과 오퍼레이터 이미지를 직접 배치했지만, 실제 타이틀 전환은 기존 `MainMenuBackground`만 제어하고 있어 플레이 시 두 로비가 겹치거나 새 로비가 항상 표시될 수 있었다.
- 메뉴 패널의 원근은 래스터 스프라이트에 들어 있고, 클릭 영역과 글자는 별도 uGUI/TMP로 유지해야 아트 교체와 기능 배선을 독립시킬 수 있었다.

### 결정
- `CommandLobbyMenuSetup`이 사용자의 `HomeBackground`를 새 `MainMenuBackground`로 승격하고, 기존 메뉴 루트는 삭제하지 않고 `LegacyMainMenuBackground`로 비활성 보존한다. 로비 대사 시스템은 새 루트로 옮긴다.
- `Live Content`, `Operators`, `Operation`, `Records`, `Configuration` 패널을 우측에 서로 다른 크기·원근 위치로 배치하고 각 패널의 Normal/Hover Sprite와 TMP 제목·부제를 연결한다.
- `CommandLobbyMenuItem`은 포인터·키보드 선택에 따른 Sprite/TMP 색 전환만 담당한다. `Operation`은 기존 오퍼레이터 선택 화면, `Configuration`은 기존 설정 화면에 `TitleMenuTextButton`으로 연결한다.
- 아직 실제 화면이 없는 `Live Content`, `Operators`, `Records`는 가짜 동작을 만들지 않고 Hover 표현까지만 제공한다.
- `TitleSceneController`, `TitleConfigurationController`, `OperatorSelectionUI`의 메인 메뉴 참조를 모두 새 로비 루트와 CanvasGroup으로 다시 연결했다.

### 의도적으로 하지 않은 것
- 사용자가 임시로 늘여 배치한 단일 흰 패널은 삭제하지 않고 `ManualPanelPreview_Disabled`로 비활성 보존했다.
- 아이콘 에셋이 아직 없으므로 패널 스프라이트에 아이콘이나 문자를 굽지 않았고, 별도 TMP만 배치했다.
- 미구현 메뉴에 임시 팝업이나 빈 화면 전환을 추가하지 않았다.

### 검증
- Unity `6000.3.13f1`에서 런타임·에디터 스크립트 재컴파일을 통과했다.
- `CommandLobbyMenuSetup.Validate`로 패널 5종, Button, Normal/Hover 시각 컴포넌트, TMP 자식과 `TitleSceneController` 참조를 검증했다.
- 저장된 TitleScene 계층에서 새 `MainMenuBackground/CommandMenuPanels`와 비활성 보존된 `LegacyMainMenuBackground`, 이동된 `LobbyOperatorDialogueSystem`을 확인했다.

## 2026-08-18 — 오퍼레이터 선택 오버레이와 전술 로스터 배선

### 맥락
- 기존 카드 행 중심 회색상자는 새 2.5D 커맨드 로비 아트와 시각적으로 분리됐고, 선택한 오퍼레이터가 해금하는 아군 유닛을 출격 전에 확인할 수 없었다.
- 원격 오퍼레이터의 실제 `OperatorDefinition`은 다운로드 전에는 로드할 수 있으리라 가정하면 안 되므로, 선택 화면이 Definition이나 원격 스프라이트를 직접 요구하지 않는 경량 데이터 경로가 필요했다.

### 결정
- `Operator-Selection-Overlay-v2.png`를 전체 화면 고정 아트로 사용하고, 오퍼레이터 초상화·이름·설명·상태·다운로드 진행률과 유닛 로스터만 별도 uGUI/TMP로 올린다. 고정 제목과 버튼 문자는 래스터 아트에 남기고 실제 클릭은 투명 Button 영역이 담당한다.
- 여러 오퍼레이터 카드를 한 행에 동시에 표시하는 대신 현재 선택 항목 하나만 큰 카드로 렌더링하고 `PREV`/`NEXT`가 카탈로그 인덱스를 바꾸도록 유지했다. 배경 프레임과 동적 정보가 겹치지 않으면서 기존 키보드·게임패드 순환 계약도 보존한다.
- `OperatorCatalogEntry`에 `OperatorUnitPreview` 목록을 추가했다. 이름·배치 CP·임시 색상은 카탈로그 생성 때 Definition에서 복사하지만, 원격 콘텐츠의 유닛 스프라이트는 기본 빌드 참조에서 제외한다. 실제 전투 수치와 동작은 계속 `AllyUnitDefinition`만 소유한다.
- 선택 패널 아래에 타원형 방사 그라데이션 `CenterDimmer`를 두어 중앙 정보 영역으로 갈수록 로비가 어두워지게 했다. 단색 전체 덮개가 아니므로 화면 가장자리의 로비와 오퍼레이터 실루엣은 유지된다.
- 카드와 로스터 아이템은 각각 공용 프리팹 하나를 동적 생성하며, `OperatorSelectionSetup`이 오버레이·딤 스프라이트·투명 버튼·모든 Inspector 참조를 반복 배선한다.

### 의도적으로 하지 않은 것
- 카탈로그에 전투 스탯 전체를 복제하거나 원격 Definition을 선택 화면에서 선로딩하지 않았다. 선택 전 미리보기와 다운로드 후 실제 플레이 데이터의 책임을 분리하기 위해서다.
- 오퍼레이터별 선택 패널 프리팹, 유닛별 UI 프리팹, 새 UI 매니저를 만들지 않았다. 신규 콘텐츠는 카탈로그와 Definition 조립만으로 같은 화면을 사용한다.
- 화면 전체를 균일하게 검게 만드는 모달 딤은 사용하지 않았다. 사용자가 요청한 중앙 집중형 어두워짐과 2.5D 로비의 공간감을 보존했다.

### 검증
- Unity `6000.3.13f1` Pipeline 재컴파일에서 `failed=false`를 확인했다.
- `OperatorSelectionSetup.ValidateTitleSelectionUI`로 오버레이·중앙 딤·카드/로스터 프리팹·투명 버튼·TitleScene Inspector 참조를 검증했다.
- Play Mode에서 선택 패널 활성화, 현재 오퍼레이터 카드 1개(`880×280`), Cassia 유닛 로스터 2개(각 `390×82`) 생성을 확인했다.

## 2026-08-18 — DefenseScene 유닛 배치 버튼 참조 복구

### 맥락
- Cassia의 `AllyUnitRoster`에는 임시 유닛 2종이 정상 등록돼 있었지만, DefenseScene의 실제 배치 메뉴에는 출격 가능한 유닛이 하나도 표시되지 않았다.
- `UnitDeployMenuUI`는 로스터가 있어도 공용 `UnitDeployButton` 프리팹 참조가 비어 있으면 버튼을 생성할 수 없다.

### 원인과 결정
- `AllyUnitVerticalSliceBuilder.BuildButtonPrefab`이 `SaveAsPrefabAsset` 직후 프리팹 컴포넌트를 반환하고 `finally`에서 임시 루트를 파괴했다. 이 순서에서 반환 참조가 무효화되어, 씬에 저장된 `buttonPrefab`이 `None`이 됐다.
- 임시 루트를 먼저 파괴한 뒤 `AssetDatabase.LoadAssetAtPath`로 영속 프리팹을 다시 로드하고, `EditorUtility.IsPersistent`까지 확인한 컴포넌트만 DefenseScene에 연결하도록 순서를 변경했다.
- 검증기는 Controller와 메뉴가 각각 정확히 하나인지 확인하고, 직렬화된 버튼 프리팹이 null이 아닌지 명시적으로 검사하도록 강화했다. 첫 번째 검색 결과만 검사해 중복 또는 빈 참조를 놓치는 경로를 제거했다.
- `Tools/eval/InspectDefenseUnitDeploy.cs`를 추가해 Controller 로스터, View 프리팹, 메뉴 참조, 런타임 가시성과 버튼 수를 한 번에 확인할 수 있게 했다.

### 검증
- Unity `6000.3.13f1` 재컴파일에서 `failed=false`를 확인했다.
- 수정된 빌더를 다시 실행한 뒤 DefenseScene의 `buttonPrefab`이 영속 프리팹과 같은 객체이고, Cassia 로스터가 2종이며 씬이 저장된 상태임을 확인했다.
- Play Mode에서 배치 메뉴 `visible=True`, 버튼 2개, 시작 CP 40을 확인했다. `전진 사수` 선택·출격에 성공해 활성 유닛 1개, 잔여 CP 15가 됐고 신규 콘솔 오류는 없었다.

## 2026-08-19 — 오퍼레이터 호감도와 귀환 대사 기반

### 맥락
- 로비 오퍼레이터 클릭을 단순 랜덤 대사가 아니라 전투 귀환과 연결된 상호작용으로 확장할 필요가 있었다.
- 호감도는 오퍼레이터 콘텐츠의 일부처럼 보이지만 플레이어마다 달라지는 영속 상태이므로 Definition/Dialogue SO에 수치를 저장하면 안 된다.

### 결정
- `PlayerProfile`에 `operatorId`별 호감도 목록을 추가하고 저장 스키마를 2로 올렸다. 호감도 구간은 0~24 낯섦, 25~49 호감, 50~74 기쁨, 75~100 사랑으로 판정하며 100에서는 EX 터치 대사 풀을 우선한다.
- 결과 화면은 현재 참전 오퍼레이터 ID와 미수령 귀환 횟수를 프로필에 예약한다. 로비 클릭 시 참전 오퍼레이터면 회당 +5, 다른 오퍼레이터면 회당 +2를 적용하고 예약을 비운다. 현재 로비는 좌측 상단 전투 오퍼레이터를 그대로 표시하므로 기본 경로는 +5다.
- `OperatorDialogueSet`에 귀환(참전/비참전)과 호감도별 터치 슬롯을 추가했다. 로비 슬롯의 문장별 `lobbySprite`는 대사 출력 시 `Canvas/MainMenuBackground/OperatorImage`의 전신 스프라이트를 교체하며, 비어 있으면 로비 기본 전신을 유지한다.
- 결과 화면과 로비 UI가 각각 같은 `PlayerPrefsProfileStorage`를 사용하도록 유지했다. 새 오브젝트 타입 매니저나 프로필 매니저는 만들지 않았다.
- `OperatorAffinityDebugWindow`에서 ID별 호감도 경계 설정, 귀환 보상 예약, 예약 초기화를 제공한다.

### 의도적으로 하지 않은 것
- 클릭할 때마다 호감도를 올리지 않았다. 클릭 연타로 EX가 즉시 해금되는 것을 막고, 전투 귀환이라는 플레이 흐름을 보상 트리거로 보존하기 위해서다.
- 호감도 수치/등급 표시용 Operators 프로필 화면은 이번 작업에서 건드리지 않았다. 로비는 대사와 귀환 연출에만 집중하고 상세 수치는 기존 오퍼레이터 화면에서 표시할 예정이다.
- 로그인·터치·임무 완료 등 모든 대사 카테고리를 한 번에 추가하지 않았다. 현재는 로비 귀환과 터치 슬롯만 만들고, 같은 `OperatorLineSet` 계약으로 후속 카테고리를 확장할 수 있게 했다.

### 검증
- 실행 중인 Unity `6000.3.13f1`이 스크립트 변경을 감지해 재컴파일했고, Editor 로그에서 새 코드 관련 C# 오류가 발생하지 않은 것을 확인했다.
- Unity Pipeline 서버는 실행 중인 에디터에서 401로 응답해 CLI 명령 연결이 되지 않았다. 따라서 `LobbyOperatorDialogueSetup.Build/Validate`의 실제 씬 저장 및 Play Mode 검증은 에디터 연결 복구 후 남은 작업이다.

## 2026-08-19 — Operator Studio 제작 창과 로비 문장별 전신 스프라이트

### 맥락
- 신규 오퍼레이터를 추가할 때 레시피 JSON, DialogueSet SO, 로스터 원본, 생성 Definition, 카탈로그와 Addressables 그룹을 여러 창에서 따로 배선해야 했다.
- 로비 상호작용은 문장마다 메인 전신 스프라이트가 달라질 수 있지만, 기존 `OperatorLineSet`은 전투 상황 포트레잇 하나와 `string[]`만 보유했다.

### 결정
- `OperatorStudioWindow`를 `RCCom/Operators/Open Operator Studio`에 추가했다. Identity, Loadout, Dialogue, Package 탭에서 레시피와 대화 SO를 한 화면에서 편집하고, 기존 `OperatorAssetBuilder`를 통해 생성물과 Addressables를 갱신한다.
- `OperatorDialogueEntry`를 도입해 로비 대사 한 줄과 로비 전신 Sprite를 한 단위로 저장한다. 로비 슬롯에는 기본 전신을 둘 수 있고, 문장 Sprite가 없으면 슬롯 기본 전신과 `lobbyIdleSprite` 순서로 폴백한다.
- 기존 `OperatorLineSet.portraitSprite`는 피격·스킬 사용·거점 피격·자금 부족 등 전투 상황의 포트레잇으로 유지한다. `OperatorDialogueUI`는 상황 단위 포트레잇만 사용하고, `LobbyOperatorDialogueUI`는 메인 `OperatorImage`만 변경한다.
- 기존 `lines`는 Studio의 `Migrate All Legacy Lines`로 문장 엔트리에 보존·변환하되 전투 포트레잇을 문장 데이터로 복제하지 않는다. 이로써 기존 Cassia 전투 연출의 책임과 로비 전신 연출의 책임을 섞지 않는다.
- 원격 오퍼레이터의 선택 초상화를 로컬 카탈로그가 참조하지 않도록 수정했다. 원격은 ID·설명·경량 유닛 미리보기만 로컬에 남기고 실제 초상화와 대사는 Definition 다운로드 후 Addressables 의존성으로 받는다.

### 의도적으로 하지 않은 것
- Studio가 런타임 `OperatorDefinition`을 직접 편집하지 않는다. 레시피/DialogueSet은 원본, Definition/Roster/Catalog는 생성물로 분리해 사람이 생성물을 덮어쓰는 사고를 막았다.
- 오퍼레이터별 C# 클래스나 오퍼레이터 전용 프리팹을 만들지 않았다. 새 콘텐츠는 기존 SO와 아트 참조를 조립한다.
- 대사 음성, 다국어 테이블, Live2D 표정 컨트롤은 현재 문장+Sprite 계약 밖의 기능이므로 후속 단계로 남겼다.

### 구현 중 정정
- 최초 배선에서는 "대사마다 스프라이트 변경"을 전투 포트레잇까지 일반화해 로비 말풍선 내부의 별도 `DialoguePortrait`를 변경했다. 이는 `ARCHITECTURE.md`에 기록된 전투 상황별 포트레잇 계약과 사용자가 의도한 로비 전신 변경을 혼동한 것이었다.
- 커밋 전에 해당 배선을 제거하고 `MainMenuBackground/OperatorImage`를 직접 연결했다. Studio도 로비 슬롯에는 `Lobby Sprite`, 전투 슬롯에는 `Situation Portrait`만 노출하도록 분리했다.

### 검증
- Unity `6000.3.13f1` 재컴파일에서 `failed=false`를 확인했다.
- 기존 Cassia 대사 22개를 문장별 엔트리로 마이그레이션하고, Operator Builder를 실행해 Definition·Catalog·Addressables 그룹을 갱신했다.
- TitleScene의 기존 Cassia 전신을 `lobbyIdleSprite`로 승격하고, 로비 컨트롤러가 `MainMenuBackground/OperatorImage`를 참조하는지 확인했다. 말풍선 내부의 잘못 생성된 `DialoguePortrait`는 제거했다.
- Operator Studio 메뉴를 실제 실행했고, Validator가 필수 참조를 통과했다. 현재 경고는 비어 있는 호감도 슬롯과 기존 미등록 타워 등 미작성 콘텐츠에 관한 것이며 새 오류는 없다.

## 2026-08-19 — 에디터 전용 호감도 디버그 오버레이

### 맥락
- 호감도는 0~100 경계와 귀환 보상(+2/+5)을 함께 확인해야 하므로, PlayerPrefs를 직접 지우거나 에디터 창과 로비를 번갈아 조작하는 방식은 대사·전신 스프라이트까지 검증하기에 느렸다.

### 결정
- TitleScene 우측 상단에 반투명 `OperatorAffinityDebugOverlay`를 추가했다. 오퍼레이터 ID, 현재 호감도·등급·귀환 예약을 표시하고, 슬라이더·경계값 버튼·±1 조절·예약/초기화·대사 출력을 제공한다.
- 패널은 `PlayerPrefsProfileStorage`와 `LobbyOperatorDialogueUI.ShowInteraction()`을 그대로 사용한다. 따라서 디버그 버튼이 별도 보상 규칙을 만들지 않고 실제 프로필 저장·귀환 소비·호감도별 대사 폴백을 검증한다.
- `귀환 +5`는 현재 ID를 예약하고, `비참전 +2`는 별도 디버그 ID를 예약해 다음 현재 오퍼레이터 클릭에서 비참여 보상을 재현한다. 예약 버튼을 누르는 즉시 호감도를 올리지 않는다.
- 패널 동작은 `UNITY_EDITOR`로 감싸고 플레이어에서는 `Awake`에서 루트를 비활성화한다. 에디터 전용 검증 UI가 WebGL/Standalone 실행 화면에 노출되지 않게 하면서, 씬 배선은 에디터 메뉴로 재현 가능하게 유지한다.

### 검증
- `OperatorAffinityDebugPanelSetup.Build/Validate`를 실행해 TitleScene의 UI와 모든 Inspector 참조를 저장·검증했다.
- Unity `6000.3.13f1` 재컴파일에서 `failed=false`를 확인했다.

## 2026-08-19 — 로비 오퍼레이터 관리 화면과 공용 카드 View

### 맥락
- 기존 `OperatorSelectionUI`는 Operation을 누른 뒤 콘텐츠를 내려받고 곧바로 DefenseScene에 진입하는 출격 전 선택 화면이다. 로비의 Operators 메뉴에서 활성 오퍼레이터만 바꾸는 관리 흐름과 책임이 달랐다.
- 사용자가 `OperatorManagingSystem`에 배경·환경 장식·좌우 버튼·Deploy 버튼을 먼저 배치했으므로, 자동화가 이 기존 아트를 지우거나 다시 만드는 방식은 피해야 했다.

### 결정
- `OperatorManagementUI`를 별도 화면으로 추가하고 Operation의 `OperatorSelectionUI`는 유지했다. 관리 화면의 Deploy는 전투 씬으로 이동하지 않고 `PlayerProfile.selectedOperatorId`와 `OperatorLoadoutSession`만 갱신한다.
- `OperatorManagementCardView` 공용 프리팹 하나가 `OperatorPanels_Managing_0/1/2`를 해금 일반·호버/포커스·잠금 상태로 전환한다. 현재 탐색 항목과 실제 활성 오퍼레이터는 별도 상태로 두어, ACTIVE 배지가 호버 표현에 종속되지 않게 했다.
- 잠긴 카드도 클릭과 정보 확인은 허용하고 Deploy만 막는다. 해금 조건을 확인하려면 카드 자체를 비활성화해서는 안 되기 때문이다.
- 관리 화면과 출격 화면의 Addressables 초기화·다운로드·ID 검증·핸들 이전을 `OperatorContentLoader` 한 경로로 통합했다. 어느 화면에서 선택해도 동일한 세션 소유권 규칙을 거친다.
- `LobbyOperatorDialogueUI.RefreshOperator()`를 추가해 TitleScene 재로드 없이도 활성 오퍼레이터의 대사 세트와 로비 전신 폴백을 즉시 다시 해석한다.
- `OperatorManagementSetup`은 기존 `OperatorManagingSystem`을 보존하고 `RuntimeContent`와 공용 카드 프리팹만 반복 생성한다. 향후 로비 메뉴를 다시 생성해도 Operators 연결이 사라지지 않도록 `CommandLobbyMenuSetup`에도 `ManageOperators` 배선을 반영했다.

### 의도적으로 하지 않은 것
- 오퍼레이터별 관리 카드 프리팹이나 관리 전용 매니저를 만들지 않았다. 카탈로그 항목이 늘면 같은 View가 자동 생성된다.
- 원격 오퍼레이터의 실제 초상화를 로컬 카탈로그에 추가하지 않았다. 다운로드 전에는 기존 경량 미리보기 또는 빈 초상화가 표시되어 원격 콘텐츠가 기본 빌드에 새지 않는다.
- 실제 시스템이 없는 Details·Upgrade·오퍼레이터 레벨을 임시 구현하지 않았다. 현재 정보 패널에는 이미 영속 데이터가 있는 호감도와 해금 조건만 표시한다.

### 검증
- Unity `6000.3.13f1` 재컴파일에서 `failed=false`를 확인했다.
- `OperatorManagementSetup.Validate`로 카드 프리팹, TitleScene Controller 참조, 로비 Operators 메뉴 연결을 검증했다.
- Play Mode에서 관리 화면을 직접 열어 Cassia 카드, 호버 프레임, PREV/NEXT, 정보 패널, Back, Deploy 배치를 시각 검증했다. 생성 이후 신규 런타임/컴파일 오류는 없었다.

### 후속 보정
- 카탈로그 오퍼레이터 수와 무관하게 화면용 예약 슬롯을 항상 6개 추가했다. 현재 Cassia 1명에서는 `01/07`로 표시되며, 나머지 6장은 `OperatorPanels_Managing_2` 잠금 상태다. 예약 슬롯은 실제 Catalog/Definition을 만들지 않는다.
- 카드 원본 프레임의 장식 영역과 TMP가 겹쳐 상단 번호와 하단 이름이 잘리던 문제를 확인했다. 카드 텍스트를 프레임 안쪽 안전 영역으로 이동하고 이름 TMP 자동 축소·오버플로 설정을 적용했다.
- Play Mode에서 Cassia 1장 + 잠금 6장, `01/07 REGISTERED`, 카드 번호·상태·UNASSIGNED 텍스트가 모두 잘리지 않는 것을 재확인했다.
- `_0/_1/_2` 원본 스프라이트의 캔버스 비율이 달라 `preserveAspect`만 사용하면 상태별 카드 높이가 달라지는 문제를 확인했다. 배경 Image는 고정 카드 영역에 채우고, 해금 오퍼레이터의 호버·포커스 상태만 View Transform을 1.08배 확대하도록 분리했다.
- Play Mode에서 선택 카드만 크게 강조되고 잠금 카드 6장의 높이·하단선은 동일하게 정렬되는 것을 재확인했다.

## 2026-08-19 — Operation Addressables 초기화 멈춤 수정

### 원인과 결정
- `OperatorContentLoader`가 인자 없는 `Addressables.InitializeAsync()`를 호출한 뒤 완료 핸들의 `Status`를 읽고 있었다. Addressables가 완료 시 핸들을 자동 해제해 `Attempting to use an invalid operation handle` 예외가 발생했고, 성공·실패 콜백이 모두 실행되지 않아 선택 UI가 `콘텐츠 확인 중…`에 고정됐다.
- 초기화 호출을 `InitializeAsync(false)`로 바꿔 로더가 완료 확인까지 핸들을 소유하고, 상태를 저장한 직후 명시적으로 해제하도록 했다. 관리 화면과 출격 화면이 같은 로더를 사용하므로 두 경로에 동일하게 적용된다.

### 검증
- Unity `6000.3.13f1` 재컴파일에서 `failed=false`를 확인했다.
- TitleScene Play Mode에서 Operation → Cassia 확정을 실행해 `DefenseScene` 전환과 `OperatorLoadoutSession.SelectedDefinition.operatorId == "cassia"`를 확인했다.

## 2026-08-19 — 모드 선택·CH1 스테이지 맵 UGUI 프로토타입

### 결정
- 오퍼레이터 콘텐츠 로딩이 끝난 뒤 바로 DefenseScene으로 이동하던 경로를 모드 선택으로 한 단계 분리했다. `ENDLESS MODE`는 기존 절차적 웨이브 레거시 진입을 유지하고, `STAGE MODE`는 챕터 맵 UGUI로 이동한다.
- `StageCatalog`와 `StageCatalogEntry`는 실제 웨이브 Definition과 분리된 가벼운 메타데이터로 두었다. CH1의 1-1~1-5 노드, 제목·설명·잠금 기준만 먼저 표시해 스테이지 아트와 전투 데이터가 없어도 UI 작업을 진행할 수 있게 했다.
- `StageSelectionUI`는 노드 선택·잠금·상세 패널까지 제공한다. 실제 StageDefinition과 유한 웨이브 공급자가 아직 없으므로 출격 버튼은 비활성화해 스테이지 선택이 현행 엔드리스로 잘못 시작되지 않게 했다.
- TitleScene에 배치된 사용자 아트는 수정하지 않고 `ModeSelectionSystem`·`StageSelectionSystem`과 생성 프리팹만 Editor API로 추가했다.

### 의도적으로 하지 않은 것
- 이번 단계에서는 `WaveManager`와 `GameResultUI`를 수정하지 않았다. 다음 단계에서 스테이지 세션·유한 웨이브·승리 결과를 연결한다.
- `PlayerProfile`에 스테이지 클리어 기록을 추가하지 않았다. 현재 잠금은 UI 프로토타입용 `requiredBestWave` 기준이며, 전투 연결 시 `clearedStageIds` 기반으로 교체한다.

### 검증
- Unity `6000.3.13f1` 재컴파일에서 `failed=false`를 확인했다.
- `RCCom/Stages/Build Mode and Chapter UI` 실행 후 `Validate Mode and Chapter UI`가 통과했다.
- Play Mode에서 `Operation → Cassia → 모드 선택 → CH1 맵` 흐름과 1-1 선택·잠금 노드를 확인했다.
- `ENDLESS MODE` 선택 시 기존 `DefenseScene`으로 진입하는 회귀 경로를 확인했다.

## 2026-08-19 — CH1 유한 웨이브 런타임 연결

### 결정
- `StageDefinition`을 `StageCatalogEntry`가 직접 참조하도록 연결했다. 이번 제출 범위에서는 로컬 에셋으로 즉시 실행하고, 이후 원격 스테이지 콘텐츠가 필요해지면 이 참조를 Addressables 키 메타데이터로 바꿀 수 있게 카탈로그 메타와 웨이브 데이터를 분리했다.
- `StageWaveDefinition`/`StageEnemySpawn`은 기존 `EnemyDefinition`을 재사용하는 편성 데이터만 소유한다. `WaveManager`는 스테이지 모드에서 이 편성을 순서대로 큐에 넣고, 엔드리스 모드에서는 기존 예산 기반 `BuildSpawnQueue`를 그대로 사용한다.
- 별도 `StageManager`를 만들지 않았다. 스테이지 웨이브의 실행 순서와 적 리스트는 기존 게임 흐름 매니저인 `WaveManager`가 맡고, 모드 전달만 `BattleSession`이 담당한다. 오브젝트 타입별 매니저를 늘리지 않는 아키텍처 규칙을 유지하기 위한 선택이다.
- `GameManager`에는 기존 패배 전용 `GameOver` 이벤트를 호환용으로 남기고, 승리·패배를 함께 전달하는 `BattleEnded(BattleOutcome)`를 추가했다. 결과 화면은 통합 이벤트를 구독하므로 스테이지 승리도 기존 Mission Result 패널에서 처리된다.
- 기존 결과 카드에 `StageOutcomeTitle` TMP를 추가하고 `GameResultUI.resultTitleText`에 연결했다. 기존 통계·Retry·Title 배치는 건드리지 않고 `MISSION CLEAR`/`MISSION FAILED`만 런타임 결과에 따라 교체한다.
- CH1 샘플 5개는 각각 3개 유한 웨이브를 가지며, 현재 잠금은 기존 `requiredBestWave` 기준을 유지한다. 첫 스테이지 승리 후 도달 웨이브가 기록되어 다음 노드가 열리는 구조이며, 명시적 `clearedStageIds` 저장은 실제 분기형 해금 규칙이 필요해질 때 도입한다.

### 검증
- Unity `6000.3.13f1` 재컴파일에서 `failed=false`를 확인했다.
- `RCCom/Stages/Build Mode and Chapter UI`를 다시 실행해 `Assets/Data/Stages/CH1/ch1-01~05.asset`과 카탈로그 참조를 Editor API로 생성·저장했고, UI Validator가 통과했다.
- Play Mode에서 CH1-01을 `BattleSession`으로 선택해 `DefenseScene`에 진입한 뒤, `WaveManager.CurrentWave`가 1→3으로 진행되고 마지막에 `GameManager.Outcome == Victory`, `IsGameOver == true`가 되는 것을 확인했다. 종료 로그가 반복되던 문제는 `_stageCompleted` 가드와 전투 종료 시 `WaveManager.Update` 조기 반환으로 수정했다.
- `BattleSession.SelectEndless()`로 별도 실행해 엔드리스 경로에서 `WaveManager.IsStageMode == false`, `DefenseScene` 진입이 유지되는 것을 확인했다.
- `BuildStageResultOutcomeTitle` eval을 실행해 DefenseScene 결과 카드의 제목 참조가 저장된 것을 확인했다.

### 모드 선택 버튼 아트 배선 보정
- 가져온 `StageSelectionUISpriteSheet`의 Normal/Hover 조각을 모드 선택 화면의 Back·Stage·Endless 버튼에 `SpriteSwap`으로 연결했다.
- 버튼 문구가 아트에 포함되어 있으므로 생성 당시의 TMP Label은 비활성화했다. 생성 도구와 검증기에 같은 스프라이트 이름 계약을 넣어 UI를 재생성해도 수동 배선이 사라지지 않게 했다.
- 최초 배선은 EventSystem 기본 포커스에도 Hover 조각을 사용해 Stage가 상시 호버처럼 보였고, 글로우 여백이 더 큰 Hover 조각이 같은 Rect 안에 맞춰지며 본체가 작아 보였다. Selected는 Normal 조각으로 분리하고, 기존 `UIHoverScale`을 각 조각의 최대 크기 비율만큼 설정해 실제 포인터 호버에서만 외곽 효과가 자연스럽게 확장되도록 보정했다.

## 2026-08-21 — 지휘 포인트 검증기의 UI 표시 계약 동기화

### 맥락
- 지휘 포인트의 보유·소비·부족 거부·자동 회복·상한 로직은 이미 구현되어 있었지만, `UnitDeployButton`이 비용을 `3 CP`로 표시하도록 바뀐 뒤 기반 검증기는 과거 표시인 `3`을 계속 기대해 지휘 포인트 검증 단계 전에 실패했다.

### 결정
- 런타임 또는 UI 표시를 되돌리지 않고 `AllyUnitFoundationVerifier`의 기대값을 현재 비용 표시 계약과 맞췄다. 단위가 없는 숫자보다 `CP`가 붙은 표시가 별도 자원이라는 의미를 명확히 전달하기 때문이다.

### 의도적으로 하지 않은 것
- 지휘 포인트 수치, 회복 속도, 소비 순서와 DefenseScene 배선은 변경하지 않았다.

### 검증
- Unity `6000.3.13f1` 재컴파일에서 `failed=false`를 확인했다.
- `AllyUnitFoundationVerifier`로 지급, 비용 확인, 소비, 부족 시 거부, 전투 중 회복, 빌드 페이즈 회복 차단과 최대 100 상한 계약을 통과했다.

## 2026-08-21 — 유닛 배치 일시정지 게이트 검증 보강

### 맥락
- `UnitDeployController`는 `Time.timeScale` 기반 게이트로 배치 선택·소환과 지휘 포인트 회복을 이미 차단했지만, 기반 검증기는 빌드 페이즈 차단만 확인하고 일시정지 경로를 직접 검증하지 않았다.

### 결정
- 런타임 분기를 중복 추가하지 않고 `AllyUnitFoundationVerifier`에서 `Time.timeScale = 0`일 때 배치 입력이 거부되고 같은 Tick 시간만큼 지휘 포인트가 증가하지 않는지 함께 검사한다.
- 검증 종료 시 성공·실패와 무관하게 기존 `Time.timeScale`을 복원해 에디터 세션 상태를 남기지 않는다.

### 의도적으로 하지 않은 것
- UI 패널을 숨기거나 게임의 전역 일시정지 소유자를 추가하지 않았다. 입력의 최종 경계인 Controller가 요청을 거부하므로 기존 UI→게임플레이 단방향 구조를 유지했다.

### 검증
- Unity `6000.3.13f1` 재컴파일에서 `failed=false`를 확인했다.
- `AllyUnitFoundationVerifier`에서 일시정지 중 `IsDeployInputEnabled == false`, 유닛 선택 거부와 1초 Tick 후에도 CP 98 유지, 검증 종료 후 기존 시간 배율 복원을 확인했다.

## 2026-08-21 — 유닛별 비용 부족 버튼 비활성

### 맥락
- 별도의 소환 버튼은 선택한 유닛의 비용이 부족하면 비활성화됐지만, 로스터의 유닛 선택 버튼은 잔액과 관계없이 계속 눌려 실제 배치 가능 여부를 즉시 알기 어려웠다.

### 결정
- `UnitDeployButton`은 외부에서 전달받은 비용 충족 여부만 `Button.interactable`에 반영하고, 비용 계산은 소유하지 않는다.
- `UnitDeployMenuUI`가 버튼 생성 직후와 `CommandPointsChanged` 이벤트마다 각 Definition을 `UnitDeployController.CanAfford`로 판정한다. 이로써 UI와 실제 소비 조건이 같은 경계를 사용하며, 소비와 회복 모두 즉시 버튼 상태에 반영된다.

### 의도적으로 하지 않은 것
- 비용 부족 유닛의 Definition을 로스터에서 숨기거나 현재 선택을 강제로 해제하지 않았다. 잔액이 회복되면 같은 버튼이 다시 활성화되고, 실제 소환은 기존 Controller 검사를 계속 통과해야 한다.

### 검증
- `AllyUnitFoundationVerifier`에 0 CP에서 비용 3 CP 버튼 비활성, 5 CP 지급 후 활성, 3 CP 소비 후 잔액 2에서 재비활성 계약을 추가했다.

## 2026-08-21 — 타워 설치·아군 배치 입력 모드 분리

### 맥락
- 타워를 선택한 뒤 유닛 배치 UI를 조작해도 기존 타워 선택이 남아 있어, 같은 포인터 입력이 타워 설치와 유닛 배치 양쪽에 해석될 수 있었다.
- 특히 UI 버튼의 선택 콜백은 포인터를 놓을 때 실행되지만 `TowerBuildController`는 누르는 프레임에 월드 클릭을 처리하므로, 모드 전환만으로는 UI 뒤 슬롯에 타워가 먼저 설치되는 경합을 막을 수 없다.

### 결정
- 씬 범위의 일반 `DeploymentInputModeController`와 `DeploymentInputMode`를 추가해 `TowerBuild`와 `AllyUnitDeploy`를 상호 배타 상태로 조율한다. 새 Manager나 static 전역 상태는 만들지 않았다.
- 타워를 선택하면 유닛 선택을 해제하고, 유닛을 선택하면 타워 선택과 프리뷰 사거리를 해제한다. 각 Controller는 모드 변경 이벤트를 구독할 뿐 서로를 직접 참조하지 않는다.
- `TowerBuildController`는 `EventSystem.IsPointerOverGameObject()`인 포인터를 월드 설치·철거·조회 입력으로 처리하지 않는다. 모드 전환보다 먼저 발생하는 UI 클릭 프레임까지 차단하기 위한 별도 경계다.
- `AllyUnitVerticalSliceBuilder`가 공용 입력 모드 Controller를 DefenseScene에 하나만 만들고 기존 타워·유닛 Controller 양쪽에 연결하도록 자동 배선을 확장했다.
- 현재 DefenseScene을 저장하면 사용 중인 Unity가 입력 모드 외의 씬·프로젝트 설정까지 대량 재직렬화하므로, 런타임에서는 두 Controller의 `Awake`가 기존 입력 모드 Controller를 찾고 없으면 하나만 생성하는 안전한 폴백을 사용한다. 참조는 static으로 보관하지 않아 Retry 씬마다 새 상태가 만들어진다.

### 의도적으로 하지 않은 것
- 타워와 유닛 UI를 서로 직접 참조시키거나 어느 한 Controller가 다른 Controller를 소유하게 하지 않았다.
- 플레이어 이동·공격, 타워 철거 규칙, 유닛 생성 규칙은 입력 모드의 대상이 아니므로 변경하지 않았다.
- 입력 모드 한 필드 때문에 DefenseScene 전체 재직렬화 결과를 커밋하지 않았다. 명시적 씬 배치가 필요할 때는 전용 에디터 메뉴로 같은 참조만 재현할 수 있다.

### 검증
- `AllyUnitFoundationVerifier`에 유닛 선택 → 타워 선택 시 유닛 해제, 타워 선택 → 유닛 선택 시 타워 해제, 선택 해제 시 `None` 복귀 계약을 추가했다.
- 수직 슬라이스 검증기가 DefenseScene의 입력 모드 Controller 단일 인스턴스와 타워·유닛 양쪽의 직렬화 참조를 검사하도록 확장했다.
- Unity `6000.3.13f1` 재컴파일에서 `failed=false`를 확인했다.
- 실제 DefenseScene Play Mode에서 입력 모드 Controller가 정확히 1개 생성되고 두 Controller가 같은 인스턴스를 공유하며, 타워 → 유닛 → 타워 전환 때 반대쪽 선택이 해제되는 것을 `Tools/eval/VerifyDeploymentInputMode.cs`로 확인했다.

## 2026-08-21 — 아군 유닛 에셋 등록·참조 검증 보강

### 맥락
- `OperatorAssetValidator`는 연결된 `AllyUnitRoster` 내부의 null Definition, 빈 Data/ID와 중복 ID를 이미 오류로 검사했지만, 프로젝트에 존재하면서 어느 Roster에도 들어가지 않은 `AllyUnitDefinition`은 찾지 않았다.
- `AllyUnitInstance`는 Definition의 효과 목록을 직접 순회하므로 목록 또는 항목이 null이면 스폰·전투 Tick에서 예외가 발생하지만 에디터 검증 단계에서 이를 차단하지 못했다.

### 결정
- 모든 `AllyUnitDefinition`과 모든 `AllyUnitRoster.units`를 대조해 미등록 Definition을 경고한다. 기존 Tower/Enemy 검증과 마찬가지로 실험·삭제 예정 에셋 가능성을 보존하기 위해 오류로 빌드를 막지는 않는다.
- 프로젝트의 모든 AllyUnitRoster를 오퍼레이터 연결 여부와 무관하게 한 번씩 검사한다. null 항목, 빈 Data/unitId, 한 Roster 안의 중복 unitId는 오류로 유지하고, 효과 목록 자체 또는 효과 항목의 null도 오류에 포함한다.

### 의도적으로 하지 않은 것
- 서로 다른 오퍼레이터 Roster에서 같은 Definition을 공유하는 것은 유효한 데이터 조립이므로 중복 등록으로 취급하지 않았다.
- 미등록 Definition을 자동 삭제하거나 임의의 Roster에 자동 편입하지 않았다.

### 검증
- `RCCom/Operators/Validate Operator Assets` 단일 메뉴에서 오퍼레이터 필수 참조와 Tower/Enemy/AllyUnit 등록 상태를 함께 검사하도록 통합했다.
- Unity `6000.3.13f1` 재컴파일에서 `failed=false`를 확인했고, 실제 프로젝트 전체 에셋 검증도 오류 없이 통과했다.
- 기존 `Assets/Data/Definition/Tower/축적.asset` 미등록 경고 1건은 그대로이며, AllyUnit 미등록·null·중복 ID 오류는 발견되지 않았다.

## 2026-08-21 — 사망 아군 활성 목록 제거 회귀 검증

### 맥락
- `UnitDeployController.RegisterInstance`는 소환한 Instance의 `Died` 이벤트에서 `_activeUnits`와 사망 핸들러를 제거하고 `UnitRemoved`를 알리고 있었지만, 기반 검증기는 개별 Instance의 사망 이벤트만 확인했다.

### 결정
- 런타임 제거 경로를 중복 구현하지 않고 `AllyUnitFoundationVerifier`가 메모리 Instance를 Controller에 등록한 뒤 사망시켜 `ActiveUnits`가 즉시 비워지고 정확한 Instance로 `UnitRemoved`가 한 번 발생하는지 검사한다.
- 다음 Tick까지 죽은 참조를 남기지 않는 것을 계약으로 삼아 다른 아군의 타깃 후보 목록에 사망 유닛이 섞이는 것을 방지한다.

### 검증
- Unity `6000.3.13f1` 재컴파일에서 `failed=false`를 확인했고, `AllyUnitFoundationVerifier` 실행으로 소환 등록 1건, 사망 후 활성 목록 0건, 제거 이벤트 1건 계약을 확인했다.

## 2026-08-22 — Stage Studio와 StageDefinition 제작 원본화

### 맥락
- CH1 샘플 웨이브는 에디터 생성 코드의 고정값이어서 적 종류·웨이브 수·설명·보상을 사람이 안전하게 반복 편집할 경로가 없었다.
- 기존 UI 생성 메뉴를 다시 실행하면 샘플 StageDefinition을 초기값으로 덮어써 이후 콘텐츠 작업이 유실될 위험이 있었다.

### 결정
- `StageDefinition` SO를 스테이지 제작 원본으로 확정했다. 챕터·표시명·부제·추천 레벨·순서·해금 조건·설명 배경·보상 매니페스트·웨이브 편성을 한 에셋이 소유한다.
- `StageStudioWindow`를 `RCCom/Stages/Open Stage Studio`에 추가하고 Identity/Waves/Rewards/Publish 탭에서 위 데이터를 편집하도록 했다. 스테이지는 Sprite와 EnemyDefinition 참조가 중심이므로 Operator Studio의 JSON 레시피를 복제하지 않고 SO를 직접 편집한다.
- `StageCatalogBuilder`가 Definition 전체를 정렬해 선택 화면용 `StageCatalog`를 생성한다. 카탈로그는 생성물이며 직접 편집하지 않는다.
- `StageAssetValidator`가 ID 중복, 웨이브·적 누락, 잘못된 수량·시간·체력 배율, 보상 ID·수량을 오류로 검사하고 설명 배경·보상 미작성은 경고로 남긴다.
- 선택 화면의 Description 영역에 스테이지별 배경 Sprite와 추천 레벨을 표시하도록 연결했다.
- 기존 UI 생성 도구는 StageDefinition이 이미 있으면 즉시 재사용해 Studio에서 편집한 원본을 덮어쓰지 않는다.

### 의도적으로 하지 않은 것
- 보상 지급 로직은 추가하지 않았다. 현재 PlayerProfile에는 계정 재화·인벤토리 계약이 없으므로 임의 저장 구조를 만들지 않고 `rewardId + 표시명 + 아이콘 + 수량` 매니페스트까지만 정의했다.
- 스테이지별 C# 클래스나 별도 StageManager를 만들지 않았다. 실행은 기존 WaveManager와 StageDefinition 데이터 조립을 그대로 사용한다.

### 검증
- 기존 CH1 5개를 새 스키마로 마이그레이션했으며 웨이브 편성과 카탈로그 참조가 유지됐다.
- Stage Validator 결과 오류 0개를 확인했다. 설명 배경·보상이 아직 비어 있는 5개 스테이지에는 의도된 경고 10개가 남는다.

## 2026-08-22 — 스테이지 노드 아트 배선

- `StageSelectionsSmallPanel`의 분리 스프라이트를 공용 `StageNode` 프리팹에 연결했다. `_0`은 해금된 일반 상태, `_1`은 현재 선택 상태, `_2`는 잠금 상태로 사용한다.
- 선택 스프라이트는 발광 외곽 때문에 원본 크기가 더 크다. 가로 레이아웃이 선택할 때마다 흔들리지 않도록 버튼의 레이아웃 크기는 고정하고, 자식 `PanelVisual`을 교체한 뒤 선택 노드 전체를 1.15배 확대해 외곽과 TMP가 함께 칸 밖으로 확장되게 했다.
- 체크 배지와 선택 화살표는 각 카드 본체 스프라이트에 이미 포함되어 있으므로 `_3`·`_4`를 중복 배치하지 않았다. 스테이지 번호·부제만 TMP로 유지해 동일 프리팹을 모든 스테이지 데이터에 재사용한다.
- 화면을 열 때 첫 해금 노드가 아니라 가장 뒤의 해금 노드를 기본 선택한다. 선형 CH1에서 이전 노드는 체크 카드, 다음 진행 지점은 `CURRENT` 카드로 보이게 하려는 결정이다.

## 2026-08-22 — 5개 초과 스테이지 가로 탐색

- 스테이지 노드 영역을 가로 `ScrollRect`와 5개 표시 viewport로 바꾸고 좌·우 버튼을 추가했다. Stage Studio에서 Definition을 추가하고 카탈로그를 재생성하면 여섯 번째 이후 노드도 별도 화면 코드 없이 같은 Content에 붙는다.
- 좌·우 버튼은 노드 한 칸 단위로 이동하며, 노드가 5개 이하면 숨긴다. 현재 선택 노드가 여섯 번째 이후라면 화면을 열 때 해당 노드가 보이는 위치로 자동 이동한다.
- 스크롤 검증용으로 `ch1-06`과 `ch1-07` StageDefinition을 생성 대상으로 추가했다. 기존 1-1~1-5와 마찬가지로 최초 생성 뒤에는 Stage Studio 편집 내용을 자동 빌더가 덮어쓰지 않는다.
- TitleScene UI 재생성 및 Inspector 배선 검증을 통과했다.

## 2026-08-22 — 아군 스폰 순서 전열과 기지 말단 중첩 예외

### 맥락
- 기존 아군은 적과의 접촉선만 계산해 여러 유닛이 같은 전방 적에게 접근하면 한 위치에 겹쳐 정지했다.
- 기지 바로 앞까지 적이 진입하면 모든 아군이 같은 경로 끝점에서 생성될 수 있으므로, 이미 생긴 중첩을 강제로 밀어내는 방식은 경로 밖 이동이나 배치 실패를 만들 수 있었다.

### 결정
- `UnitDeployController`의 `_activeUnits` 등록 순서를 스폰 순서 계약으로 사용하고, 먼저 스폰된 유닛부터 Tick하도록 순회를 정방향으로 바꿨다. 선두가 먼저 이동해야 같은 프레임에 열린 공간을 후속 유닛이 즉시 채울 수 있다.
- 각 `AllyUnitInstance`는 활성 목록에서 자신 바로 앞의 살아 있는 아군만 찾아, 기존 접촉선 sweep 계산으로 그 아군의 `contactRange` 경계까지만 이동한다. 별도 충돌체나 물리 밀어내기를 추가하지 않아 순수 C# 인스턴스와 View 분리 원칙을 유지한다.
- 이미 `contactRange` 안에 있는 후속 유닛은 뒤로 보정하지 않고 해당 Tick의 전진만 막는다. 따라서 적이 기지 말단을 막았을 때 스폰 중첩은 허용되고, 공간이 열리면 선두부터 이동한 뒤 후속 유닛이 생성 순서대로 간격을 회복한다.
- Tick 중 사망 이벤트가 활성 목록을 수정해도 정방향 순회가 유닛을 건너뛰지 않도록, 현재 인덱스에 같은 인스턴스가 남아 있을 때만 인덱스를 증가시킨다.

### 의도적으로 하지 않은 것
- 적 이동·타기팅·교전 로직은 수정하지 않았다. 이번 문제는 아군 목록의 이동 순서와 아군 간 전진 제한만으로 닫힌다.
- `Rigidbody2D` 충돌, 위치 밀어내기, `AllyUnitManager`, 별도 대기 상태는 추가하지 않았다. 밀어내기는 기지 말단 중첩 예외와 충돌하고, 대기 중에도 아군은 적 공격·효과 Tick을 계속 수행해야 하기 때문이다.
- 모든 선행 아군을 장애물로 보지 않고 직전 선행 유닛만 사용했다. 스폰 순서상 추월을 막는 데 충분하며, 굽거나 교차하는 경로에서 다른 전열을 불필요하게 차단하는 후보 수를 최소화한다.

### 검증
- Unity `6000.3.13f1` Pipeline 재컴파일에서 `failed=false`를 확인했다.
- `AllyUnitCombatVerifier`에 선두 적 조우 후 3체 contactRange 대열, 기지 말단 3체 중첩 허용, 적 사망 후 스폰 순서 재진격과 간격 회복을 추가했다. 기존 시나리오를 포함한 21개 검증이 모두 통과했다.
- `AllyUnitFoundationVerifier`가 과거 비용 표기 `3`을 기대해 현재 버튼 표기 `3 CP`와 불일치하던 검증기 자체의 회귀를 바로잡았고, 전체 기반 계약이 통과하는 것을 확인했다.
## 2026-08-22 — 오퍼레이터 관리 카드 호버 중심·텍스트 안전영역 보정

### 증상과 원인
- 관리 카드가 호버/포커스될 때 루트 Transform 전체를 1.08배 확대하고 있어 프레임뿐 아니라 초상화와 TMP까지 함께 움직였다. 상태별 프레임 원본의 캔버스 비율도 서로 달라, 실제 좌표는 동일해도 호버 전후 초상화의 시각 중심이 프레임과 어긋나 보였다.
- 상단 번호·상태와 하단 이름·호감도는 프레임 장식선 가까이에 배치돼 작은 카드 폭에서 정보 계층이 약하고 읽기 여유가 부족했다.

### 결정
- 카드 루트와 동적 콘텐츠는 고정 크기로 유지하고, 상태 스프라이트를 표시하는 `Frame` 자식만 호버 시 1.08배 확대한다. 따라서 초상화와 텍스트의 중심 좌표는 일반/호버 상태에서 변하지 않는다.
- 번호·상태는 좌상단 한 묶음으로 간격을 정리하고, 이름·호감도는 하단 안전영역의 같은 시작선에 맞췄다. 이름은 최대 22pt 자동 축소로 두어 짧은 이름은 더 또렷하게 보이면서 긴 이름도 프레임을 넘지 않게 했다.

### 의도적으로 하지 않은 것
- 초상화 Sprite 자체의 피벗·Import 설정은 건드리지 않았다. 문제는 오퍼레이터별 아트 데이터가 아니라 호버 시 카드 전체 Transform을 확대하던 UI 계층 책임에 있었기 때문이다.
- 상태별로 초상화 위치 보정값을 따로 두지 않았다. 프레임 상태마다 콘텐츠 위치를 하드코딩하면 신규 카드 아트 교체 때 다시 튜닝이 필요하므로, 프레임 강조와 콘텐츠 좌표를 구조적으로 분리했다.

## 2026-08-23 — 실비아 신규 아트·대화 에셋 등록

### 맥락
- 신규 오퍼레이터 실비아의 로비·호감도 연출을 기존 로드아웃 데이터와 분리된 콘텐츠 에셋으로 저장해야 했다.
- 표시 이름과 저장·Addressables 식별자는 분리해야 하므로 이번 레시피의 내부 ID는 `racing`, 표시 이름은 `실비아`로 유지했다.

### 결정
- `Assets/Art/Character Standing Arts/실비아/` 아래에 표정별 전신 PNG 27개와 Unity가 생성한 대응 `.meta`를 등록했다. 폴더 메타도 함께 보존해 다른 환경에서 GUID 참조가 유지되게 했다.
- `Assets/Data/Operators/racing/OperatorDialogueSet.asset`을 실비아 전용 대화 원본으로 추가하고, `Assets/Editor/OperatorRecipes/Racing.json`을 제작 레시피로 추가했다.
- 이번 작업은 아트·대화·레시피 데이터만 커밋하며, 기존 전투 코드와 씬·프로젝트 설정은 변경하지 않았다.

### 의도적으로 하지 않은 것
- 레시피에 아직 연결되지 않은 타워·카드·아군 로스터와 선택 화면 초상화 경로를 기존 에셋으로 추측해 채우지 않았다. 해당 로드아웃 구성은 별도 콘텐츠 결정과 함께 연결한다.
- 실비아 에셋과 무관한 기존 작업 트리의 렌더 텍스처·프로젝트 설정·UI 프리팹 수정은 이 커밋에 포함하지 않았다.

### 검증
- 실비아 PNG 27개 모두 대응 `.meta`가 존재하고, 실비아 대화 SO의 Sprite GUID 참조가 같은 아트 폴더의 메타와 일치하는지 확인했다.
- Unity `6000.3.13f1` Pipeline에서 `recompile` 결과 `up_to_date`를 확인했고, 콘솔에 신규 C# 컴파일 오류가 없었다.

## 2026-08-23 — 실비아 포트릿 아트 등록

### 맥락
- 전신 아트는 로비와 호감도 연출에 적합하지만, 대화·전투 상황에서 사용하는 작은 포트릿은 화면 안전영역과 표정 가독성에 맞춘 별도 크롭이 필요했다.

### 결정
- `Assets/Art/Character Standing Arts/실비아/portrait/` 아래에 기존 표정 이름과 대응하는 Chibby 포트릿 PNG 27개를 추가했다. 전신 Sprite와 포트릿 Sprite를 같은 파일로 재사용하지 않아 각 UI의 표시 비율을 독립적으로 조정할 수 있게 했다.
- Unity가 생성한 포트릿 `.meta`를 PNG마다 함께 보존했으며, 이번 커밋에서는 실제 대화 슬롯 연결은 변경하지 않고 콘텐츠 에셋 등록만 완료했다.

### 의도적으로 하지 않은 것
- `OperatorDialogueSet`의 `portraitSprite`와 레시피의 선택 초상화 경로를 임의로 연결하지 않았다. 표정별 상황 매핑과 선택 화면 대표 포트릿은 별도 콘텐츠 배선 작업에서 결정한다.

### 검증
- 포트릿 PNG 27개와 대응 `.meta` 27개의 쌍을 확인했다.
- Unity `6000.3.13f1` Pipeline에서 `recompile` 결과 `up_to_date`를 확인했고, 콘솔에 신규 C# 컴파일 오류가 없었다.

## 2026-08-23 — 실비아 레시피·전투 대사 연결

### 맥락
- 포트릿 에셋이 추가됐지만 `Racing.json`은 표시 설명과 선택 화면 대표 초상화를 비워 두고 있었고, 실비아 전용 대화 SO의 전투 상황 슬롯도 비어 있었다.

### 결정
- `Racing.json`의 플레이 스타일 설명과 선택 초상화 경로를 실비아 콘텐츠로 채웠다. 내부 ID `racing`은 유지해 저장 데이터와 Addressables 주소가 변하지 않게 했다.
- 기존 `OperatorDialogueSet` 하나에 실비아의 기본 포트릿과 출격·스킬·기지 피격·플레이어 피격·자금 부족·슬롯 부족·사망·기지 파괴 대사를 연결했다. 공용 대화 시스템을 재사용해 오퍼레이터별 런타임 클래스를 추가하지 않았다.
- 대화 SO에서 사용하는 23개 Sprite GUID는 실비아 전신·포트릿 에셋으로 해석되도록 유지했다.

### 의도적으로 하지 않은 것
- `sourceTowerRosterPath`, `sourceCardRosterPath`, `sourceAllyUnitRosterPath`는 아직 비워 두었다. 실비아 전투 로드아웃을 기존 로스터로 추측해 연결하면 신규 캐릭터의 실제 콘텐츠 결정이 데이터에 고정되므로, 로스터 확정 작업에서 별도로 연결한다.
- 로비 대사 문장과 로비 전신 Sprite 배선은 이번 전투 대사 커밋에서 변경하지 않았다.

### 검증
- 레시피의 선택 초상화와 대화 SO 경로가 실제 파일로 존재하고, 실비아 대화 SO의 Sprite GUID 23개가 누락 없이 해석되는 것을 확인했다.
- Unity `6000.3.13f1` Pipeline에서 `recompile` 결과 `up_to_date`를 확인했다.

## 2026-08-23 — 실비아 Chibby 포트릿 재임포트·참조 복구

### 맥락
- Chibby PNG를 440x440으로 수정한 뒤에도 기존 `.meta`의 단일 슬라이스 rect가 600x600(일부는 x=2, 598x600)으로 남아 있었다. Unity는 텍스처 자체는 읽었지만 rect가 실제 이미지 범위를 벗어나 `Sprite` 서브에셋을 생성하지 않았고, 그 결과 기존 GUID 참조가 인스펙터에서 끊긴 것처럼 보였다.

### 결정
- `.meta`를 직접 편집하지 않고 Unity `ISpriteEditorDataProvider`를 통해 Chibby 27개 파일의 기존 단일 슬라이스를 실제 텍스처 전체(440x440)로 재임포트했다. 사용자가 수정한 중앙 피벗은 유지하고, 재임포트 과정에서 생긴 불필요한 루트 Sprite ID는 기존 포트릿과 같은 빈 상태로 정리했다.
- 기존 표정 매핑을 보존해 `giggling → gameStart`, `evil smile → skillUsed`, `angry-1 → baseAttacked`, `confused → playerHit`, `depressed → playerHitCritical`, `annoyed → insufficientGold`, `disgusted → slotUnavailable`, `crying with eyes open → playerDied`, `crying with eyes closed → baseDestroyed`를 다시 연결했다. 선택 화면 대표 포트릿은 `default-1`로 `OperatorDefinition`과 로컬 카탈로그 미리보기에 연결했다.

### 의도적으로 하지 않은 것
- 로비 대사 엔트리의 숨김 `portraitSprite` 필드는 런타임 전투 포트릿 계약과 무관하고 기존에도 비어 있었으므로 채우지 않았다.
- 실비아 전투 코드, 씬, 공용 대화 시스템은 변경하지 않았다. 이번 문제는 아트 importer의 rect와 기존 데이터 참조 해석만으로 닫힌다.

### 검증
- Unity `6000.3.13f1` Pipeline에서 Chibby 27개 모두 `Sprite` 서브에셋 1개로 로드되고, 9개 전투 상황·선택 포트릿·카탈로그 미리보기 매핑 검증을 통과했다.
- `recompile` 결과 `up_to_date`, `recompile_status` 결과 `failed=false`, `errors=[]`를 확인했다.

## 2026-08-23 — 실비아 레시피 로드아웃·대사 마감

### 맥락
- 실비아의 표시 정보와 대화 연결은 완료됐지만, 이전 레시피에는 타워·카드·아군 유닛 원본 풀이 비어 있어 `OperatorAssetBuilder`가 런타임 로드아웃을 생성할 수 없었다.

### 결정
- `Racing.json`의 영구 ID `racing`은 유지하고, 선택 화면 대표 이미지는 실비아 전신 `default-1` Sprite로 연결했다. 저장 데이터와 Addressables 주소가 바뀌지 않게 하기 위해 표시 이름과 내부 ID를 분리했다.
- 현재 검증 가능한 기존 타워·카드·아군 유닛 로스터를 레시피의 원본 풀로 지정했다. 오퍼레이터별 복제본은 빌더가 생성하도록 해 레시피가 Definition과 Roster의 단일 원본이 되게 했다.
- 생성된 실비아 Definition/Roster, 로컬 카탈로그 항목, 오퍼레이터 전용 Addressables 그룹을 함께 갱신했다. 레시피만 커밋하고 생성 에셋을 누락하면 새 오퍼레이터를 바로 로드할 수 없기 때문이다.
- 실비아의 로비·전투 상황 슬롯에 실제 대사와 전신 Sprite를 보강했으며, 기존 공용 대화 시스템과 효과 코드는 수정하지 않았다.

### 의도적으로 하지 않은 것
- 실비아 전용 타워·카드·아군 유닛 Definition이나 런타임 클래스를 새로 만들지 않았다. 이번 단계는 기존 데이터 풀을 조립하는 레시피 검증에 집중한다.
- Unity 플레이어 빌드 결과물과 Addressables 콘텐츠 번들, Pipeline 로컬 설정은 커밋 대상에서 제외했다. 저장소에 필요한 것은 재생성 가능한 레시피와 런타임이 참조하는 프로젝트 에셋뿐이다.

### 검증
- `Racing.json`이 UTF-8 JSON으로 파싱되고, 선택 초상화·대화·로스터 원본 경로가 실제 파일로 존재하는지 확인했다.
- 실비아 생성 Definition의 타워·카드·아군 유닛·대화 참조와 카탈로그/Addressables 그룹 참조가 모두 유효한지 확인했다.
- Unity `6000.3.13f1` Pipeline에서 `recompile` 결과 `up_to_date`, `recompile_status` 결과 `failed=false`, `errors=[]`를 확인했다. Validator도 실비아 대사 엔트리 오류 없이 완료됐으며, 기존 공용 Cassia 대사 빈 슬롯 경고만 남았다.
## 2026-08-22 — 오퍼레이터 관리 카드 상태별 프리팹 분리

### 맥락
- Normal/Hover 프레임의 원본 캔버스 비율과 실제 인물 창 위치가 달라, 하나의 RectTransform을 공유한 채 Sprite만 교체하면 포트레잇과 TMP를 상태별로 눈대중 보정하기 어려웠다.

### 결정
- 논리 카드 `OperatorManagementCard.prefab`은 클릭·포커스·데이터만 소유하고, 시각 표현을 `OperatorManagementCard_Normal.prefab`, `_Hover.prefab`, `_Locked.prefab` 3개로 분리한다.
- 각 Visual 프리팹은 PortraitViewport/Portrait와 번호·상태·이름·호감도·ACTIVE 배지를 독립적으로 소유한다. 따라서 Normal과 Hover에서 포트레잇 X/Y/Scale 및 텍스트 위치를 서로 다르게 수동 조정할 수 있다.
- 관리 화면 생성기는 Visual 프리팹이 이미 존재하면 검증만 하고 덮어쓰지 않는다. 기존 단일 카드 프리팹은 새 Visual 참조가 없을 때 한 번만 분리형 호스트로 재생성한다.
- 런타임은 호버 시 프리팹을 새로 Instantiate하지 않고 카드 안에 중첩된 세 Visual 인스턴스의 활성 상태만 전환한다.

### 의도적으로 하지 않은 것
- Normal/Hover 카드 전체를 별도 인터랙션 프리팹으로 교체하지 않았다. Button/EventSystem 상태와 카탈로그 데이터는 논리 카드 하나가 계속 소유해 포커스와 클릭 상태가 끊기지 않게 했다.
- 상태별 수동 오프셋을 코드 상수로 고정하지 않았다. 이후 아트 교체 시 Unity 프리팹에서 직접 조정하고 생성기가 그 값을 보존하는 흐름을 사용한다.

## 2026-08-23 — 오퍼레이터 관리 카드 전용 포트레잇 분리

### 맥락
- 선택 화면의 `selectionPortrait`는 작은 머리 크롭 이미지라 세로형 관리 카드의 넓은 인물 영역에 사용하면 구도가 맞지 않았다.
- Normal/Hover/Locked 시각 프리팹은 이미 각각 독립된 Portrait Image를 가지지만, 데이터는 모두 카탈로그의 선택 화면 미리보기만 읽고 있었다.

### 결정
- `OperatorDefinition`과 제작 레시피에 `managementPortrait` 전용 참조를 추가하고 Operator Studio Identity 탭에서 선택 화면 초상화와 나란히 편집한다.
- 로컬 오퍼레이터는 생성된 Catalog에도 관리 카드 포트레잇을 복사해 세 Visual 프리팹이 Definition을 별도로 로드하지 않고 즉시 표시한다. 원격 오퍼레이터는 기존 CDN 경계를 유지하기 위해 다운로드 전 Catalog 참조를 비운다.
- 관리 카드 Visual은 전용 포트레잇을 우선 사용하고, 미작성 오퍼레이터는 기존 선택 초상화로 폴백한다. 아트 제작 중에도 카드가 완전히 비지 않게 하기 위한 호환 경로다.
- 카시아 레시피에는 `오퍼레이터관리_카시아.png`를 연결했다. 세 상태 프리팹의 수동 RectTransform 조정값은 생성기가 덮어쓰지 않는다.

## 2026-08-23 — 아군 유닛 데이터셋 제작 스튜디오

### 맥락
- `AllyUnitDefinition`의 수치·효과·표현과 `AllyUnitRoster`의 등록 순서를 개별 Inspector에서 오가며 편집해야 해, 유닛 추가 시 미등록 Definition이나 중복 ID를 놓치기 쉬웠다.
- 테스트 수직 슬라이스 Roster와 `OperatorAssetBuilder`가 만든 오퍼레이터별 복제 Roster가 함께 존재하므로, 제작 원본과 다음 Build에서 덮어써질 생성물을 같은 방식으로 편집하면 변경이 유실될 수 있었다.

### 결정
- `RCCom/Ally Units/Open Ally Unit Studio` 메뉴에 `AllyUnitStudioWindow`를 추가하고 기존 Operator/Stage Studio와 같은 IMGUI 좌측 목록·탭 패턴을 사용한다.
- `Units` 탭에서 ID·배치 비용·전투 수치·Sprite·Tint·효과 SO 목록을 한 번에 편집하고, 효과 순서 변경과 Definition 복제, 어느 Roster에서 사용하는지 역참조 확인을 제공한다.
- `Rosters` 탭에서 공용 Definition 참조를 추가·제거·정렬하고, 실제 선택 화면과 전투 배치 메뉴가 사용하는 순서를 그대로 보여준다. 새 Roster 생성 시 선택 중인 Definition이 있으면 첫 항목으로 넣어 전체 검증을 막는 빈 제작물을 줄인다.
- `Audit` 탭은 어느 Roster에도 없는 Definition, 어느 OperatorDefinition에도 직접 연결되지 않은 Roster, null·중복 ID·유효하지 않은 수치와 효과 참조를 읽기 전용으로 모아 보여준다. 최종 판정은 기존 `OperatorAssetValidator`를 재사용해 검증 규칙을 이중화하지 않는다.
- `RCCom.GeneratedOperator` 라벨의 오퍼레이터별 복제 Roster는 읽기 전용으로 잠근다. 원본 풀은 Operator Studio 레시피에서 지정하고 복제본은 Build 결과로만 갱신한다. 테스트 수직 슬라이스 라벨은 현재 유닛 제작 원본이기도 하므로 편집을 허용하되 재실행 시 초기화될 수 있음을 경고한다.

### 의도적으로 하지 않은 것
- 별도의 AllyUnit JSON 포맷이나 새 런타임 데이터 타입을 만들지 않았다. Sprite·효과 SO 참조를 자연스럽게 보존하는 기존 Definition/Roster SO가 계속 단일 원본이다.
- 에셋 삭제 기능은 넣지 않았다. Roster·Operator·Addressables 의존성을 확인하지 않은 삭제는 GUID 참조를 끊고 복구가 어려우므로 Project 창의 명시적 작업으로 남겼다.
- 오퍼레이터 Roster 연결을 이 창에서 직접 변경하지 않았다. 로드아웃 조립 책임은 기존 Operator Studio의 `sourceAllyUnitRosterPath`에 유지한다.

### 검증
- Unity `6000.3.13f1` Pipeline 재컴파일에서 `failed=false`, `errors=[]`를 확인했다.
- CLI에서 메뉴 진입점을 호출해 `AllyUnitStudioWindow` 인스턴스 1개가 열리고 Units/Rosters/Audit 세 탭 렌더링에 신규 콘솔 오류가 없는 것을 확인했다.
- 기존 데이터 에셋을 수정하지 않고 `OperatorAssetValidator.ValidateAll(false)`가 `true`를 반환했다. 기존 카시아 대사 빈 슬롯 3건과 미등록 `축적.asset` 경고 1건만 동일하게 남았다.
## 2026-08-23 — 오퍼레이터 관리 화면 Back 버튼 상태 배선

### 결정
- Back 버튼은 `OperatorManagementBackButtonSheet_0`을 Normal, `_1`을 Hover/Pressed로 사용하는 SpriteSwap 버튼으로 구성한다.
- 키보드 포커스만 받은 Selected 상태는 Normal을 유지한다. 실제 포인터 호버 없이 발광 상태가 고정되는 현상을 막기 위해서다.
- 스프라이트에 `BACK` 문구가 포함되어 있으므로 생성기의 중복 TMP 라벨은 비활성화한다.
- 원본 시트의 두 번째 실재 슬라이스가 과거 자동 분할 이름 `_7`로 남아 있던 것을 Editor Sprite API로 `_1`로 정규화했다. `.meta`를 직접 편집하지 않고 Unity 임포터를 통해 참조를 갱신한다.

## 2026-08-23 — 오퍼레이터 관리 화면 비활성 초기화와 버튼 입력 복구

### 증상과 원인
- `OperatorManagingSystem`이 비활성 상태인 채 로비 메뉴에서 `Open()`을 직접 호출하면 Unity가 아직 `Awake()`를 실행하지 않은 상태일 수 있었다. `Open()`은 `Awake()`에서 만들기로 한 프로필 저장소를 먼저 사용해 NullReference로 중단됐고, 패널 활성화와 `OnEnable()`의 Deploy/Back 리스너 등록까지 도달하지 못했다.

### 결정
- 프로필 저장소 초기화를 멱등 메서드로 분리해 `Awake()`와 외부 진입점 `Open()` 양쪽에서 보장한다.
- 관리 화면은 씬에 비활성 상태로 저장하고, `Awake()`가 표시 상태를 다시 끄지 않게 했다. 비활성 오브젝트를 처음 활성화하는 도중 Awake가 자기 자신을 다시 비활성화하는 생명주기 충돌을 제거하기 위해서다.
- Deploy는 이미 활성화된 오퍼레이터를 보고 있을 때도 누를 수 있게 한다. 동일 오퍼레이터를 재선택해도 로딩·상태 갱신 경로가 정상 작동하며, 단일 오퍼레이터 상태에서 버튼이 고장 난 것처럼 보이지 않는다.

### 추가 입력 차단 원인
- Back 생성에 사용하는 공용 `CreateImage`는 장식 이미지 기본값에 맞춰 `raycastTarget=false`를 설정한다. 이를 Button으로 전환한 뒤 다시 켜지 않아 포인터가 버튼을 통과했다.
- Canvas 최하단의 `RawImage`는 `Title Render Texture` 표시 전용 배경인데 `raycastTarget=true`로 남아 있었다. 통과한 Back 입력과 메뉴 입력을 이 배경이 받아 환경설정 접근도 함께 막았다.
- Back Image는 명시적으로 `raycastTarget=true`, 배경 RawImage는 `false`로 고정해 버튼과 배경의 입력 책임을 분리한다.

### 환경설정 액션 회귀
- `TitleMenuTextButton.MenuAction` 중간에 `ManageOperators`를 삽입하면서 Unity 씬에 정수 `1`로 저장된 기존 `Preference`가 `ManageOperators`로 재해석됐다. Configuration에는 관리 화면 참조가 없어 클릭이 아무 동작 없이 끝났다.
- 기존 직렬화 번호 `NewGame=0`, `Preference=1`, `ReturnToTitle=2`를 명시적으로 복원하고 신규 `ManageOperators=3`을 마지막에 배치했다. 이후 enum 선언 순서가 바뀌어도 기존 씬 액션 의미가 변하지 않는다.

## 2026-08-23 — 두 번째 오퍼레이터 즉시 해금과 카탈로그 슬롯 순서

### 결정
- 제작 중인 `calliste`는 `requiredBestWave=0`을 유지해 별도 재화나 진행 조건 없이 등록 즉시 사용할 수 있게 한다.
- 레시피에 `catalogOrder`를 추가해 카시아를 0번, 칼리스테를 1번으로 고정한다. 기존 ID 알파벳 정렬은 `calliste`를 첫 칸에 배치하므로 캐릭터 제작 순서를 표현할 수 없었다.
- 세 번째 오퍼레이터 데이터는 아직 등록하지 않는다. 관리 UI가 카탈로그 뒤에 생성하는 예약 슬롯이 그대로 `UNASSIGNED / LOCKED` 상태를 표시한다.

### 임시 콘텐츠 빌드
- 칼리스테의 타워·카드·아군 유닛 로스터는 캐릭터 전용 밸런스가 확정될 때까지 카시아 로스터를 임시 소스로 사용한다. Definition은 별도 에셋으로 생성되므로 이후 레시피의 소스 경로만 교체해 다시 빌드할 수 있다.
- 오퍼레이터 SO, 카탈로그, Addressables 그룹을 재생성하고 전체 참조 검증을 통과했다. 결과 카탈로그 순서는 카시아 → 칼리스테이며 칼리스테는 최고 웨이브 0에서 해금된다.

### 실비아 병렬 작업 통합
- 원격 main의 실비아(`racing`) 레시피를 병합하면서 명시적 `catalogOrder=2`를 부여했다. 카탈로그 자동 생성 순서는 카시아 → 칼리스테 → 실비아를 유지한다.
- 병렬 작업에서 각각 수정한 `OperatorCatalog.asset`과 `AddressableAssetSettings.asset`은 YAML을 수동 병합하지 않고, 세 오퍼레이터 레시피를 소스로 Unity 생성기를 다시 실행해 재구성한다.

## 2026-08-23 — Deploy 호버 PR #10 선택 통합

### 결정
- PR #10의 SpriteSwap 상태 구분을 반영해 Normal/Selected/Disabled는 `_0`, Highlighted/Pressed는 `_1`을 사용한다.
- Deploy 버튼은 콘텐츠 로딩 중에만 비활성화하고, 잠금 여부는 기존 `Deploy()` 가드가 판정한다. 잠금 슬롯에서도 포인터 호버 표현은 유지된다.
- PR의 “현재 활성 오퍼레이터 클릭 차단”은 적용하지 않았다. 단일 오퍼레이터 상태에서도 버튼이 죽은 것처럼 보이지 않도록 현재 선택을 다시 Deploy하는 기존 통합 결정을 유지한다.

## 2026-08-23 — 카시아 아군 유닛 아트·로드아웃 에셋 연결

### 맥락
- 카시아의 아군 유닛을 테스트 수직 슬라이스 에셋과 분리된 제출용 데이터로 만들고, 두 유닛의 Sprite·수치·출격 순서를 하나의 원본 Roster에서 관리할 필요가 있었다.
- `OperatorAssetBuilder.BuildAll()`은 모든 JSON 레시피를 순회하므로 카시아를 빌드할 때 칼리스테·실비아의 생성물과 공용 생성 파일도 함께 갱신될 수 있다. 커밋 단위에서 카시아 범위를 분리하지 않으면 이번 작업의 변경 근거와 다른 오퍼레이터 산출물이 섞인다.

### 결정
- `Assets/Art/AllyUnits/`에 방호 요원과 전진 사수 Sprite PNG 및 Unity가 생성한 폴더·텍스처 `.meta`를 등록했다. Definition은 이 Sprite GUID를 직접 참조하고 별도 런타임 뷰나 유닛별 프리팹을 만들지 않는다.
- `cassia-guard`와 `cassia-vanguard` Definition, `cassia-ally-unit-roster` 원본, 공용 `UnitCombatSettings`를 추가했다. 원본 Roster는 레시피의 입력으로 유지하고, `Assets/Data/Operators/cassia/AllyUnitRoster.asset`은 Builder가 만든 오퍼레이터 로드아웃 출력으로 분리했다.
- `Cassia.json`의 `sourceAllyUnitRosterPath`를 연결하고 기존 Builder를 실행해 카시아 `OperatorDefinition`과 카탈로그 유닛 미리보기를 생성했다. 전투 코드는 수정하지 않고 기존 효과 SO·Definition/Roster 조립 경로를 그대로 사용한다.
- `AllyUnitStudioWindow`의 배열 편집은 클릭 이벤트 중 즉시 직렬화 배열을 바꾸지 않고 다음 Layout 이벤트에서 적용하도록 보정했다. IMGUI의 Layout/Repaint 컨트롤 수 불일치로 검색·효과·Roster 편집 포커스가 옆 필드로 튀는 문제를 데이터 제작 도구에서 차단하기 위해서다.

### 의도적으로 하지 않은 것
- 칼리스테·실비아의 Definition/Roster/대화 및 그 오퍼레이터별 Builder 산출물은 이번 커밋에 포함하지 않았다. `Build All`이 함께 건드린 파일은 카시아 변경과 독립된 작업으로 남긴다.
- 공용 Addressables 설정과 카탈로그의 실비아 관리 포트레잇 변경은 포함하지 않았다. 카시아 유닛 연결에 필요한 카탈로그 hunk만 반영했다.
- 씬의 타일맵·UI·TMP 자동 직렬화 변경은 포함하지 않았다. 이번 작업과 무관한 기존 작업 트리 변경을 보존하고 커밋 범위를 오퍼레이터 데이터에 한정했다.

### 검증
- Unity `6000.3.13f1` Pipeline에서 `recompile` 결과 `up_to_date`를 확인했다.
- `OperatorAssetValidator.ValidateAll(false)`가 `True`를 반환했고, 카시아 Definition·Roster·Catalog의 Sprite/효과/Addressables 참조를 검증했다.
- 콘솔 신규 오류는 0건이었다. 기존 `Assets/Data/Definition/Tower/축적.asset` 미등록 경고 1건만 유지됐다.

## 2026-08-23 — 아군 유닛 View 스케일·단일 오퍼레이터 빌드·경계 교착 보정

### 결정
- 공용 `AllyUnitView`의 기본 목표 표시 크기를 `0.9`에서 `2.0`으로 조정하고, 생성된 공용 프리팹에도 같은 값을 반영했다. 유닛별 프리팹을 늘리지 않고 Definition Sprite의 실제 크기에 따라 런타임에서 맞추는 기존 구조는 유지한다.
- `OperatorAssetBuilder.BuildSingle()`과 `OperatorCatalogBuilder.BuildForOperator()`를 추가하고 Operator Studio에 `Save + Validate + Build This Operator` 진입점을 배치했다. 레시피를 하나만 반영할 때 다른 오퍼레이터의 Definition/Roster/Addressables 그룹을 다시 저장하지 않도록 하며, `Build All`은 오퍼레이터 추가·삭제 시의 전체 동기화 용도로 남긴다.
- Builder와 Catalog Builder는 직렬화 전후 값을 비교해 실제 내용이 달라질 때만 `SetDirty`하고, 변경된 에셋 목록을 단일 빌드 결과에 표시한다. 같은 값을 다시 쓰는 에셋이 커밋에 섞이는 것을 줄이기 위한 장치다.
- `AllyUnitTargeting.IsWithinRange()`에 `0.001` 허용 오차를 공통 적용했다. 접촉 경계에서 float 반올림으로 실제 거리가 사거리보다 극소량 커지면 이동량 0·타깃 null이 반복되던 교착을, 밸런스에 영향을 주지 않는 경계 보정으로 해소한다.

### 의도적으로 하지 않은 것
- `Build All`은 이번 작업에서 실행·커밋하지 않았다. 카시아 외 오퍼레이터의 Builder 산출물과 공용 Addressables 파일을 이번 범위에 섞지 않기 위해서다.
- 기준 커밋과 내용이 동일했던 Addressables/Roster/대화 에셋의 상태 변경과 TMP 폴백 폰트 재직렬화는 복원했다. 실비아 관리 초상화와 `DefenseScene`의 타일맵·UI 변경은 별도 작업으로 판단해 작업 트리에 보존하고 이번 커밋에서는 제외했다.

### 검증
- Unity `6000.3.13f1` Pipeline `recompile` 결과 `up_to_date`, 컴파일 오류 0건.
- `AllyUnitViewPrefabBuilder.Validate()`, `AllyUnitCombatVerifier.Verify()`, `OperatorAssetValidator.ValidateAll(false)`를 CLI에서 통과시켰다.
- 검증 후 Unity 콘솔 신규 오류 0건을 확인했다.

## 2026-08-23 — 실비아 관리 초상화 카탈로그 연결

### 결정
- 실비아(`racing`)의 `OperatorDefinition.managementPortrait`에 전용 관리 화면 포트릿을 연결했다.
- 로컬 카탈로그의 실비아 항목에도 같은 Sprite를 연결해 관리 카드가 Definition 다운로드 전에도 빈 이미지로 표시되지 않게 했다.
- `UnitDeployController`의 전투 설정 승격은 이미 `0f0ee71`에서 테스트 설정 GUID를 정식 `UnitCombatSettings` GUID로 교체했으므로 이번 커밋에서는 중복 반영하지 않았다.

### 검증
- 카탈로그와 Definition의 실비아 관리 초상화 GUID가 일치하는 것을 확인했다.
- 기존 오퍼레이터 에셋 검증과 Unity 콘솔 오류 검사를 다시 수행했다.

## 2026-08-23 — 카시아 아군 유닛 밸런스 조정

### 결정
- 방호 요원(`cassia-guard`)의 최대 체력을 `110 → 40`, 공격력을 `8 → 6`으로 낮췄다.
- 전진 사수(`cassia-vanguard`)의 최대 체력을 `40 → 20`, 공격력을 `4 → 3`으로 낮췄다.
- 배치 비용·이동 속도·공격 간격·사거리·탐지 범위는 유지해, 이번 조정의 영향이 전투 내구도와 화력에만 한정되도록 했다.

### 검증
- 두 Definition의 ID와 Roster 참조를 유지한 채 수치만 변경된 것을 확인했다.

## 2026-08-23 — 웨이포인트 경로 스플라인 베이킹 (유선형 맵 대응)

### 맥락
배경 아트의 도로는 부드러운 S자 곡선인데 적/아군 이동 경로는 씬에 배치한 웨이포인트 9개를 직선으로 이은 폴리라인이라, 궤적이 각지고 정점 통과 시 View 회전이 튀었다. 유선형 맵과 직선형 맵을 모두 지원하되 전투 로직은 건드리지 않는 방법이 필요했다.

### 결정
- `PathSmoothing.GenerateSmoothPath()`(신규, `Assets/Scripts/Core/`)로 구심(centripetal, α=0.5) Catmull-Rom 곡선을 따라 촘촘한 정점 배열을 만들고, `MapManager.Awake()`가 이를 1회 베이킹해 기존과 동일한 `IReadOnlyList<Vector2>`로 넘긴다. **전투·타기팅·스폰 코드는 한 줄도 고치지 않았다.**
- 인스펙터 파라미터는 `pathSmoothness`(0~1)와 `maxPointSpacing`(0.25~5, 기본 0.5) 두 개다. `pathSmoothness = 0`이면 **세분화조차 하지 않고** 원본 배열을 그대로 반환한다.
- `MapManager.OnDrawGizmosSelected()`를 추가해 Play 모드 없이 씬 뷰에서 곡선을 미리 본다. 곡선이 배경 도로 아트를 벗어나는지는 사람이 눈으로 검수해야 하는 항목이라, 이 프리뷰가 이번 작업의 실질적 검수 수단이다.
- `AllyUnitTargeting`에 경로 누적 거리 캐시를 넣었다(참조 비교로 무효화, `ResetPathCache()`를 `GameManager.Awake()`의 static 캐시 초기화 목록에 등록).
- `EnemyView`/`AllyUnitView`에 `turnSpeedDegreesPerSecond`(기본 0 = 기존 즉시 회전)를 추가했다.

### 근거
- **왜 Unity Splines 패키지가 아닌가** — 런타임에 스플라인 수식으로 위치를 계산하게 바꾸면 `AllyUnitTargeting`의 선분-구체 교점 판정(`DistanceBeforeContact`)과 누적 진행도 공식이 전부 재작성 대상이 된다. 반면 초기화 시점에 정점 배열로 베이킹하면 기존 시스템이 수정 없이 곡선에 적응한다. 소비처를 전수 확인한 결과 `path[0]`, `path[^1]`, `path.Count >= 2` 외에 정점 개수에 의존하는 코드가 없었다.
- **왜 Bézier/B-spline이 아니라 Catmull-Rom인가** — 제어점을 정확히 통과하므로 디자이너가 도로 중앙에 찍은 웨이포인트 의도가 어긋나지 않는다.
- **왜 α=0.5(구심)인가** — 0(uniform)은 급커브에서 오버슈트/자체 교차가 생기고, 1(chordal)은 코너가 무뎌진다. 0.5는 자체 교차가 없음이 증명된 값이다.
- **왜 `pathSmoothness = 0`에서 세분화조차 하지 않는가** — 좌표가 원본 직선과 같아도 정점 수가 늘면 아래의 속도 손실이 그대로 생긴다. "곡선을 끈다"가 "정점 수도 그대로"를 포함해야 완전한 하위호환이 된다.
- **왜 구간당 고정 분할 수가 아니라 거리 기반(`maxPointSpacing`)인가** — 웨이포인트 간격이 제각각인 맵에서 정점 밀도와 속도 손실률이 맵마다 달라지는 것을 막는다.
- **왜 진행도 캐시가 조기 최적화가 아닌가** — `CalculatePathProgress`는 O(n)인데 `PathProgress` 프로퍼티로 노출되어 `IsPreferredAlly`/`IsPreferredEnemy`의 비교문에서 후보 하나당 최대 4회 재평가되고, 그 비교가 아군×적 이중 루프 안에서 매 프레임 돈다. 정점이 9→98개가 되면 이 핫패스가 그대로 약 11배가 된다. 이번 변경이 만드는 회귀를 상쇄하는 조치다.

### 의도적으로 하지 않은 것
- **`EnemyInstance.MoveAlongPath()`를 잔여 이동량 소비 루프로 바꾸지 않았다.** 이 함수는 프레임당 웨이포인트 1개까지만 전진하고 정점 도달 시 남은 이동량을 버리는데(`AllyUnitInstance`는 `while` 루프라 버리지 않는다 — 기존부터 있던 비대칭), 그래서 **정점 밀도를 높이면 적이 프레임레이트에 비례해 느려진다.** 실측 기준 정점 98개(간격 0.5)에서 60fps 약 5%, 30fps 약 10%다(현재 9정점은 각각 0.4%/0.9%).
  루프로 바꾸는 것이 근본 해법이지만, `TryGetMovementSweep`이 `deltaTime = float.PositiveInfinity`로 호출되어 "현재 선분 전체"를 전방 sweep으로 삼는 프로토콜과 맞물려 있다. 루프로 바꾸면 `Tick(0f)`에서 `_pathIndex`가 전진하지 않아 sweep이 무효가 되고 **적이 아군 전열을 관통하는 회귀**가 난다(기존 검증 `VerifyContactBoundaryOnLargeStep`도 깨진다). 별도 작업으로 분리했다.
  → **이 속도 손실은 밸런스 판단이 필요한 사항이다.** `maxPointSpacing`을 키우거나(1.0이면 52정점·60fps 2.7%), 적 `moveSpeed`를 보정하거나, 위 별도 작업을 진행하는 선택지가 있다.
- `PathMode` enum(Linear/SmoothSpline/CornerRounding)을 도입하지 않았다. `pathSmoothness = 0`이 Linear 역할을 하므로 지금은 불필요하다.
- Unity Splines 패키지를 설치하지 않았고 `Packages/manifest.json`을 건드리지 않았다.
- 프리팹의 `turnSpeedDegreesPerSecond` 직렬화 값을 설정하지 않았다. 코드 기본값 0이라 기존 동작이 그대로다.

### 검증
- Unity 에디터가 기동되지 않는 상태라 Pipeline `recompile`을 쓸 수 없어, `dotnet build`로 `Assembly-CSharp` / `Assembly-CSharp-Editor`를 직접 컴파일해 확인했다(양쪽 경고 0·오류 0, 변경 전 baseline도 동일).
- `PathSmoothingVerifier`(신규 에디터 검증기, `RCCom/Map/Verify Path Smoothing`) 9개 시나리오를 작성했다. 이 중 Unity 로깅에 의존하지 않는 8개는 컴파일된 `Assembly-CSharp.dll`을 직접 호출하는 순수 C# 하니스로 **실제 실행해 전부 통과**시켰다. 실행 결과:
  - `smoothness=0`에서 정점 9개·좌표 완전 동일 / `smoothness=0.6` 및 `1.0`에서 정점 98개
  - 끝점 정확 보존, 모든 제어점 정확 통과, 일직선 제어점의 직선 유지(이탈 0)
  - 제어점 AABB 오버슈트 0, 경로 길이 증가율 1.017배
  - 진행도 캐시 도입 전후 비트 단위 동일 (참조 구현과 4,000회 무작위 비교, 불일치 0건)
- **정점 간격은 `maxPointSpacing`의 약 1.19배까지 나온다.** 분할 수를 현(chord) 길이로 정하는데 점은 더 긴 곡선 위에 놓이고 구심 파라미터화가 호 길이에 균일하지 않기 때문이다. 간격 값을 0.3~1.0으로 바꿔도 1.18~1.21배로 일정해, 검증기 단언은 실측 기반으로 1.25배를 상한으로 잡았다.

### 사람 액션
- **Unity 에디터를 띄운 뒤 `RCCom/Map/Verify Path Smoothing`과 기존 `RCCom/Ally Units/Verify Combat Core`(21개 시나리오)를 실행해야 한다.** 후자는 `ScriptableObject.CreateInstance`/`SerializedObject`에 의존해 에디터 밖에서 실행할 수 없어, 진행도 캐시 변경에 대한 회귀 검증이 아직 에디터에서 이뤄지지 않았다.
- `DefenseScene`의 MapManager `pathSmoothness`를 `0.6`으로 설정해야 곡선이 실제로 적용된다(기본값 0이라 현재는 기존과 동일하게 동작한다). `.unity` 텍스트 직접 편집은 금지이므로 인스펙터에서 조정할 것.
- MapManager를 선택한 상태로 씬 뷰에서 **곡선이 배경 도로 아트를 벗어나지 않는지 육안 확인**이 필요하다.
- 신규 파일 2개(`PathSmoothing.cs`, `PathSmoothingVerifier.cs`)의 `.meta`는 에디터 최초 기동 시 생성되므로, 생성된 것을 함께 커밋해야 한다.

## 2026-08-23 — 경로 스플라인 후속: 회전 보간 방식 교체와 한계 확정

### 맥락
앞 항목의 스플라인 베이킹을 씬에 적용(`pathSmoothness=1`, `maxPointSpacing=0.25` → 정점 193개)한 뒤에도 적이 코너에서 고개를 계단식으로 꺾는 현상이 남았다. 앞 항목에서 넣은 `turnSpeedDegreesPerSecond`(각속도 상한)는 이 문제를 해결하지 못했다.

### 증상 → 원인 → 해결
- **증상** — 정점을 촘촘하게(간격 0.25) 만들었는데도 적 스프라이트가 코너에서 "뚜두두둑" 끊기며 회전한다.
- **원인 1: 정점 밀도로는 회전이 부드러워지지 않는다.** 목표 각도는 웨이포인트 단위로 끊기는 계단 함수다. 간격을 줄이면 스텝 각도(≈ 간격/곡률반경)와 스텝 주기(≈ 간격/속도)가 **같은 비율로** 줄어들 뿐이라 계단 모양 자체는 그대로다. 간격 0.25·속도 3에서는 약 3.6°씩 초당 12회 꺾이는데, 이 주파수대는 사람 눈에 또렷한 래칫으로 보인다. 즉 이건 공간 해상도가 아니라 시간축 문제다.
- **원인 2: 각속도 상한 방식에는 튜닝 절벽이 있다.** 이 경로의 자연 회전 속도는 약 42°/초인데, 상한을 그보다 조금만 크게 잡아도(예: 앞 항목 Tooltip이 권장했던 360~720°/초 = 60fps에서 6~12°/프레임) 한 스텝을 한 프레임에 다 돌아버려 보간이 사실상 사라진다. 반대로 충분히 낮추면 급코너에서 방향이 오래 밀린다. **앞 항목의 권장값 표기는 틀렸다.**
- **해결** — `turnSpeedDegreesPerSecond`를 `turnSmoothTime`(시간 상수, 초)으로 교체하고 지수 감쇠 `Slerp(current, target, 1 - exp(-dt/τ))`를 쓴다. 스텝 크기와 무관하게 항상 부드럽고, `1 - exp(-dt/τ)` 형태라 프레임레이트가 변해도 감쇠 속도가 같다(`dt` 비례 Lerp와 다른 점). 등속 코너링 시 정상상태 지연은 `ω×τ`뿐이라 τ=0.1이면 약 4°로 눈에 띄지 않는다. 스폰 첫 프레임은 보간을 건너뛴다 — Instantiate가 준 회전에서 서서히 돌아오면 등장 순간 엉뚱한 방향을 본다.
- `EnemyView_Normal.prefab`과 `AllyUnitView.prefab`의 `turnSmoothTime`을 `0.1`로 설정했다(코드 기본값은 여전히 0 = 즉시 회전).

### 적 이동 속도 손실 — 프레임레이트 의존성 확정
앞 항목에서 남긴 손실은 정확히 다음 식이다(N=정점 수, L=경로 총 길이):

```
손실률 = N × speed × dt / (2 × L)
```

**`dt`에 정비례, 즉 프레임레이트에 반비례한다.** 프레임이 절반이 되면 손실은 정확히 두 배이고, 고프레임에서는 0으로 수렴한다. 크기보다 **플레이어 환경에 따라 적 속도가 달라진다**는 점이 본질적 한계다.

현재 씬 설정(`maxPointSpacing=0.25` → 정점 193개, speed 3, L≈47.8) 기준 실측 추정:

| fps | 손실률 |
|---|---|
| 120 | 5.1% |
| 60 | 10.1% |
| 30 | 20.2% |

앞 항목에 적은 5%/10%는 `maxPointSpacing=0.5`(98정점) 기준이었다. **간격을 0.25로 줄이면서 손실도 2배가 됐다.**

### 판단이 필요한 후속
회전 보간이 들어간 지금은 **정점 밀도가 시각적 부드러움을 책임질 이유가 없어졌다.** 간격 0.75에서도 곡선 근사 오차(현의 새그)는 `0.75²/(8×4) ≈ 0.018` 유닛으로 무시할 수준이다. 따라서 `maxPointSpacing`을 0.5~0.75로 되돌리면 시각 품질을 잃지 않고 손실을 3.6~5%로 줄일 수 있다. 밸런스 판단이라 임의로 바꾸지 않았다.

### 의도적으로 하지 않은 것
- `EnemyInstance.MoveAlongPath()`는 여전히 손대지 않았다(앞 항목의 이유 그대로 — 전방 sweep 프로토콜과 맞물려 적이 아군 전열을 관통하는 회귀가 남).
- `DefenseScene.unity`는 커밋하지 않았다. 작업 트리의 씬 변경에 타일맵 재직렬화가 대량(904/652줄) 섞여 있어 이번 작업 산출물과 분리했다. `pathSmoothness`/`maxPointSpacing` 인스펙터 값도 그 안에 있으므로, 씬을 커밋할지는 별도 판단이 필요하다.

### 검증
- Unity `6000.3.13f1` Pipeline `recompile` 결과 `completed`, `failed: false`, 컴파일 오류 0건. 콘솔 error/warning 0건.
- `RCCom/Map/Verify Path Smoothing` 9개 시나리오 통과 (앞 항목에서 미해결로 남겼던 에디터 실행 검증을 여기서 닫았다).
- `RCCom/Ally Units/Verify Combat Core` 기존 21개 시나리오 통과 — 진행도 캐시 변경의 전투 회귀 검증도 닫혔다.
- 두 검증기와 씬 인스펙터 값 확인을 한 번에 돌리는 `Tools/eval/VerifyPathSmoothing.cs`를 남겼다. 코드가 통과해도 인스펙터 값이 0이면 곡선이 실제로 적용되지 않으므로, 베이킹 정점 수를 같이 찍는다.
## 2026-08-23 — 오퍼레이터 최초 획득 연출

### 결정
- 해금 상태는 기존대로 `bestWave >= requiredBestWave`에서 계산하고, `PlayerProfile`에는 연출을 끝까지 본 오퍼레이터 ID만 저장한다. 해금 데이터의 이중 원본을 만들지 않으면서 재접속 때 같은 획득 화면이 반복되는 것을 막기 위함이다.
- 카탈로그 0번은 게임 시작부터 함께하는 기본 오퍼레이터로 간주해 기존 프로필에는 조용히 표시 완료 처리한다. 그 뒤 슬롯에서 새로 해금된 오퍼레이터는 카탈로그 순서대로 한 명씩 소개한다.
- `OperatorDialogueSet.operatorAcquired`를 로비 전신 Sprite를 사용할 수 있는 `OperatorLineSet`으로 추가했다. 획득 대사마다 다른 전신 Sprite를 지정할 수 있고, 비어 있으면 획득 슬롯 기본 Sprite → 로비 기본 Sprite → 관리 카드 포트레잇 순으로 폴백한다.
- 연출 순서는 `NEW` → `OPERATOR` → 표시 이름의 우→좌 슬라이드, 전신 등장, 하단 대사 패널 등장으로 고정했다. 재생 중 클릭은 최종 상태로 스킵하고, 대사가 열린 뒤 클릭은 표시 이력을 저장하고 다음 획득 대상을 진행한다.
- 기존 `GachaGainBackground`와 네 자식 오브젝트의 배치를 보존하고, 재실행 가능한 Editor Setup이 CanvasGroup·전체 화면 입력·하단 대사 패널·카탈로그 참조만 배선한다.
- 반복 검수용으로 `RCCom/디버그/획득 연출 기록 초기화` 메뉴를 제공한다. 이 메뉴는 해금·호감도·선택 오퍼레이터는 보존하고 표시 이력만 비운다.

### 의도적으로 하지 않은 것
- 별도 획득 매니저나 오퍼레이터별 런타임 클래스를 만들지 않았다. 로비 UI가 프로필과 Addressables Definition을 읽는 단방향 흐름으로 제한했다.
- 아직 없는 재화·가챠 소유 목록을 미리 도입하지 않았다. 향후 실제 획득 수단은 같은 프로필 표시 이력과 공개 연출 진입점을 사용하고, 현재는 확정된 웨이브 해금 조건을 획득 트리거로 삼는다.

### 검증
- Unity 6000.3.13f1 에디터에서 Setup 메뉴를 실행해 `AcquisitionDialogue` 생성, 참조 배선, TitleScene 저장을 확인했다.
- Unity 콘솔의 스크립트 오류 수가 0임을 확인했다. Unity CLI 실행기는 Unity Hub 로그 경로 처리 오류로 시작하지 못해 열린 에디터에서 컴파일과 메뉴 실행을 검증했다.

## 2026-08-23 — UI 공통 로딩 와이프와 Addressable 팁

### 결정
- 타이틀의 패널 전환과 TitleScene/DefenseScene 사이의 씬 로딩을 `UILoadingTransition` 하나로 감싼다. 별도 매니저는 만들지 않고, 지속 Canvas에 붙은 UI 컴포넌트가 호출자가 넘긴 동기 작업·코루틴·씬 로딩 동안 입력과 연출만 소유한다.
- 전환은 배경이 화면 위에서 중앙으로 1.5초 동안 내려와 위→아래로 덮고, 작업 완료 후 중앙에서 위로 1.5초 동안 빠져나가 아래→위로 다음 화면을 드러내는 방식으로 통일한다.
- `UILoadingImageGeometry`는 화면이 움직이거나 로딩을 기다리는 모든 구간에 회전한다. 작업 완료 시 기본 크기의 1.35배까지 짧게 펄스한 뒤 퇴장한다.
- 로딩 팁은 `LoadingTipSet` SO를 `ui/loading-tips` 주소로 분리하고 `UI-Live-Remote` Addressables 그룹에 둔다. 원격 카탈로그나 CDN이 준비되지 않은 개발 환경에서도 전환이 멈추지 않도록 동일 컴포넌트에 최소 폴백 문구를 둔다.
- 기존 `UILoadingImageBackground`와 `UILoadingImageGeometry`의 Sprite를 보존하고, 재실행 가능한 Setup 도구가 지속 Canvas·TMP·Addressables 엔트리·참조만 배선한다.

### 적용 범위
- 메인 메뉴의 Operation/Operators/Configuration 진입, 오퍼레이터·모드·스테이지 선택 화면의 전후 이동, 스테이지/엔드리스 출격, 전투 결과의 재시작/타이틀 복귀에 같은 전환을 연결했다.
- 오퍼레이터 Addressables 로딩 코루틴은 화면이 완전히 덮인 뒤 시작하므로 기존 `데이터 검증 중` 상태와 배경 UI가 노출되지 않는다.

### 의도적으로 하지 않은 것
- 팁 문구 때문에 원격 번들을 반드시 요구하지 않았다. 로딩 UI는 네트워크 실패를 설명하는 기반 화면이기도 하므로 자체 표시 가능성을 우선했다.
- Animator나 Timeline 에셋을 추가하지 않았다. 시간 정지 중 패널 전환과 씬 비동기 로딩을 한 경로로 다루기 위해 `unscaledDeltaTime` 기반 코루틴으로 상태를 명시했다.

### 검증
- Unity 6000.3.13f1 Pipeline에서 새 런타임·에디터 코드의 재컴파일 결과 `failed=false`, `errors=[]`를 확인했다.
- Setup 메뉴로 TitleScene 저장, `LoadingTipSet.asset`, `UI-Live-Remote` 그룹과 스키마 생성을 확인했다.
- 플레이 모드에서 전환을 직접 호출해 화면 차단·회전 루프·완료 펄스·복귀 경로를 실행했으며 새 런타임 오류가 발생하지 않았다.

## 2026-08-23 — 로딩 UI 프리팹화와 전환 속도 조정

### 결정
- `UILoadingTransitionCanvas`를 `Assets/Data/Prefabs/UI/UILoadingTransitionCanvas.prefab`으로 분리하고 TitleScene에는 연결된 프리팹 인스턴스를 둔다. 아트 조정자는 Prefab Mode에서 배경·기하 도형·번호·팁·상태 문구의 RectTransform과 TMP 스타일을 직접 수정할 수 있다.
- 프리팹 원본의 CanvasGroup은 편집 중 보이도록 Alpha 1을 유지하고, TitleScene 인스턴스에만 Alpha 0 오버라이드를 둔다. 플레이 진입 시 런타임도 즉시 숨김을 보장한다.
- Setup 도구는 프리팹을 최초 한 번만 현재 씬 배치에서 생성한다. 프리팹이 이미 존재하면 사용자가 조정한 레이아웃 값을 초기 좌표로 덮어쓰지 않는다.
- 메뉴 간 왕복에서 1.5초가 길게 느껴지는 문제를 줄이기 위해 덮기와 공개 시간을 각각 0.6초로 변경했다. 최소 표시 시간과 완료 시 1.35배 펄스는 유지한다.
- 무작위 3자리 번호만 표시하던 `LoadingSerialText`는 실제 로딩 상태나 게임 데이터와 연결되지 않아 제거했다. 프리팹과 씬 인스턴스에서 함께 지워 이후 Setup 재실행으로 되살아나지 않는다.

## 2026-08-23 — 호감도 디버그 패널의 합류 연출 초기화

### 결정
- TitleScene 우측 상단 에디터 전용 호감도 패널에 `합류 초기화` 버튼을 추가했다. 기존의 Editor 메뉴 전용 “획득 연출 기록 초기화”와 같은 `IProfileStorage` 경로를 사용한다.
- 이 버튼은 `presentedOperatorAcquisitionIds`만 비운다. 해금 판정의 원본인 최고 웨이브, 현재 선택 오퍼레이터, 호감도, 귀환 보상 예약은 유지한다.
- 상태 문구에 현재 표시 완료 처리된 합류 연출 인원 수를 추가해, 클릭 직후 저장 결과를 패널 안에서 확인할 수 있게 했다.

### 검증
- Unity 6000.3.13f1 Pipeline 재컴파일 결과 `failed=false`, `errors=[]`를 확인했다.
- `RCCom/Debug/Validate Affinity Overlay`를 실행해 새 버튼 참조를 포함한 TitleScene 배선을 검증했다.

## 2026-08-23 — 전투 결과 계정 재화 지급

### 결정
- 전투 중 건설에 소비되는 `GameManager.Gold`와 분리해, 귀환 뒤에도 남는 `PlayerProfile.commodity`를 계정 재화의 단일 원본으로 추가했다. 현재 재화 소비처가 없더라도 스테이지 보상을 구현하려면 전투 골드에 누적하면 안 되기 때문이다.
- 결과 화면이 전투 결과를 확정할 때 기본 100과 생존 시간 완료 1분당 +20을 합산해 지급한다. 시간 보너스는 최대 +100으로 제한해 장기 생존만으로 보상이 무한히 커지지 않게 했다.
- 승리·패배 모두 결과에 도달한 한 판으로 간주해 지급한다. `GameManager.BattleEnded`의 단발 가드와 `GameResultUI`의 지급 가드를 함께 두어 같은 결과 UI가 재호출되어도 중복 지급되지 않는다.
- 결과 화면에는 세션 통계만 남기고, TitleScene의 기존 `CommodityPanel/Image/Text (TMP)`가 저장된 총 재화를 표시한다. 전투 결과로 로비에 돌아오거나 타이틀에서 메인 메뉴를 열 때 프로필을 다시 읽으므로 씬 참조를 새로 배선할 필요가 없다.

### 의도적으로 하지 않은 것
- `StageReward` 목록을 지금 바로 실제 지급 데이터로 사용하지 않았다. 현재 스테이지 SO의 보상은 아이콘·표시용 매니페스트까지만 확정되어 있고, 아이템 인벤토리 계약이 없는 상태에서 rewardId마다 저장 규칙을 추가하면 계정 데이터 원본이 분산되기 때문이다.

### 검증
- Unity 6000.3.13f1 Pipeline 재컴파일 결과 `failed=false`, `errors=[]`를 확인했다.


### 검증
- Unity Pipeline 재컴파일 결과 `failed=false`, `errors=[]`를 확인했다.
- TitleScene의 인스턴스 원본 경로가 새 프리팹이고 직렬화된 `coverDuration=0.6`, `revealDuration=0.6`임을 Unity Editor API로 확인했다.

## 2026-08-23 — 오퍼레이터 다중 해금 조건과 획득 UI

### 결정
- `OperatorUnlockType`을 기본 지급, 최고 웨이브, 계정 재화 구매, 스테이지 클리어 보상의 네 종류로 정의했다. 조건 수치와 보상 초상화는 Operator Studio 레시피에서 편집하고, 생성기가 Definition과 로컬 카탈로그에 동일하게 복제한다.
- `PlayerProfile` 스키마를 5로 올리고 구매 소유 ID와 클리어 Stage ID를 별도 저장한다. 최고 웨이브와 스테이지 조건은 진행 기록에서 계산하며, 구매형만 명시적 소유 목록을 사용한다. 획득 연출 표시 이력은 계속 별도 목록이라 연출 초기화가 소유 상태를 지우지 않는다.
- 칼리스테는 임시 가격 300의 계정 재화 구매형, 실비아는 `ch1-02`(표시 1-2) 클리어 보상형으로 설정했다. 카시아는 기본 지급을 유지한다.
- 오퍼레이터 관리 화면에 잠긴 구매형에서만 나타나는 PURCHASE 버튼을 추가했다. 구매는 잔액 확인·차감·소유 기록·로비 잔액 갱신을 한 경로에서 처리하고, 성공하면 기존 신규 오퍼레이터 합류 연출을 즉시 호출한다.
- 스테이지 선택 화면은 선택한 Stage ID를 보상 조건으로 가진 오퍼레이터를 카탈로그에서 찾아 보상 카드, 이름, 초상화를 표시한다. 전용 보상 초상화가 비어 있으면 선택 초상화로 폴백해 아트가 늦게 들어와도 정보가 사라지지 않는다.
- 스테이지 승리 결과에서만 Stage ID를 최초 클리어 목록에 기록한다. 패배나 엔드리스 종료는 오퍼레이터 스테이지 보상을 해금하지 않는다.

### 의도적으로 하지 않은 것
- 스테이지 SO의 범용 `StageReward.rewardId`에 오퍼레이터 소유 상태를 중복 기록하지 않았다. 오퍼레이터의 해금 조건이 단일 원본이고, 스테이지 UI가 역으로 카탈로그를 조회하게 해 조건 변경 시 보상 표시와 실제 판정이 함께 바뀐다.
- 기존 관리 화면과 스테이지 화면 전체를 재생성하지 않았다. `OperatorUnlockUISetup`은 이름이 고정된 구매 버튼과 보상 패널만 추가해 사람이 조정한 기존 UGUI 배치를 보존한다.

### 검증
- Unity 6000.3.13f1 Pipeline 재컴파일 결과 `failed=false`, `errors=[]`를 확인했다.
- 세 오퍼레이터 에셋·카탈로그·Addressables를 레시피에서 재생성하고 `OperatorAssetValidator` 검증을 통과했다.
- 메모리 프로필로 카시아 기본 해금 → 칼리스테 300 재화 구매 → `ch1-02` 클리어 후 실비아 해금 순서를 실제 생성 카탈로그에서 검증했다.
- TitleScene에 구매 버튼과 스테이지 보상 패널을 추가하고 직렬화 참조 검증을 통과했다.
## 2026-08-23 — 로딩 트랜지션 적용 범위 정리

- `UILoadingTransitionCanvas`는 로비에서 `DefenseScene`으로 출격할 때만 사용하도록 범위를 제한했다. 스테이지·엔드리스 출격과 레거시 New Game 폴백은 로딩 연출을 유지한다.
- 오퍼레이터 콘텐츠 다운로드, 로비 내부 패널(오퍼레이터·모드·스테이지·환경설정·관리) 이동, 결과 화면의 재도전·로비 복귀는 즉시 전환한다. 같은 로비 안에서 매번 화면 전체가 닫히면 조작 흐름이 느려지고, 결과 이후의 재도전은 출격 연출로 오인될 수 있기 때문이다.
## 2026-08-24 — Enemy/Ally Studio 및 전투 콘텐츠 프리로드 파이프라인

### 결정
- 적과 아군 유닛 모두 JSON 레시피를 단일 원본으로 삼고, Definition·Catalog·Addressables 그룹을 에디터 빌더가 함께 갱신한다. 따라서 신규 종류를 추가할 때 전투 런타임 C# 클래스를 만들 필요가 없다.
- 아군 Roster는 직렬화된 `unitIds`만 보유한다. `units`는 Addressables 프리로드 결과를 넣는 비직렬화 런타임 목록으로 분리해, 오퍼레이터 번들이 유닛 Definition을 의존성으로 끌고 가지 않게 했다.
- `BattleContentCache`는 적/아군 Definition과 Addressables 핸들을 애플리케이션 실행 동안 보유한다. 씬 재로드 Retry에서는 캐시를 유지하고, 새 애플리케이션 실행의 SubsystemRegistration에서만 핸들을 해제한다.
- TitleScene의 모드 선택은 전투 씬을 열기 전에 선택된 아군 유닛과 스테이지/무한 모드의 적을 프리로드한다. `WaveManager`는 로컬 Roster의 직접 참조가 아니라 캐시에서 ID로 Definition을 해결한다.
- `AllyUnitFoundationVerifier`는 Roster 참조 동일성이 아니라 런타임 클론의 ID와 Definition 목록 계약을 검증한다. 클론은 원본 SO를 수정하지 않으므로 Play 모드 종료 후 에셋이 오염되지 않는다.

### 의도적으로 하지 않은 것
- 새 `AllyUnitManager`나 `EnemyManager`는 만들지 않았다. 다수 개체의 상태는 기존 순수 C# 인스턴스가 보유하고, 매니저는 전투 흐름과 목록 조율만 담당한다.
- `GameResultUI`의 직접 `SceneManager.LoadScene` Retry 경로는 캐시 생존을 검증하기 위해 유지했다. 전투 씬 재로드가 캐시를 무효화하지 않는 것이 이번 단계의 계약이다.
- 원격 URL은 저장소에 하드코딩하지 않았다. 실제 CDN 주소가 확정될 때 환경 변수와 Addressables 프로필 설정 메뉴를 실행한다.

### 검증
- Unity 6000.3.13f1에서 컴파일 `failed=false`, `errors=[]`.
- 아군 레시피/Definition/Catalog/Addressables 검증 통과(레시피 4개, 경고 0건), 오퍼레이터 검증 통과(기존 미등록 타워 경고 1건).
- Foundation 계약, Stage/Chapter UI 배선, WebGL Addressables 사전 검증 통과.
- Roster 5개를 Unity `ForceReserializeAssets`로 현재 스키마에 재직렬화해 이전 Definition 참조를 제거했다.

## 2026-08-24 — Calliste 전술 중계 드론 오라

### 결정
- 서포트 드론은 타워가 아니라 `AllyUnitDefinition`으로 조립되는 Calliste 아군 유닛이므로 `TacticalRelayAuraEffect : AllyUnitEffectBase`로 구현했다. 드론의 `attackRange`를 오라 반경으로 재사용해 별도 범위 필드를 중복 소유하지 않는다.
- 효과 SO는 매 틱 사거리 안의 다른 살아 있는 아군에게 이동 속도 1.2배·공격 속도 1.2배·0.2초 지속 버프를 갱신한다. 범위 이탈 뒤에는 갱신이 끊기고 런타임 인스턴스의 만료 시각으로 자연 해제된다.
- 임시 배율 상태는 `AllyUnitInstance`에 `(공급 유닛, 효과 SO)`별로 보관한다. 같은 드론의 매 프레임 갱신은 항목을 늘리지 않고 만료만 연장하며, 서로 다른 드론은 기존 아군 오라의 의도와 같이 곱연산 중첩된다. 공유 SO에는 런타임 상태를 두지 않는다.
- 공격 속도는 공격 직후 인터벌을 한 번 나누는 대신 현재 쿨다운의 감소 속도에 배율을 적용한다. 그래야 오라 진입 즉시 빨라지고 이탈 즉시 원래 속도로 돌아오며, 이미 줄인 쿨다운이 범위 밖에서도 남는 스냅샷 문제를 피할 수 있다.
- `TacticalRelayAuraEffectBuilder`가 공용 효과 SO를 고정 기본값으로 생성하고 `calliste-drone.json`을 통해 Definition을 다시 빌드한다. 신규 유닛은 이 효과 에셋 경로를 레시피에 조립하기만 하면 같은 동작을 재사용할 수 있다.

### 의도적으로 하지 않은 것
- 드론 전용 `AllyUnitInstance` 파생 클래스, MonoBehaviour, Manager를 만들지 않았다. 기존 공용 유닛 인스턴스·View·배치 흐름을 그대로 사용한다.
- 드론 자신에게는 오라를 적용하지 않았다. 자기 강화가 허용되면 단독 배치에서도 지원 유닛의 이동·공격 보정이 생겨 “주변 아군 지원” 역할과 달라지기 때문이다.
- `AllyUnitData`에 별도 auraRange를 추가하지 않았다. 기획에서 지정한 `attackRange`가 이미 이 유닛의 효과 범위를 표현하며, 필드를 추가하면 레시피·Studio·검증기·카탈로그 스키마까지 같은 의미가 중복된다.

### 검증
- Unity 6000.3.13f1 Pipeline 재컴파일 결과 `completed`, `failed=false`, `errors=[]`를 확인했다.
- `AllyUnitCombatVerifier`를 23개 시나리오로 확장해 사거리 안/밖 판정, 자기 제외, 이동 속도 적용, 0.2초 갱신 중단 후 만료, 현재 공격 쿨다운의 공속 배율 적용을 검증했다.
- Unity Editor API로 생성된 효과 SO의 직렬화 값을 이동 1.2·공격 1.2·지속 0.2로 확인하고, Calliste 드론 Definition의 첫 효과 참조가 해당 SO를 가리키는지 확인했다.

## 2026-08-24 — 아군 유닛 범용 오라 비주얼 훅

### 결정
- 게임 규칙을 실행하는 `AllyUnitEffectBase`와 표현만 담당하는 `AllyUnitVisualEffectBase`를 분리했다. 버프 판정이 없어도 사거리 표시를 재사용할 수 있고, 힐·디버프 드론이 같은 범위 표현에 서로 다른 색과 주기만 조립할 수 있게 하기 위함이다.
- 비주얼 SO는 Material·HDR 색상·파동 시간·간격·선 두께·글로우·광택·불투명도만 보유한다. 애니메이션 진행도와 생성된 Renderer는 `AllyUnitView`가 소유하는 `IAllyUnitVisualRuntime`에 두어 공유 SO의 무상태 계약을 유지했다.
- 범위 원은 View의 자식으로 두지 않고 독립 월드 공간 Quad로 생성한다. View 루트에는 스프라이트 맞춤 스케일과 진행 방향 회전이 적용되므로 자식으로 두면 원이 타원으로 찌그러지거나 함께 회전할 수 있기 때문이다. Quad의 지름은 매 프레임 실제 `attackRange × 2`와 동기화한다.
- `RangePulseAura.shader`는 중심에서 최대 반경까지 이동하는 얇은 스트로크, 넓고 약한 외곽 글로우, 방향성 흰색 하이라이트를 합성한다. 모든 인스턴스는 공용 Material을 공유하고 색상·진행도는 `MaterialPropertyBlock`으로 전달해 유닛별 Material 복제를 피했다.
- 아군 레시피에 `visualEffectPaths`를 추가하고 Builder·Validator·Migrator·Ally Unit Studio를 함께 확장했다. 칼리스테 드론은 청록색 버프 SO를 연결했으며, 이후 힐은 녹색, 디버프는 적색/보라색 SO를 같은 슬롯에 조립하면 된다.

### 의도적으로 하지 않은 것
- 별도 비주얼 Manager나 오라 전용 MonoBehaviour를 만들지 않았다. 효과 수명은 이미 유닛 View 수명과 같으므로 View가 런타임 표현 객체를 생성·Tick·해제하는 편이 소유 관계가 명확하다.
- 버프/힐/디버프 종류 enum을 런타임에 넣어 색상을 분기하지 않았다. 의미와 색상을 코드에 묶으면 새 지원 타입마다 분기가 늘어나므로, 시각 차이는 비주얼 SO 에셋 조립으로 남겼다.
- WebGL 플레이어 전체 빌드는 실행하지 않았다. 이번 변경의 검증 대상은 C# 컴파일, URP 셰이더 임포트, 에셋 배선과 실제 렌더 결과이며 플레이어 패키징 회귀가 아니기 때문이다.

### 검증
- Unity 6000.3.13f1 Pipeline 재컴파일 결과 `completed`, `failed=false`, `errors=[]`를 확인했다.
- 전술 중계 오라 Builder로 Shader·공용 Material·칼리스테 버프 비주얼 SO를 생성하고, 드론 Definition의 게임플레이 효과와 비주얼 효과 참조를 함께 검증했다.
- 아군 에셋 검증 6개 레시피·경고 0건, 기존 전투 코어 23개 시나리오, 공용 View 프리팹 검증을 모두 통과했다.
- Play Mode 임시 카메라에서 반경 10의 중간 파동을 실제 URP로 캡처해 얇은 청록 스트로크·외곽 글로우·하이라이트와 드론 중심 정렬을 확인했다. 프리뷰는 씬을 저장하지 않았고 생성한 캡처 에셋도 삭제했다.
- `UILoadingTransitionCanvas`는 메인 로비 안에서 기능 화면을 이동할 때 사용한다. 메인 메뉴↔오퍼레이터 선택·관리·환경설정, 오퍼레이터 선택→모드 선택→스테이지 선택의 패널 교체를 화면이 덮인 프레임에 실행한다.
- `DefenseScene` 출격과 결과 화면의 재도전·로비 복귀는 일반 씬 로딩을 사용한다. 로비 UI 전환 연출을 실제 씬 로딩 시간과 분리해 0.6초 진입·0.6초 퇴장 리듬을 일정하게 유지하기 위함이다.

## 2026-08-24 — 합류 이력 초기화와 재생 분리

- `합류 이력 초기화`는 진행 중인 획득 코루틴과 캐시를 먼저 취소하고 표시 이력만 비운다. 초기화 직후 재생까지 자동으로 시작하면 연출 완료 시 같은 이력이 다시 저장되어 초기화가 실패한 것처럼 보였기 때문이다.
- `합류 재생`은 별도 버튼으로 두고 현재 프로필에서 해금됐지만 표시되지 않은 오퍼레이터의 큐를 새로 만든다. 기본 지급 오퍼레이터의 무음 등록 규칙은 유지한다.

## 2026-08-24 — 계정 진행 디버그 조작

- TitleScene 에디터 전용 호감도 패널에 `재화 +1000`과 `완전 초기화`를 추가했다. 전자는 `PlayerProfile.commodity`만 증가시켜 구매형 오퍼레이터를 즉시 검증할 수 있게 한다.
- 완전 초기화는 새 `PlayerProfile`을 저장해 재화, 구매 소유, 스테이지 클리어, 최고 웨이브, 호감도, 합류 연출 이력, 귀환 예약을 함께 초기화하고 현재 오퍼레이터 선택 세션도 비운 뒤 TitleScene을 다시 로드한다. 여러 UI가 보유한 이전 프로필 캐시까지 확실히 제거하기 위함이다.

## 2026-08-24 — 합류 디버그 패널과 카탈로그 복구

- 디버그 패널 생성기는 중첩된 `UILoadingTransitionCanvas`가 아니라 TitleScene 루트의 메인 `Canvas`만 선택한다. 기존에 잘못 생성된 패널도 메인 Canvas로 옮겨 로딩 Canvas의 표시·입력 상태를 상속하지 않게 했다.
- 레시피 3개를 기준으로 OperatorCatalog와 Addressables를 다시 생성해 누락됐던 실비아(`racing`)를 복구했다.
- 로비 메뉴 스프라이트의 투명 클릭 판정은 원본 텍스처가 Read/Write 가능할 때만 적용한다. 아트 교체 후 읽기 불가 이미지가 들어와도 `Awake` 예외로 UI 초기화가 중단되는 것을 막는다.

### 검증
- 플레이 모드 UGUI 레이캐스트에서 `Canvas/OperatorAffinityDebugOverlay/ResetOperatorAcquisition`이 버튼 중심의 최상위 입력 대상으로 판정됨을 확인했다.
- 합류 이력 초기화 직후와 지연 확인 모두 목록이 비어 있었고 큐가 0명으로 유지됐다.
- 별도 합류 재생 버튼은 칼리스테와 실비아 2명의 큐를 생성하고 획득 화면을 `open=true`, `alpha=1`로 표시했다.

## 2026-08-24 — 로비 리크루트 Shop 기본 내비게이션

- 하단 `RecruitButton`은 로딩 와이프가 덮인 동안 `MainMenuBackground`를 숨기고 `ShopPanelBackground`를 연다. Shop의 Back 버튼은 같은 경로로 로비에 복귀한다.
- Shop의 `RecruitOperatorButton`과 `BackButton`은 기존 Image 위에 UGUI Button과 SpriteSwap만 배선했다. 모집 대상·가격·소유 처리와 같은 오퍼레이터 데이터 로직은 의도적으로 연결하지 않았다.
- Shop 패널은 아트 편집을 위해 씬에서는 켜 둘 수 있고, `LobbyShopPanelUI.Awake()`가 런타임 시작 시 Shop만 숨긴다. 타이틀과 메인 로비의 초기 활성 상태는 기존 `TitleSceneController`가 계속 소유한다.
- Normal/Hover PNG가 자동 슬라이스에서 서로 다른 투명·발광 여백으로 잘려 버튼 본체가 축소·이동해 보이는 문제를 막기 위해, 네 상태 이미지는 전체 캔버스를 사용하는 Single Sprite로 통일한다. 최초 변환 시 기존 Normal의 보이는 범위가 유지되도록 RectTransform 크기와 중심을 함께 보정한다.

## 2026-08-24 — 리크루트 Shop 오퍼레이터 데이터 바인딩

- Operator Studio 레시피에 상점용 큰 초상화, 하단 상반신 초상화, 이명, 짧은 대사를 추가했다. 생성기가 이 값을 `OperatorDefinition`과 로컬 `OperatorCatalog`에 함께 복제하므로 신규 구매형 오퍼레이터는 C# 수정 없이 레시피 입력만으로 Shop 후보에 들어온다.
- Shop UI는 카탈로그에서 `CommodityPurchase` 해금 방식만 필터링해 목록으로 보유한다. 현재는 칼리스테 한 명을 표시하지만 이전·다음 선택 API와 선택 인덱스를 미리 두어 후보가 늘어날 때 버튼 배선만 추가하면 된다.
- 구매는 기존 `PlayerProfile.TryPurchaseOperator`를 그대로 사용해 재화 차감과 소유 기록의 원본을 중복 만들지 않는다. 성공 시 프로필 저장, 로비 재화 갱신, 기존 신규 오퍼레이터 합류 연출 호출까지 한 경로로 처리한다.
- 원격 오퍼레이터의 상점 이미지는 기존 선택·관리 초상화와 같은 이유로 로컬 카탈로그에 직접 참조하지 않는다. 원격 그룹의 아트가 본체 빌드로 새는 것을 검증기가 차단한다.
- 칼리스테 레시피에는 현재 TitleScene 목업에 배치된 `오퍼레이터관리_칼리스테.png`, `CalisteShop.png`, 이명 `Bartender`, 상점 대사를 이관했다. 가격 표시는 목업의 임시 문자열이 아니라 레시피의 실제 `purchasePrice`를 사용한다.

### 검증
- Unity 6000.3.13f1 Pipeline 재컴파일 결과 `failed=false`, `errors=[]`를 확인했다.
- 칼리스테 단일 생성으로 Definition·Catalog·Addressables 메타데이터를 갱신하고 `OperatorAssetValidator` 검증을 통과했다.
- Edit Mode에서 `LobbyShopPanelSetup`을 실행해 Shop 이미지 2개, 텍스트 5개, 구매 버튼, 카탈로그 참조가 모두 직렬화됐는지 검증했다. Play Mode는 사용하지 않았다.

## 2026-08-24 — 리크루트 Shop 고유 유닛 미리보기

- 우측 `OPERATOR INFO` 영역에 고유 유닛 슬롯 2개를 배치하고, 별도 상점용 데이터를 만들지 않은 채 `OperatorCatalogEntry.unitPreviews`의 앞 두 항목을 표시한다. 이름·아이콘·배치 CP는 AllyUnitRoster에서 카탈로그 생성 시 이미 파생되므로 로스터 변경이 상점에도 자동 반영된다.
- 상점 컨트롤러는 구매형 오퍼레이터 선택이 바뀔 때 두 슬롯을 다시 그린다. 유닛이 2개 미만이면 남는 슬롯만 숨겨 빈 데이터가 이전 오퍼레이터의 정보로 남지 않게 한다.
- 기존 선택 화면용 `OperatorRosterPreviewItem`의 데이터 바인딩 계약을 재사용하되, 상점 전용 크기와 청색 윤곽 스타일은 씬 인스턴스로 분리했다. 공용 프리팹 스타일 변경이 상점 레이아웃을 되돌리는 것을 막기 위함이다.

### 검증
- Unity 6000.3.13f1 Pipeline 재컴파일 결과 `failed=false`, `errors=[]`를 확인했다.
- Edit Mode 배선 검증에서 고유 유닛 슬롯 정확히 2개, 칼리스테 카탈로그와 일치하는 이름·CP 비용·아이콘 참조를 확인했다. Play Mode는 사용하지 않았다.

## 2026-08-24 — Shop 구매 직후 합류 연출 표시

- `GachaGainBackground`가 `ShopPanelBackground`보다 앞에 오도록 합류 연출을 시작할 때 최상단 형제 순서로 이동한다. 기존에는 구매 성공 후 연출 큐가 생성돼도 Shop이 더 뒤에 배치돼 있어 화면에 가려졌다.
- `PresentNewlyUnlocked`는 초기 `Start()` 이전에도 사용할 수 있도록 프로필 저장소를 필요 시 직접 준비한다. 따라서 구매 버튼이 호출한 같은 프레임에 합류 연출을 시작할 수 있다.

## 2026-08-24 — Shop 좌측 탭 및 스테이지 보상 합류 연출

- `ShopPanelBackground/LeftFrame/VerticalLayout`에만 `LeftPanelSheet` 상태를 배선했다. Recruit는 현재 페이지를 나타내므로 4번 선택 스프라이트를 상시 유지하고, Exchange·Enhance·Material은 각각 1→5, 2→6, 3→7로 포인터 호버 중에만 전환한다. 로비의 `MainMenuBackground/underPanel`은 별개 UI이므로 건드리지 않는다.
- TitleScene의 `GachaGainBackground`를 `OperatorAcquisitionOverlay` 프리팹으로 보존하고 DefenseScene Canvas에 결과 전용 인스턴스를 배치했다. 같은 연출 자산을 두 씬에서 공유해 스테이지 보상과 Shop 구매가 서로 다른 획득 화면으로 갈라지지 않게 한다.
- 결과 전용 합류 UI는 씬 시작 자동 검사를 끈다. `GameResultUI`가 승리한 스테이지 ID를 프로필에 저장한 다음 그 스테이지의 미표시 보상만 호출하므로, 1-2의 실비아는 결과 패널 위에서 즉시 등장하고 이미 표시한 보상은 재클리어 시 반복되지 않는다.

## 2026-08-24 — 리크루트 구매 완료 버튼 상태

- 구매형 오퍼레이터가 이미 프로필에 소유된 경우 `RecruitOperatorButton_AlreadyPurchased`를 고정 표시하고 버튼 입력을 막는다. 구매 전에는 기존 Normal/Hover SpriteSwap을 복원하므로, 향후 구매형 오퍼레이터가 여러 명으로 늘어나 선택을 이동해도 각 항목의 소유 상태에 맞춰 버튼 외형이 갱신된다.

## 2026-08-25 — 로비 귀환 친밀도 상승 토스트

- 전투 귀환 보상은 TitleScene의 메인 로비 전환이 끝난 직후 정산하고, 기존 `MainMenuBackground/IntimacyElevationText`를 위로 떠오르며 사라지는 알림으로 재사용한다. 결과 화면에서 바로 올리지 않아 귀환이라는 흐름을 유지하면서도, 오퍼레이터를 클릭하기 전 상승 사실을 확인할 수 있게 하기 위함이다.
- 정산된 귀환 대사 종류는 메모리에 보존해 다음 오퍼레이터 클릭에서 기존 참전/비참전 대사가 그대로 출력된다. 디버그처럼 이미 열린 로비에서 귀환을 예약한 경우에는 클릭 시 정산하는 기존 폴백도 유지한다.
- 알림에는 요청 보상량이 아니라 100 상한 적용 후의 실제 증가량만 표시한다. 이미 최대 친밀도라 증가량이 0이면 귀환 예약은 소비하되 잘못된 상승 알림은 띄우지 않는다.
- 토스트 애니메이션은 `Time.unscaledDeltaTime` 기반 수동 타이머로 처리하고 입력을 차단하지 않는다. 별도 매니저나 영속 알림 상태는 만들지 않았다.

## 2026-08-25 — Stage Studio 전투 배경·경로 제작 작업대

### 결정
- `StageDefinition`이 선택 화면의 `descriptionBackground`와 별도로 전투용 `battleBackground`, 위치·크기, `routePoints`, 곡선 보간 설정을 소유한다. 첫 점은 적 생성점, 마지막 점은 거점 및 아군 출격점으로 고정해 별도 랠리 좌표와 경로 끝이 어긋나는 상태를 만들지 않는다.
- 스테이지마다 DefenseScene을 복제해 런타임 콘텐츠로 사용하지 않는다. `StageRouteTestScene` 한 장만 제작 작업대로 두고, Scene View에서 옮긴 Transform 좌표를 최종 제작 원본인 `StageDefinition`으로 캡처한다.
- `MapManager`는 스테이지 모드에서 `StageDefinition.routePoints`와 전투 배경을 우선 적용하고, 엔드리스 모드에서는 기존 씬 Transform 경로를 유지한다. 적·아군은 계속 공용 `MapManager.Waypoints`만 소비하므로 전투 인스턴스 코드는 변경하지 않았다.
- 테스트 씬의 경로 표시는 MonoBehaviour를 붙이지 않고 Editor의 Scene View 콜백으로 그린다. 테스트용 시각화가 플레이어 빌드와 씬 직렬화 계약에 들어가지 않게 하기 위함이다.

### 제작 도구
- Stage Studio에 `Map` 탭을 추가해 전투 배경, 위치·크기, 경로 보간값과 좌표 목록을 편집한다.
- `Open Test Scene & Load Selected Stage`는 공용 DefenseScene 복사본에 선택 스테이지의 배경과 경로 점을 생성한다. `Capture Test Scene Into Selected Stage`는 Scene View에서 수정한 좌표·배경 Transform·보간값을 SO에 되돌려 저장한다.
- 기존 DefenseScene의 9개 웨이포인트를 CH1 7개 StageDefinition의 초기 경로로 이관했다. 런타임 DefenseScene에는 스테이지 배경을 표시할 공용 SpriteRenderer 하나만 추가했다.

### 검증
- Unity 6000.3.13f1 재컴파일 결과 `failed=false`, `errors=[]`를 확인했다.
- 1-1의 9개 경로 점을 테스트 씬에 로드한 뒤 다시 Definition으로 캡처하는 왕복을 확인했다.
- Stage Validator는 7개 스테이지에서 오류 0건을 확인했다. 아직 제작되지 않은 설명 배경·전투 배경·보상은 각 스테이지별 경고로 유지한다.
- Play Mode와 플레이어 빌드는 실행하지 않았다. 이번 검증은 Edit Mode 에셋 왕복, 씬 배선, 컴파일과 데이터 검증 범위다.

## 2026-08-25 — 오로라(Aurora) 대사 스크립트 및 상황별 표정 스프라이트 바인딩

### 결정
- 오로라(Aurora)의 천재 해커/전자전 스페셜리스트 컨셉에 맞춰 전체 18개 상황 슬롯(로비 상호작용, 귀환, 호감도 5단계 터치, 전투 개시, 스킬 발동, 거점 피격, 플레이어 일반/위기 피격, 건설 실패 2종, 패배 2종)의 대사 스크립트를 완성했다.
- `Assets/Art/Character Standing Arts/오로라/레베카/`의 감정별 스탠딩 스프라이트(`smile-1`, `curious-1`, `annoyed-1/2/3`, `happy smile`, `smug`, `flustered-1`, `blushing shyly-1/2/3`, `fidgeting shyly`, `aroused-1` 등)와 전투 치비 포트레잇(`chibby_portrait_1~12.png`)을 각 대사의 감정에 맞춰 1:1로 매핑했다.
- 자동화 빌더 `AuroraDialogueBuilder`를 추가해 `RCCom/Operators/Build Aurora Dialogue` 메뉴로 언제든 `OperatorDialogueSet.asset`을 멱등하게 재생성·갱신할 수 있게 했다.
- `Aurora.json` 레시피에 `playStyleDescription`, `alternateName`("Cyber Hacker"), `shopDialogue`를 함께 동기화했다.

### 의도적으로 하지 않은 것
- 기존 타워/카드/유닛 SO나 다른 오퍼레이터의 대사 데이터를 임의로 수정하지 않았다.
- 불필요한 C# 클래스 분기를 만들지 않고 기존 `OperatorDialogueSet` 및 `OperatorDialogueEntry` 직렬화 구조를 그대로 따랐다.

## 2026-08-25 — 오퍼레이터 강화 트랙 정밀 데이터화

### 결정
- 강화 한 행을 `OperatorUpgradeTrack`, 실제 전투 수치 변경을 그 안의 `OperatorUpgradeModifier` 목록으로 분리했다. 한 번의 구매로 여러 유닛에 서로 다른 수치를 적용할 수 있어 오로라의 드론 장갑처럼 UI에서는 한 트랙이지만 대상별 증가량이 다른 기획도 별도 트랙으로 쪼개지 않는다.
- 레벨 수치는 `레벨 × 고정 델타`가 아니라 Lv1~Lv8의 정확한 누적 델타 표로 저장한다. 정수 반올림이 있는 CP·배치 비용과 비선형 기획값이 설계표와 어긋나지 않게 하기 위함이다.
- Core/Support별 레벨 비용표와 호감도 요구 표를 트랙 데이터에 포함했다. 현재 호감도 요구는 전부 0으로 두되, 이후 콘텐츠 조정은 코드 수정 없이 Operator Studio에서 가능하다.
- 구매 조건 확인, 재화 차감, 레벨 상승은 `PlayerProfile.TryPurchaseUpgradeLevel` 한 호출에서 처리한다. UI 중복 클릭으로 재화만 차감되는 중간 상태를 만들지 않는다.
- 전투용 강화 Definition 캐시는 DefenseScene 진입마다 복제 효과 SO와 함께 제거한다. 같은 실행 중 상점에서 올린 강화가 다음 전투에 즉시 반영되고 Retry 누적 메모리가 남지 않게 하기 위함이다.

### 도구와 검증
- Operator Studio의 Upgrades 탭에서 5개 트랙, 비용·호감도 표, 복수 modifier와 레벨별 델타를 편집하도록 확장했다. 빌더는 트랙 수, 표 길이, 비용, 호감도 범위, Roster Unit ID, 중복 대상을 검증한다.
- Cassia, Calliste, Aurora, Racing 레시피를 5트랙·8레벨 데이터로 변환했고 Racing의 이동 강화 대상을 Heavy가 아닌 Pit Crew로 바로잡았다.
- Unity 6000.3.13f1 재컴파일 결과 `failed=false`, `errors=[]`를 확인했다. Play Mode와 플레이어 빌드는 실행하지 않았다.

## 2026-08-25 — 복수 해금 조건과 리크루트 Shop 순환 선택

### 결정
- 오퍼레이터 해금 조건을 단일 열거형에서 조건 목록으로 확장하고, 목록 안의 어느 하나라도 만족하면 획득되는 OR 규칙으로 통일했다. 기존 Definition과 저장 데이터의 호환을 위해 목록이 비어 있을 때만 구형 단일 필드를 읽는다.
- 칼리스테는 골드 구매, 오로라는 최고 웨이브 또는 골드 구매, 실비아는 `ch1-02` 클리어 또는 골드 구매로 설정했다. 따라서 스테이지 보상으로 먼저 획득한 오퍼레이터를 Shop에서 다시 구매할 수 없다.
- 리크루트 Shop 목록은 골드 구매 조건을 포함한 카탈로그 항목을 자동 수집한다. 중앙에는 선택된 밝은 상반신 이미지를, 좌우에는 각 오퍼레이터별로 미리 전처리한 어두운 상반신 이미지를 표시하며 버튼으로 인덱스를 순환한다.
- 어두운 상반신 이미지는 밝은 이미지에 런타임 색상을 곱해 흉내 내지 않고 `OperatorDefinition`의 독립 에셋으로 둔다. 아트가 이미 전처리되어 있고 오퍼레이터마다 명암 표현이 다르므로, Operator Studio와 JSON 레시피 양쪽에서 직접 지정하게 했다.
- 상점 이명과 짧은 대사는 창작 데이터이므로 비어 있어도 구조 검증을 실패시키지 않고 경고한다. 반면 구매 가격과 메인·밝은·어두운 초상화는 기능에 필요한 값이라 계속 오류로 취급한다.

### 검증
- 카시아·칼리스테·오로라·실비아 Definition과 OperatorCatalog를 에디터 빌더로 갱신했다.
- `LobbyShopPanelSetup`으로 TitleScene의 중앙/좌/우 슬롯, 잠금 표시, 이름, 좌우 버튼을 연결했고 Edit Mode 배선 검증을 통과했다.
- Play Mode와 플레이어 빌드는 실행하지 않았다.

## 2026-08-25 — 로비·Shop 전신 이미지 종횡비 보호

- 로비 `OperatorImage`와 리크루트 Shop 중앙 `OperatorPortrait`가 원본 전신 비율을 유지하도록 `Image.preserveAspect`를 씬과 런타임 양쪽에서 강제했다. 현재 오로라 원본과 RectTransform은 모두 832×1216이고 상위 X/Y 배율도 동일했으므로, 기존 데이터 자체를 재가공하지 않고 이후 해상도·레이아웃 변화에서 발생할 수 있는 비균일 확대만 차단했다.
- TitleScene 저장값에서 두 Image 모두 `m_PreserveAspect: 1`임을 확인했고 로비 대사·Shop 배선 검증을 통과했다. Play Mode와 플레이어 빌드는 실행하지 않았다.

## 2026-08-25 — 보유 오퍼레이터 강화 uGUI

### 결정
- 강화 화면은 별도 전신·하단 카드 UI를 복제하지 않고 `ShopPanelBackground/OperatorPanel`과 `UnderPanel`을 리크루트 화면과 공유한다. 탭별 컨트롤러만 교대로 활성화해 같은 아트의 위치와 크기가 두 화면에서 갈라지지 않게 했다.
- 강화 대상 목록에는 현재 프로필에서 해금된 오퍼레이터만 포함한다. 보유자가 1명이면 중앙 슬롯만 채우고 좌우는 기존 `LockSprite`만 표시하며, 2명이면 실제 이웃 한 칸과 빈 잠금 한 칸, 3명 이상이면 이전·다음 오퍼레이터를 순환 표시한다.
- 중앙과 좌우 카드는 기존 상점용 밝은·전처리된 어두운 상반신 이미지를 재사용한다. 전용 이미지가 아직 없는 카시아 등은 관리/미리보기 초상화로 폴백하므로 기능 구현과 최종 아트 교체를 분리했다.
- 강화 데이터 열람은 선택 중인 전투 오퍼레이터를 변경하지 않고 해당 카탈로그 주소의 `OperatorDefinition`만 Addressables로 읽는다. 탭 종료·대상 변경 시 소유한 핸들을 해제해 로비 탐색이 세션 로드아웃이나 메모리 수명을 바꾸지 않게 했다.
- 실제 강화 구매는 기존 `PlayerProfile.TryPurchaseUpgradeLevel`의 원자적 조건 확인·재화 차감·레벨 상승 계약을 그대로 사용한다. UI가 강화 규칙을 재구현하지 않으며 구매 후 로비 재화 표시만 즉시 갱신한다.

### uGUI와 검증
- 우측 임시 강화 패널에 트랙 5개, 현재/다음 레벨, 설명, 비용·보유 재화, 상태, Enhance/Back 버튼을 생성했다. 최종 스프라이트가 준비되면 이 오브젝트들의 Image와 RectTransform만 교체할 수 있도록 기능 배선을 분리했다.
- 로비 하단 Enhance 버튼은 로딩 트랜지션을 거쳐 강화 탭으로 진입하고, Shop의 Recruit/Enhance 좌측 탭은 `LeftPanelSheet`의 기존 Normal/Selected 스프라이트를 유지하며 즉시 전환한다.
- `OperatorEnhancePanelSetup`을 멱등한 Edit Mode 생성·검증 도구로 추가했다. 모든 카탈로그, 공용 패널, 트랙 행, 버튼과 탭 스프라이트 참조 및 TitleScene 기본 비활성 상태를 검사한다.
- Unity 6000.3.13f1 재컴파일 결과 `failed=false`, `errors=[]`, 강화 UI 전용 배선 검증 통과를 확인했다. Play Mode와 플레이어 빌드는 실행하지 않았다.

## 2026-08-25 — 발렌티나 대사 표정 Missing 참조 복구

### 결정
- `valentina-dialogue-set.md`의 번호가 붙은 대사 행에서 Primary 표정 121개를 순서대로 읽어 `OperatorDialogueSet.asset`의 18개 상황·121개 문장에 연결했다.
- 전신은 `발렌티나.{표정}.png`, 전투 포트레잇은 `portrait/발렌티나.Chibby.{표정}.png`라는 실제 에셋 경로에서만 로드한다. 어느 하나라도 없거나 표의 행 수와 대사 수가 다르면 저장 전에 예외를 내므로, 존재하지 않는 파일을 추측해 Missing 참조를 만들지 않는다.
- `ValentinaDialogueBuilder`를 남겨 문서 또는 아트가 갱신된 뒤에도 `RCCom/Operators/Build Valentina Dialogue`로 같은 데이터 배선을 재현할 수 있게 했다.

### 검증
- Unity 에디터 API로 `OperatorDialogueSet.asset`을 저장했다.
- 18개 상황의 기본 전신·포트레잇과 121개 문장별 전신·포트레잇 참조가 모두 비어 있지 않음을 검증했다.
- Unity 6000.3.13f1 재컴파일 결과 `failed=false`, `errors=[]`를 확인했다. Play Mode와 플레이어 빌드는 실행하지 않았다.

## 2026-08-25 — 라이브 드랍: 플레이어 재빌드 없이 신규 콘텐츠 제공

### 배경 — 무엇이 막혀 있었나

"Addressable만 빌드해 서빙하면 신규 오퍼레이터를 제공할 수 있는가"를 실제 저장소 상태로
확인한 결과 **불가능**이었다. 차단점이 두 개였다.

1. **발견 인덱스가 빌드에 박혀 있었다.** `OperatorCatalog`는 씬 UI 6곳이
   `[SerializeField]`로 직접 참조해 플레이어 데이터에 통째로 직렬화된다(클래스 주석도
   "빌드에 항상 포함되어"라고 명시). Definition과 초상화는 원격 그룹에 있어 CDN에서
   받을 수 있었지만, "그런 오퍼레이터가 존재한다"는 사실이 빌드 안에 있어서 원격 번들을
   아무리 잘 올려도 구 플레이어의 선택 화면에는 끝내 나타나지 않았다. 스테이지는 더해서
   `StageCatalogEntry`가 `StageDefinition`을 하드 참조하고 있어 Addressables에 아예
   올라가 있지도 않았다.
2. **Content Update 워크플로가 없었다.** `BuildScript`는 `BuildPlayerContent`(=New Build)만
   호출했고 저장소 전체에 `ContentUpdateScript` 참조가 0건이었다. New Build는 카탈로그를
   통째로 새로 만드는데, `BuildRemoteCatalog=1` / `DisableCatalogUpdateOnStart=0`이라
   구 플레이어가 부팅 때 그 카탈로그로 갈아탄다. 그 카탈로그에는 로컬 그룹 엔트리까지
   들어 있고 로드 경로가 플레이어 자신의 StreamingAssets를 가리킨다.

두 번째는 이론이 아니라 이미 벌어져 있었다. `Builds/WebGL`(08-24)의 monoscripts 번들은
`…_cbdfbb56…`인데 서빙 중이던 `ServerData/WebGL/catalog_1.0.bin`(08-25)은
`…_466d9331…`을 가리키고 있었다. 그 상태로 배포됐다면 적 3종을 뺀 오퍼레이터·유닛
로딩이 광범위하게 깨졌을 것이다.

### 결정

- **로컬 그룹은 StaticContent(Prevent Updates)로 묶는다.** 원격 카탈로그가 로컬 번들 해시를
  흔들지 못하게 하는 유일한 장치다. 세 카탈로그 빌더에 똑같이 복사되어 있던 그룹 설정
  블록(약 70줄 × 3)을 `AddressableGroupPolicy` 하나로 모으고 그 정책을 거기에 뒀다.
  그룹의 로컬·원격 판정은 이름(`-Remote` 접미사)이 아니라 실제 BuildPath 변수로 한다 —
  빌더가 만들지 않은 손수 만든 그룹도 같은 기준으로 다뤄야 하기 때문이다.
- **콘텐츠 상태 파일을 릴리스별로 커밋한다.** `addressables_content_state.bin`이 없으면
  이미 배포된 빌드에는 두 번 다시 콘텐츠를 내려보낼 수 없는데, 기본 경로의 그 파일은
  `.gitignore` 대상이고 콘텐츠를 다시 구울 때마다 덮어써진다. 플레이어 빌드가 **성공한
  뒤에만** `ReleaseStates/{타깃}/{bundleVersion}/`으로 복사한다. 실패한 빌드의 상태를
  남기면 이후 드랍이 배포된 적 없는 기준으로 나가기 때문이다.
- **라이브 카탈로그는 원격 항목만 담는다.** 정본 카탈로그를 통째로 원격에 올리는 쪽이
  단순하지만, 그러면 로컬 오퍼레이터가 직접 참조하는 스프라이트가 전부 원격 번들의
  의존으로 딸려 들어가 번들이 비대해지고 static으로 묶은 로컬 그룹과 교차 의존이 생긴다.
  원격 항목은 `CreateEntry`가 이미 모든 Sprite 참조를 null로 비우고 주소 문자열만 남기므로
  (원격 콘텐츠가 본체 빌드로 새어 나가지 않게 하려던 기존 규칙이 여기서 그대로 값을 한다)
  라이브 카탈로그는 로컬 에셋 의존이 0이 된다. 그 불변식이 깨지면 빌드가 서도록 검사를 넣었다.
- **병합은 추가 전용이다.** 이미 빌드에 있는 항목은 내장본을 그대로 쓴다. 기존 항목까지
  원격본으로 갈아치우면 위의 교차 의존 문제가 그대로 돌아온다. 즉 이 구조로 되는 것은
  "신규 항목 추가"이고, "기존 항목의 원격 교체"는 의도적으로 범위 밖이다.
- **`LiveCatalogService`는 어떤 MonoBehaviour에도 매달지 않는다.** 앱 수명 전체를 사는
  것이라 `RuntimeInitializeOnLoadMethod`로 시작하고 완료 콜백만 쓴다(코루틴 호스트 불필요).
  씬 재로드 시 캐시를 비우지 않는데, 담고 있는 것이 세션 상태가 아니라 계정 단위 콘텐츠
  목록이라 전투 씬을 오갈 때마다 다시 받는 쪽이 오히려 잘못된 동작이기 때문이다.
- **스테이지도 오퍼레이터와 같은 구조로 맞춘다.** 표시용 경량 메타데이터(권장 레벨, 배경,
  웨이브 유무)는 카탈로그에 복사하고 실행 데이터는 `stage/{id}` 주소로 받는다. 선택 화면이
  목록을 그리는 것만으로 모든 스테이지의 웨이브·적 참조가 메모리에 올라오던 문제도 같이
  없어진다. `stageDefinition` 직접 참조는 로컬 스테이지용으로 남겼다 — 카탈로그를 아직 다시
  굽지 않은 저장소에서도 기존 스테이지가 돌아야 하고, 이미 빌드에 든 스테이지를 굳이
  Addressables 왕복으로 다시 얻을 이유도 없다.
- **의도적으로 하지 않은 것**: `AllyUnitCatalog`와 `EnemyCatalog`는 런타임에서 아무도 읽지
  않는 에디터 저작 산출물이라(유닛·적은 `ally-unit/{id}`, `enemy/{id}` 주소로 직접 해결한다)
  라이브 대상에서 제외했다. 신규 유닛·적은 오퍼레이터/스테이지에 딸려 이미 배송된다.

### 남은 경계선

새 C# 클래스가 필요한 콘텐츠는 여전히 플레이어 재빌드가 필요하다. 번들 안의 SO는 스크립트를
`MonoScript` 참조로 직렬화하고, 그것을 해결하는 monoscripts 번들의 로드 경로가
`{Addressables.RuntimePath}` — 즉 로컬이기 때문이다. WebGL은 IL2CPP AOT라 어셈블리 동적
로드라는 우회로도 없다. 더 강하게는, **C# 코드를 한 줄이라도 바꾸면** monoscripts 번들 해시가
바뀌므로 드랍 전용 작업 중에는 런타임 어셈블리를 건드리면 안 된다.

따라서 재빌드 없이 되는 것: 기존 효과 SO 조립만으로 만든 신규 오퍼레이터·아군 유닛·적·스테이지,
그리고 그들의 아트·대사·수치·강화 트랙. 재빌드가 필요한 것: 새 카드 로직(`CardEffectBase` 파생),
새 타워 종류(`TowerData` 파생), 그 밖의 모든 런타임 코드 변경.

### 검증

- Unity 에디터가 실행 중이 아니어서 이 커밋 시점에는 재컴파일·에셋 생성·빌드를 수행하지 못했다.
  코드 변경만 반영되어 있고, `.asset` 갱신(그룹 정책 적용, 라이브 카탈로그 생성, 스테이지
  Addressables 등록)은 아래 순서로 에디터에서 실행해야 한다.
  1. `RCCom/Operators/Build Operator Catalog And Addressables`
  2. `RCCom/Stages/Rebuild Stage Catalog`
  3. `RCCom/Addressables/Apply Group Update Policy`
  4. `RCCom/Addressables/Validate Active Build Configuration`

## 2026-08-25 — Enemy Studio 생성물의 전투 Roster 자동 등록

- 적 전체/단일 빌드가 생성된 Definition의 `enemyId`를 전투용 `EnemyRoster`에 함께 등록하도록 묶었다. 레시피·Definition·Addressables는 정상인데 Roster 수작업 누락 때문에 절차적 웨이브에 나오지 않는 반쪽 상태를 방지하기 위함이다.
- 자동화는 기존 Roster ID를 삭제하거나 재정렬하지 않고 실제 Definition이 존재하는 레시피 ID만 추가한다. 사람이 만든 편성이나 기존 직렬화 순서를 자동 빌드가 침범하지 않게 하기 위한 보수적 동기화다.
- 적 검증기는 레시피의 Roster 미등록, 빈 ID, 중복 ID를 오류로 보고한다. Kind는 여전히 출현 조건이 아니며, 실제 출현 가능 여부는 Definition 생성·Roster 등록·`minWave` 조건으로 결정된다.

## 2026-08-25 — Enemy Studio 안전 삭제 경로

- Enemy Studio에 선택 적 삭제 기능을 추가했다. 삭제 전에 모든 StageDefinition의 웨이브 참조를 검사하고, 참조가 있으면 데이터 유실 대신 삭제를 중단한다.
- 삭제가 허용되면 레시피, 생성 Definition 폴더, EnemyRoster ID, EnemyCatalog 항목, 로컬·원격 Addressables 그룹을 함께 제거한 뒤 남은 레시피 전체를 다시 빌드·검증한다. 레시피만 지워 고아 그룹 때문에 다음 Build All이 막히는 상태를 방지하기 위함이다.
- 원본 스프라이트는 다른 적이나 후속 디자인에서 재사용할 수 있으므로 자동 삭제하지 않는다. 아트 제거는 참조 여부를 사람이 별도로 확인한 뒤 수행한다.

## 2026-08-25 — 자폭 적의 치명 접촉 Effect

- 자폭 적은 기존 `ContactDamageEffect`로 Studio의 `contactDamage`를 정확히 한 번 적용하고, 별도 `SelfDestructOnCriticalContactEffect`가 대상이 플레이어 또는 최종 거점일 때만 남은 체력과 무관하게 즉사시킨다. 피해 수치를 Effect SO에 중복 저장하지 않아 Studio가 단일 진실 공급원으로 남는다.
- 아군 유닛과 조우하면 기존 접촉 공격을 계속하고 자폭하지 않는다. 플레이어·거점만 치명 접촉으로 본다는 기획을 지키면서, 아군 전열에 막힌 자폭 적이 아무 행동도 하지 않는 상태를 피하기 위함이다.
- 거점 접촉 효과가 적 자신을 사망시킬 수 있도록 `EnemyInstance`는 접촉 효과를 도달 완료 플래그보다 먼저 실행한다. 효과가 사망시켰다면 `Died`만 발생하고 `ReachedGoal`은 중복 발생하지 않아 View와 WaveManager의 제거 경로가 하나로 유지된다.
- 자폭 Effect SO 생성, 레시피 효과 순서 배선, Definition 재생성은 에디터 빌더로 자동화했다. 검증기는 거점과 DefenseScene 플레이어의 체력을 각각 100으로 준비하고 Studio 피해 40 적용 후 60이 되는지, 자폭 적 체력이 즉시 0이 되는지, `Died`가 정확히 1회인지 확인한다. 거점 경로에서는 `ReachedGoal`이 0회인지도 함께 검증한다.

## 2026-08-25 — 힐러 적의 범위 주기 회복 Effect

- 힐러를 `EnemyKind` 분기나 별도 행동 클래스가 아니라 상태 없는 `HealNearbyEnemiesEffect` SO로 구현했다. `WaveManager`가 기존 활성 적 목록을 `EnemyContext`로 전달하고 Effect가 Studio의 `attackRange` 안에 있는 살아 있는 적만 고르므로, 적 목록의 소유권은 계속 `WaveManager`에 남고 별도 EnemyManager는 만들지 않았다.
- 회복 주기는 별도 중복 필드 대신 Studio의 `attackInterval`을 재사용한다. 힐러 레시피의 초기값은 범위 3, 주기 1.5초, 접촉 피해 0이며, 회복량 5와 자기 회복 여부(false)는 Effect SO에 직렬화했다. 모두 이후 Inspector/Studio에서 조정 가능한 회색상자 값이고 전투 코드에 밸런스 상수를 하드코딩하지 않았다.
- 여러 힐러가 같은 Effect SO를 공유해도 주기가 섞이지 않도록 Effect별 남은 시간은 각 `EnemyInstance`의 런타임 딕셔너리가 소유한다. 첫 회복은 스폰 즉시가 아니라 1.5초 뒤 발생하며 프레임 초과분은 다음 주기로 이월한다.
- 웨이브 체력 배율이 현재 체력에만 적용되던 경로를 런타임 `MaxHealth`까지 함께 갱신하도록 정리했다. 치유는 이 런타임 상한으로 캡핑하므로 후반 웨이브 적이 Definition 원본 체력까지만 회복되는 문제를 막고, SO 원본은 수정하지 않는다.
- 에디터 빌더가 힐러 Effect SO 생성, 접촉 피해 Effect 제거, 레시피 배선, Definition 재생성을 한 번에 수행한다. Unity 6000.3.13f1 배치 검증에서 실제 생성 SO/Definition 참조, 공격력 0, 범위 3, 1.5초 주기, 회복량 5, 범위 밖 제외, 자기 제외, 최대 체력 캡을 확인했다.

## 2026-08-25 — 힐러 회복 발동 원형 파장 연출

- 힐러 전용 프리팹을 새로 복제하지 않고 기존 위치 기반 공용 `ShockwaveRing` 프리팹과 `RangePulseAura` 셰이더를 재사용했다. 회복 발동 시 힐러 중심에서 Studio의 `attackRange`까지 얇은 연두색 링이 한 번 확산한 뒤 셰이더의 수명 페이드로 사라진다.
- 폭발용 붉은 파장 설정과 힐 파장을 분리하기 위해 `Enemy_HealingPulseVisual` SO를 추가했다. 초기값은 확산 0.45초, 선 두께 0.012, 녹색 HDR 색상이며 인스펙터에서 조정 가능하다. 빌더 재실행은 이미 존재하는 SO의 사람이 튜닝한 값을 덮어쓰지 않는다.
- 기존 `ShockwaveRing`의 최대 재생 시간이 0.2초로 고정돼 지원 스킬의 부드러운 파장을 표현할 수 없어서 상한만 1초로 확장했다. 폭발 SO는 계속 0.18초를 요청하므로 기존 스플래시 연출의 실제 재생 시간은 바뀌지 않는다.
- 시각 참조가 누락되어도 회복 판정은 정상 동작하도록 VFX 호출을 선택 경로로 유지했다. Unity 6000.3.13f1 배치 검증에서 컴파일, 실제 힐러 Effect의 공용 파장 프리팹·힐 전용 시각 SO 배선, 기존 1.5초 회복 계약을 함께 확인했다.

## 2026-08-25 — 공용 UI 선택 효과음

- 제공된 `Ui_select.wav`를 기존 횡단 관심사인 `SoundManager`의 공용 UI 선택음으로 추가했다. 일반 Button은 `UISelectSoundEmitter`가 포인터 누름과 키보드/패드 Submit을 감지하고, 기존 타이틀·결과·튜토리얼의 명시적 클릭음 호출도 같은 재생 경로로 모아 화면마다 음향 계약이 갈라지지 않게 했다. 포인터는 실제 onClick보다 앞선 Down 시점에 재생해 버튼 동작이 패널을 즉시 닫더라도 피드백이 유실되지 않는다.
- 한 Button에 기존 명시적 호출과 공용 Emitter가 함께 있는 과도기 상태에서도 같은 프레임에는 한 번만 재생한다. 비활성 또는 비용 부족 등으로 `interactable=false`인 버튼은 소리를 내지 않아 시각적 차단 상태와 피드백이 일치한다.
- 전투 효과음의 0.5초 강제 컷오프는 유지하고 UI 선택음만 별도 1초 컷오프를 사용한다. 두 플레이 씬의 SoundManager 참조와 모든 일반 Button 컴포넌트 배선은 Editor 도구가 수행해 `.unity`를 직접 편집하지 않고 재실행 가능하게 만들었다.

## 2026-08-25 — 리크루트 상점 전용 반복 BGM

- 제공된 `Rare_Item_Bgm.mp3`를 리크루트 화면 전용 BGM으로 추가했다. `LobbyShopPanelUI`가 로딩 커버 안에서 실제 패널을 여는 순간 `SoundManager`에 임시 반복 재생을 요청하므로 상점 화면과 음악의 전환 시점이 일치한다.
- 상점 진입 전 로비 BGM의 클립과 재생 위치를 `SoundManager` 런타임 상태로 보존한다. 뒤로 나갈 때 랜덤 곡을 새로 시작하지 않고 기존 로비곡의 같은 위치로 복귀해 메뉴 이동 때문에 청취 흐름이 매번 초기화되지 않게 했다.
- 상점 BGM은 기존 BGM AudioSource와 Mixer 그룹을 그대로 사용하므로 사용자의 Master/BGM 볼륨 설정이 별도 처리 없이 적용된다. MP3 참조는 전용 Editor 도구가 TitleScene에 배선해 `.unity` 텍스트 직접 편집을 피했다.

## 2026-08-25 — 리크루트 상점 BGM 교체

- 사용자 검수 결과에 따라 리크루트 반복곡을 `Rare_Item_Bgm`에서 `Late_Hours_Rainfall`로 교체했다. 런타임 전환·반복·로비곡 복귀 계약은 그대로 유지하고 데이터 참조만 바꿨다.
- Editor 도구는 새 참조가 TitleScene에 저장됐는지 먼저 검증한 뒤 이전 MP3를 `AssetDatabase.DeleteAsset`으로 제거한다. 교체 중 실패하더라도 씬에 Missing 오디오 참조가 남지 않도록 삭제 순서를 보수적으로 잡았다.

## 2026-08-25 — PR #20 최신 main 통합과 생성 에셋 재배선

- 충돌한 `TitleScene`과 Addressables 설정은 최신 main 산출물을 정본으로 선택한 뒤 PR의 `UISelectSoundAssetBuilder`, `ShopBgmAssetBuilder`, `EnemyCatalogBuilder`를 다시 실행해 기능을 재배선했다. Unity YAML의 fileID와 Addressables 그룹 GUID를 손으로 합치지 않고, 이미 검증 가능한 에디터 자동화를 재사용하기 위함이다.
- `LobbyShopPanelUI`는 main의 Recruit/Enhance 탭 상태 전환을 유지하면서 PR의 임시 반복 BGM 진입·복귀를 같은 로딩 커버 구간에 결합했다. 두 기능이 서로 다른 화면 상태를 소유하므로 어느 한쪽을 버릴 이유가 없다.
- 통합 후 TitleScene 80개와 DefenseScene 9개 버튼의 공용 선택음, 리크루트 BGM 참조, 적 6종 카탈로그·Roster, 자폭·힐러 전투 계약을 Unity 6000.3.13f1에서 다시 검증했으며 콘솔 오류는 없었다.

## 2026-08-25 — PR #20과 Addressables 라이브 드랍 브랜치 통합

- PR #20을 먼저 최신 main에 통합한 뒤 그 main을 라이브 드랍 브랜치로 가져왔다. 적 기능의 충돌 해결을 PR 안에 귀속시키고, 라이브 드랍 브랜치에는 Addressables 정책 결합만 남겨 두어 두 작업의 증빙과 되돌림 경계를 섞지 않기 위함이다.
- `EnemyCatalogBuilder`의 Roster 자동 등록과 `AddressableGroupPolicy` 기반 그룹 생성은 자동 병합 결과에 둘 다 보존했다. Addressables 설정은 라이브 카탈로그·스테이지 그룹이 있는 브랜치 버전을 정본으로 삼고 적 빌더를 다시 실행해 신규 적 3종 그룹을 에디터 API로 등록했다.
- Unity 6000.3.13f1에서 스크립트 재컴파일, 35개 Addressables 그룹 정책, StandaloneWindows64·WebGL 사전 검증, 적 6종 에셋, 자폭·힐러 전투 계약을 확인했다. 그룹 정책은 재실행 시 갱신 0개였고 콘솔 오류도 0건이어서 생성 결과가 현재 정책과 이미 일치한다.

## 2026-08-25 — 아군 유닛 Effect 조합 안전망과 사전 구비 효과 회귀 검증

### 결정

- 기본·관통·스플래시·맹독 공격에 `IAllyUnitPrimaryAttackEffect` 표식 계약을 붙이고 `AllyUnitAssetValidator`가 Definition당 구현 수를 센다. `AllyUnitInstance`는 모든 `OnAttack` 훅을 호출하므로 주 공격 두 개를 조립하면 피해가 중복되지만, 구체 클래스 이름 목록으로 막으면 후속 주 공격을 추가할 때 검증기 갱신을 빠뜨릴 수 있어 역할 계약을 단일 판정 기준으로 삼았다.
- 주 공격은 최대 1개만 허용하고 최소 1개는 강제하지 않는다. 오라만 조립한 지원 드론이 이미 정상 콘텐츠이므로 중복 피해만 차단하고 서포트 전용 데이터 계약은 유지한다.
- 기존 `AllyUnitCombatVerifier`를 29개 시나리오로 확장했다. 메모리 임시 SO와 저장되지 않는 임시 GameObject만 사용해 주 공격 역할 판정, 피해량 버프의 서로 다른 공급자 곱연산·만료, 관통 각도/사거리, 스플래시 반경/감쇠, 맹독 즉발/DoT, 타워 지원 오라의 사거리/중첩/만료를 실제 런타임 진입점으로 검증한다.

### 의도적으로 하지 않은 것

- `AllyUnitInstance.TriggerAttack`에서 두 번째 주 공격을 런타임에 무시하는 분기는 넣지 않았다. 잘못된 라이브 콘텐츠를 조용히 다른 동작으로 바꾸기보다 제작·빌드 단계에서 명시적인 오류로 중단해야 데이터 원본과 실제 전투가 일치한다.
- 새 asmdef나 테스트 어셈블리는 도입하지 않았다. 기존 Assembly-CSharp 구조와 Editor 메뉴 검증 경로를 유지해 전체 참조 구조를 흔들지 않았다.

### 검증

- Unity 6000.3.13f1 Pipeline 재컴파일 결과 `completed`, `failed=false`, `errors=[]`.
- `AllyUnitCombatVerifier`의 기존 23개와 신규 6개를 합친 29개 시나리오가 통과했다.
- `AllyUnitFoundationVerifier`의 기존 배치·전투 기반 계약과 `AllyUnitAssetValidator`의 레시피
  12개·경고 0건 검증이 함께 통과했다.

## 2026-08-25 — 오퍼레이터 강화 디버그 UI Home 키 토글

### 맥락
- `OperatorUpgradeDebugOverlay`가 TitleScene에서 항상 활성 상태라, 강화값을 확인한 뒤에도 로비 UI를 가리고 입력을 가로챘다.
- 루트 `GameObject` 자체를 비활성화하면 같은 오브젝트의 `OperatorUpgradeDebugPanel.Update()`도 멈춰 Home 키로 다시 열 수 없다.

### 결정
- `OperatorUpgradeDebugPanel`이 Editor Play Mode에서 신규 Input System의 Home 키를 직접 폴링해 루트 `CanvasGroup`의 `alpha`·`interactable`·`blocksRaycasts`를 함께 토글한다.
- 씬에 이미 배치된 `CanvasGroup`을 재사용하고, 누락된 환경에서도 디버그 경로가 끊기지 않도록 런타임에만 보완한다. 초기 표시 여부는 씬에 저장된 alpha 값을 유지한다.
- 디버그 입력과 패널 조작은 기존처럼 `UNITY_EDITOR` 안에만 두어 플레이어 빌드에는 노출하지 않는다.

## 2026-08-25 — 플레이어 기체 파츠 전투 기반과 디버그 테스트 드라이버

### 결정

- 추진기·포탑·바디·드라이버·특수 소켓을 `PlayerPartDefinition`과 상태 없는
  `PlayerPartEffectBase` 조립으로 표현한다. 쿨다운·보호막 잔량·부활 사용 여부 같은 전투별 상태는
  `PlayerController`의 `PlayerPartRuntimeState`가 소유한다. 신규 파츠를 데이터와 기존 Effect의
  조립만으로 추가하고 SO 원본을 런타임에 수정하지 않기 위한 경계다.
- `PlayerController`의 단발 공격 판정을 `IPlayerPrimaryAttackEffect`로 분리하고, 오버드라이브의
  버스트 횟수·간격·이동 배율·충전 용량을 `PlayerData`로 승격했다. 이중 배럴·관통·스플래시·연쇄와
  2스택 드라이버를 파츠별 전투 클래스 분기 없이 같은 호출 지점에서 구동하기 위함이다.
- 설계안의 38개 파츠를 JSON 레시피로 작성하고 `PlayerPartAssetBuilder`가 Definition·Effect·Resources
  카탈로그를 재현 가능하게 생성한다. 검증기는 Common 폴백, ID/슬롯/가격, 포탑의 주 공격 계약과
  상점 아이콘 참조를 확인한다. 상점 아이콘은 현재 범위의 필수 데이터라 누락 시 오류로 중단한다.
- **인게임 기체 3레이어 비주얼 시스템은 이번 범위에서 구현하지 않고 추후 작업으로 확정했다.**
  DefenseScene과 `PlayerController`는 기존 단일 함선 `SpriteRenderer`를 그대로 유지한다. 상점용
  아이콘은 인게임 오버레이와 피벗·합성 규격이 다른 자산이므로, 이번 아이콘을 임시 오버레이로
  재사용해 잘못된 기준을 굳히지 않는다.
- 실제 상점·구매·영속 장착 대신 `PlayerPartDebugSession`을 계정 메모리 테스트 드라이버로 두었다.
  TitleScene의 기존 Home 오퍼레이터 강화 오버레이 아래에 5슬롯 순환 선택 UI를 배치하고, 선택값은
  다음 DefenseScene 진입 때만 조립한다. 앱 재실행 시 초기화되어 `PlayerProfile`과 재화를 오염시키지
  않는다.

### 의도적으로 하지 않은 것

- `PlayerProfile` 스키마 확장, 재화 소비, 구매·판매·소유권 검증, 실제 상점 UI는 만들지 않았다.
  현재 UI는 전투 수치와 Effect 조립을 먼저 검증하기 위한 드라이버이며, 상점 구현 시
  `PlayerPartDebugSession`만 영속 로드아웃 공급자로 교체할 수 있게 조립 경계를 유지했다.
- 별도 아트 저장소에서 투명 RGBA 상점 아이콘 24개만 `Assets/Art/PlayerParts/Icons`로 이관했다.
  마젠타 원본과 크로마키 재현 스크립트는 아트 저장소에 남겼다. Mk1/Mk2는 같은 아키타입 아이콘을
  공유하며 `PlayerPartAssetBuilder`가 `slot/archetypeId` 경로 규칙으로 38개 Definition에 배선한다.
  WebGL UI 메모리를 고려해 임포트 상한은 512px, mipmap 비활성, 압축 Sprite로 통일한다.
- `RCCom/Player Parts/Build All Player Part Assets`를 실행해 24개 Sprite를 38개 생성 Definition의
  `icon` 필드에 저장했다. 이 호출은 에셋 배선만 수행했으며, 사용자 요청에 따라 별도의 컴파일
  조회·검증 메뉴·플레이 모드 확인은 실행하지 않았다.
- 새 asmdef나 테스트 어셈블리는 도입하지 않았다. 회귀 확인은 에디터 메뉴 검증기로 제공하며,
  최종 플레이 모드 검수와 컴파일 확인은 사용자 요청에 따라 사람에게 위임한다.

## 2026-08-25 — 플레이어 포탑 파츠에 공용 전투 VFX 재사용

- 최초 구현은 관통·스플래시의 판정 수학만 재사용하고 기존 타워/아군 Effect가 이미 사용하던
  레이저·착탄 연출을 누락했다. 그 결과 관통 대상과 스플래시 2차 피해자마다 플레이어발 일반
  투사체가 생겨 판정 모양과 표현이 어긋났다. 이는 아트 미완성과 무관한 누락이므로 별도 신규
  VFX를 만들지 않고 기존 공용 자산을 그대로 연결했다.
- 관통과 오버로드 관통은 피해를 투사체 없이 적용하고, `LaserBeamView` 한 개를 플레이어에서
  사거리 끝까지 재생한다. 스플래시는 주 대상에만 기존 포물선 투사체를 표시하고 착탄 지점에
  `ParticleBurst`·`ShockwaveRing`·`ScorchDecal`을 한 번 재생하며, 2차 피해의 넉백 원점도 실제
  폭발 중심으로 맞췄다.
- 연쇄 스파크는 같은 레이저 프리팹을 공격 구간마다 재사용하되 좌표를 `플레이어 → 첫 적 → 다음
  적 → 다음 적`으로 갱신한다. 모든 적을 플레이어와 방사형으로 잇지 않아 최근접 전이 판정과
  화면의 사슬 구조가 일치한다.
- `PlayerPartAssetBuilder`는 `CombatVfxAssetBuilder`의 공개 경로 상수를 정본으로 사용해 생성 Effect
  SO에 공용 VFX를 자동 배선한다. `PlayerPartAssetValidator`는 다른 프리팹 복제본이나 null이
  연결되면 오류로 중단해 타워·아군·플레이어의 연출 자산이 갈라지는 것을 막는다.
- 사용자 요청에 따라 이 후속 수정에서는 Unity CLI·플레이 모드 검증을 실행하지 않았다. 에셋
  재생성, 컴파일 및 실제 연쇄 구간 확인은 에디터에서 사람 검수로 인계한다.
## 2026-08-25 — 연속 구매 합류 연출 중복 방지

- 리크루트 Shop과 오퍼레이터 관리 화면은 구매 직전에 `PlayerProfile` 최신본을 다시 읽는다. 합류 연출이 별도 프로필 인스턴스로 저장한 `presentedOperatorAcquisitionIds`를 화면 진입 때부터 보관하던 낡은 Shop 프로필이 다음 구매 시 덮어쓰는 문제가 있었기 때문이다.
- 구매 성공 시 전체 미표시 오퍼레이터를 재검색하지 않고 방금 구매한 `operatorId`만 합류 연출에 전달한다. 스테이지 결과와 로비 복구 검사는 기존 전체/스테이지 필터 방식을 유지한다.
- 합류 연출은 Addressables 로딩이 끝나기 전부터 큐 활성 상태를 기록한다. 연속 호출이 들어오면 별도 코루틴을 중복 시작하지 않고 기존 큐 뒤에 중복 없이 추가하며, 완료 또는 디버그 취소 시 큐와 인덱스를 함께 비운다.
- Unity 6000.3.13f1 재컴파일 결과 `failed=false`, `errors=[]`를 확인했다. 사용자 저장 데이터를 건드리는 Play Mode 재현은 실행하지 않았다.

## 2026-08-25 — 오퍼레이터 자료·인연 기록 Material UI

### 결정
- Codename, Role, Faction, Height, Birthday, Speciality, Weapon, Origin과 인연 기록 5건을 `OperatorDefinition`에 포함했다. 자료 본문도 오퍼레이터 Addressables 패키지와 함께 내려받아, 라이브서비스 중 전투 코드나 로컬 카탈로그를 다시 빌드하지 않고 콘텐츠만 교체할 수 있게 하기 위함이다.
- 인연 단계명과 해금 기준은 모든 오퍼레이터가 공유하는 규칙으로 두고 Definition에는 본문만 저장한다. 기존 친밀도 규칙과 동일하게 낯섦 0, 호감 25, 기쁨 50, 사랑 75, EX 100에서 열리며 1단계는 본문 작성 여부와 관계없이 처음부터 공개된다.
- Material 탭은 별도 오퍼레이터 카드 UI를 만들지 않고 Recruit·Enhance가 사용하는 `OperatorPanel`과 하단 3칸을 재사용한다. 현재 `PlayerProfile`에서 해금된 오퍼레이터만 순환시켜 미보유 자료가 Addressables 로딩이나 좌우 카드로 노출되지 않게 했다.
- 인연 상세는 사용자가 만든 공용 `SmallDosierForCommuText`를 재사용한다. 잠긴 행은 클릭할 수 없고, 열린 행을 누르면 본문을 표시하며 오퍼레이터 이동·탭 종료 시 즉시 닫는다.

### 제작 도구와 검증
- Operator Studio에 `Dossier` 탭을 추가해 프로필 8개 항목과 고정 5단계 인연 본문을 편집하도록 했다. 저장·단일 빌드 시 JSON 레시피에서 Definition으로 복제되며, 기존 레시피는 누락된 5칸을 자동 보완한다.
- `OperatorDossierPanelSetup`은 기존 `DossierPanel`, `CommuData1~5`, `MaterialLockedSprite`, 상세 팝업을 보존한 채 클릭 영역과 직렬화 참조만 연결한다. 로비 하단과 Shop 좌측 Material 버튼도 같은 컨트롤러에 연결한다.
- 자료 텍스트가 비어 있거나 인연 기록 수가 5건이 아니면 전체 검증에서 경고하되, 창작 데이터 미완성 때문에 전투 패키지 빌드가 막히지는 않게 했다.
- Play Mode와 플레이어 빌드는 실행하지 않았다. Edit Mode 씬 배선 검증과 Unity 스크립트 컴파일로 확인했다.

## 2026-08-25 — 오로라(Aurora) 프로필 자료 및 5단계 인연 기록(BondRecords) 작성

### 결정
- 공유 세계관(Project Vertex 대체역사 연대기: 2060년대 추축국 승전 대체역사, 5대 열강 냉전, 자유세계조약기구 FWTO-캐나다 연방, 라이히스팍트 게슈타포/아프베어 정보망, 경계공명 능력자 설정)을 반영하여 오로라의 프로필 8개 항목과 5단계 인연 기록을 완성했다.
- 다른 전선/지역을 다루는 특성을 감안해 버텍스 직접 언급은 5단계 결말에서 세계의 메타포로 단 1회만 제한적으로 사용하고, FWTO(자유세계조약기구) 캐나다 출신 넷러너 의적단 및 만성 신경 과부하를 앓는 전뇌계 공명 능력자로서의 정체성을 구축했다.
- `Aurora.json` 레시피의 `codename`, `role`, `faction`, `height`, `birthday`, `speciality`, `weapon`, `origin`, `bondRecords` 5건을 모두 동기화했다.
## 2026-08-25 — 무한 모드 5웨이브 보스 승급

### 결정

- 무한 모드에서만 매 5웨이브마다 정상 예산으로 절차적 스폰 큐를 먼저 완성한 뒤, 그 큐의 한 슬롯을 전용 `System.Random`으로 선택해 보스로 승급한다. 보스를 추가 스폰하지 않고 원래 나올 적 한 마리를 대체하므로 웨이브 편성 수와 예산은 일반 웨이브 규칙을 그대로 유지한다.
- 보스는 선택된 `EnemyDefinition`을 계속 참조하고 `EnemyData`의 세션 전용 복제본만 사용한다. 이에 따라 스프라이트·Effects와 미지정 전투 수치(공격 범위·공격 주기 등)는 선택된 적을 유지하면서 원본 SO를 변경하지 않는다.
- 보스 기본 체력은 승급 전 웨이브 큐의 모든 적 기본 체력 합계 × 2.25로 계산하고, 이후 일반 적과 같은 `1 + healthGrowthPerWave × n` 배율을 정확히 한 번 적용한다. 따라서 결과는 "해당 웨이브에서 실제 적용될 모든 적 체력 합계 × 2.25"와 같고 현행 무한 성장 곡선을 공유한다.
- 이동속도는 0.75, 접촉 공격력은 웨이브 내 최대 `contactDamage` × 3으로 덮어쓴다. 보상 기준은 `goldReward`가 가장 높은 적이며, 골드 동률이면 사용자가 결정한 대로 `expReward`가 높은 적을 선택해 그 적의 골드와 EXP를 각각 3배 지급한다.
- 과거의 `bossDefinition` 전용 적 분기와 보스 웨이브 예산 0.5배 규칙은 새 요구와 충돌해 제거했다. 스테이지 모드는 계속 `StageDefinition`의 디자이너 편성만 실행하며 런타임 승급을 적용하지 않는다.

### 의도적으로 하지 않은 것

- 보스 전용 적 클래스·Definition·프리팹·Effect를 만들지 않았다. 기존 적의 데이터와 Effect 조립을 그대로 쓰는 것이 이번 승급 계약이며, 신규 적 타입이 생기지 않아 Addressables 적 패키징도 바뀌지 않는다.
- 보스 전용 외형 확대·색상·HUD는 요구에 없으므로 임의로 추가하지 않았다. 런타임에는 `EnemyInstance.IsBoss`를 남기고 WaveManager 디버그 상태에 `(BOSS)`를 표시해 후속 연출이 전투 수식과 분리된 채 연결될 수 있게 했다.
- 새 asmdef나 테스트 어셈블리는 도입하지 않았다. 기존 프로젝트 방식과 같은 Editor 메뉴 검증기를 사용했다.

### 검증

- Unity `6000.3.13f1` Pipeline 재컴파일 결과 `completed`, `failed=false`, `errors=[]`.
- `EndlessBossPromotionVerifier`에서 무한/스테이지 모드 경계, 체력 합계 × 2.25, 고정 이동속도 0.75, 최대 접촉 공격력 × 3, 최고 골드·동률 고EXP 적의 골드/EXP × 3, 공통 웨이브 체력 배율 1회 적용, 원본 SO 불변성을 확인했다.
- `EnemySelfDestructVerifier`와 `EnemyHealerVerifier`를 다시 실행해 선택 인자를 추가한 `EnemyInstance.Spawn`이 기존 접촉·자폭·회복 Effect 경로를 변경하지 않았음을 확인했으며, 해당 검증 이후 콘솔 오류는 0건이었다.

## 2026-08-25 — 무한 모드 보스 크기·전투 수치 후속 조정

### 결정

- 사용자 플레이 밸런스 조정에 따라 보스 체력 계수를 웨이브 전체 체력의 2.25배에서 1.75배로, 공격력 계수를 웨이브 최대 `contactDamage`의 3배에서 1.75배로 낮췄다. 이동속도와 골드·EXP 보상 규칙은 유지한다.
- 승급 보스의 스프라이트 drawing size는 선택된 적의 기존 크기를 기준으로 1.5배 확대한다. 적 스프라이트의 native bounds가 종류별로 다르고 일반 적은 현재 그 크기를 그대로 쓰므로, 공통 target size를 새로 강제하지 않고 `SpriteFit.CalculateUniformScale`에 각 스프라이트의 기존 최장축을 target으로 전달해 일반 적의 화면 크기를 보존했다.
- `EnemyView` 루트 확대는 같은 오브젝트의 접촉 Collider와 자식 체력바도 함께 키운다. 보스의 전투 판정까지 넓어지는 것은 "크게 그리기" 범위를 벗어나므로 `CircleCollider2D` 반경·오프셋과 체력바 로컬 스케일을 역보정해 월드 판정 크기와 UI 크기는 그대로 유지한다.

### 검증

- `EndlessBossPromotionVerifier`가 체력 1.75배, 공격력 1.75배, 스프라이트 1.5배 균일 확대와 Collider 월드 반경·오프셋 불변성을 함께 검증한다.
- Unity 스크립트 재컴파일이 오류 없이 완료됐고 `RCCom/Verify/Endless Boss Promotion`, `RCCom/Enemies/Verify Exploder Special Effect`, `RCCom/Verify/Enemy Healer` 검증이 모두 PASS했다. 검증 시작 시점 이후 콘솔 오류는 0건이었다.

## 2026-08-26 — 승급 보스 확대에 접촉 판정 동기화

### 결정

- 앞선 구현에서 "1.5배 크게 그리기"를 시각 연출만으로 해석해 `CircleCollider2D`를 역보정했으나, 큰 외형과 작은 접촉 경계가 어긋나면 플레이어·거점 접촉뿐 아니라 아군 교전 시 보스 안쪽으로 파고들어 보이는 문제가 생긴다. 따라서 Collider 역보정을 제거하고 보스 루트와 함께 1.5배 확대되도록 정정했다.
- 아군 프리팹에는 Collider가 없고 아군·적 조우는 `UnitCombatSettings.ContactRange`를 이용한 순수 C# 중심점 거리로 처리된다. 물리 Collider만 확대해서는 아군 겹침이 해결되지 않으므로, `EnemyInstance.GetContactRange`에서 승급 보스와 교전할 때의 논리 접촉 거리를 기본값의 1.5배로 계산한다.
- 대상별 접촉 거리를 아군과 적 양쪽의 이동 차단, 접촉 후보 선택, 근접 공격 가능 거리, 공격 대상 유지에 일관되게 사용한다. 기본·독·범위 공격의 원거리 투사체 판별과 관통 공격의 빔 길이도 대상별 실제 접촉 경계를 사용해, 확대된 보스 앞에서 근접 유닛이 공격하지 못하거나 원거리로 오인되는 부작용을 막았다.
- 체력바는 전투 판정과 무관한 UI이므로 기존 역스케일을 유지해 일반 적과 같은 화면 크기로 표시한다. 일반 적과 디자이너가 정의한 스테이지 보스는 `IsPromotedBoss`가 아니므로 모든 거리와 크기가 종전과 같다.

### 검증

- Unity `6000.3.13f1` 스크립트 재컴파일이 `failed=false`, `errors=[]`로 완료됐다.
- `EndlessBossPromotionVerifier`에서 스프라이트와 `CircleCollider2D`의 월드 반경·오프셋이 함께 1.5배 확대됨을 확인했다.
- `AllyUnitCombatVerifier`에 승급 보스 전용 30번째 시나리오를 추가해 아군이 접근하는 경우와 보스가 접근하는 경우 모두 중심거리 `0.75 × 1.5 = 1.125`에서 멈추고 교전함을 확인했다. 기본·관통·범위·독 공격을 포함한 전체 30개 시나리오가 PASS했다.
- `EnemySelfDestructVerifier`와 `EnemyHealerVerifier` 회귀 검증도 PASS했으며 최종 검증 구간의 콘솔 오류는 0건이었다.

## 2026-08-26 — 승급 보스 이동속도·공격력 추가 하향

### 결정

- 승급 보스의 고정 이동속도를 0.75에서 0.5로 낮췄다.
- 공격력은 웨이브 최고 `contactDamage × 1.75`에서 웨이브 평균 `contactDamage × 1.5`로 변경했다. 평균의 모집단은 `BuildSpawnQueue`가 생성한 실제 스폰 슬롯 전체이며, 같은 적이 여러 번 편성되면 그 마릿수만큼 평균에 반영한다. null 데이터는 제외하고 음수 공격력은 0으로 취급한다.
- 체력·크기·접촉 거리·골드·EXP 보상과 선택된 적의 나머지 Combat/Effects 승계 규칙은 변경하지 않았다.

### 검증

- `EndlessBossPromotionVerifier`의 공격력 표본 `10, 30, 20`에서 평균 20 × 1.5 = 30, 고정 이동속도 0.5를 검증하도록 갱신했다. 추가로 `10, 10, 30` 편성을 넣어 같은 적이 여러 슬롯에 등장할 때도 마릿수 가중 평균 16.666… × 1.5 = 25가 계산되는지 확인한다.
- Unity `6000.3.13f1` 스크립트 재컴파일이 `failed=false`, `errors=[]`로 완료됐고 `EndlessBossPromotionVerifier`와 아군 전투 코어 30개 시나리오가 모두 PASS했다. 최종 검증 구간의 콘솔 오류는 0건이었다.

## 2026-08-26 — 스테이지별 설치 슬롯 타일맵 + 기본 맵 배경 가림 수정

**맥락** — 스테이지마다 배경 아트와 경로는 Stage Studio로 편집할 수 있었지만(2026-08-25 항목), 타워를
설치할 수 있는 칠해진 타일(`slotTilemap`)은 `StageDefinition`이 전혀 소유하지 않았다. DefenseScene에
고정으로 칠해진 엔드리스 모드용 레이아웃 하나만 모든 스테이지가 공유했으므로, 맵 모양이 다른 스테이지를
만들어도 설치 가능 영역은 항상 같았다. 별개로, `MapManager.ApplyStageBackground`가 스테이지 배경
스프라이트를 `StageBattleBackground`(Order in Layer -100)에 올바르게 주입하고 있었지만, 엔드리스 모드용
기본 맵 아트를 그리는 `Square`(Order in Layer 0)가 스테이지 유무와 무관하게 항상 켜져 있어 항상 그
앞을 가렸다 — 배경 로딩 자체가 아니라 두 배경이 동시에 존재할 때의 표시 순서 문제였다.

**결정**
- `StageDefinition`에 `buildableCells`(`List<Vector3Int>`, Tilemap 셀 좌표)를 추가했다. 비어 있으면
  "슬롯 없음"이 아니라 "DefenseScene 기본 레이아웃 사용"으로 해석해, 기존 스테이지와 엔드리스 모드를
  하나도 건드리지 않는다.
- `MapManager.Awake()`가 `ApplyStageBuildableCells()`로 `buildableCells`가 있을 때만 `slotTilemap`을
  비우고 공용 `buildableSlotTile`(신규 `Assets/Data/Tilemaps/BuildableSlotTile.asset`, `TilemapRenderer`가
  꺼져 있어 스프라이트 불필요)로 다시 칠한다. 비어 있으면 아무것도 하지 않아 씬에 이미 칠해진 레이아웃이
  그대로 유지된다.
- `MapManager`에 `defaultMapBackgroundRenderer` 참조(= `Square`)를 추가하고, `ApplyStageBackground`가
  스테이지 배경이 있을 때만 이 렌더러를 끈다. 엔드리스 모드(`stage == null`)에서는 계속 켜져 있으므로
  기존 화면과 동일하다.
- `StageRouteAuthoringTool`의 Load/Capture 왕복에 타일 캡처를 얹었다. `LoadIntoOpenTestScene`은
  `stage.buildableCells`(없으면 DefenseScene 기존 레이아웃)로 테스트 씬의 `SlotMarkers`를 다시 칠하고,
  `CaptureFromOpenTestScene`은 그 씬에서 `HasTile`인 셀을 모두 읽어 되돌려 저장한다. 디자이너는 Load 후
  Hierarchy에서 `SlotMarkers`를 선택해 Tile Palette로 칠하거나 지우면 된다. `TilemapRenderer`가 꺼져 있어
  Scene View에서 안 보이는 문제는 `StageBuildableCellGizmo`(신규, `StageRouteTestScene`에서만 동작)가
  칠해진 셀을 청록색 와이어큐브로 그려 대신 보여준다.
- `SetupRuntimeBattlefieldBackground` 메뉴가 `battleBackgroundRenderer`뿐 아니라
  `defaultMapBackgroundRenderer`·`buildableSlotTile` 배선까지 함께(멱등하게) 복구하도록 넓혔다 — 씬을
  다시 만들거나 배선이 끊겨도 이 메뉴 하나로 되돌릴 수 있게 하기 위함이다.
- `StageAssetValidator`에 `buildableCells`가 비어 있으면 경고(에러 아님)를 남기도록 추가했다.

**근거** — 웨이포인트 경로를 스테이지별 자유 좌표로, 타워 설치는 그리드로 분리한 기존 설계
(`MapManager` 클래스 주석)를 그대로 따랐다. 슬롯 정보도 같은 이유로 "그리드 셀 좌표 목록"이라는 가장
단순한 자료형으로만 저장하고, 타일의 시각 정보(스프라이트·색)는 다루지 않는다 — 어차피 런타임에는
`TilemapRenderer`가 꺼져 있어 그려지지 않기 때문이다. 배경 가림 문제는 새 배경 오브젝트를 만드는 대신
기존 `Square`를 MapManager가 제어하는 쪽을 택해, 씬에 이미 배치된 아트와 좌표를 재사용했다.

**의도적으로 하지 않은 것**
- `slotTilemap` 자체를 스테이지별로 교체하거나 씬을 복제하지 않았다. 배경·경로와 같은 패턴으로 좌표
  목록만 `StageDefinition`이 소유한다.
- Stage Studio Map 탭에 `buildableCells` 전체를 인라인 리스트로 노출하지 않았다. 수백 개가 될 수 있는
  좌표를 IMGUI 리스트로 펼치면 편집성이 떨어지므로, 개수 요약과 Tile Palette 안내만 표시하고 실제 편집은
  Test Scene의 표준 Tile Palette 워크플로에 맡긴다.
- `buildableSlotTile`에 실제 시각 스프라이트를 넣지 않았다. 런타임에 보이지 않는 순수 마커 타일이라
  필요하지 않다.

**사람 액션**
- Unity 에디터를 열어 스크립트 재컴파일 오류가 없는지 확인해야 한다(이번 세션은 Unity 에디터 라이브
  연결 없이 스크립트와 씬 YAML을 직접 편집했다 — MapManager의 새 필드 3개(`defaultMapBackgroundRenderer`,
  `buildableSlotTile`, 그리고 기존 `editorPreviewStage`와의 순서)가 `DefenseScene.unity`·
  `StageRouteTestScene.unity`에 올바르게 배선됐는지 인스펙터에서 육안 확인 필요).
- 각 CH1 스테이지(1-1~1-7)는 아직 `buildableCells`가 비어 있어 기존 DefenseScene 레이아웃을 그대로
  쓴다. 맵 모양이 다른 스테이지마다 Stage Studio → Map 탭 → `Open Test Scene & Load Selected Stage` →
  `SlotMarkers`를 Tile Palette로 칠함 → `Capture Test Scene Into Selected Stage` 순서로 실제 슬롯을
  제작해야 한다.
- Play Mode에서 스테이지 모드로 진입해 설치 슬롯이 배경 위 원하는 위치에만 나타나는지, 엔드리스 모드는
  기존과 동일하게 동작하는지 확인이 필요하다. 이번 세션은 Edit Mode 코드·데이터 변경만 했고 Play Mode는
  실행하지 않았다.

## 2026-08-26 — 해상도 독립 로딩 전환 이동 거리

- `UILoadingTransition`은 초기 Awake 시점의 Canvas 높이를 숨김 위치로 고정하지 않는다. CanvasScaler의 실제 배치가 해상도별로 완료된 뒤, 각 전환 직전에 로딩 배경과 부모 Canvas의 현재 높이 중 큰 값으로 이동 거리를 다시 계산한다. QHD 외 해상도에서 와이프가 화면 중간에 멈추는 것을 막고, 런타임 창 크기 변경에도 같은 전환 프리팹을 재사용하기 위한 처리다.

## 2026-08-26 — 범용 Normal/Hover 스프라이트 전환 컴포넌트

- 화면별 메뉴가 각각 포인터 이벤트와 Image 스프라이트를 직접 관리하던 중복을 줄이기 위해 `UISpriteHoverSwap`을 추가했다. 포인터 진입·이탈과 키보드/패드 선택·해제를 같은 강조 상태로 취급하며, 화면 흐름은 기존 Button에 남긴다.
- `SetHighlighted`를 제공해 Recruit 탭처럼 포인터가 없어도 Hover를 유지해야 하는 화면도 같은 컴포넌트를 재사용할 수 있게 했다. Normal 스프라이트가 비어 있으면 기존 Image 스프라이트를 보존해 에디터 배선 누락으로 이미지가 사라지지 않게 한다.

## 2026-08-26 — Exchange 슬롯 스프라이트 밝기 보존

- `PlayerPartShopUI.RenderSlots`가 슬롯 Image에 어두운 색을 곱하던 처리를 제거했다. GearSpriteSheet의 Normal/Hover 스프라이트가 이미 비선택·선택 밝기와 발광을 포함하므로, 런타임 색상 곱은 교체 아트의 글자와 테두리를 훼손한다.
- 선택 슬롯의 고정 강조는 `UISpriteHoverSwap.SetHighlighted`로 전달해 포인터가 빠진 뒤에도 선택된 슬롯의 Hover 스프라이트가 유지되도록 했다.

## 2026-08-26 — PartCarousel 카드 Normal/Hover 스프라이트 배선

- `PartCard_01~05`의 배경 Image에 `SubgearSpriteSheet_0`을 Normal, `SubgearSpriteSheet_1`을 Hover로 공통 배선한다. 카드마다 별도 로직을 만들지 않고 `UISpriteHoverSwap`과 `PlayerPartShopCardView.Bind`의 선택 상태 전달을 재사용한다.
- 카드가 선택되면 Hover 스프라이트를 유지하고, 선택되지 않은 카드도 Image 색상을 흰색으로 보존해 아트에 포함된 외곽 발광과 텍스트가 런타임 색상 곱으로 어두워지지 않게 했다. `PlayerPartShopPanelSetup`을 다시 실행하면 다섯 카드의 배선이 동일하게 갱신된다.

## 2026-08-26 — 스테이지 선택 우측 정보 패널

- `StageSelectionRightPanel`의 제작된 배경은 유지하고, 런타임에 스테이지 제목·권장 레벨·브리핑·적 편성 4칸·보상 5칸만 채우는 `StageSelectionRightPanelUI`를 추가했다.
- 선택 화면이 원격 `StageDefinition`을 미리 내려받지 않는 기존 원칙을 유지하기 위해, 웨이브의 적 ID·총 등장 수와 일반 보상 표시 데이터를 `StageCatalogEntry`의 경량 메타데이터로 복사한다. 적 아이콘과 이름은 내장 `EnemyCatalog`에서 ID로 해석한다.
- 골드 보상은 스테이지마다 같은 아이콘을 중복 참조하지 않고 우측 패널의 공용 `goldsprite`를 사용한다. 스테이지 클리어 오퍼레이터 보상은 일반 보상 목록에 중복 저장하지 않고, 해금 조건의 정본인 `OperatorCatalog`를 스테이지 ID로 역조회해 마지막 보상 칸에 합성한다.
- `RCCom/UI/Wire Stage Selection Right Panel`은 새 패널 아래에 없는 자식만 만들고 기존 RectTransform을 다시 쓰지 않는다. 수동으로 위치를 다듬은 뒤 재실행해도 배치를 덮어쓰지 않기 위한 제한이다.
- 최초 배선에서 상단 기준 `offsetMin/offsetMax`의 Y 순서가 뒤집혀 생성 요소의 높이가 음수가 된 문제를 수정했다. 잘못 생성된 음수 높이만 자동 복구하고, 정상 크기로 수동 조정된 요소는 보존한다. 빈 슬롯은 비활성화해 Scene View의 빨간 X를 없애고, 현재 카탈로그 데이터로 편집 모드 미리보기를 채운다.
- 모든 스테이지에 실제 결과 정산의 기본 재화 100과 플레이타임 보너스가 있으므로, 명시적인 골드 보상이 없는 경우 `goldsprite`와 `100+`를 기본 표시한다. Stage Studio에서 `gold`/`commodity` 보상을 명시하면 그 수치를 우선한다.

## 2026-08-26 — Addressables 끊어진 그룹 참조 복구

- `AddressableAssetSettings.asset`의 그룹 목록에 삭제된 에셋을 가리키는 null 참조가 2개 남아 있었다. Addressables가 Undo/Redo 직후 모든 그룹의 해시를 다시 계산할 때 이 항목을 역참조해 `ResetHashes()`에서 `NullReferenceException`이 발생했다.
- 설정 YAML을 직접 고치지 않고 `RCCom/Addressables/Repair Null Group References` 에디터 메뉴를 추가했다. 이 도구는 그룹 목록을 뒤에서부터 검사해 null 항목만 제거하며, 정상 그룹의 이름·엔트리·스키마·주소는 재작성하지 않는다.
- 복구 메뉴 실행 후 에셋을 `SaveAssets`/`Refresh`로 저장했고, `m_GroupAssets`에 유효한 그룹 참조만 남은 것을 확인했다. Unity 스크립트 재컴파일도 `failed=false`, `errors=[]`로 완료됐다.

## 2026-08-26 — 리크루트 Shop Back 버튼 클릭 범위 축소

- `StrategistPanel/BackButton`의 2172×724 상태 이미지에는 실제 패널 위·아래로 큰 투명 여백이 있지만, uGUI `Image`가 전체 RectTransform을 Raycast 영역으로 사용해 버튼 밖에서도 눌리는 문제가 있었다.
- 표시 크기와 Normal/Hover 정렬을 바꾸지 않고 `Image.raycastPadding = (0, 270, 0, 200)`을 적용해 실제 패널이 있는 세로 영역만 포인터를 받게 했다. `LobbyShopPanelSetup`에 설정과 검증을 함께 넣어 재배선해도 클릭 범위가 다시 커지지 않는다.

## 2026-08-26 — 로비 스테이지 이행 기록 화면

- 로비 `Records` 메뉴에서 진입하는 `LobbyRecordsUI`를 추가했다. 화면 전환은 다른 로비 하위 화면과 동일하게
  `UILoadingTransition`이 완전히 덮은 시점에 수행하며, Back으로 로비를 복원한다.
- 현행 `PlayerProfile`의 정본 데이터인 `clearedStageIds`와 `bestWave`만 읽어 CH1 스테이지를
  `CLEARED / AVAILABLE / LOCKED`로 표시하고, 챕터 달성률·엔드리스 최고 웨이브·다음 작전을 함께 보여준다.
  아직 저장하지 않는 클리어 시간·점수·등급은 목업 수치로 위조하지 않았다.
- 스테이지 행은 `StageRecordItemView` 프리팹과 `ScrollRect` 목록으로 분리했다. CH1 스테이지 수가 늘어나도
  화면 계층을 다시 손보지 않고 `StageCatalog` 데이터만 추가하면 기록 목록이 확장된다.
- `RCCom/UI/Setup Lobby Records Panel` 에디터 메뉴가 화면 계층, 행 프리팹, TMP 텍스트, 버튼 및 런타임 참조를
  한 번에 생성·배선한다. 완성 아트가 들어오면 데이터 바인딩을 유지한 채 배경 Image만 교체할 수 있다.

**근거** — 기록 화면은 전투 결과를 새로 소유하지 않고 프로필을 읽기만 하게 해 UI→데이터 단방향을 유지했다.
별도 기록 매니저나 중복 저장 구조를 만들지 않았으며, 추후 클리어 시간·별·점수가 실제 프로필 데이터로
정의될 때 같은 행 뷰에 필드만 확장할 수 있도록 했다.

## 2026-08-26 — 발렌티나 중복 초상화 Addressables 키 및 릴리스 버전 원자성 핫픽스

### 결정

- 같은 Sprite GUID를 여러 오퍼레이터 초상화 슬롯이 공유하면 마지막으로 등록되는 슬롯 주소를 canonical 주소로 삼고, 모든 카탈로그 필드가 그 주소를 재사용한다. Addressables 엔트리는 GUID당 하나뿐이므로 슬롯별 주소 문자열을 따로 남기면 실제 Location이 없는 키가 생기기 때문이다.
- 라이브 오퍼레이터 카탈로그의 Definition·초상화·유닛 주소가 실제 Addressables 엔트리에 모두 존재하는지 빌드 전 검증한다. 기존 검증은 라이브 카탈로그 자체의 주소와 그룹만 확인해 내부 필드의 끊어진 키를 놓쳤다.
- 빌드 완료 후 자동 버전 증가는 제거하고 버전 메뉴를 명시적 Major/Minor/Patch 증가로 바꿨다. `BuildScript`는 시작 시 버전을 고정하고 빌드 도중 값이 바뀌면 릴리스 상태 보관을 중단해, 플레이어에 구워진 버전과 `ReleaseStates` 폴더가 항상 같게 한다.

### 의도적으로 하지 않은 것

- 같은 PNG를 슬롯별로 복제하지 않았다. 아트 중복은 번들·저장소 용량만 늘리고 동일 이미지라는 원본 관계를 숨기므로, 주소 별칭을 데이터 생성 단계에서 canonical key로 수렴시켰다.
- 빌드마다 브랜치를 자동 생성하지 않았다. 개발 변경은 PR 브랜치로 검토하되, 배포 식별은 main의 추적된 `PlayerSettings.bundleVersion`, `ReleaseStates`, 최종 Git 태그가 담당하도록 분리한다.

### 1.0.1 릴리스 검증

- Unity 6000.3.13f1에서 WebGL 플레이어와 Addressables를 같은 `1.0.1` 버전으로 빌드했고, 빌드 직후 `ReleaseStates/WebGL/1.0.1/addressables_content_state.bin`을 다시 보관했다.
- 업로드용 `ServerData.zip`을 새 원격 콘텐츠 결과로 재생성했다. ZIP에는 `catalog_1.0.1`과 Valentina 오퍼레이터·아군 유닛 번들이 포함되며, 원격 로드 경로는 `RCCOM_REMOTE_LOAD_PATH`에서 해석한 `https://arcade.codingbot.kr/content/6a4b0df00880a934f010d7c1/live/WebGL`이다.
- 릴리스 브랜치는 빌드 산출물 자체가 아니라 재현에 필요한 콘텐츠 상태와 릴리스 기록만 PR로 main에 반영하고, 병합 커밋에 `v1.0.1` 태그를 붙인다. `Builds/WebGL`과 `ServerData.zip`은 배포용 로컬 산출물이므로 Git에는 넣지 않는다.
## 2026-08-26 — 1-6 힐러·1-7 자폭 드론 지형별 편성

### 결정

- `StageRouteTestScene`은 스테이지별 런타임 씬이 아니라 선택한 `StageDefinition`의 배경·경로·설치 셀을
  임시로 펼치는 공용 제작 작업대로 유지한다. 마지막으로 불러온 스테이지 모습이 씬에 저장되더라도 실제
  전투는 선택한 Definition을 공용 `DefenseScene`에 주입하므로 런타임 스테이지 선택에는 영향을 주지 않는다.
- `enemy-heal`은 1-6, `enemy-explode`는 용암 지형 1-7의 대표 특수 적으로 분리했다. 두 적 모두 첫
  웨이브부터 등장하고 3개 웨이브에서 수량이 `1 → 2 → 3`으로 늘어나, 마지막 웨이브 한 기만 보는
  단조로운 구성을 피하면서 능력을 단계적으로 학습하게 한다.
- `StageEnemyCompositionSetup`은 동일 적 ID를 찾아 갱신하고 반대 지형의 특수 적 행을 제거하는 멱등형
  Editor 도구로 만들었다. 메뉴를 다시 실행해도 행이 중복되지 않으며, 저장 후 Stage Catalog 재생성과
  전체 검증까지 수행한다.

### 의도적으로 하지 않은 것

- `enemy-heavytanker`는 등장 스테이지가 정해지지 않아 임의로 배치하지 않았다.
- 테스트 씬이나 전투 씬을 추가로 복제하지 않았고 런타임 C#도 변경하지 않았다.

## 2026-08-26 — Defend 최대 체력 오라와 1-2 첫 등장

### 결정

- 직렬화 호환성을 지키기 위해 `EnemyKind.Defend = 8`을 기존 값 뒤에 추가했다. `enemy-defend` 레시피는
  접촉 공격력 0, 오라 반경 4, `MaxHealthAuraEffect` 한 개를 조립한다.
- 방어 오라는 반경 안의 **다른 적** 최대 체력을 제공자 한 기당 10% 높이고, 사거리 안에서 매 틱 짧은
  지속시간을 갱신한다. 사거리 이탈 또는 제공자 제거로 갱신이 끊기면 원래 상한으로 돌아간다. 여러 방어
  유닛이 겹치면 제공자별 항목이 곱연산으로 중첩된다.
- 오라 진입·이탈 시 현재 체력의 비율을 보존한다. 범위를 오가는 것만으로 영구 회복을 얻거나, 오라가
  사라지는 순간 체력 수치가 비정상적으로 잘리는 일을 막기 위한 선택이다. 웨이브 체력 배율은 오라보다
  아래의 런타임 기본 상한에 적용한다.
- 공유 Effect SO에는 상태를 저장하지 않고, 받는 `EnemyInstance`가
  `RefreshableAuraBag<EnemyInstance, EnemyEffectBase, float>`로 제공자별 만료 상태를 소유한다.
- `IEnemyRangeAuraVisualEffect` 계약과 공용 `EnemyRangeAuraVisualRuntime`을 추가했다. `EnemyView`는 구체
  적 종류를 분기하지 않고 이 계약을 구현한 Effect를 찾아 월드 반경 4와 일치하는 초록색 지속 링을 만들며,
  사망 시 즉시 정리한다.
- 1-2에서 처음 두 웨이브는 1기, 세 번째 이후는 2기로 등장하도록 `StageEnemyCompositionSetup`에 넣었다.
  초반부터 능력을 보여주되 최대 체력 오라의 중첩 난도가 갑자기 커지지 않게 제한한 편성이다.

### 의도적으로 하지 않은 것

- Defend 전용 프리팹이나 전용 MonoBehaviour/Manager를 만들지 않았다. 공용 `EnemyView`와 Effect 조립을
  그대로 사용해 같은 오라를 이후 다른 적 Definition에도 재사용할 수 있다.
- 힐러처럼 일정 주기로 현재 체력을 회복시키지 않았다. 이번 능력은 범위 안에서 유지되는 최대 체력 버프로
  분리해 두 역할이 겹치지 않게 했다.

### 검증

- Unity `6000.3.13f1` 배치 컴파일과 `EnemyDefenderVerifier`가 PASS했다. 범위 안·경계·범위 밖 판정,
  웨이브 체력 배율 뒤 +10%, 자기 제외, 이탈 만료, 현재 체력 비율 보존, 초록 머티리얼 배선을 확인했다.
- 같은 최대 체력 경로를 사용하는 `EnemyHealerVerifier`와 `EndlessBossPromotionVerifier`도 함께 PASS해
  기존 회복 상한과 무한 보스 체력 승급 계산에 회귀가 없음을 확인했다.
- 에디터 API로 Effect·Material·Definition·Addressables 전용 그룹을 생성하고 Roster/Catalog를 갱신했다.
  1-1에는 Defend가 없고 1-2의 모든 웨이브에는 `1 → 1 → 2` 편성이 존재하는지를 검증한다.

### 후속 크기 보정

- Defend 원본은 445px 폭에 100 PPU라 Tight Sprite 실제 폭도 약 3.46으로 표시되어 다른 특수 적보다 지나치게 컸다.
  전용 프리팹이나 런타임 배율 분기를 추가하지 않고 TextureImporter를 160 PPU로 설정했다. 투명 여백을
  제외한 Tight Sprite의 실제 최장축은 약 2.16으로 줄어 다른 특수 적과 비슷한 체급이며, 빌더 재실행
  시에도 이 값이 유지된다.

## 2026-08-26 — Endless 사망 플립북 풀의 파괴 참조 예외 수정

### 원인과 결정

- 적 사망 폭발 `SpriteFlipbook`은 씬 오브젝트를 prefab별 static 대기열에 반납하지만,
  `GameManager.Awake()`의 Retry/씬 재로드 초기화 목록에서 이 풀만 빠져 있었다. 이전 DefenseScene의
  대기 인스턴스가 파괴된 뒤 다음 세션 첫 적 사망에서 그 참조를 꺼내 `Play()`가 transform에 접근하면서
  `MissingReferenceException`이 발생했다.
- `SpriteFlipbook.ClearPool()`을 `GameManager`의 최선행 세션 초기화에 등록했다. 동시에
  `GetOrCreate()`도 대기열에서 Unity null인 파괴 참조를 발견하면 폐기하고 새 인스턴스를 만들도록 보강해,
  도메인 리로드 비활성화나 비정상 씬 전환에서도 사망 처리가 멈추지 않게 했다.
- 이 예외는 `EnemyInstance.Died` 이벤트 호출 도중 발생하므로 뒤쪽 구독자의 목록 정리·보상·웨이브 진행을
  끊을 수 있었다. 자폭드론의 스폰 로직 자체를 바꾸지 않고 이벤트 체인을 정상화하는 쪽으로 수정했다.

### 검증

- `SpriteFlipbookPoolVerifier`가 의도적으로 파괴된 컴포넌트를 static 풀에 넣은 뒤 다음 요청에서 이를
  건너뛰고 새 인스턴스를 생성하는지 확인한다.
- `EnemySelfDestructVerifier`에 `enemy-explode`가 Endless 프리로드 Roster/Catalog에 등록되어 있고
  `minWave <= 1`, 양수 `waveCost`, `EnemyKind.Explode`인지 확인하는 검증을 추가했다.
- Unity `6000.3.13f1` 배치 컴파일에서 `SpriteFlipbookPoolVerifier`, `EnemySelfDestructVerifier`,
  `EndlessBossPromotionVerifier`가 모두 PASS했고 프로세스가 return code 0으로 종료됐다.

## 2026-08-26 — Heavy Tanker 정면 반원 방어막

### 결정

- `FrontalShieldEffect`를 Heavy Tanker Definition에 조립했다. 적의 현재 이동 방향과 공격 발신 위치를
  내적해 앞쪽 180도 안의 직접 피해만 `×0.5`로 줄인다. 측면·후면 공격과 발신 위치가 없는 독 지속
  피해는 원래 피해를 받으므로, 무조건적인 피해 절반이 아니라 배치와 측면 공격으로 대응할 수 있다.
- `IEnemyIncomingDamageModifier` 선택 계약을 추가했다. `EnemyInstance.TakeDamage`는 이 계약을 구현한
  Effect만 순서대로 적용하며, 다른 Enemy Effect의 기존 훅 계약은 늘리지 않았다. 공유 SO에는 상태를
  저장하지 않고 현재 위치·이동 방향·발신 위치만으로 매 피격을 계산한다.
- `EnemyInstance.FacingDirection`은 스폰 시 경로의 첫 유효 선분으로 초기화하고 이동 및 웨이포인트 도착
  때 다음 선분으로 갱신한다. 따라서 코너 도착 프레임에도 피해 판정과 방어막 방향이 어긋나지 않는다.
- `IEnemyFrontShieldVisualEffect`와 `EnemyFrontShieldVisualRuntime`을 추가해 공용 `EnemyView`가 구체 적
  종류를 분기하지 않고 Effect 설정으로 파란 반원 방어막을 만든다. 반경 2.15의 반원은 이동 방향을
  계속 따라가며 사망 시 즉시 제거된다.

### 의도적으로 하지 않은 것

- 공격을 완전히 무효화하거나 현재 체력과 별도의 방어막 HP를 만들지 않았다. 요청한 정면 피해 50%
  감소만 적용해 기존 Heavy Tanker의 체력·이동·접촉 공격 밸런스를 유지했다.
- Heavy Tanker 전용 프리팹·MonoBehaviour·Manager를 만들지 않았다. 다른 적도 같은 Effect 에셋을
  Definition에 조립하면 같은 능력을 재사용할 수 있다.

### 검증

- `EnemyHeavyTankerShieldVerifier`가 정면 20 피해→10 적용, 측면·후면·지속 피해 20 유지, 경로 회전과
  코너 도착 즉시 방향 갱신, 파란 Material·반경·Definition 배선을 확인한다.
- 자폭드론, Defend 오라, 무한 보스 승급 회귀 검증도 함께 실행한다.
- Unity `6000.3.13f1` 배치 컴파일과 위 네 검증이 모두 PASS했고 return code 0으로 종료됐다.

## 2026-08-26 — Stage Studio 1-8 전장 초안 추가

### 결정

- `ch1-08` StageDefinition을 1-2의 전장 제작 데이터에서 복제했다. 경로 제어점, 설치 가능 셀,
  배경 위치·배율과 웨이브 편성을 그대로 초안으로 사용하고, 식별자는 `ch1-08`, 표시 순서는 7,
  진입 조건은 이전 진행도 7, 권장 레벨은 29로 분리했다.
- 사용자가 만든 `stage 1-8.png`를 스테이지 선택 설명 배경과 실제 전투 배경에 모두 연결했다.
  원본 픽셀 크기가 1-2보다 작아 같은 Transform 배율만 복사하면 전장에서 절반가량 작아지므로,
  Sprite bounds 비율로 배율을 보정해 1-2와 동일한 월드 영역을 채우게 했다.
  Stage Studio를 다시 열면 Definition 검색 결과에 1-8이 나타나며, 공용
  `StageRouteTestScene`에 불러와 경로와 설치 셀을 수정할 수 있다.
- `StageEightSetup`은 최초 생성에만 1-2를 복제한다. 이후 같은 메뉴를 실행하면 기존 1-8을
  덮어쓰지 않고 Catalog만 다시 생성해, Stage Studio에서 사람이 수정한 동선·설치 셀이 보존된다.
- Stage Catalog에 로컬 직접 참조를 등록하고 `Stage-ch1-08-Local` Addressables 그룹을 생성했다.

### 의도적으로 하지 않은 것

- 런타임 스테이지별 `.unity` 씬은 만들지 않았다. `_정현stage1-2.unity` 같은 파일은 개인 제작용
  보조 복사본이고, 실제 전투는 공용 `DefenseScene`이 선택된 StageDefinition을 주입받는 구조다.
  스테이지마다 씬을 복제하면 배선 수정이 여러 씬으로 갈라지므로 기존 아키텍처를 유지했다.
- 1-8의 최종 경로와 설치 셀은 임의로 확정하지 않았다. 요청대로 1-2 초안까지만 제공하고 실제
  배경 도로에 맞춘 좌표 편집은 Stage Studio의 Map 탭에서 이어서 수행하도록 남겼다.

### 검증

- Unity `6000.3.13f1` 배치 컴파일이 성공했고 `StageEightSetup` 검증이 PASS했다. 1-8 배경 연결,
  1-2 전장 초안 복제, 배경 월드 크기 보정, 3개 웨이브 유지, Stage Catalog 로컬 참조와
  Addressables 그룹 생성을 확인했다.

### 제작 보조 씬 크기 보정

- 별도로 복제되어 있던 `_정현stage1-8.unity`는 1-7 배경 참조와 `2 × 2` 배율을 그대로 갖고 있었다.
  1-8 이미지는 1-2보다 픽셀 크기가 작아 이 보조 씬에서만 전장이 작게 보였다.
- Editor API로 `AuthoringBattleBackground`를 1-8 Sprite에 다시 연결하고 StageDefinition의 보정 배율
  `3.703 × 3.756`을 적용했다. MapManager의 제작 프리뷰 Definition과 배경 Renderer도 1-8에 맞춰
  다시 배선했으며, 씬 저장 뒤 Sprite GUID·Transform 배율과 Unity 컴파일 성공을 확인했다.

### 1-8 Heavy Tanker 정식 편성

- 1-2 전장 초안을 복제하면서 1웨이브에만 우연히 남아 있던 `enemy-heavytanker`를 1-8의 대표 특수
  적 편성으로 명시했다. 모든 웨이브에 등장하며 수량은 `1 → 1 → 2`, 간격은 2.5초로 설정했다.
- Heavy Tanker는 체력 55와 정면 직접 피해 50% 감소를 함께 가지므로 일반 특수 적의 `1 → 2 → 3`
  증가를 그대로 쓰지 않았다. 처음 두 웨이브에서 한 기의 측면 대응을 학습시키고 마지막 웨이브에만
  두 기를 배치해 난이도 급등을 제한했다.
- `StageEnemyCompositionSetup`의 멱등형 편성에 포함했다. 메뉴를 반복 실행해도 Heavy Tanker 행이
  중복되지 않고 기존 행의 수량·간격만 확정값으로 갱신된다.

### 검증

- Unity `6000.3.13f1` 배치 컴파일과 Heavy Tanker 웨이브별 수량 검증이 PASS했다. Stage Catalog를
  다시 생성했고 `StageAssetValidator`는 8개 스테이지에서 오류 0건을 확인했다.

## 2026-08-26 — CH1 스테이지 웨이브 수 확장

### 결정

- 스테이지별 웨이브 수를 `1-1=3`, `1-2=5`, `1-3=6`, `1-4=7`, `1-5=8`, `1-6=9`,
  `1-7=10`, `1-8=11`로 확장했다. 1-1은 입문 길이를 유지하고 1-2부터 스테이지 번호에 3을
  더한 수만큼 진행한다.
- 기존 1~3웨이브는 사람이 조정한 편성을 그대로 보존했다. 추가 웨이브는 직전 웨이브를 깊은 복사한
  뒤 일반 적을 한 기 늘리고 체력 배율을 `+0.08` 적용해 기존 생성기의 난이도 증가 규칙을 이어간다.
- 힐러와 자폭 드론은 기존 `waveIndex + 1` 규칙을 11웨이브까지 적용하면 최대 10~11기가 동시에
  편성되므로 3기 상한을 추가했다. Defend와 Heavy Tanker는 처음 두 웨이브 1기, 이후 2기 상한을
  유지해 후반 난이도는 일반 적 수와 체력 배율이 담당하게 했다.
- `StageWaveCountExpander` Editor 도구를 추가했다. 목표보다 부족한 웨이브만 추가하며, 이미 목표보다
  많은 수작업 데이터는 자동 삭제하지 않고 중단해 제작 데이터 유실을 막는다.

### 검증

- Unity `6000.3.13f1` 배치 컴파일과 전체 CH1 웨이브 수 검증이 PASS했다. 모든 추가 웨이브에 이름,
  적 편성, 양수 체력 배율이 존재하고 Stage Catalog 재생성 및 `StageAssetValidator` 오류 0건을 확인했다.

## 2026-08-26 — WebGL 1.1.0 릴리스

- PR #29의 적 특수 능력과 CH1 확장은 새 런타임 C#과 셰이더를 포함하므로 1.0.1 콘텐츠 드랍으로 우회하지 않고, 최신 main과 통합한 새 플레이어 호환성 라인 `1.1.0`으로 빌드했다.
- Unity `6000.3.13f1`에서 컴파일, Defend·Heavy Tanker·사망 플립북·자폭드론·1-8·CH1 웨이브 수 검증과 Addressables WebGL 사전 검증을 통과했다. WebGL 플레이어는 241,555,970바이트로 완료됐고 `ReleaseStates/WebGL/1.1.0/addressables_content_state.bin`을 같은 빌드 직후 보관했다.
- 업로드용 `ServerData.zip`은 62,637,955바이트이며 `catalog_1.1.0`과 기존 원격 오퍼레이터 번들을 포함한다. 신규 Defend와 1-8은 Local 그룹이므로 플레이어 빌드에 포함되고, ZIP만 기존 1.0.1 플레이어에 올리는 배포는 지원하지 않는다.

## 2026-08-26 — 타이틀 인사·튜토리얼 스킵·스토리 연속 출격

### 결정

- `OperatorDialogueSet`에 `titleLobbyGreeting`을 별도 슬롯으로 추가했다. 기존 `gameStart`는
  DefenseScene 전투 개시 대사이고, 새 슬롯은 Press Any Start 전환이 끝나 메인 로비가 완전히
  보이는 시점에만 재생한다. 두 상황을 같은 슬롯으로 재사용하면 전투 대사와 귀환 인사의 문맥 및
  로비 전신 스프라이트가 묶이므로 분리했다.
- Operator Studio의 Dialogue 탭에서 `타이틀 진입 인사`를 문장별 로비 전신 스프라이트와 함께
  편집하도록 연결했다. 자동 출력에는 별도 클릭음을 겹치지 않고, 기존 로비 클릭 대사와 동일한
  랜덤 문장·스프라이트·자동 페이드 경로만 재사용한다.
- 튜토리얼 패널 우측 상단에 `SKIP` uGUI 버튼을 추가했다. Next 마지막 페이지와 Skip이 하나의
  완료 경로를 사용해 `Time.timeScale` 복원과 패널 비활성화가 서로 달라지지 않게 했다.
- 스토리 모드 승리 결과 화면에만 `NEXT STAGE` uGUI 버튼을 표시한다. 현재 StageCatalog 항목과
  같은 챕터에서 더 큰 `order` 중 가장 가까운 플레이 가능 항목을 선택하고, 기존
  `StageContentLoader`와 `BattleContentCache`를 거쳐 원격 스테이지와 적까지 준비한 뒤 같은
  DefenseScene을 다시 로드한다. 엔드리스·패배·마지막 스테이지에는 버튼을 표시하지 않는다.
- `DefenseFlowUISetup`을 반복 가능한 Editor 메뉴로 남겨 기존 사람이 배치한 결과·튜토리얼 패널을
  재구성하지 않고 버튼 자식과 직렬화 참조만 생성·검증하도록 했다.

### 의도적으로 하지 않은 것

- 튜토리얼 스킵 여부를 계정에 저장하지 않았다. 이번 요청은 현재 튜토리얼을 즉시 닫는 조작이며,
  최초 1회 표시 정책이나 다시 보기 정책은 별도 기획 결정이 필요하다.
- 다음 스테이지용 새 씬이나 매니저를 만들지 않았다. 기존의 공용 DefenseScene + StageDefinition
  주입 구조를 유지해 스테이지 추가가 씬 복제와 코드 분기로 이어지지 않게 했다.

### 검증

- Unity `6000.3.13f1` 연결형 CLI에서 스크립트 컴파일이 오류 없이 완료됐다.
- Editor 검증 메뉴로 DefenseScene의 Skip/Next Stage/StageCatalog 참조와 EventSystem 1개를 확인했다.
- PlayMode 및 플레이어 빌드는 실행하지 않았다. Pipeline의 콘솔 조회 명령이 호출 시점마다
  `Access version should be odd when acquiring lock`을 자체 발생시키는 현상이 있어, 컴파일 성공은
  `recompile_status`의 `failed=false, errors=[]` 결과를 정본으로 삼았다.

## 2026-08-26 — 로비·상점 재화 패널 표시 범위 제한

- `CommodityPanel`은 Canvas 직속 형제라 다른 전체 화면 패널을 열어도 부모와 함께 비활성화되지
  않고 항상 최상단에 남아 있었다. 로비 자식으로 옮기면 상점에서 재사용할 수 없으므로 계층은
  보존하고 `TitleSceneController`가 메인 로비 또는 `ShopPanelBackground`가 열린 동안에만 패널을
  활성화하도록 했다.
- 타이틀 첫 프레임에는 즉시 숨기고, 로비·Recruit·Exchange·Enhance·Material 화면에서는 표시한다.
  오퍼레이터 관리, 스테이지 선택, Records, Configuration 등 나머지 화면에서는 비활성화되어
  렌더링과 Raycast를 함께 차단한다. 다시 표시될 때 PlayerProfile의 최신 재화 값을 갱신한다.
- Unity 연결형 CLI 컴파일은 `failed=false, errors=[]`로 완료했으며 PlayMode는 실행하지 않았다.
## 2026-08-26 — 스테이지 선택 해금 정본 통일

- 스테이지 결과는 `PlayerProfile.clearedStageIds`에 클리어를 기록하지만, 스테이지 선택 화면은 초기 프로토타입의 전역 `bestWave`만 읽고 있었다. 이 때문에 결과 화면의 Next Stage는 방금 갱신된 프로필로 다음 전투에 진입할 수 있는 반면, 로비의 스테이지 선택 화면에서는 1-4 이후 노드가 잠긴 채 남는 경로 불일치가 생겼다.
- `StageCatalog`에 프로필 기반 해금 판정을 모았다. 같은 챕터에서 이미 클리어한 스테이지, 그 바로 다음 스테이지, 이미 더 뒤쪽을 클리어한 경우의 앞쪽 재도전 노드를 연다.
- `StageSelectionUI`, 결과 화면의 Next Stage, `LobbyRecordsUI`가 모두 같은 카탈로그 판정을 사용한다. 화면마다 서로 다른 진행 상태를 보여주지 않게 하기 위한 변경이다.
- `clearedStageIds`가 전혀 없는 구버전 저장 데이터에 한해서만 `bestWave`를 레거시 호환값으로 사용한다. 기존 플레이 기록을 즉시 잃지 않으면서, 새 스테이지 진행은 유한 스테이지의 실제 클리어 기록을 정본으로 삼는다.

**검증** — PlayMode와 플레이어 빌드는 실행하지 않았고, Unity 6000.3.13f1 스크립트 재컴파일이 `failed=false`, `errors=[]`로 완료됐다.

## 2026-08-26 — UnitDeploy 유닛 버튼 배경 투명화

- `Content` 자체에는 배경 Image가 없었고, 유닛 뒤의 임시 청색 면은 동적으로 생성되는 `UnitDeployButton` 프리팹 루트 Image의 색상(`alpha 0.96`)이었다.
- 버튼의 Graphic을 제거하면 클릭 Raycast 대상도 사라지므로 Image와 `raycastTarget`은 유지하고 알파만 0으로 변경했다. 아이콘·텍스트·선택 강조는 기존대로 표시된다.
- 현재 프리팹을 Editor API로 저장했으며, 수직 슬라이스 빌더의 생성 기본값과 전용 수정 메뉴도 같은 투명값을 사용하게 해 재생성 시 회귀하지 않도록 했다.

**검증** — 저장된 프리팹에서 루트 Image의 알파가 0임을 확인했고, Unity 6000.3.13f1 스크립트 재컴파일이 `failed=false`, `errors=[]`로 완료됐다. PlayMode와 플레이어 빌드는 실행하지 않았다.

## 2026-08-26 — 레벨업 특전 선택 중 UnitDeploy 탭 일시 숨김

- 유닛 배치 탭이 레벨업 카드 선택 패널보다 앞에서 렌더링되어 카드 일부를 가리던 문제를 수정했다.
- `CardSelectionUI`가 열릴 때 `UnitDeployMenuUI`에 일시 숨김을 요청하고, 카드 선택이 끝나면 해제한다. 두 UI가 이미 사용하는 `CanvasGroup` 계약을 유지해 GameObject 비활성화로 이벤트 구독이 끊기는 문제를 만들지 않았다.
- 복원 시 무조건 표시하지 않고 현재 오퍼레이터가 실제 유닛 로스터를 제공하는지 다시 판정한다. 따라서 타워형 오퍼레이터에게 없던 UnitDeploy 탭이 잘못 나타나지 않는다.
- 기존 DefenseScene 재배선 없이 동작하도록 직렬화 참조를 우선 사용하고, 비어 있는 기존 씬에서는 같은 전투 Canvas의 단일 `UnitDeployMenuUI`를 찾아 연결한다.

**검증** — PlayMode와 플레이어 빌드는 실행하지 않았고, Unity 6000.3.13f1 스크립트 재컴파일이 `failed=false`, `errors=[]`로 완료됐다.
