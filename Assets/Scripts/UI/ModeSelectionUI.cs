using RCCom.Runtime;
using RCCom.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 오퍼레이터 확정 후 스테이지·엔드리스 모드를 고르는 UGUI.
    /// 엔드리스는 기존 DefenseScene 진입을 유지하고, 스테이지는 챕터 맵으로 넘긴다.
    /// </summary>
    public sealed class ModeSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private CanvasGroup mainMenuGroup;
        [SerializeField] private OperatorSelectionUI operatorSelectionUI;
        [SerializeField] private StageSelectionUI stageSelectionUI;
        [SerializeField] private TextMeshProUGUI operatorNameText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button stageButton;
        [SerializeField] private Button endlessButton;
        [SerializeField] private Button backButton;
        [SerializeField] private string defenseSceneName = "DefenseScene";

        private string _operatorId;

        private void Awake()
        {
            SetPanelVisible(false);
        }

        public void Open()
        {
            Open(string.Empty);
        }

        public void Open(string operatorId)
        {
            _operatorId = operatorId ?? string.Empty;
            if (operatorNameText != null)
            {
                operatorNameText.text = string.IsNullOrWhiteSpace(_operatorId)
                    ? "OPERATOR LOADOUT READY"
                    : $"OPERATOR  /  {_operatorId.ToUpperInvariant()}";
            }

            if (statusText != null)
            {
                statusText.text = "작전 모드를 선택하십시오.";
            }

            SetPanelVisible(true);
            FocusDefaultButton();
        }

        public void SelectStageMode()
        {
            SetPanelVisible(false);
            if (stageSelectionUI != null)
            {
                stageSelectionUI.Open();
                return;
            }

            if (statusText != null) { statusText.text = "스테이지 맵을 준비 중입니다."; }
            SetPanelVisible(true);
        }

        public void SelectEndlessMode()
        {
            BattleSession.SelectEndless();
            Time.timeScale = 1f;
            SceneManager.LoadScene(defenseSceneName);
        }

        public void Back()
        {
            SetPanelVisible(false);
            if (operatorSelectionUI != null)
            {
                operatorSelectionUI.Open();
                return;
            }

            SetMainMenuVisible(true);
        }

        private void SetPanelVisible(bool visible)
        {
            if (panel != null) { panel.SetActive(visible); }
            SetMainMenuVisible(!visible);
        }

        private void SetMainMenuVisible(bool visible)
        {
            if (mainMenuGroup == null)
            {
                return;
            }

            mainMenuGroup.interactable = visible;
            mainMenuGroup.blocksRaycasts = visible;
        }

        private void FocusDefaultButton()
        {
            if (EventSystem.current == null)
            {
                return;
            }

            Button target = stageButton != null && stageButton.interactable ? stageButton : endlessButton;
            if (target != null && target.interactable)
            {
                EventSystem.current.SetSelectedGameObject(target.gameObject);
            }
        }
    }
}
