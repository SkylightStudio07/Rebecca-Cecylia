using RCCom.Definitions.Stage;
using TMPro;
using UnityEngine;

namespace RCCom.UI
{
    /// <summary>
    /// 스테이지 기록 한 행의 표시만 담당한다. 저장 데이터 판정은 상위 Records 화면에 남겨
    /// 프리팹이 프로필 저장소에 직접 의존하지 않게 한다.
    /// </summary>
    public sealed class StageRecordItemView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image backgroundImage;
        [SerializeField] private UnityEngine.UI.Image stateAccentImage;
        [SerializeField] private TMP_Text stageCodeText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text recommendedLevelText;
        [SerializeField] private TMP_Text stateText;

        private static readonly Color ClearedColor = new(0.08f, 0.68f, 1f, 1f);
        private static readonly Color AvailableColor = new(0.92f, 0.94f, 0.98f, 1f);
        private static readonly Color LockedColor = new(0.34f, 0.39f, 0.45f, 1f);

        public void Bind(StageCatalogEntry entry, bool cleared, bool unlocked)
        {
            if (entry == null)
            {
                gameObject.SetActive(false);
                return;
            }

            Color stateColor = cleared ? ClearedColor : unlocked ? AvailableColor : LockedColor;
            string state = cleared ? "CLEARED" : unlocked ? "AVAILABLE" : "LOCKED";

            if (stageCodeText != null) { stageCodeText.text = entry.displayName; }
            if (titleText != null) { titleText.text = entry.subtitle; }
            if (descriptionText != null)
            {
                descriptionText.text = string.IsNullOrWhiteSpace(entry.description)
                    ? "NO ARCHIVE DESCRIPTION"
                    : entry.description;
            }

            if (recommendedLevelText != null)
            {
                recommendedLevelText.text = $"REC. LV {Mathf.Max(1, entry.recommendedLevel):00}";
            }

            if (stateText != null)
            {
                stateText.text = state;
                stateText.color = stateColor;
            }

            if (stateAccentImage != null) { stateAccentImage.color = stateColor; }
            if (backgroundImage != null)
            {
                backgroundImage.color = cleared
                    ? new Color(0.02f, 0.12f, 0.2f, 0.94f)
                    : unlocked
                        ? new Color(0.025f, 0.055f, 0.085f, 0.94f)
                        : new Color(0.018f, 0.026f, 0.036f, 0.9f);
            }
        }
    }
}
