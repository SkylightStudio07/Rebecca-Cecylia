using System;
using RCCom.Definitions.Unit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 유닛 선택 목록에 동적으로 생성되는 버튼 하나. 표시할 Definition만 받아 데이터로
    /// 내용을 채우고, 로스터 인덱스와 실제 선택 책임은 UnitDeployMenuUI에 남긴다.
    /// </summary>
    public class UnitDeployButton : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button button;
        [SerializeField] private GameObject selectionIndicator;

        public AllyUnitDefinition Definition { get; private set; }

        public void Setup(AllyUnitDefinition definition, Action onClick)
        {
            Definition = definition;
            if (icon != null)
            {
                icon.sprite = definition.sprite;
                // 정식 아이콘이 들어오기 전에도 Definition의 회색상자 색으로 유닛을
                // 구분한다. 스프라이트가 연결되면 흰색으로 복귀해 원본 색을 보존한다.
                icon.color = definition.sprite != null ? Color.white : definition.tint;
            }

            if (nameText != null)
            {
                nameText.text = definition.data.displayName;
            }

            if (costText != null)
            {
                costText.text = $"{definition.data.deployCost} CP";
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick());
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(selected);
            }
        }

        public void SetAffordable(bool affordable)
        {
            if (button != null)
            {
                button.interactable = affordable;
            }
        }
    }
}
