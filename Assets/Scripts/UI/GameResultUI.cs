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
    /// "MISSION RESULT" 결과 화면. GameManager.GameOver(플레이어 사망 또는 거점 파괴)를 구독해
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
            gameManager.GameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            gameManager.GameOver -= HandleGameOver;
        }

        private void HandleGameOver()
        {
            PlayerProfile profile = _profileStorage.Load();
            if (profile.TryRecordBestWave(waveManager.CurrentWave))
            {
                // 결과 화면이 세션 통계를 확정하는 체크포인트이므로 최고 기록도 여기서 한 번만 저장한다.
                // 매 웨이브마다 PlayerPrefs.Save를 호출하지 않아 WebGL 저장 비용과 중간 상태 기록을 피한다.
                _profileStorage.Save(profile);
            }

            reachedWaveText.text = $"{waveManager.CurrentWave}";
            defeatedEnemiesText.text = $"{gameManager.EnemiesDefeated}";
            earnedGoldText.text = $"{gameManager.TotalGoldEarned}";
            survivalTimeText.text = FormatTime(gameManager.SurvivalTime);

            Show();
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
