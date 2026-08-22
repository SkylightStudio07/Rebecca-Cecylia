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
