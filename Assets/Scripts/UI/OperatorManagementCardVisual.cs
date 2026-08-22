using RCCom.Definitions.Operator;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 오퍼레이터 관리 카드의 한 시각 상태만 표현한다.
    /// Normal/Hover/Locked를 서로 다른 프리팹으로 분리해 상태별 오프셋을 독립적으로 조정할 수 있게 한다.
    /// </summary>
    public sealed class OperatorManagementCardVisual : MonoBehaviour
    {
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI indexText;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private TextMeshProUGUI affinityText;
        [SerializeField] private GameObject activeBadge;

        public void Apply(OperatorCatalogEntry entry, int displayIndex, bool unlocked, bool active, int affinity)
        {
            if (indexText != null) { indexText.text = (displayIndex + 1).ToString("00"); }
            if (nameText != null) { nameText.text = entry != null ? entry.displayName : "UNASSIGNED"; }
            if (stateText != null)
            {
                stateText.text = unlocked ? (active ? "ACTIVE" : "AVAILABLE") : "LOCKED";
                stateText.color = unlocked
                    ? new Color(0.12f, 0.7f, 1f, 1f)
                    : new Color(0.58f, 0.61f, 0.65f, 1f);
            }
            if (affinityText != null) { affinityText.text = unlocked ? $"AFFINITY {affinity:000}" : string.Empty; }
            if (activeBadge != null) { activeBadge.SetActive(active); }

            if (portraitImage != null)
            {
                portraitImage.sprite = entry != null && entry.managementPortrait != null
                    ? entry.managementPortrait
                    : entry != null ? entry.previewPortrait : null;
                portraitImage.enabled = portraitImage.sprite != null;
            }
        }
    }
}
