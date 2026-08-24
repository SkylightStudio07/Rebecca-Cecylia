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
    /// 보유 오퍼레이터의 영구 강화 트랙을 표시하고 한 레벨 구매를 처리한다.
    /// 전신·하단 3칸은 Recruit와 공유하고, Enhance 탭이 활성화된 동안에만 이 컨트롤러가 갱신한다.
    /// </summary>
    public sealed class OperatorEnhanceShopUI : MonoBehaviour
    {
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

        [Header("Enhance Panel")]
        [SerializeField] private TMP_Text panelOperatorName;
        [SerializeField] private TMP_Text panelAlternateName;
        [SerializeField] private TMP_Text levelPreviewText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text ownedText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private OperatorUpgradeTrackView[] trackViews;
        [SerializeField] private Button enhanceButton;

        private readonly List<OperatorCatalogEntry> _ownedEntries = new();
        private IProfileStorage _storage;
        private PlayerProfile _profile;
        private OperatorDefinition _definition;
        private AsyncOperationHandle<OperatorDefinition> _definitionHandle;
        private bool _ownsDefinitionHandle;
        private int _selectedOperatorIndex;
        private int _selectedTrackIndex;
        private int _loadVersion;

        private void Awake()
        {
            _storage = new PlayerPrefsProfileStorage();
            if (operatorPortrait != null) { operatorPortrait.preserveAspect = true; }
        }

        private void OnEnable()
        {
            if (previousButton != null) { previousButton.onClick.AddListener(PreviousOperator); }
            if (nextButton != null) { nextButton.onClick.AddListener(NextOperator); }
            if (enhanceButton != null) { enhanceButton.onClick.AddListener(PurchaseSelectedTrack); }
            Refresh();
        }

        private void OnDisable()
        {
            if (previousButton != null) { previousButton.onClick.RemoveListener(PreviousOperator); }
            if (nextButton != null) { nextButton.onClick.RemoveListener(NextOperator); }
            if (enhanceButton != null) { enhanceButton.onClick.RemoveListener(PurchaseSelectedTrack); }
            _loadVersion++;
            ReleaseDefinitionHandle();
        }

        public void Refresh()
        {
            _storage ??= new PlayerPrefsProfileStorage();
            _profile = _storage.Load();
            RebuildOwnedEntries();
            RenderOperator();
        }

        private void PreviousOperator()
        {
            MoveOperator(-1);
        }

        private void NextOperator()
        {
            MoveOperator(1);
        }

        private void MoveOperator(int direction)
        {
            if (_ownedEntries.Count <= 1)
            {
                return;
            }

            _selectedOperatorIndex = (_selectedOperatorIndex + direction + _ownedEntries.Count) %
                _ownedEntries.Count;
            _selectedTrackIndex = 0;
            RenderOperator();
        }

        private void RebuildOwnedEntries()
        {
            string selectedId = GetSelectedEntry()?.operatorId;
            _ownedEntries.Clear();
            if (catalog != null && catalog.entries != null)
            {
                for (int i = 0; i < catalog.entries.Count; i++)
                {
                    OperatorCatalogEntry entry = catalog.entries[i];
                    if (entry != null && entry.IsUnlocked(_profile))
                    {
                        _ownedEntries.Add(entry);
                    }
                }
            }

            _selectedOperatorIndex = 0;
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                int restored = _ownedEntries.FindIndex(entry => entry.operatorId == selectedId);
                if (restored >= 0) { _selectedOperatorIndex = restored; }
            }
        }

        private void RenderOperator()
        {
            OperatorCatalogEntry entry = GetSelectedEntry();
            RemotePreviewSpriteLoader.LoadInto(
                operatorPortrait,
                entry != null ? (entry.shopPortrait != null ? entry.shopPortrait : entry.managementPortrait) : null,
                ResolveShopOrManagementPortraitAddress(entry), Color.clear);
            RemotePreviewSpriteLoader.LoadInto(
                operatorUpperBodyPortrait, ResolveBrightPortrait(entry), ResolveBrightPortraitAddress(entry),
                Color.clear);
            SetText(operatorName, entry != null ? entry.displayName : string.Empty);
            SetText(anotherNameText, entry != null ? entry.alternateName : string.Empty);
            SetText(panelOperatorName, entry != null ? entry.displayName : "NO OPERATOR");
            SetText(panelAlternateName, entry != null ? entry.alternateName : string.Empty);
            RenderSideSlots();

            bool canNavigate = _ownedEntries.Count > 1;
            if (previousButton != null) { previousButton.interactable = canNavigate; }
            if (nextButton != null) { nextButton.interactable = canNavigate; }

            _definition = null;
            RenderTracks();
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

        private static void RenderSideSlot(Image portrait, TMP_Text label, GameObject lockSprite,
            OperatorCatalogEntry entry)
        {
            RemotePreviewSpriteLoader.LoadInto(
                portrait, ResolveDimmedPortrait(entry), ResolveDimmedPortraitAddress(entry), Color.clear);
            SetText(label, entry != null ? entry.displayName : string.Empty);
            if (lockSprite != null) { lockSprite.SetActive(entry == null); }
        }

        private void BeginLoadDefinition(OperatorCatalogEntry entry)
        {
            _loadVersion++;
            int version = _loadVersion;
            ReleaseDefinitionHandle();
            if (entry == null)
            {
                SetText(statusText, "보유한 오퍼레이터가 없습니다.");
                return;
            }

            if (OperatorLoadoutSession.SelectedDefinition != null &&
                OperatorLoadoutSession.SelectedDefinition.operatorId == entry.operatorId)
            {
                _definition = OperatorLoadoutSession.SelectedDefinition;
                RenderTracks();
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.address))
            {
                SetText(statusText, "오퍼레이터 콘텐츠 주소가 비어 있습니다.");
                return;
            }

            SetText(statusText, "강화 데이터 불러오는 중…");
            _definitionHandle = Addressables.LoadAssetAsync<OperatorDefinition>(entry.address);
            _ownsDefinitionHandle = true;
            _definitionHandle.Completed += operation =>
            {
                if (version != _loadVersion || !isActiveAndEnabled)
                {
                    return;
                }

                if (operation.Status != AsyncOperationStatus.Succeeded || operation.Result == null ||
                    operation.Result.operatorId != entry.operatorId)
                {
                    SetText(statusText, "강화 데이터를 불러오지 못했습니다.");
                    return;
                }

                _definition = operation.Result;
                RenderTracks();
            };
        }

        private void RenderTracks()
        {
            OperatorUpgradeTrackSet trackSet = _definition != null ? _definition.upgradeTracks : null;
            List<OperatorUpgradeTrack> tracks = trackSet != null ? trackSet.tracks : null;
            int trackCount = tracks != null ? tracks.Count : 0;
            _selectedTrackIndex = trackCount > 0
                ? Mathf.Clamp(_selectedTrackIndex, 0, trackCount - 1)
                : 0;

            if (trackViews != null)
            {
                for (int i = 0; i < trackViews.Length; i++)
                {
                    OperatorUpgradeTrack track = i < trackCount ? tracks[i] : null;
                    int level = track != null && _profile != null
                        ? _profile.GetUpgradeLevel(GetSelectedEntry()?.operatorId, track.trackId)
                        : 0;
                    if (trackViews[i] != null)
                    {
                        trackViews[i].Bind(track, level, i, i == _selectedTrackIndex, SelectTrack);
                    }
                }
            }

            RenderSelectedTrack();
        }

        private void SelectTrack(int index)
        {
            _selectedTrackIndex = index;
            RenderTracks();
        }

        private void RenderSelectedTrack()
        {
            OperatorCatalogEntry entry = GetSelectedEntry();
            OperatorUpgradeTrack track = GetSelectedTrack();
            SetText(ownedText, _profile != null ? _profile.commodity.ToString() : "0");
            if (entry == null || track == null || _profile == null)
            {
                SetText(levelPreviewText, "LV. --");
                SetText(descriptionText, "강화 트랙 데이터가 없습니다.");
                SetText(costText, "--");
                SetText(statusText, entry == null
                    ? "보유한 오퍼레이터가 없습니다."
                    : "강화 트랙 데이터가 없습니다.");
                if (enhanceButton != null) { enhanceButton.interactable = false; }
                return;
            }

            int currentLevel = Mathf.Clamp(_profile.GetUpgradeLevel(entry.operatorId, track.trackId),
                0, track.maxLevel);
            bool maxed = currentLevel >= track.maxLevel;
            int nextLevel = Mathf.Min(track.maxLevel, currentLevel + 1);
            int cost = maxed ? 0 : track.GetCostForLevel(nextLevel);
            int requiredAffinity = maxed ? 0 : track.GetRequiredAffinityForLevel(nextLevel);
            int currentAffinity = _profile.GetOperatorAffinity(entry.operatorId);

            SetText(levelPreviewText, maxed
                ? $"LV. {currentLevel:00} / MAX"
                : $"LV. {currentLevel:00}  >  LV. {nextLevel:00}");
            SetText(descriptionText, track.description);
            SetText(costText, maxed ? "MAX" : cost.ToString());
            if (maxed)
            {
                SetText(statusText, "최대 레벨입니다.");
            }
            else if (currentAffinity < requiredAffinity)
            {
                SetText(statusText, $"친밀도 {requiredAffinity} 필요 (현재 {currentAffinity})");
            }
            else if (_profile.commodity < cost)
            {
                SetText(statusText, "재화가 부족합니다.");
            }
            else
            {
                SetText(statusText, "강화할 수 있습니다.");
            }

            if (enhanceButton != null)
            {
                enhanceButton.interactable = !maxed && currentAffinity >= requiredAffinity &&
                    _profile.commodity >= cost;
            }
        }

        private void PurchaseSelectedTrack()
        {
            OperatorCatalogEntry entry = GetSelectedEntry();
            OperatorUpgradeTrack track = GetSelectedTrack();
            if (entry == null || track == null || _profile == null)
            {
                return;
            }

            int nextLevel = _profile.GetUpgradeLevel(entry.operatorId, track.trackId) + 1;
            if (!_profile.TryPurchaseUpgradeLevel(entry.operatorId, track.trackId, track.maxLevel,
                    track.GetCostForLevel(nextLevel), track.GetRequiredAffinityForLevel(nextLevel)))
            {
                RenderTracks();
                return;
            }

            _storage.Save(_profile);
            TitleSceneController title = FindFirstObjectByType<TitleSceneController>(FindObjectsInactive.Include);
            if (title != null) { title.RefreshCommodityText(); }
            RenderTracks();
        }

        private OperatorCatalogEntry GetSelectedEntry()
        {
            return _selectedOperatorIndex >= 0 && _selectedOperatorIndex < _ownedEntries.Count
                ? _ownedEntries[_selectedOperatorIndex]
                : null;
        }

        private OperatorUpgradeTrack GetSelectedTrack()
        {
            List<OperatorUpgradeTrack> tracks = _definition != null && _definition.upgradeTracks != null
                ? _definition.upgradeTracks.tracks
                : null;
            return tracks != null && _selectedTrackIndex >= 0 && _selectedTrackIndex < tracks.Count
                ? tracks[_selectedTrackIndex]
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

        // 아래 세 메서드는 위 Resolve*Portrait와 같은 우선순위로, Sprite가 원격이라 비어
        // 있을 때 RemotePreviewSpriteLoader가 대신 받을 주소를 고른다.
        private static string ResolveShopOrManagementPortraitAddress(OperatorCatalogEntry entry)
        {
            if (entry == null) { return null; }
            return !string.IsNullOrEmpty(entry.shopPortraitAddress)
                ? entry.shopPortraitAddress
                : entry.managementPortraitAddress;
        }

        private static string ResolveBrightPortraitAddress(OperatorCatalogEntry entry)
        {
            if (entry == null) { return null; }
            if (!string.IsNullOrEmpty(entry.shopUpperBodyPortraitAddress)) { return entry.shopUpperBodyPortraitAddress; }
            if (!string.IsNullOrEmpty(entry.managementPortraitAddress)) { return entry.managementPortraitAddress; }
            return entry.previewPortraitAddress;
        }

        private static string ResolveDimmedPortraitAddress(OperatorCatalogEntry entry)
        {
            if (entry == null) { return null; }
            return !string.IsNullOrEmpty(entry.shopUpperBodyPortraitDimmedAddress)
                ? entry.shopUpperBodyPortraitDimmedAddress
                : ResolveBrightPortraitAddress(entry);
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) { label.text = value ?? string.Empty; }
        }
    }
}
