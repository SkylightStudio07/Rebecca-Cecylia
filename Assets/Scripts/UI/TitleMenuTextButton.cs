using RCCom.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RCCom.UI
{
    public class TitleMenuTextButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISubmitHandler
    {
        public enum MenuAction
        {
            NewGame,
            ManageOperators,
            Preference,
            ReturnToTitle
        }

        [SerializeField] private MenuAction action;
        [SerializeField] private TitleSceneController titleSceneController;
        [SerializeField] private TitleConfigurationController configurationController;
        [SerializeField] private OperatorSelectionUI operatorSelectionUI;
        [SerializeField] private OperatorManagementUI operatorManagementUI;
        [SerializeField] private string defenseSceneName = "DefenseScene";
        [SerializeField] private float hoverScale = 1.12f;
        [SerializeField] private float scaleSpeed = 12f;
        [SerializeField] private float shakeAmount = 3f;
        [SerializeField] private float shakeSpeed = 18f;

        private RectTransform rectTransform;
        private Graphic graphic;
        private Vector3 baseScale;
        private Vector2 basePosition;
        private bool isHovering;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            graphic = GetComponent<Graphic>();

            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }

            if (rectTransform != null)
            {
                baseScale = rectTransform.localScale;
                basePosition = rectTransform.anchoredPosition;
            }
            else
            {
                baseScale = transform.localScale;
            }
        }

        private void OnDisable()
        {
            isHovering = false;
            ApplyVisual(0f, 0f);
        }

        private void Update()
        {
            float target = isHovering ? 1f : 0f;
            float currentScale = GetHoverAmount();
            float nextScale = Mathf.MoveTowards(currentScale, target, scaleSpeed * Time.unscaledDeltaTime);
            float shake = isHovering ? Mathf.Sin(Time.unscaledTime * shakeSpeed) * shakeAmount : 0f;

            ApplyVisual(nextScale, shake);
        }

        public void Configure(MenuAction menuAction, TitleSceneController controller)
        {
            action = menuAction;
            titleSceneController = controller;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            InvokeAction();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            InvokeAction();
        }

        private void InvokeAction()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayMainMenuClick();
            }

            switch (action)
            {
                case MenuAction.NewGame:
                    if (operatorSelectionUI != null)
                    {
                        operatorSelectionUI.Open();
                    }
                    else
                    {
                        // 선택 UI가 아직 배선되지 않은 개발 씬에서도 기존 진입 경로는 유지한다.
                        SceneManager.LoadScene(defenseSceneName);
                    }
                    break;
                case MenuAction.Preference:
                    configurationController?.Open();
                    break;
                case MenuAction.ManageOperators:
                    if (operatorManagementUI != null)
                    {
                        operatorManagementUI.Open();
                    }
                    break;
                case MenuAction.ReturnToTitle:
                    titleSceneController?.ReturnToTitle();
                    break;
            }
        }

        private float GetHoverAmount()
        {
            Vector3 currentScale = rectTransform != null ? rectTransform.localScale : transform.localScale;
            float denominator = Mathf.Max(hoverScale - 1f, 0.001f);
            return Mathf.Clamp01((currentScale.x / Mathf.Max(baseScale.x, 0.001f) - 1f) / denominator);
        }

        private void ApplyVisual(float hoverAmount, float shake)
        {
            float scale = Mathf.Lerp(1f, hoverScale, hoverAmount);

            if (rectTransform != null)
            {
                rectTransform.localScale = baseScale * scale;
                rectTransform.anchoredPosition = basePosition + new Vector2(shake, 0f);
            }
            else
            {
                transform.localScale = baseScale * scale;
            }
        }
    }
}
