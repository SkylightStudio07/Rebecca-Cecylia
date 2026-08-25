using System.Collections.Generic;
using TMPro;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Operator;
using RCCom.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 카탈로그의 골드 구매형 오퍼레이터를 리크루트 화면에 표시하고 구매를 처리한다.
    /// 현재는 칼리스테 한 명뿐이지만 목록과 선택 인덱스를 유지해 구매형 데이터가 늘어나도
    /// 씬이나 이 클래스를 다시 만들지 않고 이전/다음 선택만 연결할 수 있게 한다.
    /// </summary>
    public sealed class OperatorRecruitShopUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private OperatorCatalog catalog;
        [SerializeField] private OperatorAcquisitionUI acquisitionUI;

        [Header("Portraits")]
        [SerializeField] private Image operatorPortrait;
        [SerializeField] private Image operatorUpperBodyPortrait;
        [SerializeField] private Image operatorUpperBodyPortraitLeft;
        [SerializeField] private Image operatorUpperBodyPortraitRight;
        [SerializeField] private GameObject lockSpriteLeft;
        [SerializeField] private GameObject lockSpriteRight;

        [Header("Labels")]
        [SerializeField] private TMP_Text operatorName;
        [SerializeField] private TMP_Text operatorNameLeft;
        [SerializeField] private TMP_Text operatorNameRight;
        [SerializeField] private TMP_Text anotherNameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text rightOperatorNameText;
        [SerializeField] private TMP_Text operatorDialogue;

        [Header("Unique Units")]
        [SerializeField] private OperatorRosterPreviewItem[] unitPreviewItems;

        [Header("Actions")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button recruitOperatorButton;
        [SerializeField] private Image recruitOperatorButtonImage;
        [SerializeField] private Sprite recruitNormalSprite;
        [SerializeField] private Sprite recruitHoverSprite;
        [SerializeField] private Sprite recruitAlreadyPurchasedSprite;

        private readonly List<OperatorCatalogEntry> _purchaseEntries = new();
        private IProfileStorage _profileStorage;
        private PlayerProfile _profile;
        private int _selectedIndex;

        private void Awake()
        {
            // 빌드 이후 원격으로 추가된 오퍼레이터까지 포함한 카탈로그로 바꾼다.
            // 원격 카탈로그가 아직 안 왔거나 추가분이 없으면 내장본을 그대로 돌려준다.
            catalog = LiveCatalogService.Resolve(catalog);
            _profileStorage = new PlayerPrefsProfileStorage();
            if (operatorPortrait != null)
            {
                // 중앙 전신은 원본 비율을 유지하고 패널 밖으로 넘치는 부분만 자연스럽게 잘리게 한다.
                operatorPortrait.preserveAspect = true;
            }
        }

        private void OnEnable()
        {
            catalog = LiveCatalogService.Resolve(catalog);
            if (recruitOperatorButton != null) { recruitOperatorButton.onClick.AddListener(PurchaseSelected); }
            if (previousButton != null) { previousButton.onClick.AddListener(Previous); }
            if (nextButton != null) { nextButton.onClick.AddListener(Next); }
            Refresh();
        }

        private void OnDisable()
        {
            if (recruitOperatorButton != null) { recruitOperatorButton.onClick.RemoveListener(PurchaseSelected); }
            if (previousButton != null) { previousButton.onClick.RemoveListener(Previous); }
            if (nextButton != null) { nextButton.onClick.RemoveListener(Next); }
        }

        public void Refresh()
        {
            _profileStorage ??= new PlayerPrefsProfileStorage();
            _profile = _profileStorage.Load();
            RebuildPurchaseEntries();
            RenderSelected();
        }

        public void Previous()
        {
            MoveSelection(-1);
        }

        public void Next()
        {
            MoveSelection(1);
        }

        public void PurchaseSelected()
        {
            OperatorCatalogEntry entry = GetSelectedEntry();
            if (entry == null || _profile == null || entry.IsUnlocked(_profile) ||
                !_profile.TryPurchaseOperator(entry.operatorId, entry.GetPurchasePrice()))
            {
                RenderSelected();
                return;
            }

            _profileStorage.Save(_profile);
            TitleSceneController titleController = FindFirstObjectByType<TitleSceneController>(
                FindObjectsInactive.Include);
            if (titleController != null)
            {
                titleController.RefreshCommodityText();
            }

            RenderSelected();
            if (acquisitionUI != null)
            {
                acquisitionUI.PresentNewlyUnlocked();
            }
        }

        private void RebuildPurchaseEntries()
        {
            string selectedId = GetSelectedEntry()?.operatorId;
            _purchaseEntries.Clear();
            if (catalog != null && catalog.entries != null)
            {
                for (int i = 0; i < catalog.entries.Count; i++)
                {
                    OperatorCatalogEntry entry = catalog.entries[i];
                    if (entry != null && entry.HasUnlockCondition(OperatorUnlockType.CommodityPurchase))
                    {
                        _purchaseEntries.Add(entry);
                    }
                }
            }

            _selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                int restoredIndex = _purchaseEntries.FindIndex(entry =>
                    entry != null && entry.operatorId == selectedId);
                if (restoredIndex >= 0)
                {
                    _selectedIndex = restoredIndex;
                }
            }
        }

        private void MoveSelection(int direction)
        {
            if (_purchaseEntries.Count <= 1)
            {
                return;
            }

            _selectedIndex = (_selectedIndex + direction + _purchaseEntries.Count) %
                _purchaseEntries.Count;
            RenderSelected();
        }

        private void RenderSelected()
        {
            OperatorCatalogEntry entry = GetSelectedEntry();
            bool hasEntry = entry != null;
            RemotePreviewSpriteLoader.LoadInto(operatorPortrait,
                hasEntry ? entry.shopPortrait : null,
                hasEntry ? entry.shopPortraitAddress : null, Color.clear);
            RemotePreviewSpriteLoader.LoadInto(operatorUpperBodyPortrait,
                hasEntry ? entry.shopUpperBodyPortrait : null,
                hasEntry ? entry.shopUpperBodyPortraitAddress : null, Color.clear);
            RenderSideSlots();

            SetText(operatorName, hasEntry ? entry.displayName : string.Empty);
            SetText(anotherNameText, hasEntry ? entry.alternateName : string.Empty);
            SetText(priceText, hasEntry ? entry.GetPurchasePrice().ToString() : string.Empty);
            SetText(rightOperatorNameText, hasEntry ? entry.displayName : string.Empty);
            SetText(operatorDialogue, hasEntry ? entry.shopDialogue : string.Empty);
            RenderUnitPreviews(entry);

            if (recruitOperatorButton != null)
            {
                bool alreadyPurchased = hasEntry && _profile != null && entry.IsUnlocked(_profile);
                ApplyRecruitButtonVisual(alreadyPurchased);
                recruitOperatorButton.interactable = hasEntry && _profile != null && !alreadyPurchased;
            }

            bool canNavigate = _purchaseEntries.Count > 1;
            if (previousButton != null) { previousButton.interactable = canNavigate; }
            if (nextButton != null) { nextButton.interactable = canNavigate; }
        }

        private void RenderSideSlots()
        {
            if (_purchaseEntries.Count <= 1)
            {
                RenderSideSlot(operatorUpperBodyPortraitLeft, operatorNameLeft, lockSpriteLeft, null);
                RenderSideSlot(operatorUpperBodyPortraitRight, operatorNameRight, lockSpriteRight, null);
                return;
            }

            int leftIndex = (_selectedIndex - 1 + _purchaseEntries.Count) % _purchaseEntries.Count;
            int rightIndex = (_selectedIndex + 1) % _purchaseEntries.Count;
            RenderSideSlot(operatorUpperBodyPortraitLeft, operatorNameLeft, lockSpriteLeft,
                _purchaseEntries[leftIndex]);
            RenderSideSlot(operatorUpperBodyPortraitRight, operatorNameRight, lockSpriteRight,
                _purchaseEntries[rightIndex]);
        }

        private void RenderSideSlot(Image portrait, TMP_Text nameLabel, GameObject lockSprite,
            OperatorCatalogEntry entry)
        {
            Sprite sprite = entry != null && entry.shopUpperBodyPortraitDimmed != null
                ? entry.shopUpperBodyPortraitDimmed
                : entry?.shopUpperBodyPortrait;
            string address = entry != null && !string.IsNullOrEmpty(entry.shopUpperBodyPortraitDimmedAddress)
                ? entry.shopUpperBodyPortraitDimmedAddress
                : entry?.shopUpperBodyPortraitAddress;
            RemotePreviewSpriteLoader.LoadInto(portrait, sprite, address, Color.clear);
            SetText(nameLabel, entry != null ? entry.displayName : string.Empty);
            if (lockSprite != null)
            {
                lockSprite.SetActive(entry != null && (_profile == null || !entry.IsUnlocked(_profile)));
            }
        }

        private void ApplyRecruitButtonVisual(bool alreadyPurchased)
        {
            if (recruitOperatorButton == null || recruitOperatorButtonImage == null)
            {
                return;
            }

            if (alreadyPurchased)
            {
                // 구매 완료 버튼은 더 이상 입력 상태가 없으므로 별도 완료 이미지를 고정한다.
                recruitOperatorButton.transition = Selectable.Transition.None;
                recruitOperatorButtonImage.sprite = recruitAlreadyPurchasedSprite != null
                    ? recruitAlreadyPurchasedSprite
                    : recruitNormalSprite;
                return;
            }

            recruitOperatorButtonImage.sprite = recruitNormalSprite;
            recruitOperatorButton.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = recruitOperatorButton.spriteState;
            state.highlightedSprite = recruitHoverSprite;
            state.selectedSprite = recruitHoverSprite;
            state.pressedSprite = recruitHoverSprite;
            state.disabledSprite = recruitNormalSprite;
            recruitOperatorButton.spriteState = state;
        }

        private OperatorCatalogEntry GetSelectedEntry()
        {
            return _selectedIndex >= 0 && _selectedIndex < _purchaseEntries.Count
                ? _purchaseEntries[_selectedIndex]
                : null;
        }

        private void RenderUnitPreviews(OperatorCatalogEntry entry)
        {
            if (unitPreviewItems == null)
            {
                return;
            }

            for (int i = 0; i < unitPreviewItems.Length; i++)
            {
                OperatorRosterPreviewItem item = unitPreviewItems[i];
                if (item == null)
                {
                    continue;
                }

                bool hasPreview = entry != null && entry.unitPreviews != null &&
                    i < entry.unitPreviews.Count && entry.unitPreviews[i] != null;
                item.gameObject.SetActive(hasPreview);
                if (hasPreview)
                {
                    item.Setup(entry.unitPreviews[i]);
                }
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }
    }
}
