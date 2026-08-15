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
