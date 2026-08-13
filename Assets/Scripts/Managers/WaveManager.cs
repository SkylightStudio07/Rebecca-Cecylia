using System.Collections.Generic;
using RCCom.Definitions.Enemy;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Managers
{
    /// <summary>
    /// 게임 흐름 단계 매니저 중 하나 (ARCHITECTURE.md 5단계 원칙) — 웨이브 절차적 생성과
    /// List&lt;EnemyInstance&gt; 순회(Tick)를 담당한다. 별도 EnemyManager 없이 이 매니저가
    /// 직접 리스트를 들고 자기 Update()에서 Tick()만 호출 — 각 EnemyInstance가 스스로
    /// 이동/효과를 처리한다.
    ///
    /// GDD "웨이브 난이도 생성 규칙(예산 방식)" 반영. 정예/무리 수식어와 실제 보스 유닛은
    /// GDD에도 "여유시"로 명시되어 있어 이번 단계에서는 제외 (보스 웨이브 "분기"는 만들되,
    /// bossDefinition을 비워두면 일반 소환으로 대체됨).
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private MapManager mapManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BaseController baseController;
        [SerializeField] private EnemyRoster enemyRoster;
        [SerializeField] private EnemyView viewPrefab;

        [Header("난이도 예산 공식: 예산(n) = baseBudget + budgetGrowthPerWave × n (GDD 초안값)")]
        [SerializeField] private float baseBudget = 10f;
        [SerializeField] private float budgetGrowthPerWave = 2f;

        [Header("웨이브별 적 체력 배율: 1 + healthGrowthPerWave × n — 예산(수량)만으로는 후반에 타워 DPS를 못 따라가서 추가")]
        [SerializeField] private float healthGrowthPerWave = 0.10f;

        [Header("스폰 간격 공식: max(minSpawnInterval, baseSpawnInterval - decayPerWave × n)")]
        [SerializeField] private float baseSpawnInterval = 2.0f;
        [SerializeField] private float spawnIntervalDecayPerWave = 0.05f;
        [SerializeField] private float minSpawnInterval = 0.5f;

        [Header("보스 웨이브: n % bossWaveInterval == 0 → 예산 절반. bossDefinition 비우면 일반 소환으로 대체 (여유시 확장 지점)")]
        [SerializeField] private int bossWaveInterval = 5;
        [SerializeField] private float bossBudgetMultiplier = 0.5f;
        [SerializeField] private EnemyDefinition bossDefinition;

        [Header("웨이브 클리어 후 다음 웨이브 시작까지 대기(빌드 페이즈) — 첫 웨이브 전에도 동일하게 적용")]
        [SerializeField] private float buildPhaseDuration = 10f;

        [Header("난수 (독립성·재현성 보장 — UnityEngine.Random 대신 전용 System.Random 사용)")]
        [SerializeField] private int randomSeed = 12345;

        [Header("실행 상태 (읽기 전용 — Play 모드에서 인스펙터로 실시간 확인용, 코드가 매 프레임 덮어씀)")]
        [SerializeField] private int currentWave;

        /// <summary>HUD가 웨이브 번호를 표시할 때 참조.</summary>
        public int CurrentWave => currentWave;
        [SerializeField] private bool isWaitingForNextWave;
        [SerializeField] private float stateTimerRemaining;

        /// <summary>HUD가 "빌드 페이즈(대기 중)인지" 판단할 때 참조 — 스폰 중엔 false.</summary>
        public bool IsWaitingForNextWave => isWaitingForNextWave;

        /// <summary>
        /// 다음 웨이브 시작까지 남은 시간(초). 빌드 페이즈가 아닐 때(스폰 진행 중)는 0을 반환 —
        /// HUD가 이 값을 "다음 웨이브까지 카운트다운"으로만 쓰도록 보장.
        /// </summary>
        public float NextWaveCountdown => isWaitingForNextWave ? stateTimerRemaining : 0f;
        [SerializeField] private List<string> queuedEnemyNames = new();
        [SerializeField] private List<string> aliveEnemyNames = new();

        private readonly List<EnemyInstance> _aliveEnemies = new();
        /// <summary>
        /// UnitDeployController가 아군 Tick에 전달할 읽기 전용 적 후보 목록.
        /// 리스트 소유권과 추가·제거 권한은 계속 WaveManager에만 둔다.
        /// </summary>
        public IReadOnlyList<EnemyInstance> ActiveEnemies => _aliveEnemies;

        private readonly Queue<EnemyDefinition> _spawnQueue = new();
        private System.Random _rng;

        private int _waveNumber;
        private bool _isWaiting;
        private float _stateTimer;

        private void Awake()
        {
            _rng = new System.Random(randomSeed);
            _isWaiting = true;
            _stateTimer = buildPhaseDuration;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (_isWaiting)
            {
                TickBuildPhase(deltaTime);
            }
            else
            {
                TickSpawning(deltaTime);
            }

            TickEnemies(deltaTime);
            SyncDebugStatus();
        }

        private void SyncDebugStatus()
        {
            currentWave = _waveNumber;
            isWaitingForNextWave = _isWaiting;
            stateTimerRemaining = _stateTimer;

            queuedEnemyNames.Clear();
            foreach (EnemyDefinition definition in _spawnQueue)
            {
                queuedEnemyNames.Add(definition.data.displayName);
            }

            aliveEnemyNames.Clear();
            foreach (EnemyInstance enemy in _aliveEnemies)
            {
                aliveEnemyNames.Add(enemy.Data.displayName);
            }
        }

        private void TickBuildPhase(float deltaTime)
        {
            _stateTimer -= deltaTime;
            if (_stateTimer > 0f)
            {
                return;
            }

            StartNextWave();
        }

        private void StartNextWave()
        {
            _waveNumber++;
            _spawnQueue.Clear();

            foreach (EnemyDefinition definition in BuildSpawnQueue(_waveNumber))
            {
                _spawnQueue.Enqueue(definition);
            }

            Debug.Log($"[Wave] {_waveNumber} 웨이브 시작 (적 {_spawnQueue.Count}마리)");

            _isWaiting = false;
            _stateTimer = 0f;
        }

        private void TickSpawning(float deltaTime)
        {
            if (_spawnQueue.Count == 0)
            {
                if (_aliveEnemies.Count == 0)
                {
                    Debug.Log($"[Wave] {_waveNumber} 웨이브 클리어 — {buildPhaseDuration}초 후 다음 웨이브");
                    _isWaiting = true;
                    _stateTimer = buildPhaseDuration;
                }

                return;
            }

            _stateTimer -= deltaTime;
            if (_stateTimer > 0f)
            {
                return;
            }

            SpawnOne(_spawnQueue.Dequeue());
            _stateTimer = CalculateSpawnInterval(_waveNumber);
        }

        private float CalculateSpawnInterval(int waveNumber)
        {
            return Mathf.Max(minSpawnInterval, baseSpawnInterval - spawnIntervalDecayPerWave * waveNumber);
        }

        private float CalculateBudget(int waveNumber)
        {
            return baseBudget + budgetGrowthPerWave * waveNumber;
        }

        /// <summary>
        /// 수량(예산)만 늘리면 타워/플레이어 DPS가 쌓일수록 상대적으로 쉬워지는 문제가 있어
        /// (레벨업 제곱식 도입과 같은 이유) 체력에도 별도 배율을 곱한다 — 후반 웨이브일수록
        /// 개별 적도 확실히 더 오래 버팀.
        /// </summary>
        private float CalculateHealthMultiplier(int waveNumber)
        {
            return 1f + healthGrowthPerWave * waveNumber;
        }

        private List<EnemyDefinition> BuildSpawnQueue(int waveNumber)
        {
            var queue = new List<EnemyDefinition>();

            bool isBossWave = bossWaveInterval > 0 && waveNumber % bossWaveInterval == 0;

            if (isBossWave && bossDefinition != null)
            {
                queue.Add(bossDefinition);
                return queue;
            }

            float budget = CalculateBudget(waveNumber) * (isBossWave ? bossBudgetMultiplier : 1f);

            var eligible = new List<EnemyDefinition>();
            foreach (EnemyDefinition definition in enemyRoster.enemies)
            {
                if (waveNumber >= definition.data.minWave)
                {
                    eligible.Add(definition);
                }
            }

            if (eligible.Count == 0)
            {
                return queue;
            }

            // 가중치는 GDD 초안에 구체 수치가 없어 균등 확률로 둠 — 플레이테스트하며 조정 대상.
            float remainingBudget = budget;
            int safetyLimit = 500; // EnemyData.waveCost가 0 이하로 잘못 설정된 경우 무한루프 방지

            while (remainingBudget > 0f && safetyLimit-- > 0)
            {
                EnemyDefinition chosen = eligible[_rng.Next(eligible.Count)];
                queue.Add(chosen);
                remainingBudget -= chosen.data.waveCost;
            }

            if (safetyLimit <= 0)
            {
                Debug.LogWarning("[Wave] 스폰 큐 생성이 안전 한도에 도달함 — EnemyData.waveCost가 0 이하인 항목이 있는지 확인하세요.");
            }

            return queue;
        }

        private void SpawnOne(EnemyDefinition definition)
        {
            IReadOnlyList<Vector2> path = mapManager.Waypoints;
            if (path.Count == 0)
            {
                return;
            }

            var instance = new EnemyInstance
            {
                definition = definition,
                position = path[0],
            };
            instance.Spawn(path, baseController);
            instance.currentHealth *= CalculateHealthMultiplier(_waveNumber);

            instance.Died += () =>
            {
                _aliveEnemies.Remove(instance);
                GrantReward(instance.Data.goldReward, instance.Data.expReward);
            };
            instance.ReachedGoal += () => _aliveEnemies.Remove(instance);

            EnemyView view = Instantiate(viewPrefab, path[0], Quaternion.identity);
            view.Bind(instance);

            _aliveEnemies.Add(instance);
        }

        private void TickEnemies(float deltaTime)
        {
            // Tick 도중 Died/ReachedGoal로 리스트에서 빠질 수 있어 역순으로 순회한다.
            for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
            {
                _aliveEnemies[i].Tick(deltaTime);
            }
        }

        private void GrantReward(int gold, int exp)
        {
            gameManager.AddGold(gold);
            gameManager.AddExp(exp);
            gameManager.RecordEnemyDefeated();
            Debug.Log($"[Wave] 처치 보상 +{gold}G/+{exp}EXP (골드 보유 {gameManager.Gold}, 레벨 {gameManager.Level}, 경험치 {gameManager.CurrentExp})");
        }
    }
}
