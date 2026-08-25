# Stage Studio 사용법

## 열기

Unity 상단 메뉴에서 `RCCom > Stages > Open Stage Studio`를 선택한다.

## 기본 작업 흐름

1. 왼쪽 목록에서 스테이지를 선택하거나 `New Stage`로 새 `StageDefinition`을 만든다.
2. `Identity`에서 ID, 챕터, 표시 이름, 부제, 추천 레벨, 순서, 해금 조건을 입력한다.
3. `Description`과 가로형 `Description Background` Sprite를 연결한다.
4. `Map`에서 전투 배경, 경로, 타워 설치 슬롯을 편성한다(아래 "맵 제작" 참고).
5. `Waves`에서 웨이브를 추가하고 각 웨이브의 준비 시간, 적 체력 배율, 적 종류·수량·간격을 편성한다.
6. `Rewards`에서 표시용 Reward ID, 이름, 아이콘, 수량을 입력한다.
7. `Publish`에서 `Validate Current Stage`로 현재 데이터를 검사한다.
8. `Save & Rebuild Stage Catalog`를 눌러 선택 화면용 카탈로그까지 갱신한다.

## 맵 제작 (배경 · 경로 · 설치 슬롯)

`Map` 탭의 `Open Test Scene & Load Selected Stage`를 누르면 공용 `StageRouteTestScene`이 열리고 선택한
스테이지의 배경·경로 점·설치 슬롯이 그 씬에 로드된다. 스테이지마다 씬을 따로 만들지 않고 이 한 장을
작업대로 재사용한다.

- **배경**: `AuthoringBattleBackground` 오브젝트의 Transform과 Sprite를 조정한다.
- **경로**: `StageRouteAuthoring/RoutePoints` 아래 포인트를 옮기거나 추가/삭제한다. 첫 점은 적 생성점,
  마지막 점은 거점 및 아군 출격점이다.
- **설치 슬롯**: Hierarchy에서 `SlotMarkers`를 선택하고 `Window > 2D > Tile Palette`를 열어 타워를 설치할
  수 있는 칸을 칠하거나 지운다. `SlotMarkers`의 `TilemapRenderer`는 런타임에 항상 꺼져 있어(설치 슬롯은
  순수 데이터라 화면에 그리지 않는다) Scene View에 안 보일 수 있는데, 이 씬에서는 청록색 와이어큐브
  Gizmo가 대신 칠해진 셀을 보여준다.
  - **Load하면 이 스테이지에 저장된 값(없으면 완전히 빈 상태)으로 초기화된다.** DefenseScene의 기존
    기본 레이아웃을 자동으로 미리 칠해 주지 않는다 — 맵 모양이 다른 새 스테이지를 그 위에 이어 칠하고
    Capture하면 원치 않는 옛 레이아웃까지 함께 저장되는 사고를 막기 위해서다.
  - 기존 DefenseScene 기본 레이아웃을 일부러 시작점으로 쓰고 싶다면(예: 옛 경로를 그대로 재사용하는
    스테이지) `Copy DefenseScene Default Layout Into Test Scene` 버튼을 명시적으로 눌러야 한다.

다 편집했으면 `Capture Test Scene Into Selected Stage`로 세 가지를 한 번에 `StageDefinition`에 되돌려
저장한다. **Capture는 그 순간 SlotMarkers에 실제로 칠해져 있는 칸을 있는 그대로 저장한다** — 그러니
캡처 전에 원치 않는 칸(특히 옛 기본 레이아웃 잔여분)이 남아 있지 않은지 확인할 것. 반대로 설치 슬롯을
한 번도 칠하지 않고 비워 둔 채 저장하면(0칸), 런타임에서는 DefenseScene의 기본 슬롯 레이아웃을 그대로
쓴다 — 이건 Studio가 미리 채워주는 게 아니라 `MapManager`의 Play 타임 폴백일 뿐이다.

## 데이터 책임

- `StageDefinition`이 제작 원본이다.
- `StageCatalog`는 선택 화면용 생성물이므로 직접 편집하지 않는다.
- `Stage Description Background`는 약 3.2:1의 가로형 이미지를 권장한다.
- 보상 목록은 현재 표시용 매니페스트다. 실제 계정 재화·인벤토리 지급은 해당 시스템이 확정된 뒤 `rewardId`로 연결한다.
- `RCCom > Stages > Build Mode and Chapter UI`를 다시 실행해도 Studio에서 편집한 기존 StageDefinition은 덮어쓰지 않는다.
