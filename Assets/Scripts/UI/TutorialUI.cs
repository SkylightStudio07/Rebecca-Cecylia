using RCCom.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 게임 시작 시 자동으로 뜨는 페이지 넘김형 튜토리얼. TutorialSet(SO)의 페이지를 순서대로
    /// 보여주고, Next 버튼을 누르면 다음 페이지로 — 마지막 페이지에서 누르면 닫힘.
    /// 읽는 동안 게임이 진행되면 안 되니 Time.timeScale=0으로 일시정지(CardManager의 레벨업
    /// 선택 패널과 같은 메커니즘) — 닫힐 때 1로 복원.
    /// </summary>
    public class TutorialUI : MonoBehaviour
    {
        [SerializeField] private TutorialSet tutorialSet;

        [SerializeField] private CanvasGroup panelGroup;
        [SerializeField] private TextMeshProUGUI indexText;
        [SerializeField] private TextMeshProUGUI topicText;
        [SerializeField] private Image tutorialImage;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button skipButton;

        private int _pageIndex;

        private void Awake()
        {
            if (nextButton != null)
            {
                nextButton.onClick.AddListener(HandleNext);
            }

            if (skipButton != null)
            {
                skipButton.onClick.AddListener(HandleSkip);
            }

            if (tutorialSet != null && tutorialSet.pages.Count > 0)
            {
                _pageIndex = 0;
                ShowPage();
                Show();
                Time.timeScale = 0f;
            }
            else
            {
                Hide();
            }
        }

        private void OnDestroy()
        {
            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(HandleNext);
            }

            if (skipButton != null)
            {
                skipButton.onClick.RemoveListener(HandleSkip);
            }
        }

        private void ShowPage()
        {
            TutorialPage page = tutorialSet.pages[_pageIndex];

            if (indexText != null)
            {
                indexText.text = $"{_pageIndex + 1} / {tutorialSet.pages.Count}";
            }

            if (topicText != null)
            {
                topicText.text = page.topic;
            }

            if (descriptionText != null)
            {
                descriptionText.text = page.description;
            }

            if (tutorialImage != null)
            {
                tutorialImage.sprite = page.image;
                tutorialImage.enabled = page.image != null;
            }
        }

        private void HandleNext()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayButtonClick();
            }

            _pageIndex++;

            if (_pageIndex >= tutorialSet.pages.Count)
            {
                CompleteTutorial();
                return;
            }

            ShowPage();
        }

        private void HandleSkip()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayButtonClick();
            }

            CompleteTutorial();
        }

        private void CompleteTutorial()
        {
            Time.timeScale = 1f;
            Hide();
        }

        private void Show()
        {
            panelGroup.alpha = 1f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }

        private void Hide()
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }
    }
}
