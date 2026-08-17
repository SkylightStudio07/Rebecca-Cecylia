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
            icon.sprite = definition.sprite;
            nameText.text = definition.data.displayName;
            costText.text = definition.data.deployCost.ToString();

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }

        public void SetSelected(bool selected)
        {
            if (selectionIndicator != null)
            {
                selectionIndicator.SetActive(selected);
            }
        }
    }
}
