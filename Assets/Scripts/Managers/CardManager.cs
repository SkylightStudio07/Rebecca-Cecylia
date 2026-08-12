using System;
using System.Collections.Generic;
using RCCom.Definitions.Card;
using RCCom.Definitions.Tower;
using RCCom.Effects.Card;
using RCCom.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RCCom.Managers
{
    /// <summary>
    /// 게임 흐름 단계 매니저 중 하나 (ARCHITECTURE.md 5단계 원칙) — 레벨업 시 카드 3장을
    /// 중복 없이 뽑아 제시하고 선택을 적용한다.
    ///
    /// 숫자키(1/2/3)와 UI 버튼(CardSelectionUI) 둘 다 SelectChoice를 호출하는 동일 경로 —
    /// 숫자키는 백업용으로 남겨둠. 선택 대기 중엔 Time.timeScale=0으로 일시정지 (Input System
    /// 폴링은 timeScale과 무관하게 계속 동작하므로 숫자키/버튼 입력 모두 정상적으로 받힌다).
    /// </summary>
    public class CardManager : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerController player;
        [SerializeField] private TowerRoster towerRoster;
        [SerializeField] private MapManager mapManager;
        [SerializeField] private BaseController baseController;
        [SerializeField] private CardRoster cardRoster;

        private readonly List<CardEffectBase> _currentChoices = new();
        private bool _isChoosing;

        /// <summary>레벨업 선택 패널이 떠 있는 동안인지 — TowerBuildController/TowerBuildPreview가 건설 모드를 강제로 끄는 데 참조.</summary>
        public bool IsChoosing => _isChoosing;

        /// <summary>카드 3장이 정해졌을 때 UI에 알림 (CardSelectionUI가 구독).</summary>
        public event Action<IReadOnlyList<CardEffectBase>> ChoicesPresented;

        /// <summary>선택이 끝났을 때 UI에 알림 (패널 닫기 용도).</summary>
        public event Action ChoiceResolved;

        private void Awake()
        {
            cardRoster = OperatorLoadoutSession.ResolveCardRoster(cardRoster);

            // 원본 에셋이 아니라 세션 전용 복제본을 참조하도록 교체 — 카드가 Data를 직접
            // 수정해도 원본 .asset이 오염되지 않게 함 (TowerRoster.GetRuntimeInstance 참고).
            // 같은 원본 에셋을 참조하는 TowerBuildController/TowerBuildMenuUI도 각자 이 호출을
            // 하는데, 캐시가 원본 쪽에 있어서 Awake 호출 순서와 무관하게 전부 같은 복제본을 받는다.
            towerRoster = OperatorLoadoutSession.ResolveTowerRoster(towerRoster).GetRuntimeInstance();
        }

        private void OnEnable()
        {
            gameManager.LeveledUp += HandleLeveledUp;
        }

        private void OnDisable()
        {
            gameManager.LeveledUp -= HandleLeveledUp;
        }

        private void Update()
        {
            if (!_isChoosing || Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                SelectChoice(0);
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                SelectChoice(1);
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                SelectChoice(2);
            }
        }

        private void HandleLeveledUp(int newLevel)
        {
            PresentChoices(newLevel);
        }

        private void PresentChoices(int newLevel)
        {
            _currentChoices.Clear();

            CardContext context = BuildContext();

            // 이미 효과가 없는 카드(예: 이미 해금된 신규 타워 카드)는 후보에서 제외한 뒤,
            // 중복 없이 최대 3장: 남은 후보를 복사해서 하나씩 뽑고 제거.
            var remaining = new List<CardEffectBase>();
            foreach (CardEffectBase card in cardRoster.cards)
            {
                if (card.IsAvailable(context))
                {
                    remaining.Add(card);
                }
            }

            int pickCount = Mathf.Min(3, remaining.Count);

            for (int i = 0; i < pickCount; i++)
            {
                int index = UnityEngine.Random.Range(0, remaining.Count);
                _currentChoices.Add(remaining[index]);
                remaining.RemoveAt(index);
            }

            _isChoosing = true;
            Time.timeScale = 0f;

            Debug.Log($"[Card] 레벨 {newLevel} 달성! 카드를 선택하세요:");
            for (int i = 0; i < _currentChoices.Count; i++)
            {
                Debug.Log($"  {i + 1}. {_currentChoices[i].displayName} — {_currentChoices[i].description}");
            }

            ChoicesPresented?.Invoke(_currentChoices);
        }

        public void SelectChoice(int index)
        {
            if (!_isChoosing || index >= _currentChoices.Count)
            {
                return;
            }

            CardEffectBase chosen = _currentChoices[index];
            chosen.Apply(BuildContext());
            Debug.Log($"[Card] 선택 완료: {chosen.displayName}");

            _isChoosing = false;
            Time.timeScale = 1f;

            ChoiceResolved?.Invoke();
        }

        private CardContext BuildContext() => new()
        {
            player = player,
            towerRoster = towerRoster,
            mapManager = mapManager,
            baseController = baseController,
            gameManager = gameManager,
        };
    }
}
