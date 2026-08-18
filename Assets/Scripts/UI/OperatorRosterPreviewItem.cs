using RCCom.Definitions.Operator;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 선택 화면의 전술 로스터 한 줄을 표시한다. 전투 로직을 참조하지 않고
    /// 로컬 카탈로그의 경량 미리보기만 소비해 원격 Definition 로딩을 앞당기지 않는다.
    /// </summary>
    public sealed class OperatorRosterPreviewItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;

        public void Setup(OperatorUnitPreview preview)
        {
            if (preview == null)
            {
                return;
            }

            if (icon != null)
            {
                icon.sprite = preview.previewIcon;
                icon.color = preview.previewIcon != null ? Color.white : preview.fallbackColor;
            }

            if (nameText != null)
            {
                nameText.text = preview.displayName;
            }

            if (costText != null)
            {
                costText.text = $"CP {preview.deployCost}";
            }
        }
    }
}
