# Stage Studio 사용법

## 열기

Unity 상단 메뉴에서 `RCCom > Stages > Open Stage Studio`를 선택한다.

## 기본 작업 흐름

1. 왼쪽 목록에서 스테이지를 선택하거나 `New Stage`로 새 `StageDefinition`을 만든다.
2. `Identity`에서 ID, 챕터, 표시 이름, 부제, 추천 레벨, 순서, 해금 조건을 입력한다.
3. `Description`과 가로형 `Description Background` Sprite를 연결한다.
4. `Waves`에서 웨이브를 추가하고 각 웨이브의 준비 시간, 적 체력 배율, 적 종류·수량·간격을 편성한다.
5. `Rewards`에서 표시용 Reward ID, 이름, 아이콘, 수량을 입력한다.
6. `Publish`에서 `Validate Current Stage`로 현재 데이터를 검사한다.
7. `Save & Rebuild Stage Catalog`를 눌러 선택 화면용 카탈로그까지 갱신한다.

## 데이터 책임

- `StageDefinition`이 제작 원본이다.
- `StageCatalog`는 선택 화면용 생성물이므로 직접 편집하지 않는다.
- `Stage Description Background`는 약 3.2:1의 가로형 이미지를 권장한다.
- 보상 목록은 현재 표시용 매니페스트다. 실제 계정 재화·인벤토리 지급은 해당 시스템이 확정된 뒤 `rewardId`로 연결한다.
- `RCCom > Stages > Build Mode and Chapter UI`를 다시 실행해도 Studio에서 편집한 기존 StageDefinition은 덮어쓰지 않는다.
