# Addressables 라이브 드랍 가이드

플레이어를 다시 빌드하지 않고 CDN에 콘텐츠만 올려서 신규 오퍼레이터·스테이지를 제공하는 절차.

이 문서가 Addressables 빌드·서빙의 정본이다. 처음이라면 §1(경계선)과 §2(구조)를 읽고
§4 또는 §5의 절차를 따라간다.

---

## 1. 무엇이 되고 무엇이 안 되는가

**재빌드 없이 배포 가능**

- 신규 오퍼레이터 — 기존 효과 SO 조립만으로 만든 것
- 신규 아군 유닛 / 신규 적 — 위 오퍼레이터에 딸려서, 또는 단독으로
- 신규 스테이지 — 웨이브 편성, 경로, 배경, 보상
- 위 콘텐츠의 아트·대사·수치·강화 트랙
- 이미 배포된 원격 항목의 내용 교체 (발렌티나 수치 조정 등)
- 로딩 화면 팁 (`ui/loading-tips`)

**플레이어 재빌드 필수**

- **런타임 C# 코드 변경 — 한 줄이라도.** 이게 가장 중요한 제약이다.
- 새 카드 로직 (`CardEffectBase` 파생 클래스)
- 새 타워 종류 (`TowerData` 파생 클래스)
- 신규 **로컬** 오퍼레이터/스테이지 (`remoteContent`를 끈 것)
- 씬 변경, 프리팹 구조 변경

### 왜 C# 한 줄도 안 되는가

번들 안의 ScriptableObject는 스크립트를 `MonoScript` 참조(어셈블리명 + 네임스페이스 +
클래스명)로 직렬화한다. 그 참조를 실제 타입으로 해결하는 것이 `..._monoscripts_<해시>.bundle`
인데, 이 번들의 로드 경로는 `{Addressables.RuntimePath}` — 즉 **플레이어 자신의
StreamingAssets**다. 원격으로 갈아끼울 수 없다.

그리고 그 해시는 프로젝트의 스크립트 내용에서 나온다. C#을 고치면 해시가 바뀌고, 새로 구운
카탈로그는 구 플레이어에 존재하지 않는 monoscripts 번들을 가리키게 된다. 실제로 이번 작업
전 저장소가 그 상태였다 — `Builds/WebGL`은 `..._cbdfbb56...`, 서빙 중이던 카탈로그는
`..._466d9331...`을 가리키고 있었다.

WebGL은 IL2CPP AOT라 어셈블리 동적 로드라는 우회로도 없다.

> **드랍 전용 작업 중에는 `Assets/Scripts/` 아래를 건드리지 않는다.**
> 에디터 툴(`Assets/Editor/`)은 플레이어에 들어가지 않으므로 고쳐도 된다.

---

## 2. 구조 — 3층

```
[1] 내장 카탈로그        OperatorCatalog.asset / StageCatalog.asset
    플레이어 빌드에 박힘.  씬 UI가 [SerializeField]로 직접 참조한다.
    빌드 시점의 전체 목록. 배포 후에는 절대 바뀌지 않는다.
                              │
                              │ LiveCatalogService가 부팅 시 병합 (추가 전용)
                              ▼
[2] 라이브 카탈로그       catalog/operator, catalog/stage
    원격 그룹 Catalog-Live-Remote.  "빌드 이후 추가된 항목"만 담는다.
    로컬 에셋 참조가 0이라 번들이 작고 로컬 그룹과 교차 의존이 없다.
                              │
                              │ 화면이 항목의 address로 온디맨드 로드
                              ▼
[3] 콘텐츠 번들           operator/{id}, stage/{id}, ally-unit/{id}, enemy/{id}
    오퍼레이터 1명 = 그룹 1개, 스테이지 1개 = 그룹 1개.
    초상화·배경은 PackSeparately로 쪼개 한 장만 먼저 받을 수 있다.
```

### 병합 규칙 — 추가 전용

`LiveCatalogService`는 **내장 카탈로그에 없는 ID만** 뒤에 덧붙인다. 이미 빌드에 있는 항목은
내장본을 그대로 쓴다.

기존 항목까지 원격본으로 갈아치우지 않는 이유: 로컬 오퍼레이터의 카탈로그 항목은 스프라이트를
직접 참조하므로, 그걸 원격 번들에 실으면 로컬 아트가 전부 원격 번들의 의존으로 딸려 들어간다.
번들이 비대해지고 static으로 묶어둔 로컬 그룹과 교차 의존이 생겨 콘텐츠 업데이트가 깨진다.

원격 항목은 빌더(`CreateEntry`)가 모든 Sprite 참조를 `null`로 비우고 주소 문자열만 남기므로
이 문제가 없다. `OperatorLiveCatalogBuilder`/`StageLiveCatalogBuilder`가 그 불변식을 빌드
시점에 검사하고, 깨지면 예외를 던진다.

> 이미 배포된 항목의 **내용 교체**는 된다(§5). 안 되는 것은 그 항목의 **카탈로그 메타데이터**
> (표시 이름, 해금 조건, 가격 등) 교체다. 그건 내장 카탈로그에 있기 때문이다.

### 그룹 정책 — 로컬은 static, 원격은 non-static

`AddressableGroupPolicy`가 강제한다. 로컬 그룹은 `StaticContent = true`(인스펙터의
"Prevent Updates"), 원격 그룹은 `false`.

이 프로젝트는 `BuildRemoteCatalog`가 켜져 있고 `DisableCatalogUpdateOnStart`가 꺼져 있어서,
배포된 플레이어가 **부팅할 때마다 원격 카탈로그로 자기 카탈로그를 갈아탄다.** 그런데 그
카탈로그에는 원격 그룹뿐 아니라 **로컬 그룹 엔트리까지 전부** 들어 있고, 그 엔트리의 로드
경로는 플레이어 자신의 StreamingAssets를 가리킨다.

로컬 그룹이 static이 아니면 콘텐츠 업데이트가 로컬 번들까지 새 해시로 다시 굽고, 새 카탈로그가
구 플레이어에 없는 파일명을 가리키게 되어 **잘 돌던 기존 콘텐츠까지 통째로 깨진다.**

`RCCom/Addressables/Validate Active Build Configuration`이 이걸 빌드 전에 검사한다.

---

## 3. 릴리스는 두 종류다

| | 플레이어 릴리스 (§4) | 라이브 드랍 (§5) |
|---|---|---|
| 언제 | 코드 변경, 씬 변경, 신규 로컬 콘텐츠 | 원격 콘텐츠만 추가·수정 |
| 빌드 | New Build + 플레이어 빌드 | Content Update |
| 산출물 | `Builds/` + `ServerData/` | `ServerData/`만 |
| 사용자 | 새 빌드를 받아야 함 | **기존 빌드 그대로, 새로고침만** |
| 기준점 | 새로 만든다 | §4가 남긴 것을 쓴다 |

라이브 드랍은 **플레이어 릴리스가 남긴 콘텐츠 상태 파일이 있어야만** 가능하다. 그 파일이
`ReleaseStates/`에 있다.

---

## 4. 절차 A — 플레이어 릴리스 (기준점 만들기)

### 4-1. 버전 확인

`Edit ▸ Project Settings ▸ Player ▸ Version` (`PlayerSettings.bundleVersion`).

> **이미 배포한 버전으로 다시 빌드하지 않는다.** 같은 버전으로 다시 빌드하면
> `ReleaseStates/{타깃}/{버전}/`을 덮어쓰고, 그 순간 이전에 배포한 빌드에는 두 번 다시
> 콘텐츠를 내려보낼 수 없게 된다. 배포했으면 버전을 올리고 다음 작업을 시작한다.
> (덮어쓸 때 경고 로그가 남지만, 로그를 보기 전에 이미 덮어써진다.)

### 4-2. 원격 서버 주소 설정 (최초 1회, 또는 서버가 바뀔 때)

리포에 실서버 URL을 하드코딩하지 않고 환경 변수로 주입한다.

```
RCCOM_REMOTE_LOAD_PATH=https://arcade.codingbot.kr/content/<gameId>/<channel>/[BuildTarget]
RCCOM_REMOTE_BUILD_PATH=ServerData/[BuildTarget]     # 선택, 기본값이 이것
```

- **HTTPS 필수.** 로컬 스파이크만 `http://localhost` / `http://127.0.0.1` 허용
  (`AddressablesRemoteProfileConfigurator.NormalizeRemoteLoadPath`가 강제).
- Windows에서 `setx`로 설정했다면 **이미 떠 있는 Unity Hub/에디터를 재시작해야 반영된다.**
  `setx`는 레지스트리에 쓰고 브로드캐스트만 할 뿐, 실행 중인 프로세스의 환경변수 블록은
  갱신하지 않는다.

```
RCCom/Addressables/Configure Active Remote Profile From Environment
```

활성 프로필의 `Remote.LoadPath`/`Remote.BuildPath`를 채우고 `BuildRemoteCatalog`를 켠다.
꺼져 있으면 카탈로그가 본체 빌드에 내장되어 라이브 드랍 자체가 성립하지 않는다.

> **`Remote.LoadPath`는 배포 후 바꿀 수 없다.** 구 플레이어는 자기가 구워진 주소만 보므로,
> 주소를 바꾸면 그 빌드는 새 카탈로그를 영영 못 받는다. 콘텐츠 업데이트 빌드도 이 불일치를
> 감지하면 시작 자체를 거부한다.

### 4-3. 콘텐츠 에셋 갱신

레시피/Studio에서 콘텐츠를 편집했다면 순서대로 실행한다.

```
RCCom/Operators/Build Operator Catalog And Addressables
RCCom/Stages/Rebuild Stage Catalog
RCCom/Addressables/Apply Group Update Policy
```

- 1번은 `AllyUnitCatalogBuilder`도 함께 돌려 유닛 카탈로그를 맞추고, 원격 오퍼레이터만 담은
  `OperatorLiveCatalog.asset`을 만든다.
- 3번은 빌더가 관리하지 않는 그룹(`Default Local Group`, `UI-Live-Remote`)까지 정책을 맞춘다.
  빌더를 돌린 직후라면 대부분 "0개 갱신"이 나오는 게 정상이다.

### 4-4. 검증

```
RCCom/Addressables/Validate Active Build Configuration
```

여기서 걸리는 것: 그룹 업데이트 정책 불일치, 라이브 카탈로그 누락/오배치, WebGL에 localhost
원격 주소, 오퍼레이터 에셋 정합성.

### 4-5. 빌드

```
unity build --target WebGL --execute-method BuildScript.BuildWebGL
```

`BuildScript`가 하는 일:

1. 구성 검증 (§4-4와 동일)
2. Addressables New Build (`BuildPlayerContent`) → `ServerData/[BuildTarget]/`
3. 플레이어 빌드 → `Builds/WebGL`
4. **성공했을 때만** 콘텐츠 상태를 `ReleaseStates/{타깃}/{bundleVersion}/`에 보관

4번이 라이브 드랍의 기준점이다. 실패한 빌드의 상태를 남기면 이후 드랍이 배포된 적 없는 기준으로
나가므로, 성공한 뒤에만 보관한다.

보관 결과:

```
ReleaseStates/WebGL/1.0/
  addressables_content_state.bin   # 콘텐츠 업데이트의 기준점
  release.json                     # 버전/타깃/유니티/원격 경로
```

**이 폴더는 반드시 커밋한다.** Addressables 기본 경로의 상태 파일은 `.gitignore` 대상이고
콘텐츠를 다시 구울 때마다 덮어써진다. 유실되면 그 빌드는 영구히 드랍 불가다.

### 4-6. 업로드

§6으로.

---

## 5. 절차 B — 라이브 드랍 (플레이어 재빌드 없이)

전제: §4가 남긴 `ReleaseStates/{타깃}/{현재 bundleVersion}/addressables_content_state.bin`이
있고, **그 이후로 런타임 C#을 건드리지 않았다.**

### 5-1. 콘텐츠 만들기

평소대로 만들되 **`remoteContent`를 켠다.**

- 오퍼레이터: `Assets/Editor/OperatorRecipes/{Name}.json`의 `remoteContent: true`
  (Operator Studio의 `Package` 탭에서도 설정 가능)
- 아군 유닛: `AllyUnitAssetRecipe`의 `remoteContent`
- 적: `EnemyAssetRecipe`의 `remoteContent`
- 스테이지: `StageDefinition`의 `Remote Content` 체크박스 (스테이지는 SO가 제작 원본이라
  레시피가 아니라 여기에 있다)

끄면 로컬 그룹으로 들어가고, 로컬 그룹은 static이라 이미 배포된 빌드에는 절대 닿지 않는다.

### 5-2. 에셋 갱신

```
RCCom/Operators/Build Operator Catalog And Addressables
RCCom/Stages/Rebuild Stage Catalog
RCCom/Addressables/Apply Group Update Policy
RCCom/Addressables/Validate Active Build Configuration
```

§4-3, §4-4와 같다.

### 5-3. 콘텐츠 업데이트 빌드

```
RCCom/Addressables/Build Content Update (Live Drop)
```

또는 CLI:

```
unity command eval_file Tools/eval/BuildAddressablesContent.cs   # New Build (기준점 만들 때)
unity command eval_file Tools/eval/BuildAddressablesContentUpdate.cs  # Content Update
```

이 메뉴가 하는 일:

1. `ReleaseStates/{타깃}/{bundleVersion}/`의 상태 파일을 찾는다. 없으면 **거부한다** —
   보관본 없이 New Build를 올리면 배포된 빌드가 깨지기 때문이다.
2. 이전 빌드 이후 바뀐 엔트리를 콘솔에 나열한다.
3. 그중 **static(로컬) 그룹에 있어 이번 드랍으로 나가지 않는 변경**을 따로 경고한다.
   여기 이름이 뜬 콘텐츠는 `remoteContent`를 안 켰거나 로컬 전용이다.
4. `ContentUpdateScript.BuildContentUpdate` 실행 → `ServerData/[BuildTarget]/`

> 3번 경고를 반드시 읽는다. "고쳤는데 반영이 안 된다"의 원인이 거의 전부 여기다.

### 5-4. 업로드

§6으로. 라이브 드랍은 **반드시 merge**로 올린다(replace 금지) — 구 카탈로그가 아직 참조하는
번들을 지우면 안 되기 때문이다.

---

## 6. 업로드 · 검증 · 롤백

### 6-1. zip 만들기

```
RCCom/Addressables/Package ServerData Zip
```

`Remote.BuildPath` 프로필 값을 읽어 `ServerData/` 폴더 **자체**를 압축한다(내용물만이 아니라).
결과는 프로젝트 루트의 `ServerData.zip`.

> GBaaS 업로드 API는 zip 최상위의 단일 래퍼 폴더(`serverdata`/`streamingassets`, 대소문자
> 무관)를 자동으로 벗겨내고 그 아래를 채널에 그대로 설치한다. `ServerData/WebGL/...`처럼
> `[BuildTarget]` 디렉터리가 zip 안에 살아있어야 한다. `WebGL/` 내용물만 넣으면 그 레벨이
> 사라져 업로드는 성공해도 런타임 카탈로그/번들 URL이 전부 404가 된다.

### 6-2. 업로드

대시보드의 Addressables content 탭에서 zip을 업로드한다.

- **채널**은 플레이어/콘텐츠 호환성 라인이다. 기존 플레이어와 호환되지 않는 변경은 새
  채널(`v2` 등)로 분리하고, 그 채널을 `RemoteLoadPath`에 구워서 플레이어를 다시 빌드해야 한다.
- **merge를 쓴다.** merge는 번들을 먼저 쓰고 카탈로그를 마지막에 써서, 옛 카탈로그가 참조할 수도
  있는 번들을 지우지 않는다.
- **replace는 라이브 채널에 쓰지 않는다.** 성공하면 이전 디렉터리가 그냥 삭제되고 서버 쪽
  리비전 히스토리가 없어 복구가 안 된다.
- 업로드 전 계정 스토리지 쿼터를 확인한다 (Builds와 Addressables 콘텐츠가 쿼터를 공유).
- 응답의 `warnings`는 에러가 아니다. HTTP 200은 "압축 해제·설치 성공"일 뿐이므로 레이아웃
  경고를 반드시 확인한다. `catalog_*.bin`(또는 `.json`)을 못 찾겠다는 경고가 뜨면 십중팔구
  `ServerData` 폴더가 `BuildRemoteCatalog`가 꺼져 있던 시점의 옛 산출물이다 — §4-2부터 다시.

### 6-3. 검증

업로드한 BuildTarget마다:

1. `/content/<gameId>/<channel>/<BuildTarget>/catalog_*.bin`과 짝 `.hash`를 요청해 200 확인
2. 그 카탈로그가 참조하는 번들 하나 이상 요청해 200 확인
3. **캐시를 비운 브라우저로 기존 WebGL 플레이어를 띄워** 이번 드랍에만 있는 콘텐츠가
   목록에 나타나는지 확인 — 라이브 드랍의 성공 판정은 이것이다
4. 기존 콘텐츠(로컬 오퍼레이터·스테이지)가 여전히 정상 로드되는지 확인 — static 정책이
   제대로 걸렸는지의 판정이다

이 확인이 끝나기 전엔 이전 채널을 승격하거나 삭제하지 않는다.

### 6-4. 롤백 (merge로 올렸을 때만)

이전 릴리스 아카이브에서 카탈로그 + 짝 `.hash`를 원래 있던 `[BuildTarget]/` 경로 그대로 담아
새 zip을 만들고, 같은 채널에 **merge**로 다시 올린다. 그 카탈로그가 참조하는 모든 번들이 아직
남아 있을 때만 동작한다 — 채널을 지웠거나 한 번이라도 replace를 썼다면 못 쓴다.

### 6-5. CORS

이 플랫폼에 호스팅된 WebGL 플레이어는 같은 오리진이라 CORS가 필요 없다. GitHub Pages 등 외부에
호스팅하면서 `RemoteLoadPath`만 이쪽 `/content/<gameId>/<channel>/`를 가리키는 구성이면 진짜
크로스 오리진 요청이 된다 — 대시보드의 **Allowed external origins**에 그 오리진(스킴 + 호스트
(+포트)만, 경로/트레일링 슬래시 없이)을 등록해야 한다. 게임당 최대 20개, 와일드카드 없음.

### 6-6. 보안 메모

`/content/<gameId>/<channel>/*` GET 경로는 무인증 공개다(WebGL 플레이어가 로그인 없이 받아야
하므로). URL이 커밋 히스토리에 남는 것 자체는 실질적 위험이 아니다 — 출시되면 어차피 모든
플레이어의 브라우저 요청에 노출된다. 업로드/삭제는 별도 인증이 걸려 있다. 다만 **올리는 시점**은
신경 쓴다: 정식 공개 전에 `live` 채널에 실제 콘텐츠를 올리면 URL을 아는 누구나 받아갈 수 있다.

---

## 7. 함정 모음

| 증상 | 원인 |
|---|---|
| 신규 콘텐츠가 기존 빌드에 안 보임 | `remoteContent`를 안 켰다 → 드랍 빌드의 static 경고 확인 |
| 신규 콘텐츠가 안 보이는데 경고도 없음 | 라이브 카탈로그를 안 구웠다 → `Build Operator Catalog And Addressables` |
| 드랍 후 **기존** 콘텐츠까지 깨짐 | 로컬 그룹이 static이 아니거나, C#을 고친 뒤 New Build를 올렸다 |
| 드랍 빌드가 "보관된 콘텐츠 상태가 없습니다"로 실패 | `ReleaseStates/`가 없거나 `bundleVersion`이 배포 시점과 다르다 |
| 드랍 빌드가 "호환되지 않습니다"로 실패 | `Remote.LoadPath`가 원본 플레이어와 다르다 |
| 업로드는 성공했는데 전부 404 | zip에 `[BuildTarget]` 디렉터리 레벨이 없다 (§6-1) |
| 콘솔에 `InvalidKeyException: catalog/operator` | 정상 — 라이브 카탈로그를 아직 한 번도 배포 안 한 빌드. `LiveCatalogService`가 로케이션 조회로 먼저 걸러서 오류를 내지 않지만, 다른 경로로 직접 로드하면 뜬다 |

---

## 8. 메뉴 · 파일 레퍼런스

### 메뉴

| 메뉴 | 하는 일 |
|---|---|
| `RCCom/Addressables/Configure Active Remote Profile From Environment` | 환경 변수에서 원격 경로 주입, `BuildRemoteCatalog` 켜기 |
| `RCCom/Addressables/Apply Group Update Policy` | 모든 그룹의 로컬=static / 원격=non-static 정렬 |
| `RCCom/Addressables/Validate Active Build Configuration` | 빌드 전 전체 검증 |
| `RCCom/Addressables/Build Content Update (Live Drop)` | 보관 상태 기준 증분 빌드 |
| `RCCom/Addressables/Archive Content State For Current Version` | 상태 파일 수동 보관 (플레이어 빌드가 자동으로 하므로 보통 불필요) |
| `RCCom/Addressables/Package ServerData Zip` | 업로드용 zip 생성 |

### 코드

| 파일 | 역할 |
|---|---|
| `Assets/Editor/AddressableGroupPolicy.cs` | 그룹 스키마 정책의 정본 (static 규칙 포함) |
| `Assets/Editor/AddressablesContentUpdateBuilder.cs` | 콘텐츠 업데이트 빌드 + 상태 보관 |
| `Assets/Editor/OperatorLiveCatalogBuilder.cs` | `catalog/operator` 생성·배선 |
| `Assets/Editor/StageLiveCatalogBuilder.cs` | `catalog/stage` 생성·배선 |
| `Assets/Editor/AddressablesBuildValidator.cs` | 빌드 전 검증 |
| `Assets/Scripts/Runtime/LiveCatalogService.cs` | 부팅 시 원격 카탈로그 병합 |
| `Assets/Scripts/Runtime/OperatorContentLoader.cs` | 오퍼레이터 온디맨드 다운로드 |
| `Assets/Scripts/Runtime/StageContentLoader.cs` | 스테이지 온디맨드 다운로드 |
| `Assets/Scripts/Runtime/RemotePreviewSpriteLoader.cs` | 초상화·배경 한 장만 경량 로드 |

### 주소 규칙

| 주소 | 내용 |
|---|---|
| `catalog/operator` · `catalog/stage` | 라이브 카탈로그 |
| `operator/{id}` | `OperatorDefinition` |
| `operator/{id}/portrait/{slot}` | 초상화 (`selection`, `management`, `shop`, `shop-upper`, `shop-upper-dimmed`, `unlock-reward`) |
| `stage/{id}` | `StageDefinition` |
| `stage/{id}/description-background` | 작전 브리핑 배경 |
| `ally-unit/{id}` · `ally-unit/{id}/preview` | 아군 유닛 정의 / 미리보기 아이콘 |
| `enemy/{id}` | `EnemyDefinition` |
| `ui/loading-tips` | 로딩 화면 팁 |
