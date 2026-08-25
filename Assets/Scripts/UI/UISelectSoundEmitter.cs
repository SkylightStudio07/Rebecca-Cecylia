using RCCom.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 일반 UI 버튼의 포인터 누름과 키보드/패드 Submit을 공용 선택음으로 연결한다.
    /// 버튼별 PersistentCall을 복제하지 않아 새 버튼도 같은 계약으로 자동 배선할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UISelectSoundEmitter : MonoBehaviour, IPointerDownHandler, ISubmitHandler
    {
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !CanPlay())
            {
                return;
            }

            Play();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (CanPlay())
            {
                Play();
            }
        }

        private bool CanPlay()
        {
            return button != null && button.IsActive() && button.IsInteractable();
        }

        private static void Play()
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayUiSelect();
            }
        }
    }
}
