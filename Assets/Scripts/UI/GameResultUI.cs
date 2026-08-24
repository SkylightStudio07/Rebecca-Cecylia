using RCCom.Core;
using RCCom.Data;
using RCCom.Managers;
using RCCom.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// "MISSION RESULT" 결과 화면. GameManager.BattleEnded를 구독해 승리·패배에 관계없이
    /// 도달 웨이브/처치 수/획득 골드/생존 시간을 채워 표시한다. Retry는 현재 씬을 다시 로드,
    /// Title은 지정한 씬으로 이동 — 둘 다 Time.timeScale을 1로 되돌린 뒤 씬을 전환한다
    /// (GameManager.HandleGameOver가 0으로 낮춰둔 채로 다음 씬에 넘어가면 그 씬도 멈춰버림).
    /// </summary>
    public class GameResultUI : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private WaveManager waveManager;

        [SerializeField] private CanvasGroup panelGroup;

        [Tooltip("화면 전체를 덮는 어둡게 처리용 배경 — 결과 카드(panelGroup)와 별개 오브젝트라 따로 연결해야 함(카드 크기 안에서만 어두워지는 것 방지)")]
        [SerializeField] private CanvasGroup dimBackgroundGroup;

        [SerializeField] private TextMeshProUGUI reachedWaveText;
        [SerializeField] private TextMeshProUGUI defeatedEnemiesText;
        [SerializeField] private TextMeshProUGUI earnedGoldText;
        [SerializeField] private TextMeshProUGUI survivalTimeText;
        [Header("계정 보상")]
        [Tooltip("전투 결과마다 지급하는 기본 계정 재화. 전투 중 골드와는 별개로 PlayerProfile에 누적된다.")]
        [SerializeField, Min(0)] private int baseCommodityReward = 100;
        [Tooltip("전투 시간 1분마다 더하는 계정 재화. 결과 화면·카드 선택으로 멈춘 시간은 SurvivalTime에 포함되지 않는다.")]
        [SerializeField, Min(0)] private int commodityPerCompletedMinute = 20;
        [Tooltip("플레이타임 보너스 상한. 장시간 생존으로 보상이 끝없이 커지지 않게 막는다.")]
        [SerializeField, Min(0)] private int maxCommodityTimeBonus = 100;
        [SerializeField] private TextMeshProUGUI resultTitleText;
        [SerializeField] private string victoryTitle = "MISSION CLEAR";
        [SerializeField] private string defeatTitle = "MISSION FAILED";

        [Header("오퍼레이터 보상")]
        [Tooltip("스테이지 클리어 기록 저장 직후 결과 화면 위에 합류 연출을 표시한다.")]
        [SerializeField] private OperatorAcquisitionUI operatorAcquisitionUI;

        [SerializeField] private Button retryButton;
        [SerializeField] private Button titleButton;
        [SerializeField] private string titleSceneName = "TitleScene";

        [Tooltip("씬 전환 전에 클릭음이 들릴 시간을 잠깐 벌어준다 — SceneManager.LoadScene은 현재 씬을 즉시 파괴해서, 지연 없이 바로 넘기면 클릭음이 거의 안 들림")]
        [SerializeField] private float sceneChangeDelay = 0.15f;

        /// <summary>
        /// 대기 중인 씬 전환까지 남은 시간(초). null이면 대기 중 아님. 이 화면은 Time.timeScale=0
        /// 상태에서 뜨므로 Invoke() 대신 Time.unscaledDeltaTime 기반 수동 타이머로 처리한다 —
        /// Invoke의 지연이 timeScale에 영향받는지 불확실해서, 확실히 검증된 이 프로젝트의 기존
        /// 패턴(AttackFlash/UIHoverScale 등)을 그대로 재사용.
        /// </summary>
        private float? _pendingSceneDelay;
        private string _pendingSceneName;
        private IProfileStorage _profileStorage;
        private bool _hasGrantedCommodity;
        private int _grantedCommodity;

        private void Awake()
        {
            _profileStorage = new PlayerPrefsProfileStorage();
            Hide();

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(HandleRetry);
            }

            if (titleButton != null)
            {
                titleButton.onClick.AddListener(HandleTitle);
            }
        }

        private void OnEnable()
        {
            gameManager.BattleEnded += HandleBattleEnded;
        }

        private void OnDisable()
        {
            gameManager.BattleEnded -= HandleBattleEnded;
        }

        private void HandleBattleEnded(BattleOutcome outcome)
        {
            PlayerProfile profile = _profileStorage.Load();
            bool shouldSaveProfile = false;
            string clearedStageId = string.Empty;
            GrantCommodity(profile);
            shouldSaveProfile = true;
            if (profile.TryRecordBestWave(waveManager.CurrentWave))
            {
                // 결과 화면이 세션 통계를 확정하는 체크포인트이므로 최고 기록도 여기서 한 번만 저장한다.
                // 매 웨이브마다 PlayerPrefs.Save를 호출하지 않아 WebGL 저장 비용과 중간 상태 기록을 피한다.
                shouldSaveProfile = true;
            }

            if (outcome == BattleOutcome.Victory && BattleSession.IsStageMode &&
                BattleSession.SelectedStage != null)
            {
                clearedStageId = BattleSession.SelectedStage.stageId;
                // 스테이지 보상 오퍼레이터는 이 클리어 기록에서 파생한다. 별도 획득 목록에도
                // 중복 기록하면 조건 변경 시 두 원본이 어긋날 수 있어 스테이지 ID만 저장한다.
                shouldSaveProfile |= profile.MarkStageCleared(clearedStageId);
            }

            string participatingOperatorId = string.Empty;
            if (OperatorLoadoutSession.SelectedDefinition != null)
            {
                participatingOperatorId = OperatorLoadoutSession.SelectedDefinition.operatorId;
            }

            if (string.IsNullOrWhiteSpace(participatingOperatorId))
            {
                participatingOperatorId = profile.selectedOperatorId;
            }

            if (!string.IsNullOrWhiteSpace(participatingOperatorId))
            {
                // 호감도는 전투 종료 즉시 올리지 않고, 귀환 후 메인 로비가 열릴 때
                // 정산한다. 전투가 다시 시작돼도 보상이 소실되지 않게
                // PlayerProfile에 예약 상태를 함께 저장한다.
                profile.QueueBattleReturn(participatingOperatorId);
                shouldSaveProfile = true;
            }

            if (shouldSaveProfile)
            {
                _profileStorage.Save(profile);
            }

            reachedWaveText.text = $"{waveManager.CurrentWave}";
            defeatedEnemiesText.text = $"{gameManager.EnemiesDefeated}";
            earnedGoldText.text = $"{gameManager.TotalGoldEarned}";
            survivalTimeText.text = FormatTime(gameManager.SurvivalTime);
            if (resultTitleText != null)
            {
                resultTitleText.text = outcome == BattleOutcome.Victory ? victoryTitle : defeatTitle;
            }

            Show();
            if (operatorAcquisitionUI != null && !string.IsNullOrWhiteSpace(clearedStageId))
            {
                // 이미 소개한 보상은 내부 표시 이력에서 걸러지므로 재클리어에도 중복 재생되지 않는다.
                // 반대로 과거 클리어 기록만 있고 소개되지 않은 경우에는 결과 화면에서 복구된다.
                operatorAcquisitionUI.PresentNewlyUnlockedForStage(clearedStageId);
            }
        }

        private int GrantCommodity(PlayerProfile profile)
        {
            if (_hasGrantedCommodity)
            {
                return _grantedCommodity;
            }

            int completedMinutes = Mathf.FloorToInt(Mathf.Max(0f, gameManager.SurvivalTime) / 60f);
            int timeBonus = Mathf.Min(maxCommodityTimeBonus,
                completedMinutes * commodityPerCompletedMinute);
            _grantedCommodity = Mathf.Max(0, baseCommodityReward) + timeBonus;
            profile.AddCommodity(_grantedCommodity);
            _hasGrantedCommodity = true;
            return _grantedCommodity;
        }

        private static string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.FloorToInt(seconds);
            int minutes = totalSeconds / 60;
            int secs = totalSeconds % 60;
            return $"{minutes:00}:{secs:00}";
        }

        private void Update()
        {
            if (!_pendingSceneDelay.HasValue)
            {
                return;
            }

            _pendingSceneDelay -= Time.unscaledDeltaTime;
            if (_pendingSceneDelay.Value > 0f)
            {
                return;
            }

            _pendingSceneDelay = null;
            Time.timeScale = 1f;
            // 결과 화면의 재도전·로비 복귀는 이미 종료 연출을 마친 뒤의 선택이다.
            // 출격 전용 로딩 캔버스를 다시 노출하지 않고 즉시 씬을 전환한다.
            SceneManager.LoadScene(_pendingSceneName ?? SceneManager.GetActiveScene().name);
        }

        private void HandleRetry()
        {
            PlayClickSound();
            _pendingSceneName = null;
            _pendingSceneDelay = sceneChangeDelay;
        }

        private void HandleTitle()
        {
            PlayClickSound();
            _pendingSceneName = titleSceneName;
            _pendingSceneDelay = sceneChangeDelay;
        }

        private static void PlayClickSound()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayButtonClick();
            }
        }

        private void Show()
        {
            panelGroup.alpha = 1f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;

            if (dimBackgroundGroup != null)
            {
                dimBackgroundGroup.alpha = 1f;
                dimBackgroundGroup.blocksRaycasts = true;
            }
        }

        private void Hide()
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;

            if (dimBackgroundGroup != null)
            {
                dimBackgroundGroup.alpha = 0f;
                dimBackgroundGroup.blocksRaycasts = false;
            }
        }
    }
}
