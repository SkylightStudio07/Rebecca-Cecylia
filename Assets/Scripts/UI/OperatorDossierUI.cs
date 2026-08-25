using System;
using System.Collections.Generic;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Operator;
using RCCom.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 보유 오퍼레이터만 순환하며 프로필과 호감도별 인연 기록을 보여준다.
    /// 인연 본문은 Definition에서 비동기로 읽어 신규 오퍼레이터 자료를 코드 수정 없이
    /// Addressables 콘텐츠 갱신만으로 추가할 수 있게 한다.
    /// </summary>
    public sealed class OperatorDossierUI : MonoBehaviour
    {
        private static readonly int[] BondRequirements = { 0, 25, 50, 75, 100 };

        [Header("Data")]
        [SerializeField] private OperatorCatalog catalog;

        [Header("Shared Operator Panel")]
        [SerializeField] private Image operatorPortrait;
        [SerializeField] private Image operatorUpperBodyPortrait;
        [SerializeField] private Image operatorUpperBodyPortraitLeft;
        [SerializeField] private Image operatorUpperBodyPortraitRight;
        [SerializeField] private GameObject lockSpriteLeft;
        [SerializeField] private GameObject lockSpriteRight;
        [SerializeField] private TMP_Text operatorName;
        [SerializeField] private TMP_Text operatorNameLeft;
        [SerializeField] private TMP_Text operatorNameRight;
        [SerializeField] private TMP_Text anotherNameText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Header("Dossier Panel")]
        [SerializeField] private TMP_Text rightOperatorNameText;
        [SerializeField] private TMP_Text operatorDialogue;
        [SerializeField] private TMP_Text codenameText;
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private TMP_Text factionText;
        [SerializeField] private TMP_Text heightText;
        [SerializeField] private TMP_Text birthdayText;
        [SerializeField] private TMP_Text specialityText;
        [SerializeField] private TMP_Text weaponText;
        [SerializeField] private TMP_Text originText;
        [SerializeField] private Button[] bondButtons;
        [SerializeField] private GameObject[] materialLockedSprites;
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Button[] detailCloseButtons;

        private readonly List<OperatorCatalogEntry> _ownedEntries = new();
        private readonly List<Action> _bondListeners = new();
        private IProfileStorage _storage;
        private PlayerProfile _profile;
        private OperatorDefinition _definition;
        private AsyncOperationHandle<OperatorDefinition> _definitionHandle;
        private bool _ownsDefinitionHandle;
        private int _selectedOperatorIndex;
        private int _loadVersion;

        private void Awake()
        {
            _storage = new PlayerPrefsProfileStorage();
            if (operatorPortrait != null) { operatorPortrait.preserveAspect = true; }
            HideDetail();
        }

        private void OnEnable()
        {
            if (previousButton != null) { previousButton.onClick.AddListener(PreviousOperator); }
            if (nextButton != null) { nextButton.onClick.AddListener(NextOperator); }
            BindBondButtons();
            if (detailCloseButtons != null)
            {
                for (int i = 0; i < detailCloseButtons.Length; i++)
                {
                    if (detailCloseButtons[i] != null)
                    {
                        detailCloseButtons[i].onClick.AddListener(HideDetail);
                    }
                }
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (previousButton != null) { previousButton.onClick.RemoveListener(PreviousOperator); }
            if (nextButton != null) { nextButton.onClick.RemoveListener(NextOperator); }
            UnbindBondButtons();
            if (detailCloseButtons != null)
            {
                for (int i = 0; i < detailCloseButtons.Length; i++)
                {
                    if (detailCloseButtons[i] != null)
                    {
                        detailCloseButtons[i].onClick.RemoveListener(HideDetail);
                    }
                }
            }

            _loadVersion++;
            ReleaseDefinitionHandle();
            HideDetail();
        }

        public void Refresh()
        {
            _storage ??= new PlayerPrefsProfileStorage();
            _profile = _storage.Load();
            RebuildOwnedEntries();
            RenderOperator();
        }

        private void BindBondButtons()
        {
            UnbindBondButtons();
            if (bondButtons == null) { return; }
            for (int i = 0; i < bondButtons.Length; i++)
            {
                int index = i;
                Action listener = () => ShowBondRecord(index);
                _bondListeners.Add(listener);
                if (bondButtons[i] != null) { bondButtons[i].onClick.AddListener(listener.Invoke); }
            }
        }

        private void UnbindBondButtons()
        {
            if (bondButtons != null)
            {
                for (int i = 0; i < bondButtons.Length && i < _bondListeners.Count; i++)
                {
                    if (bondButtons[i] != null)
                    {
                        bondButtons[i].onClick.RemoveListener(_bondListeners[i].Invoke);
                    }
                }
            }

            _bondListeners.Clear();
        }

        private void PreviousOperator() => MoveOperator(-1);
        private void NextOperator() => MoveOperator(1);

        private void MoveOperator(int direction)
        {
            if (_ownedEntries.Count <= 1) { return; }
            _selectedOperatorIndex = (_selectedOperatorIndex + direction + _ownedEntries.Count) %
                _ownedEntries.Count;
            RenderOperator();
        }

        private void RebuildOwnedEntries()
        {
            string selectedId = GetSelectedEntry()?.operatorId;
            if (string.IsNullOrWhiteSpace(selectedId))
            {
                selectedId = _profile != null ? _profile.selectedOperatorId : string.Empty;
            }

            _ownedEntries.Clear();
            if (catalog != null && catalog.entries != null)
            {
                for (int i = 0; i < catalog.entries.Count; i++)
                {
                    OperatorCatalogEntry entry = catalog.entries[i];
                    if (entry != null && entry.IsUnlocked(_profile)) { _ownedEntries.Add(entry); }
                }
            }

            _selectedOperatorIndex = 0;
            int restored = _ownedEntries.FindIndex(entry => entry.operatorId == selectedId);
            if (restored >= 0) { _selectedOperatorIndex = restored; }
        }

        private void RenderOperator()
        {
            HideDetail();
            OperatorCatalogEntry entry = GetSelectedEntry();
            SetSprite(operatorPortrait, entry != null
                ? entry.shopPortrait != null ? entry.shopPortrait : entry.managementPortrait
                : null);
            SetSprite(operatorUpperBodyPortrait, ResolveBrightPortrait(entry));
            SetText(operatorName, entry != null ? entry.displayName : string.Empty);
            SetText(anotherNameText, entry != null ? entry.alternateName : string.Empty);
            SetText(rightOperatorNameText, entry != null ? entry.displayName : "NO OPERATOR");
            SetText(operatorDialogue, entry != null ? entry.shopDialogue : string.Empty);
            RenderSideSlots();
            ClearDossierFields();
            RenderBondLocks(null);

            bool canNavigate = _ownedEntries.Count > 1;
            if (previousButton != null) { previousButton.interactable = canNavigate; }
            if (nextButton != null) { nextButton.interactable = canNavigate; }

            BeginLoadDefinition(entry);
        }

        private void RenderSideSlots()
        {
            OperatorCatalogEntry left = null;
            OperatorCatalogEntry right = null;
            if (_ownedEntries.Count == 2)
            {
                if (_selectedOperatorIndex == 0) { right = _ownedEntries[1]; }
                else { left = _ownedEntries[0]; }
            }
            else if (_ownedEntries.Count >= 3)
            {
                left = _ownedEntries[(_selectedOperatorIndex - 1 + _ownedEntries.Count) % _ownedEntries.Count];
                right = _ownedEntries[(_selectedOperatorIndex + 1) % _ownedEntries.Count];
            }

            RenderSideSlot(operatorUpperBodyPortraitLeft, operatorNameLeft, lockSpriteLeft, left);
            RenderSideSlot(operatorUpperBodyPortraitRight, operatorNameRight, lockSpriteRight, right);
        }

        private void BeginLoadDefinition(OperatorCatalogEntry entry)
        {
            _loadVersion++;
            int version = _loadVersion;
            ReleaseDefinitionHandle();
            if (entry == null || string.IsNullOrWhiteSpace(entry.address)) { return; }

            if (OperatorLoadoutSession.SelectedDefinition != null &&
                OperatorLoadoutSession.SelectedDefinition.operatorId == entry.operatorId)
            {
                _definition = OperatorLoadoutSession.SelectedDefinition;
                RenderDefinition();
                return;
            }

            _definitionHandle = Addressables.LoadAssetAsync<OperatorDefinition>(entry.address);
            _ownsDefinitionHandle = true;
            _definitionHandle.Completed += operation =>
            {
                if (version != _loadVersion || !isActiveAndEnabled) { return; }
                if (operation.Status != AsyncOperationStatus.Succeeded || operation.Result == null ||
                    operation.Result.operatorId != entry.operatorId)
                {
                    return;
                }

                _definition = operation.Result;
                RenderDefinition();
            };
        }

        private void RenderDefinition()
        {
            if (_definition == null) { return; }
            SetText(rightOperatorNameText, _definition.displayName);
            SetText(operatorDialogue, _definition.shopDialogue);
            SetProfileText(codenameText, _definition.codename);
            SetProfileText(roleText, _definition.role);
            SetProfileText(factionText, _definition.faction);
            SetProfileText(heightText, _definition.height);
            SetProfileText(birthdayText, _definition.birthday);
            SetProfileText(specialityText, _definition.speciality);
            SetProfileText(weaponText, _definition.weapon);
            SetProfileText(originText, _definition.origin);
            RenderBondLocks(_definition);
        }

        private void ClearDossierFields()
        {
            SetProfileText(codenameText, string.Empty);
            SetProfileText(roleText, string.Empty);
            SetProfileText(factionText, string.Empty);
            SetProfileText(heightText, string.Empty);
            SetProfileText(birthdayText, string.Empty);
            SetProfileText(specialityText, string.Empty);
            SetProfileText(weaponText, string.Empty);
            SetProfileText(originText, string.Empty);
        }

        private void RenderBondLocks(OperatorDefinition definition)
        {
            OperatorCatalogEntry entry = GetSelectedEntry();
            int affinity = entry != null && _profile != null
                ? _profile.GetOperatorAffinity(entry.operatorId)
                : 0;
            for (int i = 0; i < BondRequirements.Length; i++)
            {
                // 본문을 아직 작성하지 않았더라도 단계 해금 상태는 친밀도만으로 결정한다.
                // 그래야 1단계가 신규 오퍼레이터 제작 직후부터 항상 열려 있다는 규칙이 유지된다.
                bool unlocked = entry != null && definition != null &&
                    affinity >= BondRequirements[i];
                if (materialLockedSprites != null && i < materialLockedSprites.Length &&
                    materialLockedSprites[i] != null)
                {
                    materialLockedSprites[i].SetActive(!unlocked);
                }

                if (bondButtons != null && i < bondButtons.Length && bondButtons[i] != null)
                {
                    bondButtons[i].interactable = unlocked;
                }
            }
        }

        private void ShowBondRecord(int index)
        {
            OperatorCatalogEntry entry = GetSelectedEntry();
            int affinity = entry != null && _profile != null
                ? _profile.GetOperatorAffinity(entry.operatorId)
                : 0;
            if (_definition == null || index < 0 || index >= BondRequirements.Length ||
                affinity < BondRequirements[index])
            {
                return;
            }

            string description = _definition.bondRecords != null && index < _definition.bondRecords.Count
                ? _definition.bondRecords[index]?.description
                : string.Empty;
            SetText(detailText, string.IsNullOrWhiteSpace(description)
                ? "아직 기록된 내용이 없습니다."
                : description);
            if (detailPanel != null) { detailPanel.SetActive(true); }
        }

        public void HideDetail()
        {
            if (detailPanel != null) { detailPanel.SetActive(false); }
        }

        private OperatorCatalogEntry GetSelectedEntry()
        {
            return _selectedOperatorIndex >= 0 && _selectedOperatorIndex < _ownedEntries.Count
                ? _ownedEntries[_selectedOperatorIndex]
                : null;
        }

        private void ReleaseDefinitionHandle()
        {
            _definition = null;
            if (_ownsDefinitionHandle && _definitionHandle.IsValid())
            {
                Addressables.Release(_definitionHandle);
            }
            _ownsDefinitionHandle = false;
        }

        private static void RenderSideSlot(Image portrait, TMP_Text label, GameObject lockSprite,
            OperatorCatalogEntry entry)
        {
            SetSprite(portrait, ResolveDimmedPortrait(entry));
            SetText(label, entry != null ? entry.displayName : string.Empty);
            if (lockSprite != null) { lockSprite.SetActive(entry == null); }
        }

        private static Sprite ResolveBrightPortrait(OperatorCatalogEntry entry)
        {
            if (entry == null) { return null; }
            if (entry.shopUpperBodyPortrait != null) { return entry.shopUpperBodyPortrait; }
            if (entry.managementPortrait != null) { return entry.managementPortrait; }
            return entry.previewPortrait;
        }

        private static Sprite ResolveDimmedPortrait(OperatorCatalogEntry entry)
        {
            if (entry == null) { return null; }
            return entry.shopUpperBodyPortraitDimmed != null
                ? entry.shopUpperBodyPortraitDimmed
                : ResolveBrightPortrait(entry);
        }

        private static void SetProfileText(TMP_Text label, string value)
        {
            if (label != null)
            {
                // CODE NAME 등의 라벨은 DossierPanel 배경 이미지에 이미 포함되어 있다.
                // TMP에는 데이터 값만 넣어 아트 라벨과 이중으로 표시되지 않게 한다.
                label.text = value ?? string.Empty;
            }
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            if (image == null) { return; }
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) { label.text = value ?? string.Empty; }
        }
    }
}
