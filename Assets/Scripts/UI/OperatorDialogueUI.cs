using RCCom.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 오퍼레이터(카시아) 대사 팝업. 게임 이벤트(스킬 사용, 피격, 건설 실패 등)를 구독해
    /// 해당 상황의 OperatorLineSet에서 랜덤 한 줄을 뽑아 보여주고, 클릭하면 즉시, 아니면
    /// displayDuration초 뒤 자동으로(알파 페이드) 사라진다. 상황마다 초상화도 같이 바뀌었다가
    /// 대사가 사라지면 기본(idle) 초상화로 되돌아간다.
    ///
    /// CanvasGroup은 "말풍선/텍스트" 쪽에만 걸 것 — 초상화(portraitImage)는 이 그룹 밖에 둬서
    /// 페이드와 무관하게 항상 보이고 스프라이트만 바뀌도록 한다(대사만 사라지는 연출).
    /// </summary>
    public class OperatorDialogueUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private OperatorDialogueSet dialogueSet;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI dialogueText;

        [Tooltip("말풍선/텍스트 쪽에만 걸린 CanvasGroup — 초상화는 이 밖에 둘 것")]
        [SerializeField] private CanvasGroup canvasGroup;

        [SerializeField] private float displayDuration = 4f;
        [SerializeField] private float fadeDuration = 0.5f;

        [Header("이벤트 구독 대상")]
        [SerializeField] private PlayerController player;
        [SerializeField] private BaseController baseController;
        [SerializeField] private TowerBuildController buildController;

        private float _remainingDisplay;
        private float _remainingFade;
        private bool _isFading;

        private void Awake()
        {
            dialogueSet = OperatorLoadoutSession.ResolveDialogueSet(dialogueSet);
            Hide();
        }

        private void Start()
        {
            ShowRandom(dialogueSet.gameStart);
        }

        private void OnEnable()
        {
            player.Damaged += HandlePlayerDamaged;
            player.Died += HandlePlayerDied;
            player.SkillUsed += HandleSkillUsed;
            baseController.Damaged += HandleBaseDamaged;
            baseController.Defeated += HandleBaseDefeated;
            buildController.BuildFailedInsufficientGold += HandleInsufficientGold;
            buildController.BuildFailedNoSlot += HandleNoSlot;
        }

        private void OnDisable()
        {
            player.Damaged -= HandlePlayerDamaged;
            player.Died -= HandlePlayerDied;
            player.SkillUsed -= HandleSkillUsed;
            baseController.Damaged -= HandleBaseDamaged;
            baseController.Defeated -= HandleBaseDefeated;
            buildController.BuildFailedInsufficientGold -= HandleInsufficientGold;
            buildController.BuildFailedNoSlot -= HandleNoSlot;
        }

        private void Update()
        {
            if (_isFading)
            {
                TickFade();
                return;
            }

            if (_remainingDisplay <= 0f)
            {
                return;
            }

            _remainingDisplay -= Time.deltaTime;
            if (_remainingDisplay <= 0f)
            {
                _isFading = true;
                _remainingFade = fadeDuration;
            }
        }

        private void TickFade()
        {
            _remainingFade -= Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(_remainingFade / fadeDuration);

            if (_remainingFade <= 0f)
            {
                Hide();
            }
        }

        /// <summary>말풍선 클릭 시 즉시 닫기 (자동 페이드와 달리 스르륵 사라지지 않고 바로 닫힘).</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            Hide();
        }

        private void HandlePlayerDamaged(float amount)
        {
            bool isCritical = player.data.maxHealth > 0f && player.CurrentHealth / player.data.maxHealth <= 0.3f;
            ShowRandom(isCritical ? dialogueSet.playerHitCritical : dialogueSet.playerHit);
        }

        private void HandlePlayerDied() => ShowRandom(dialogueSet.playerDied);

        private void HandleSkillUsed() => ShowRandom(dialogueSet.skillUsed);

        private void HandleBaseDamaged(float amount) => ShowRandom(dialogueSet.baseAttacked);

        private void HandleBaseDefeated() => ShowRandom(dialogueSet.baseDestroyed);

        private void HandleInsufficientGold() => ShowRandom(dialogueSet.insufficientGold);

        private void HandleNoSlot() => ShowRandom(dialogueSet.slotUnavailable);

        private void ShowRandom(OperatorLineSet lineSet)
        {
            if (lineSet == null || lineSet.lines == null || lineSet.lines.Length == 0)
            {
                return;
            }

            dialogueText.text = lineSet.lines[Random.Range(0, lineSet.lines.Length)];

            if (lineSet.portraitSprite != null)
            {
                portraitImage.sprite = lineSet.portraitSprite;
            }

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            _isFading = false;
            _remainingDisplay = displayDuration;
        }

        private void Hide()
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            _remainingDisplay = 0f;
            _isFading = false;

            if (portraitImage != null && dialogueSet != null)
            {
                portraitImage.sprite = dialogueSet.idleSprite;
            }
        }
    }
}
