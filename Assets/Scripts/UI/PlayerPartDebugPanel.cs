using System;
using System.Collections.Generic;
using RCCom.Definitions.PlayerPart;
using RCCom.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 상점 UI/구매 영속화를 만들기 전 실제 카탈로그와 전투 조립을 구동하는 테스트 드라이버.
    /// 기존 Home 오버레이의 자식이라 표시 입력과 빌드 제외 정책은 부모가 그대로 소유한다.
    /// </summary>
    public sealed class PlayerPartDebugPanel : MonoBehaviour
    {
        [SerializeField] private PlayerPartCatalog catalog;
        [SerializeField] private Button[] previousButtons = new Button[5];
        [SerializeField] private Button[] nextButtons = new Button[5];
        [SerializeField] private TextMeshProUGUI[] valueTexts = new TextMeshProUGUI[5];
        [SerializeField] private Button resetButton;
        [SerializeField] private TextMeshProUGUI statusText;

        private readonly Dictionary<PlayerPartSlot, List<PlayerPartDefinition>> _partsBySlot = new();
        private readonly int[] _indices = new int[5];
        private readonly UnityAction[] _previousActions = new UnityAction[5];
        private readonly UnityAction[] _nextActions = new UnityAction[5];

        private void Awake()
        {
#if !UNITY_EDITOR
            gameObject.SetActive(false);
            return;
#else
            PlayerPartDebugSession.SetCatalog(catalog);
            BuildSlotLists();
            SyncIndicesFromSession();
            Refresh();
#endif
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            for (int index = 0; index < 5; index++)
            {
                int capturedIndex = index;
                _previousActions[index] = () => Cycle(capturedIndex, -1);
                _nextActions[index] = () => Cycle(capturedIndex, 1);
                if (previousButtons[index] != null) { previousButtons[index].onClick.AddListener(_previousActions[index]); }
                if (nextButtons[index] != null) { nextButtons[index].onClick.AddListener(_nextActions[index]); }
            }

            if (resetButton != null) { resetButton.onClick.AddListener(ResetLoadout); }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            for (int index = 0; index < 5; index++)
            {
                if (previousButtons[index] != null && _previousActions[index] != null)
                {
                    previousButtons[index].onClick.RemoveListener(_previousActions[index]);
                }
                if (nextButtons[index] != null && _nextActions[index] != null)
                {
                    nextButtons[index].onClick.RemoveListener(_nextActions[index]);
                }
            }

            if (resetButton != null) { resetButton.onClick.RemoveListener(ResetLoadout); }
#endif
        }

#if UNITY_EDITOR
        private void BuildSlotLists()
        {
            foreach (PlayerPartSlot slot in Enum.GetValues(typeof(PlayerPartSlot)))
            {
                var parts = new List<PlayerPartDefinition>();
                if (slot == PlayerPartSlot.Special)
                {
                    parts.Add(null);
                }

                if (catalog != null && catalog.parts != null)
                {
                    foreach (PlayerPartDefinition part in catalog.parts)
                    {
                        if (part != null && part.slot == slot)
                        {
                            parts.Add(part);
                        }
                    }
                }

                parts.Sort((left, right) =>
                {
                    if (left == null) { return right == null ? 0 : -1; }
                    if (right == null) { return 1; }
                    int grade = left.grade.CompareTo(right.grade);
                    return grade != 0 ? grade : string.CompareOrdinal(left.partId, right.partId);
                });
                _partsBySlot[slot] = parts;
            }
        }

        private void SyncIndicesFromSession()
        {
            foreach (PlayerPartSlot slot in Enum.GetValues(typeof(PlayerPartSlot)))
            {
                string partId = PlayerPartDebugSession.GetEquippedId(slot);
                List<PlayerPartDefinition> parts = _partsBySlot[slot];
                int index = parts.FindIndex(part => string.Equals(
                    part != null ? part.partId : string.Empty,
                    partId,
                    StringComparison.Ordinal));
                _indices[(int)slot] = Mathf.Max(0, index);
            }
        }

        private void Cycle(int slotIndex, int direction)
        {
            PlayerPartSlot slot = (PlayerPartSlot)slotIndex;
            List<PlayerPartDefinition> parts = _partsBySlot[slot];
            if (parts.Count == 0)
            {
                return;
            }

            _indices[slotIndex] = (_indices[slotIndex] + direction + parts.Count) % parts.Count;
            PlayerPartDefinition selected = parts[_indices[slotIndex]];
            PlayerPartDebugSession.TryEquip(slot, selected != null ? selected.partId : string.Empty);
            Refresh();
        }

        private void ResetLoadout()
        {
            PlayerPartDebugSession.ClearEquipment();
            SyncIndicesFromSession();
            Refresh();
        }

        private void Refresh()
        {
            var statusLines = new List<string>();
            foreach (PlayerPartSlot slot in Enum.GetValues(typeof(PlayerPartSlot)))
            {
                List<PlayerPartDefinition> parts = _partsBySlot[slot];
                PlayerPartDefinition selected = parts.Count > 0 ? parts[_indices[(int)slot]] : null;
                if (valueTexts[(int)slot] != null)
                {
                    valueTexts[(int)slot].text = selected != null
                        ? $"[{selected.grade}] {selected.displayName}"
                        : "(비움)";
                }

                statusLines.Add($"{GetSlotLabel(slot)}: {(selected != null ? selected.partId : "none")}");
            }

            if (statusText != null)
            {
                statusText.text = "다음 전투 입장 시 적용\n" + string.Join("  |  ", statusLines);
            }
        }

        private static string GetSlotLabel(PlayerPartSlot slot)
        {
            return slot switch
            {
                PlayerPartSlot.Thruster => "추진기",
                PlayerPartSlot.Turret => "포탑",
                PlayerPartSlot.Body => "바디",
                PlayerPartSlot.Driver => "드라이버",
                PlayerPartSlot.Special => "특수",
                _ => slot.ToString(),
            };
        }
#endif
    }
}
