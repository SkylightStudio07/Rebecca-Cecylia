using System;
using RCCom.Definitions.Operator;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 오퍼레이터 선택 화면에서 카탈로그 항목 하나를 표시하는 공용 카드다.
    /// 선택 상태와 잠김·원격 상태 표시는 여기서만 맡고, 실제 선택·다운로드 흐름은
    /// OperatorSelectionUI에 남겨 카드가 게임 진행 상태를 소유하지 않게 한다.
    /// </summary>
    public sealed class OperatorSelectionCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Image selectionFrame;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI typeText;
        [SerializeField] private TextMeshProUGUI stateText;

        public void Setup(OperatorCatalogEntry entry, bool unlocked, bool selected, Action onClick)
        {
            if (entry == null)
            {
                return;
            }

            if (portraitImage != null)
            {
                portraitImage.sprite = entry.previewPortrait;
                portraitImage.enabled = entry.previewPortrait != null;
            }

            if (nameText != null)
            {
                nameText.text = entry.displayName;
            }

            if (typeText != null)
            {
                typeText.text = entry.remoteContent ? "REMOTE OPERATOR" : "LOCAL OPERATOR";
            }

            if (stateText != null)
            {
                stateText.text = unlocked
                    ? (entry.remoteContent ? "DOWNLOAD READY" : "AVAILABLE")
                    : $"WAVE {entry.requiredBestWave} REQUIRED";
                stateText.color = unlocked
                    ? new Color(0.45f, 0.88f, 1f, 1f)
                    : new Color(0.74f, 0.48f, 0.48f, 1f);
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke());
            }

            SetSelected(selected);
        }

        public void SetSelected(bool selected)
        {
            if (selectionFrame != null)
            {
                selectionFrame.enabled = selected;
            }
        }
    }
}
