using RCCom.Definitions.Unit;
using RCCom.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 선택 화면의 전술 로스터 한 줄을 표시한다. 전투 로직을 참조하지 않고
    /// 로컬 카탈로그의 경량 미리보기만 소비해 원격 Definition 전체 로딩을 앞당기지 않는다.
    /// 아이콘 자체는 필요하므로, 원격 유닛이면 아이콘 한 장만 담긴 독립 번들을
    /// RemotePreviewSpriteLoader로 받는다.
    /// </summary>
    public sealed class OperatorRosterPreviewItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;

        public void Setup(AllyUnitCatalogEntry preview)
        {
            if (preview == null)
            {
                return;
            }

            if (icon != null)
            {
                RemotePreviewSpriteLoader.LoadInto(
                    icon, preview.previewIcon, preview.previewIconAddress, preview.fallbackColor);
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
