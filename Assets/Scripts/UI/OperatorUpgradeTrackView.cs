using System;
using RCCom.Definitions.Operator;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>강화 트랙 한 행의 표시와 선택 입력만 담당한다.</summary>
    public sealed class OperatorUpgradeTrackView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Color normalColor = new(0.025f, 0.07f, 0.1f, 0.94f);
        [SerializeField] private Color selectedColor = new(0.02f, 0.35f, 0.68f, 0.98f);

        private Action<int> _onSelected;
        private int _index;

        private void Awake()
        {
            if (button != null) { button.onClick.AddListener(HandleClicked); }
        }

        private void OnDestroy()
        {
            if (button != null) { button.onClick.RemoveListener(HandleClicked); }
        }

        public void Bind(OperatorUpgradeTrack track, int level, int index, bool selected,
            Action<int> onSelected)
        {
            _index = index;
            _onSelected = onSelected;
            bool hasTrack = track != null;
            gameObject.SetActive(hasTrack);
            if (!hasTrack)
            {
                return;
            }

            int maxLevel = Mathf.Max(1, track.maxLevel);
            int normalizedLevel = Mathf.Clamp(level, 0, maxLevel);
            if (categoryText != null)
            {
                categoryText.text = track.category == OperatorUpgradeCategory.Core ? "CORE" : "SUPPORT";
            }
            if (nameText != null) { nameText.text = track.displayName; }
            if (levelText != null) { levelText.text = $"LV. {normalizedLevel:00}"; }
            if (progressText != null)
            {
                progressText.text = new string('■', normalizedLevel) + new string('□', maxLevel - normalizedLevel);
            }
            if (background != null) { background.color = selected ? selectedColor : normalColor; }
            if (button != null) { button.interactable = true; }
        }

        private void HandleClicked()
        {
            _onSelected?.Invoke(_index);
        }
    }
}
