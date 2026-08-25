using System;
using System.Collections.Generic;
using System.Text;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.PlayerPart;
using RCCom.Runtime;
using TMPro;
using UnityEngine;

namespace RCCom.UI
{
    /// <summary>
    /// 계정 전역 기체의 5슬롯 파츠를 구매·장착·판매하는 Exchange 화면. 고정된 카드 뷰 5개를
    /// 페이지처럼 재사용해 파츠 수가 늘어나도 씬 오브젝트를 계속 복제하지 않는다.
    /// </summary>
    public sealed class PlayerPartShopUI : MonoBehaviour
    {
        private const int VisibleCardCount = 5;

        [Header("Data")]
        [SerializeField] private PlayerPartCatalog catalog;

        [Header("Slot Navigation")]
        [SerializeField] private UnityEngine.UI.Button[] slotButtons;
        [SerializeField] private UnityEngine.UI.Image[] slotBackgrounds;
        [SerializeField] private TMP_Text[] slotEquippedTexts;

        [Header("Part Carousel")]
        [SerializeField] private PlayerPartShopCardView[] cardViews;
        [SerializeField] private UnityEngine.UI.Button previousPageButton;
        [SerializeField] private UnityEngine.UI.Button nextPageButton;

        [Header("Specification")]
        [SerializeField] private UnityEngine.UI.Image selectedIcon;
        [SerializeField] private TMP_Text partNameText;
        [SerializeField] private TMP_Text gradeText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text comparisonText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text ownedText;
        [SerializeField] private TMP_Text statusText;

        [Header("Actions")]
        [SerializeField] private UnityEngine.UI.Button buyButton;
        [SerializeField] private UnityEngine.UI.Button equipButton;
        [SerializeField] private UnityEngine.UI.Button sellButton;
        [SerializeField] private TMP_Text buyButtonText;
        [SerializeField] private TMP_Text equipButtonText;
        [SerializeField] private TMP_Text sellButtonText;

        private readonly List<PlayerPartDefinition> _slotParts = new();
        private IProfileStorage _storage;
        private PlayerProfile _profile;
        private PlayerPartSlot _slot;
        private int _selectedIndex;
        private int _pageStart;

        private void Awake()
        {
            _storage = new PlayerPrefsProfileStorage();
            if (slotButtons != null)
            {
                for (int i = 0; i < slotButtons.Length; i++)
                {
                    int captured = i;
                    if (slotButtons[i] != null) { slotButtons[i].onClick.AddListener(() => SelectSlot(captured)); }
                }
            }

            if (previousPageButton != null) { previousPageButton.onClick.AddListener(PreviousPage); }
            if (nextPageButton != null) { nextPageButton.onClick.AddListener(NextPage); }
            if (buyButton != null) { buyButton.onClick.AddListener(BuySelected); }
            if (equipButton != null) { equipButton.onClick.AddListener(EquipSelected); }
            if (sellButton != null) { sellButton.onClick.AddListener(SellSelected); }
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            catalog = PlayerPartContentLoader.Resolve(catalog);
            _storage ??= new PlayerPrefsProfileStorage();
            _profile = _storage.Load();
            PlayerPartDebugSession.SetCatalog(catalog);
            PlayerPartDebugSession.ApplyProfile(_profile);
            RebuildSlotParts(true);
        }

        private void SelectSlot(int index)
        {
            if (index < 0 || index >= Enum.GetValues(typeof(PlayerPartSlot)).Length) { return; }
            _slot = (PlayerPartSlot)index;
            _selectedIndex = 0;
            _pageStart = 0;
            RebuildSlotParts(true);
        }

        private void RebuildSlotParts(bool selectEquipped)
        {
            _slotParts.Clear();
            if (catalog != null && catalog.parts != null)
            {
                for (int i = 0; i < catalog.parts.Count; i++)
                {
                    PlayerPartDefinition part = catalog.parts[i];
                    if (part != null && part.slot == _slot) { _slotParts.Add(part); }
                }
            }

            _slotParts.Sort((left, right) =>
            {
                int grade = left.grade.CompareTo(right.grade);
                return grade != 0 ? grade : left.price.CompareTo(right.price);
            });

            if (selectEquipped)
            {
                string equippedId = GetEquippedId(_slot);
                int equippedIndex = _slotParts.FindIndex(part => part.partId == equippedId);
                _selectedIndex = equippedIndex >= 0 ? equippedIndex : 0;
                _pageStart = Mathf.Max(0, (_selectedIndex / VisibleCardCount) * VisibleCardCount);
            }
            else
            {
                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _slotParts.Count - 1));
            }

            Render();
        }

        private void Render()
        {
            RenderSlots();
            RenderCards();
            RenderSpecification();
        }

        private void RenderSlots()
        {
            int count = Enum.GetValues(typeof(PlayerPartSlot)).Length;
            for (int i = 0; i < count; i++)
            {
                bool selected = i == (int)_slot;
                if (slotBackgrounds != null && i < slotBackgrounds.Length && slotBackgrounds[i] != null)
                {
                    // 스프라이트의 Normal/Hover 아트가 이미 밝기와 선택 테두리를 포함한다.
                    // 색상 곱으로 다시 어둡게 만들면 새 스프라이트의 발광과 글자가 사라지므로,
                    // 선택 상태는 범용 호버 컴포넌트에 맡기고 Image는 원본 색을 유지한다.
                    slotBackgrounds[i].color = Color.white;
                    UISpriteHoverSwap spriteSwap =
                        slotBackgrounds[i].GetComponent<UISpriteHoverSwap>();
                    if (spriteSwap != null)
                    {
                        spriteSwap.SetHighlighted(selected);
                    }
                }

                if (slotEquippedTexts != null && i < slotEquippedTexts.Length)
                {
                    PlayerPartDefinition equipped = ResolveEquipped((PlayerPartSlot)i);
                    SetText(slotEquippedTexts[i], equipped != null ? equipped.displayName : "EMPTY");
                }
            }
        }

        private void RenderCards()
        {
            if (cardViews != null)
            {
                for (int i = 0; i < cardViews.Length; i++)
                {
                    int dataIndex = _pageStart + i;
                    PlayerPartDefinition part = dataIndex < _slotParts.Count ? _slotParts[dataIndex] : null;
                    if (cardViews[i] != null)
                    {
                        cardViews[i].Bind(part, IsOwned(part), IsEquipped(part), dataIndex == _selectedIndex,
                            dataIndex, SelectPart);
                    }
                }
            }

            if (previousPageButton != null) { previousPageButton.interactable = _pageStart > 0; }
            if (nextPageButton != null)
            {
                nextPageButton.interactable = _pageStart + VisibleCardCount < _slotParts.Count;
            }
        }

        private void SelectPart(int index)
        {
            if (index < 0 || index >= _slotParts.Count) { return; }
            _selectedIndex = index;
            Render();
        }

        private void PreviousPage()
        {
            _pageStart = Mathf.Max(0, _pageStart - VisibleCardCount);
            _selectedIndex = _pageStart;
            Render();
        }

        private void NextPage()
        {
            if (_pageStart + VisibleCardCount >= _slotParts.Count) { return; }
            _pageStart += VisibleCardCount;
            _selectedIndex = _pageStart;
            Render();
        }

        private void RenderSpecification()
        {
            PlayerPartDefinition part = GetSelectedPart();
            PlayerPartDefinition current = ResolveEquipped(_slot);
            bool owned = IsOwned(part);
            bool equipped = IsEquipped(part);
            if (selectedIcon != null)
            {
                selectedIcon.sprite = part != null ? part.icon : null;
                selectedIcon.preserveAspect = true;
                selectedIcon.color = part != null && part.icon != null ? Color.white : Color.clear;
            }

            SetText(partNameText, part != null ? part.displayName : "NO PART");
            SetText(gradeText, part != null ? part.grade.ToString().ToUpperInvariant() : string.Empty);
            SetText(descriptionText, part != null ? part.description : "선택한 슬롯에 파츠가 없습니다.");
            SetText(comparisonText, BuildComparison(current, part));
            SetText(costText, part != null ? part.price.ToString() : "0");
            SetText(ownedText, _profile != null ? _profile.commodity.ToString() : "0");
            SetText(buyButtonText, owned ? "OWNED" : "BUY");
            SetText(equipButtonText, equipped ? "EQUIPPED" : "EQUIP");
            int refund = part != null ? Mathf.FloorToInt(part.price * 0.4f) : 0;
            SetText(sellButtonText, $"SELL {refund}");

            bool common = part != null && part.grade == PlayerPartGrade.Common;
            if (buyButton != null)
            {
                buyButton.interactable = part != null && !common && !owned && _profile != null &&
                    _profile.commodity >= part.price;
            }
            if (equipButton != null) { equipButton.interactable = part != null && owned && !equipped; }
            if (sellButton != null) { sellButton.interactable = part != null && !common && owned; }

            string state = equipped ? "현재 장착 중입니다."
                : owned ? "장착할 수 있습니다."
                : part != null && _profile != null && _profile.commodity < part.price
                    ? $"골드가 부족합니다. 필요 {part.price} / 보유 {_profile.commodity}"
                    : "구매할 수 있습니다.";
            SetText(statusText, part != null ? state : string.Empty);
        }

        private void BuySelected()
        {
            PlayerPartDefinition part = GetSelectedPart();
            if (part == null || part.grade == PlayerPartGrade.Common || _profile == null ||
                !_profile.TryPurchasePlayerPart(part.partId, part.price))
            {
                RenderSpecification();
                return;
            }

            SaveAndRefresh();
        }

        private void EquipSelected()
        {
            PlayerPartDefinition part = GetSelectedPart();
            if (part == null || !IsOwned(part) || _profile == null ||
                !_profile.TryEquipPlayerPart(part.slot.ToString(), part.partId))
            {
                return;
            }

            SaveAndRefresh();
        }

        private void SellSelected()
        {
            PlayerPartDefinition part = GetSelectedPart();
            if (part == null || part.grade == PlayerPartGrade.Common || _profile == null ||
                !_profile.TrySellPlayerPart(part.partId, Mathf.FloorToInt(part.price * 0.4f)))
            {
                return;
            }

            SaveAndRefresh();
        }

        private void SaveAndRefresh()
        {
            _storage.Save(_profile);
            PlayerPartDebugSession.SetCatalog(catalog);
            PlayerPartDebugSession.ApplyProfile(_profile);
            TitleSceneController title = FindFirstObjectByType<TitleSceneController>(FindObjectsInactive.Include);
            if (title != null) { title.RefreshCommodityText(); }
            RebuildSlotParts(false);
        }

        private bool IsOwned(PlayerPartDefinition part)
        {
            return part != null && (part.grade == PlayerPartGrade.Common ||
                (_profile != null && _profile.OwnsPlayerPart(part.partId)));
        }

        private bool IsEquipped(PlayerPartDefinition part)
        {
            return part != null && string.Equals(GetEquippedId(part.slot), part.partId,
                StringComparison.Ordinal);
        }

        private string GetEquippedId(PlayerPartSlot slot)
        {
            string saved = _profile != null ? _profile.GetEquippedPlayerPartId(slot.ToString()) : string.Empty;
            if (!string.IsNullOrWhiteSpace(saved)) { return saved; }
            return slot == PlayerPartSlot.Special ? string.Empty : catalog?.FindCommon(slot)?.partId ?? string.Empty;
        }

        private PlayerPartDefinition ResolveEquipped(PlayerPartSlot slot)
        {
            return catalog != null ? catalog.FindById(GetEquippedId(slot)) : null;
        }

        private PlayerPartDefinition GetSelectedPart()
        {
            return _selectedIndex >= 0 && _selectedIndex < _slotParts.Count
                ? _slotParts[_selectedIndex]
                : null;
        }

        private static string BuildComparison(PlayerPartDefinition current, PlayerPartDefinition selected)
        {
            if (selected == null) { return string.Empty; }
            PlayerPartStatOverride before = current != null ? current.statOverride : null;
            PlayerPartStatOverride after = selected.statOverride;
            var builder = new StringBuilder();
            switch (selected.slot)
            {
                case PlayerPartSlot.Thruster:
                    Append(builder, "MOVE SPEED", before?.moveSpeed ?? 0f, after?.moveSpeed ?? 0f);
                    Append(builder, "OVERDRIVE SPEED", before?.skillOverdriveMoveSpeedMultiplier ?? 0f,
                        after?.skillOverdriveMoveSpeedMultiplier ?? 0f);
                    break;
                case PlayerPartSlot.Turret:
                    Append(builder, "DAMAGE", before?.attackDamage ?? 0f, after?.attackDamage ?? 0f);
                    Append(builder, "RANGE", before?.attackRange ?? 0f, after?.attackRange ?? 0f);
                    Append(builder, "INTERVAL", before?.attackInterval ?? 0f, after?.attackInterval ?? 0f);
                    break;
                case PlayerPartSlot.Body:
                    Append(builder, "MAX HP", before?.maxHealth ?? 0f, after?.maxHealth ?? 0f);
                    Append(builder, "INVULNERABILITY", before?.hitInvulnerabilityDuration ?? 0f,
                        after?.hitInvulnerabilityDuration ?? 0f);
                    break;
                case PlayerPartSlot.Driver:
                    Append(builder, "SKILL COOLDOWN", before?.skillCooldown ?? 0f, after?.skillCooldown ?? 0f);
                    Append(builder, "SKILL DAMAGE", before?.skillDamage ?? 0f, after?.skillDamage ?? 0f);
                    Append(builder, "SKILL RANGE", before?.skillRange ?? 0f, after?.skillRange ?? 0f);
                    break;
                case PlayerPartSlot.Special:
                    builder.AppendLine("SPECIAL EFFECT MODULE");
                    builder.Append("기믹 상세는 파츠 설명을 확인하십시오.");
                    break;
            }
            return builder.ToString().TrimEnd();
        }

        private static void Append(StringBuilder builder, string label, float before, float after)
        {
            if (after <= 0f) { return; }
            builder.Append(label).Append("   ").Append(before.ToString("0.##"))
                .Append("  →  ").Append(after.ToString("0.##")).AppendLine();
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) { label.text = value ?? string.Empty; }
        }
    }
}
