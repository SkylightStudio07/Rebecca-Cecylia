using System;
using System.Collections.Generic;
using RCCom.Definitions.Unit;
using RCCom.Managers;
using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// 플레이어의 아군 유닛 선택·배치와 자신이 배치한 순수 C# 인스턴스의 Tick 순회를 담당한다.
    /// 유닛 종류 전체를 관리하는 Manager가 아니라 TowerBuildController와 대칭인 입력 흐름
    /// Controller다. 적 목록의 소유권은 WaveManager에 그대로 두고 읽기 전용 후보만 전달한다.
    /// </summary>
    public class UnitDeployController : MonoBehaviour
    {
        [Header("배치 흐름 참조")]
        [SerializeField] private MapManager mapManager;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private DeploymentInputModeController inputModeController;

        [Header("오퍼레이터 유닛 로드아웃")]
        [SerializeField] private AllyUnitRoster allyUnitRoster;

        [Header("모든 유닛이 공유하는 View 프리팹")]
        [SerializeField] private AllyUnitView viewPrefab;

        [Header("아군·적 공통 교전 거리")]
        [SerializeField] private UnitCombatSettings combatSettings;

        [Header("지휘 포인트")]
        [SerializeField, Min(0)] private int startingCommandPoints;
        [SerializeField, Min(1)] private int maxCommandPoints = 100;
        [SerializeField, Min(0f)] private float commandPointRecoveryPerSecond = 4f;

        private readonly List<AllyUnitInstance> _activeUnits = new();
        private readonly Dictionary<AllyUnitInstance, Action> _deathHandlers = new();
        private int? _selectedIndex;
        private int _commandPoints;
        private float _commandPointRecoveryRemainder;

        public IReadOnlyList<AllyUnitInstance> ActiveUnits => _activeUnits;
        public AllyUnitRoster Roster => allyUnitRoster;
        public bool IsAvailable =>
            allyUnitRoster != null && allyUnitRoster.units != null && allyUnitRoster.units.Count > 0;
        public bool IsDeployInputEnabled => Time.timeScale > 0f && IsAvailable;
        public int CommandPoints => _commandPoints;
        public int MaxCommandPoints => maxCommandPoints;
        public float CommandPointRecoveryPerSecond => commandPointRecoveryPerSecond;
        public bool IsAllyUnitDeployModeActive =>
            inputModeController == null || inputModeController.CurrentMode == DeploymentInputMode.AllyUnitDeploy;

        public AllyUnitDefinition SelectedDefinition =>
            _selectedIndex.HasValue && IsValidRosterIndex(_selectedIndex.Value)
                ? allyUnitRoster.units[_selectedIndex.Value]
                : null;

        /// <summary>배치 UI와 후속 HUD가 게임플레이를 역참조하지 않고 상태를 갱신할 수 있도록 알린다.</summary>
        public event Action<AllyUnitInstance> UnitDeployed;
        public event Action<AllyUnitInstance> UnitRemoved;
        public event Action<AllyUnitDefinition> SelectionChanged;
        public event Action<int> CommandPointsChanged;
        public event Action<AllyUnitDefinition> DeployFailedInsufficientCommandPoints;

        private void Awake()
        {
            inputModeController = DeploymentInputModeController.Resolve(inputModeController);

            // 타워형 오퍼레이터는 유닛 로스터가 없는 것이 정상이다. 이 경우 비어 있는 기본
            // 로스터를 섞지 않고 Controller를 안전한 비활성 상태로 유지해 로드아웃 경계를 지킨다.
            allyUnitRoster = OperatorLoadoutSession.ResolveAllyUnitRoster(allyUnitRoster);
            maxCommandPoints = Mathf.Max(1, maxCommandPoints);
            _commandPoints = Mathf.Clamp(startingCommandPoints, 0, maxCommandPoints);
            _commandPointRecoveryRemainder = 0f;
        }

        private void OnEnable()
        {
            if (waveManager != null)
            {
                waveManager.BeforeEnemiesTick += HandleBeforeEnemiesTick;
            }

            if (inputModeController != null)
            {
                inputModeController.ModeChanged += HandleInputModeChanged;
            }
        }

        private void OnDisable()
        {
            if (waveManager != null)
            {
                waveManager.BeforeEnemiesTick -= HandleBeforeEnemiesTick;
            }

            if (inputModeController != null)
            {
                inputModeController.ModeChanged -= HandleInputModeChanged;
            }
        }

        private void HandleBeforeEnemiesTick(float deltaTime)
        {
            // 일시정지 중에도 WaveManager.Update와 이벤트 호출 자체는 계속되므로 여기서 먼저 막는다.
            // 적 Tick 직전 호출되어 새 적이 생성된 프레임에도 최신 아군 위치를 후보로 제시한다.
            if (!IsDeployInputEnabled)
            {
                return;
            }

            TickActiveUnits(deltaTime);
            TickCommandPointRecovery(deltaTime);
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<AllyUnitInstance, Action> pair in _deathHandlers)
            {
                pair.Key.Died -= pair.Value;
            }

            _deathHandlers.Clear();
            _activeUnits.Clear();
        }

        /// <summary>배치 UI 버튼이 호출한다. 구체 입력 키는 UI/입력 설계가 확정될 때 연결한다.</summary>
        public bool SelectUnit(int index)
        {
            if (!IsDeployInputEnabled || !IsValidRosterIndex(index))
            {
                return false;
            }

            if (inputModeController != null)
            {
                inputModeController.EnterMode(DeploymentInputMode.AllyUnitDeploy);
            }

            _selectedIndex = index;
            SelectionChanged?.Invoke(allyUnitRoster.units[index]);
            return true;
        }

        public void ClearSelection()
        {
            ClearSelectionState();

            if (inputModeController != null)
            {
                inputModeController.ClearMode(DeploymentInputMode.AllyUnitDeploy);
            }
        }

        /// <summary>
        /// 현재 선택을 배치한다. 직접 인덱스로 배치하는 경로와 같은 TryDeploy를 사용해
        /// 버튼·단축키 등 호출 위치와 무관하게 동일한 지휘 포인트 검증을 거친다.
        /// </summary>
        public bool TryDeploySelected()
        {
            return IsAllyUnitDeployModeActive && _selectedIndex.HasValue && TryDeploy(_selectedIndex.Value);
        }

        private void HandleInputModeChanged(DeploymentInputMode mode)
        {
            if (mode != DeploymentInputMode.AllyUnitDeploy)
            {
                ClearSelectionState();
            }
        }

        private void ClearSelectionState()
        {
            if (!_selectedIndex.HasValue)
            {
                return;
            }

            _selectedIndex = null;
            SelectionChanged?.Invoke(null);
        }

        /// <summary>
        /// 로스터의 Definition으로 순수 C# 인스턴스를 만들고, 공용 View 프리팹을 즉시 Bind한다.
        /// 종류별 프리팹이나 MonoBehaviour 유닛 클래스를 추가하지 않는 데이터 주입 경로다.
        /// </summary>
        public bool TryDeploy(int index)
        {
            if (!IsDeployInputEnabled || !IsValidRosterIndex(index))
            {
                return false;
            }

            if (mapManager == null || waveManager == null || viewPrefab == null)
            {
                Debug.LogError("[UnitDeploy] MapManager, WaveManager 또는 공용 View 프리팹 참조가 비어 있습니다.");
                return false;
            }

            IReadOnlyList<Vector2> path = mapManager.Waypoints;
            if (path == null || path.Count < 2)
            {
                Debug.LogError("[UnitDeploy] 역주행에는 웨이포인트가 최소 2개 필요합니다.");
                return false;
            }

            AllyUnitDefinition definition = allyUnitRoster.units[index];
            int deployCost = definition.data.deployCost;
            if (deployCost < 0)
            {
                Debug.LogError($"[UnitDeploy] {definition.data.displayName}의 배치 비용이 음수입니다.");
                return false;
            }

            if (!CanSpendCommandPoints(deployCost))
            {
                Debug.Log($"[UnitDeploy] 지휘 포인트 부족 (필요 {deployCost}, 보유 {_commandPoints})");
                DeployFailedInsufficientCommandPoints?.Invoke(definition);
                return false;
            }

            var instance = new AllyUnitInstance();
            instance.Spawn(definition, path, combatSettings);

            if (instance.IsDead)
            {
                Debug.LogWarning($"[UnitDeploy] {definition.data.displayName}이(가) 스폰 효과 중 사망해 View를 생성하지 않습니다.");
                return false;
            }

            AllyUnitView view = Instantiate(viewPrefab, instance.Position, Quaternion.identity);
            view.Bind(instance);

            // 스폰 효과와 View 바인딩까지 성공한 뒤에만 소비해, 실패한 배치가 자원만
            // 차감하는 일을 막는다. 그 사이 지휘 포인트를 바꾸는 비동기 경로는 없다.
            if (!TrySpendCommandPoints(deployCost))
            {
                Destroy(view.gameObject);
                return false;
            }

            RegisterInstance(instance);
            UnitDeployed?.Invoke(instance);
            Debug.Log($"[UnitDeploy] {definition.data.displayName} 배치 완료 (-{deployCost} CP, 남은 {_commandPoints})");
            return true;
        }

        public bool CanSpendCommandPoints(int amount)
        {
            return amount >= 0 && _commandPoints >= amount;
        }

        public bool CanAfford(AllyUnitDefinition definition)
        {
            return definition != null && definition.data != null &&
                   CanSpendCommandPoints(definition.data.deployCost);
        }

        public bool TrySpendCommandPoints(int amount)
        {
            if (!CanSpendCommandPoints(amount))
            {
                return false;
            }

            if (amount == 0)
            {
                return true;
            }

            _commandPoints -= amount;
            CommandPointsChanged?.Invoke(_commandPoints);
            return true;
        }

        /// <summary>
        /// 보상처럼 정수 단위로 지휘 포인트를 지급할 공용 진입점.
        /// 회복·보상 어느 경로도 상한을 우회하지 않도록 여기서 함께 제한한다.
        /// </summary>
        public void AddCommandPoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int previous = _commandPoints;
            _commandPoints = Mathf.Min(maxCommandPoints, _commandPoints + amount);
            if (_commandPoints != previous)
            {
                CommandPointsChanged?.Invoke(_commandPoints);
            }
        }

        private void TickCommandPointRecovery(float deltaTime)
        {
            // 빌드 페이즈에서 회복하면 첫 웨이브 전에 자원이 상한까지 차 버려 소환 선택의
            // 의미가 사라진다. 전투가 실제로 진행되는 동안에만 회복시킨다.
            if (waveManager == null || waveManager.IsWaitingForNextWave ||
                _commandPoints >= maxCommandPoints || commandPointRecoveryPerSecond <= 0f)
            {
                return;
            }

            _commandPointRecoveryRemainder += Mathf.Max(0f, deltaTime) * commandPointRecoveryPerSecond;
            int recovered = Mathf.FloorToInt(_commandPointRecoveryRemainder);
            if (recovered <= 0)
            {
                return;
            }

            _commandPointRecoveryRemainder -= recovered;
            AddCommandPoints(recovered);

            if (_commandPoints >= maxCommandPoints)
            {
                _commandPointRecoveryRemainder = 0f;
            }
        }

        private void TickActiveUnits(float deltaTime)
        {
            // 등록 순서는 곧 스폰 순서다. 먼저 스폰된 전열부터 이동해야 후속 유닛이 그 프레임에
            // 새로 열린 공간을 보고 contactRange 간격을 채울 수 있으므로 정방향으로 순회한다.
            // Tick 중 현재 유닛이 사망해 목록에서 제거되면 당겨진 다음 유닛을 같은 인덱스에서 처리한다.
            for (int i = 0; i < _activeUnits.Count;)
            {
                AllyUnitInstance unit = _activeUnits[i];
                unit.Tick(deltaTime, waveManager.ActiveEnemies, _activeUnits);

                if (i < _activeUnits.Count && ReferenceEquals(_activeUnits[i], unit))
                {
                    i++;
                }
            }
        }

        private void RegisterInstance(AllyUnitInstance instance)
        {
            Action deathHandler = null;
            deathHandler = () =>
            {
                instance.Died -= deathHandler;
                _deathHandlers.Remove(instance);
                _activeUnits.Remove(instance);
                UnitRemoved?.Invoke(instance);
            };

            instance.Died += deathHandler;
            _deathHandlers.Add(instance, deathHandler);
            _activeUnits.Add(instance);
        }

        private bool IsValidRosterIndex(int index)
        {
            return IsAvailable &&
                   index >= 0 &&
                   index < allyUnitRoster.units.Count &&
                   allyUnitRoster.units[index] != null &&
                   allyUnitRoster.units[index].data != null;
        }
    }
}
