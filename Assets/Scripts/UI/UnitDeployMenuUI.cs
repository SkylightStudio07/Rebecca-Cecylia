using System.Collections.Generic;
using RCCom.Definitions.Unit;
using RCCom.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 선택된 오퍼레이터가 유닛 로스터를 제공할 때만 배치 UI를 노출한다.
    /// 로스터가 없는 타워형 오퍼레이터도 정상 로드아웃이므로 오류 화면을 띄우지 않고,
    /// CanvasGroup으로 시각 요소와 포인터 입력을 함께 차단한다.
    /// </summary>
    public class UnitDeployMenuUI : MonoBehaviour
    {
        [SerializeField] private UnitDeployController deployController;
        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private Transform contentParent;
        [SerializeField] private UnitDeployButton buttonPrefab;
        [SerializeField] private Button deployButton;
        [SerializeField] private TextMeshProUGUI commandPointsText;

        private readonly List<UnitDeployButton> _buttons = new();

        public bool IsVisible { get; private set; }
        public int ButtonCount => _buttons.Count;

        private void Start()
        {
            // Unity는 오브젝트 사이 Awake 순서를 보장하지 않는다. 모든 Awake에서 오퍼레이터
            // 로드아웃 해석이 끝난 뒤 실행되는 Start에서 최종 노출 상태를 결정한다.
            RefreshAvailability();

            if (IsVisible)
            {
                Rebuild();
            }

            RefreshCommandPoints();
            RefreshDeployButton();
        }

        private void OnEnable()
        {
            if (deployController != null)
            {
                deployController.SelectionChanged += HandleSelectionChanged;
                deployController.CommandPointsChanged += HandleCommandPointsChanged;
            }
        }

        private void OnDisable()
        {
            if (deployController != null)
            {
                deployController.SelectionChanged -= HandleSelectionChanged;
                deployController.CommandPointsChanged -= HandleCommandPointsChanged;
            }
        }

        /// <summary>씬 재사용이나 에디터 검증에서 현재 로스터 상태를 다시 반영한다.</summary>
        public void RefreshAvailability()
        {
            bool shouldShow = deployController != null && deployController.IsAvailable;
            SetVisible(shouldShow);
        }

        /// <summary>현재 오퍼레이터의 유닛 Definition 목록으로 선택 버튼을 다시 만든다.</summary>
        public void Rebuild()
        {
            ClearButtons();

            if (deployController == null || !deployController.IsAvailable)
            {
                return;
            }

            if (contentParent == null || buttonPrefab == null)
            {
                Debug.LogError("[UnitDeployUI] 버튼 Content 또는 공용 버튼 프리팹 참조가 비어 있습니다.", this);
                return;
            }

            AllyUnitRoster roster = deployController.Roster;
            for (int i = 0; i < roster.units.Count; i++)
            {
                AllyUnitDefinition definition = roster.units[i];
                if (definition == null || definition.data == null)
                {
                    // 잘못된 로스터 한 칸 때문에 나머지 유닛까지 선택할 수 없게 하지 않는다.
                    Debug.LogWarning($"[UnitDeployUI] 로스터 {i}번 Definition이 비어 있어 버튼 생성을 건너뜁니다.", this);
                    continue;
                }

                int rosterIndex = i;
                UnitDeployButton button = Instantiate(buttonPrefab, contentParent);
                button.Setup(definition, () => deployController.SelectUnit(rosterIndex));
                button.SetSelected(definition == deployController.SelectedDefinition);
                _buttons.Add(button);
            }

            RefreshUnitButtons();
        }

        private void HandleSelectionChanged(AllyUnitDefinition selectedDefinition)
        {
            foreach (UnitDeployButton button in _buttons)
            {
                if (button != null)
                {
                    button.SetSelected(button.Definition == selectedDefinition);
                }
            }

            RefreshDeployButton();
        }

        /// <summary>씬의 소환 버튼이 호출한다. 선택과 실제 생성의 경계를 Controller에 유지한다.</summary>
        public void TryDeploySelected()
        {
            if (deployController == null)
            {
                return;
            }

            deployController.TryDeploySelected();
            RefreshCommandPoints();
            RefreshDeployButton();
        }

        private void HandleCommandPointsChanged(int _)
        {
            RefreshCommandPoints();
            RefreshUnitButtons();
            RefreshDeployButton();
        }

        private void RefreshUnitButtons()
        {
            foreach (UnitDeployButton button in _buttons)
            {
                if (button != null)
                {
                    // 비용 판정은 Controller에 남겨 UI와 실제 소비 조건이 달라지지 않게 한다.
                    button.SetAffordable(deployController != null &&
                                         deployController.CanAfford(button.Definition));
                }
            }
        }

        private void RefreshCommandPoints()
        {
            if (commandPointsText != null && deployController != null)
            {
                commandPointsText.text = $"CP {deployController.CommandPoints} / {deployController.MaxCommandPoints}";
            }
        }

        private void RefreshDeployButton()
        {
            if (deployButton == null)
            {
                return;
            }

            AllyUnitDefinition selected = deployController != null ? deployController.SelectedDefinition : null;
            deployButton.interactable = deployController != null && selected != null &&
                                       deployController.IsDeployInputEnabled &&
                                       deployController.CanAfford(selected);
        }

        private void ClearButtons()
        {
            foreach (UnitDeployButton button in _buttons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            _buttons.Clear();
        }

        private void SetVisible(bool visible)
        {
            IsVisible = visible;

            if (panelGroup == null)
            {
                Debug.LogError("[UnitDeployUI] 배치 패널 CanvasGroup 참조가 비어 있습니다.", this);
                return;
            }

            // GameObject를 끄면 같은 오브젝트의 UI 스크립트도 비활성화되어 후속 갱신을
            // 받을 수 없으므로, 기존 CardSelectionUI와 같은 CanvasGroup 계약을 사용한다.
            panelGroup.alpha = visible ? 1f : 0f;
            panelGroup.interactable = visible;
            panelGroup.blocksRaycasts = visible;
        }
    }
}
