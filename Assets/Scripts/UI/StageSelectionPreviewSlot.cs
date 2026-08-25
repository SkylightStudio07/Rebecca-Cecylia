using TMPro;
using RCCom.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>스테이지 우측 패널의 적 또는 보상 한 칸.</summary>
    public sealed class StageSelectionPreviewSlot : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI amountText;

        public void Show(Sprite icon, string displayName, string amount)
        {
            gameObject.SetActive(true);
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.color = icon != null ? Color.white : new Color(0.18f, 0.35f, 0.45f, 0.65f);
                iconImage.preserveAspect = true;
            }
            if (nameText != null) { nameText.text = displayName ?? string.Empty; }
            if (amountText != null) { amountText.text = amount ?? string.Empty; }
        }

        public void ShowRemote(Sprite localIcon, string remoteAddress, string displayName, string amount)
        {
            Show(localIcon, displayName, amount);
            if (iconImage != null)
            {
                RemotePreviewSpriteLoader.LoadInto(iconImage, localIcon, remoteAddress,
                    new Color(0.18f, 0.35f, 0.45f, 0.65f));
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
