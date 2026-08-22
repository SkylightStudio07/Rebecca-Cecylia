using System;
using RCCom.Definitions.Card;
using RCCom.Definitions.Tower;
using RCCom.Effects.Card;
using RCCom.Effects.Card.Concrete;
using RCCom.Runtime;
using UnityEngine;

namespace RCCom.Managers
{
    /// <summary>
    /// 게임 흐름 단계 매니저 중 하나 (ARCHITECTURE.md 5단계 원칙). 골드(재화)와 레벨/경험치를
    /// 관리 — 전역 상태가 늘어날 때마다(게임오버 판정 등) 이 클래스에 계속 추가될 예정.
    ///
    /// [DefaultExecutionOrder(-1000)]: 이 스크립트의 Awake()가 다른 모든 스크립트보다 먼저
    /// 실행되도록 강제한다 — 아래에서 TowerRoster/TowerDefinition/AttackFlash/GlobalTowerAuraRegistry의
    /// 세션 캐시를 초기화하는데, TowerBuildController/CardManager/TowerBuildMenuUI 등이 먼저
    /// Awake에서 GetRuntimeInstance()를 호출해버리면 초기화 전에 복제본이 만들어져 버려서
    /// (재시작 시 이전 판 데이터를 그대로 물려받음) 순서 보장이 반드시 필요하다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameManager : MonoBehaviour
    {
        /// <summary>씬에 하나뿐임이 보장되므로 정적 싱글톤으로 노출 (BaseController.Instance와 동일한 근거) — 골드 채굴 파워 타워(GoldMineEffect)처럼 SO 효과 자산이 씬의 매니저를 직접 참조해야 할 때 사용.</summary>
        public static GameManager Instance { get; private set; }

        [SerializeField] private int startingGold = 45;

        [Header("재시작(Retry) 시 세션 캐시 초기화 대상")]
        [SerializeField] private TowerRoster towerRoster;
        [SerializeField] private CardRoster cardRoster;

        [Header("게임오버 판정 대상")]
        [SerializeField] private PlayerController player;
        [SerializeField] private BaseController baseController;

        /// <summary>
        /// 레벨업 필요 경험치 공식: baseXpToLevel + xpGrowthPerLevel × 현재레벨².
        /// 원래는 선형(× 현재레벨)이었는데, 선형은 후반으로 갈수록 증가폭이 일정해서 타워
        /// DPS가 쌓일수록(처치 속도 ↑) 상대적으로 레벨업이 점점 쉬워지는 역설이 있었음
        /// ({{user}} 확인: 레벨업이 너무 쉬워서 적을 입구에서 막아버릴 수 있는 문제).
        /// 제곱식으로 바꿔 후반 레벨일수록 확실히 가팔라지게 함 — 지수함수도 고려했으나
        /// 배율 하나만 잘못 잡아도 극단적으로 튀어서(영원히 쉬움 ↔ 레벨업 벽), 남은 튜닝
        /// 시간이 짧은 지금은 계수 2개로 직관적으로 조절되는 제곱식이 더 안전한 선택.
        /// GDD가 세부 수치를 "추후 논의"로 남겨둬서 초안값 — 플레이테스트하며 조정 대상.
        /// </summary>
        [Header("레벨업 필요 경험치 공식 (GDD 미정 — 초안값, 제곱 스케일링)")]
        [SerializeField] private int baseXpToLevel = 10;
        [SerializeField] private int xpGrowthPerLevel = 5;

        /// <summary>
        /// 타워 철거 시 건설비 대비 추가로 내야 하는 비율 (환불이 아니라 페널티 — 마구
        /// 짓고 부수는 걸 막기 위함). 카드로 이 비율을 낮춰 철거 비용을 할인할 수 있도록
        /// private set + 전용 메서드로 노출.
        /// </summary>
        [Header("타워 철거 비용 배율 (건설비 대비, 카드로 할인 가능)")]
        [SerializeField] private float demolishCostRatio = 0.75f;

        public int Gold { get; private set; }
        public int Level { get; private set; } = 1;
        public int CurrentExp { get; private set; }

        /// <summary>결과 화면의 "GOLD EARNED" — Gold(현재 보유액, 소비하면 줄어듦)와 달리 누적 총합이라 줄지 않는다.</summary>
        public int TotalGoldEarned { get; private set; }

        /// <summary>결과 화면의 "ENEMIES DEFEATED" — WaveManager가 처치 보상 지급 시 호출.</summary>
        public int EnemiesDefeated { get; private set; }

        /// <summary>결과 화면의 "SURVIVAL TIME" — Time.deltaTime 누적이라 카드 선택 일시정지 중엔 자동으로 멈춘다.</summary>
        public float SurvivalTime { get; private set; }

        /// <summary>플레이어 사망 또는 거점 파괴로 게임이 끝났는지 — 한 번 true가 되면 유지.</summary>
        public bool IsGameOver { get; private set; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Defeat;

        public event Action<int> GoldChanged;

        /// <summary>새 레벨 값을 인자로 전달. CardManager가 구독해 카드 3택을 띄운다.</summary>
        public event Action<int> LeveledUp;

        /// <summary>플레이어 사망 또는 거점 파괴 시 1회 발생 — 결과 화면 UI가 구독.</summary>
        public event Action GameOver;

        /// <summary>승리와 패배를 모두 한 번만 전달하는 결과 화면용 이벤트.</summary>
        public event Action<BattleOutcome> BattleEnded;

        private void Awake()
        {
            Instance = this;
            Gold = startingGold;

            // 선택 상태는 TitleScene → DefenseScene 및 Retry를 넘어 유지되어야 하므로 자동으로
            // 비우지 않는다. 대신 이 최선행 Awake에서 유효성을 검증하고 선택된 Roster로
            // 초기화 대상을 교체해, 아래 캐시 정리가 실제 플레이할 로드아웃에 적용되게 한다.
            OperatorLoadoutSession.PrepareForGameplay();
            towerRoster = OperatorLoadoutSession.ResolveTowerRoster(towerRoster);
            cardRoster = OperatorLoadoutSession.ResolveCardRoster(cardRoster);

            // 재시작(Retry, SceneManager.LoadScene) 시 이전 세션의 잔여 static 캐시를 전부 초기화.
            // Editor Play 재시작은 도메인 리로드로 저절로 비워지지만, 런타임 씬 재로드는 그렇지
            // 않아서 명시적으로 비워야 한다 — [DefaultExecutionOrder(-1000)]로 이게 다른 스크립트의
            // GetRuntimeInstance() 호출보다 반드시 먼저 실행되도록 보장돼 있음.
            towerRoster.ClearRuntimeInstance();
            foreach (CardEffectBase card in cardRoster.cards)
            {
                if (card is UnlockTowerCard unlockCard)
                {
                    unlockCard.ClearRuntimeCache();
                }
            }

            GlobalTowerAuraRegistry.Auras.Clear();
            AttackFlash.ClearPool();
        }

        private void OnEnable()
        {
            player.Died += HandleGameOver;
            baseController.Defeated += HandleGameOver;
        }

        private void OnDisable()
        {
            player.Died -= HandleGameOver;
            baseController.Defeated -= HandleGameOver;
        }

        private void Update()
        {
            if (!IsGameOver)
            {
                SurvivalTime += Time.deltaTime;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>플레이어 사망/거점 파괴 공용 핸들러 — 어느 쪽이 먼저 오든 1회만 처리.</summary>
        private void HandleGameOver()
        {
            EndBattle(BattleOutcome.Defeat);
        }

        /// <summary>
        /// 유한 스테이지의 마지막 웨이브가 끝났을 때 호출한다. 기존 GameOver 이벤트는
        /// 패배 전용 호환 계약으로 남기고, 결과 화면은 BattleEnded를 사용한다.
        /// </summary>
        public void CompleteBattle()
        {
            EndBattle(BattleOutcome.Victory);
        }

        private void EndBattle(BattleOutcome outcome)
        {
            if (IsGameOver)
            {
                return;
            }

            IsGameOver = true;
            Outcome = outcome;
            Time.timeScale = 0f;
            if (outcome == BattleOutcome.Defeat)
            {
                GameOver?.Invoke();
            }

            BattleEnded?.Invoke(outcome);
        }

        /// <summary>WaveManager가 처치 보상 지급 시 호출.</summary>
        public void RecordEnemyDefeated()
        {
            EnemiesDefeated++;
        }

        public void AddGold(int amount)
        {
            Gold += amount;
            TotalGoldEarned += amount;
            GoldChanged?.Invoke(Gold);
        }

        public bool TrySpendGold(int amount)
        {
            if (Gold < amount)
            {
                return false;
            }

            Gold -= amount;
            GoldChanged?.Invoke(Gold);
            return true;
        }

        public void AddExp(int amount)
        {
            CurrentExp += amount;

            // 한 번에 여러 레벨을 넘을 수도 있으니 while로 처리 (경험치 몰아서 들어오는 경우 대비).
            while (CurrentExp >= ExpRequiredForNextLevel())
            {
                CurrentExp -= ExpRequiredForNextLevel();
                Level++;
                LeveledUp?.Invoke(Level);
            }
        }

        /// <summary>HUD가 경험치 바의 최대값으로 참조.</summary>
        public int ExpRequiredForNextLevel()
        {
            return baseXpToLevel + xpGrowthPerLevel * Level * Level;
        }

        /// <summary>TowerBuildController가 철거 비용(buildCost × 이 비율) 계산에 사용.</summary>
        public float DemolishCostRatio => demolishCostRatio;

        /// <summary>철거 비용 할인 카드가 호출 — 0 밑으로는 내려가지 않는다.</summary>
        public void ReduceDemolishCostRatio(float amount)
        {
            demolishCostRatio = Mathf.Max(0f, demolishCostRatio - amount);
        }
    }
}
