using System;
using RCCom.Definitions.Tower;
using RCCom.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RCCom.Runtime
{
    /// <summary>
    /// 그리드 클릭으로 타워를 설치하는 입력 컨트롤러. Player/Base와 마찬가지로 개체가
    /// 하나뿐이라 그냥 MonoBehaviour — 별도 "TowerManager"가 아니다. 이건 타워 오브젝트
    /// 자체를 관리하는 게 아니라 "설치 입력"만 처리하는 컨트롤러라는 점에 유의
    /// (매니저 아키텍처 원칙, ARCHITECTURE.md 5단계).
    ///
    /// 타워 종류 선택은 TowerBuildMenuUI의 스크롤 버튼 목록이 SelectTower(index)를 호출하는
    /// 방식 — 예전엔 숫자키(1~6)로 임시 처리했었지만, 빌드 메뉴 클릭과 입력이 자꾸 겹쳐서
    /// ({{user}} 발견) 제거함.
    ///
    /// 카드로 신규 타워가 "해금"되면 기존 기본형을 대체하는 게 아니라 로스터에 추가된다 —
    /// 슬롯 제한이 TowerKind(공격/스킬/파워) 단위라, 같은 종류의 기본형/특수형이 같은 슬롯
    /// 한도를 나눠 쓰며 공존한다 (상황에 따라 골라 짓는 선택지).
    /// </summary>
    public class TowerBuildController : MonoBehaviour
    {
        [SerializeField] private MapManager mapManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TowerRoster towerRoster;
        [SerializeField] private DeploymentInputModeController inputModeController;

        [Tooltip("이미 지어진 타워를 클릭했을 때 사거리를 보여줄 공용 인디케이터 (TowerBuildPreview와 같은 오브젝트를 공유해도 됨)")]
        [SerializeField] private RangeIndicator rangeIndicator;

        /// <summary>슬롯 부족(이미 사용 중이거나 종류별 한도 초과)으로 건설 실패 시 — 오퍼레이터 대사가 구독.</summary>
        public event Action BuildFailedNoSlot;

        /// <summary>골드 부족으로 건설 실패 시 — 오퍼레이터 대사가 구독.</summary>
        public event Action BuildFailedInsufficientGold;

        /// <summary>null = 건설 모드 꺼짐(우클릭으로 취소한 상태). 초기값은 미선택.</summary>
        private int? _selectedIndex;

        /// <summary>TowerBuildPreview가 현재 뭘 지으려는지 알아야 해서 공개. 건설 모드가 꺼져 있으면 null.</summary>
        public TowerDefinition SelectedDefinition =>
            IsTowerBuildModeActive && _selectedIndex.HasValue && _selectedIndex.Value < towerRoster.towers.Count
                ? towerRoster.towers[_selectedIndex.Value]
                : null;

        public bool IsTowerBuildModeActive =>
            inputModeController == null || inputModeController.CurrentMode == DeploymentInputMode.TowerBuild;

        private void Awake()
        {
            inputModeController = DeploymentInputModeController.Resolve(inputModeController);

            // 원본 에셋이 아니라 세션 전용 복제본을 참조하도록 교체 — 카드가 Data를 직접
            // 수정해도 원본 .asset이 오염되지 않게 함 (TowerRoster.GetRuntimeInstance 참고).
            towerRoster = OperatorLoadoutSession.ResolveTowerRoster(towerRoster).GetRuntimeInstance();
        }

        private void OnEnable()
        {
            if (inputModeController != null)
            {
                inputModeController.ModeChanged += HandleInputModeChanged;
            }
        }

        private void OnDisable()
        {
            if (inputModeController != null)
            {
                inputModeController.ModeChanged -= HandleInputModeChanged;
            }
        }

        private void Update()
        {
            // 카드 선택/게임오버/튜토리얼 등 게임을 멈추는 모든 상황이 공통적으로 Time.timeScale=0을
            // 쓰므로, 그 하나만 확인하면 건설 모드를 전부 막을 수 있다 — 개별 매니저 상태를 하나씩
            // 참조할 필요가 없어 새로운 화면(튜토리얼 등)이 늘어나도 이 코드를 안 건드려도 된다.
            // timeScale=0은 deltaTime 기반 로직만 멈추지 Update()/Input System 폴링은 계속 돌아서
            // 이 게이트가 없으면 패널/결과 화면/튜토리얼 뒤에서 건설이 그대로 동작해버린다.
            if (Time.timeScale <= 0f)
            {
                return;
            }

            // UI 버튼을 누른 같은 프레임의 포인터 입력이 뒤쪽 월드 셀까지 전달되면, 모드가
            // 전환되기 전에 타워가 설치될 수 있다. 입력 모드와 별개로 UI 포인터는 여기서 차단한다.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            HandleCancelSelection();

            // clickAction("UI/Click")은 눌림/뗌 둘 다에서 Performed가 발생하는 상호작용으로
            // 설정돼 있어, 클릭 한 번에 건설 로직이 두 번(누를 때 성공 → 뗄 때 같은 셀이 이미
            // 점유돼 실패) 실행되는 버그가 있었다 ({{user}} 발견: 타워는 정상 설치되는데
            // "슬롯 부족" 대사가 같이 뜨는 현상). 이 파일의 우클릭/숫자키처럼 Mouse.current를
            // 직접 폴링해 눌리는 순간에만 정확히 한 번 반응하도록 통일.
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            bool isAltHeld = Keyboard.current != null &&
                (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);

            if (isAltHeld)
            {
                HandleDemolishClick();
                return;
            }

            if (!TryGetClickedCell(out Vector3Int cell))
            {
                if (rangeIndicator != null)
                {
                    rangeIndicator.Hide();
                }

                return;
            }

            HandleBuildClick(cell);
            HandleRangeInspectClick(cell);
        }

        /// <summary>
        /// 클릭한 셀에 이미 지어진 타워가 있으면 사거리 원을 보여준다 — 건설 시도(HandleBuildClick)
        /// 성공/실패와 무관하게 독립적으로 동작(건설 직후 방금 지은 타워의 사거리가 바로 보이는
        /// 효과도 겸함, {{user}} 요청 "설치할 때부터 표시"와 자연스럽게 맞물림).
        /// </summary>
        private void HandleRangeInspectClick(Vector3Int cell)
        {
            if (rangeIndicator == null)
            {
                return;
            }

            if (mapManager.TryGetTowerAt(cell, out TowerInstance instance))
            {
                rangeIndicator.Show(instance.Position, instance.DisplayRange);
            }
            else
            {
                rangeIndicator.Hide();
            }
        }

        /// <summary>우클릭 = 건설 모드 취소 (선택 해제). 다시 숫자키/버튼으로 선택해야 건설 가능.</summary>
        private void HandleCancelSelection()
        {
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                ClearSelection();
                Debug.Log("[Build] 건설 모드 취소");
            }
        }

        /// <summary>빌드 메뉴 UI 버튼(OnClick)이 호출 — 숫자키 선택은 제거됨(스크롤 빌드 메뉴로 대체됨).</summary>
        public void SelectTower(int index)
        {
            if (index < 0 || index >= towerRoster.towers.Count)
            {
                return;
            }

            if (inputModeController != null)
            {
                inputModeController.EnterMode(DeploymentInputMode.TowerBuild);
            }

            _selectedIndex = index;
            Debug.Log($"[Build] 선택: {towerRoster.towers[index].Data.displayName}");
        }

        public void ClearSelection()
        {
            ClearSelectionState();

            if (inputModeController != null)
            {
                inputModeController.ClearMode(DeploymentInputMode.TowerBuild);
            }
        }

        private void HandleInputModeChanged(DeploymentInputMode mode)
        {
            if (mode != DeploymentInputMode.TowerBuild)
            {
                ClearSelectionState();
            }
        }

        private void ClearSelectionState()
        {
            _selectedIndex = null;

            // TowerBuildPreview는 선택이 없으면 인디케이터를 건드리지 않으므로 모드가 바뀌는
            // 그 시점에 이전 타워의 프리뷰 사거리를 함께 정리한다.
            if (rangeIndicator != null)
            {
                rangeIndicator.Hide();
            }
        }

        private void HandleBuildClick(Vector3Int cell)
        {
            if (!_selectedIndex.HasValue || _selectedIndex.Value >= towerRoster.towers.Count)
            {
                return;
            }

            TowerDefinition definition = towerRoster.towers[_selectedIndex.Value];

            if (!mapManager.CanBuild(definition.Data.kind, cell))
            {
                Debug.Log("[Build] 설치 불가 — 이미 사용 중이거나 종류별 한도 초과");
                BuildFailedNoSlot?.Invoke();
                return;
            }

            if (!gameManager.TrySpendGold(definition.Data.buildCost))
            {
                Debug.Log($"[Build] 골드 부족 (필요 {definition.Data.buildCost}, 보유 {gameManager.Gold})");
                BuildFailedInsufficientGold?.Invoke();
                return;
            }

            mapManager.Build(definition, cell);
            Debug.Log($"[Build] {definition.Data.displayName} 건설 완료 (셀 {cell})");

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayTowerBuild();
            }
        }

        /// <summary>
        /// Alt+클릭 = 철거. 환불이 아니라 반대로 건설비의 DemolishCostRatio(기본 75%)만큼
        /// 추가로 지불해야 한다 — 건설/철거를 마구 반복해 슬롯을 우회하는 걸 막기 위한 페널티.
        /// 이 비율은 카드로 할인 가능 (GameManager.ReduceDemolishCostRatio).
        /// </summary>
        private void HandleDemolishClick()
        {
            if (!TryGetClickedCell(out Vector3Int cell) || !mapManager.TryGetTowerAt(cell, out TowerInstance instance))
            {
                Debug.Log("[Demolish] 철거 불가 — 그 셀에 타워가 없음");
                return;
            }

            int cost = Mathf.CeilToInt(instance.Data.buildCost * gameManager.DemolishCostRatio);

            if (!gameManager.TrySpendGold(cost))
            {
                Debug.Log($"[Demolish] 골드 부족 (철거 비용 {cost}, 보유 {gameManager.Gold})");
                return;
            }

            string demolishedName = instance.Data.displayName;
            mapManager.RemoveTower(cell);
            Debug.Log($"[Demolish] {demolishedName} 철거 완료 (셀 {cell}, 비용 {cost})");

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayTowerDemolish();
            }
        }

        private bool TryGetClickedCell(out Vector3Int cell)
        {
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            worldPosition.z = 0f;

            if (!mapManager.TryGetSlotCell(worldPosition, out cell))
            {
                Debug.Log("[Build] 대상 없음 — 슬롯이 아님");
                return false;
            }

            return true;
        }
    }
}
