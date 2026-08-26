using System.Collections.Generic;
using RCCom.Data;
using RCCom.Effects.Card;
using RCCom.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 카드 선택 패널. CardManager.ChoicesPresented를 구독해 패널을 켜고 버튼 3개에 카드
    /// 이름/설명을 채운다. 버튼 클릭 시 CardManager.SelectChoice를 직접 호출 — 숫자키와
    /// 완전히 같은 경로라 둘 다 정상 동작하고, 어느 쪽으로 선택해도 ChoiceResolved로 패널이 닫힌다.
    ///
    /// 보이기/숨기기는 GameObject.SetActive가 아니라 CanvasGroup으로 처리한다 — 이 스크립트가
    /// 패널과 같은 오브젝트에 붙어있어서, SetActive(false)로 자기 자신을 끄면 그 순간 Unity가
    /// OnDisable도 같이 불러버려 이벤트 구독이 풀리고 다시는 안 켜지는 문제가 있었다.
    /// CanvasGroup은 오브젝트를 비활성화하지 않고 alpha/interactable/blocksRaycasts만
    /// 바꾸므로 이 스크립트 자신은 계속 활성 상태로 남는다.
    ///
    /// cardButtons/cardNameTexts/cardDescriptionTexts는 인덱스가 서로 대응하도록(같은 개수,
    /// 같은 순서) 인스펙터에서 연결할 것 — 최대 3개까지 지원.
    /// </summary>
    public class CardSelectionUI : MonoBehaviour
    {
        [SerializeField] private CardManager cardManager;
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private Button[] cardButtons;
        [SerializeField] private TextMeshProUGUI[] cardNameTexts;
        [SerializeField] private TextMeshProUGUI[] cardDescriptionTexts;
        [SerializeField] private UnitDeployMenuUI unitDeployMenuUI;

        [Tooltip("카드 종류별 프레임 이미지 (인덱스는 cardButtons 등과 동일하게 대응)")]
        [SerializeField] private Image[] cardFrameImages;

        [Header("카드 카테고리별 프레임 스프라이트 (CardEffectBase.Category 기준)")]
        [SerializeField] private Sprite attackFrameSprite;
        [SerializeField] private Sprite skillFrameSprite;
        [SerializeField] private Sprite powerFrameSprite;

        private void Awake()
        {
            if (unitDeployMenuUI == null)
            {
                // 기존 DefenseScene을 다시 굽지 않아도 동작하게 하되, 인스펙터 연결이 있으면
                // 그것을 우선한다. 두 UI 모두 같은 전투 Canvas에 하나씩만 존재한다.
                unitDeployMenuUI = FindFirstObjectByType<UnitDeployMenuUI>();
            }

            Hide();
        }

        private void OnEnable()
        {
            cardManager.ChoicesPresented += HandleChoicesPresented;
            cardManager.ChoiceResolved += HandleChoiceResolved;
        }

        private void OnDisable()
        {
            cardManager.ChoicesPresented -= HandleChoicesPresented;
            cardManager.ChoiceResolved -= HandleChoiceResolved;
        }

        private void HandleChoicesPresented(IReadOnlyList<CardEffectBase> choices)
        {
            Show();

            for (int i = 0; i < cardButtons.Length; i++)
            {
                bool hasChoice = i < choices.Count;
                cardButtons[i].gameObject.SetActive(hasChoice);

                if (!hasChoice)
                {
                    continue;
                }

                cardNameTexts[i].text = choices[i].displayName;
                cardDescriptionTexts[i].text = choices[i].description;

                if (cardFrameImages != null && i < cardFrameImages.Length && cardFrameImages[i] != null)
                {
                    cardFrameImages[i].sprite = GetFrameSprite(choices[i].Category);
                }

                int index = i;
                cardButtons[i].onClick.RemoveAllListeners();
                cardButtons[i].onClick.AddListener(() => cardManager.SelectChoice(index));
            }
        }

        private void HandleChoiceResolved()
        {
            Hide();
        }

        /// <summary>TowerBuildMenuUI.GetFrameSprite와 동일한 규칙 — 파워/스킬만 전용 프레임, 그 외(공격 포함 기본값)는 공격 프레임.</summary>
        private Sprite GetFrameSprite(TowerKind category) => category switch
        {
            TowerKind.Power => powerFrameSprite,
            TowerKind.Skill => skillFrameSprite,
            _ => attackFrameSprite,
        };

        private void Show()
        {
            if (unitDeployMenuUI != null)
            {
                unitDeployMenuUI.SetTemporarilyHidden(true);
            }

            panelGroup.alpha = 1f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }

        private void Hide()
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;

            if (unitDeployMenuUI != null)
            {
                unitDeployMenuUI.SetTemporarilyHidden(false);
            }
        }
    }
}
