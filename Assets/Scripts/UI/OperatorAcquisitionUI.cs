using System.Collections;
using System.Collections.Generic;
using TMPro;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Operator;
using RCCom.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 로비 진입 또는 스테이지 결과 확정 뒤 새로 해금된 오퍼레이터를 한 명씩 소개한다.
    /// 해금 판정은 카탈로그와 프로필에서 파생하고, 이 컴포넌트는 연출과 표시 이력만 소유해
    /// 이후 실제 획득 수단이 추가되어도 전투 및 오퍼레이터 선택 흐름과 결합되지 않게 한다.
    /// </summary>
    public sealed class OperatorAcquisitionUI : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private OperatorCatalog catalog;
        [SerializeField] private GameObject mainMenuBackground;
        [Tooltip("로비에서는 씬 진입 후 미표시 합류 연출을 자동 검사한다. 전투 결과용 인스턴스는 결과 확정 뒤 직접 호출하므로 끈다.")]
        [SerializeField] private bool autoPresentOnStart = true;
        [SerializeField] private bool silentlyRegisterStarterOperator = true;

        [Header("Root")]
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private Button advanceButton;

        [Header("Headline")]
        [SerializeField] private RectTransform newLabel;
        [SerializeField] private RectTransform operatorLabel;
        [SerializeField] private RectTransform operatorNameLabel;
        [SerializeField] private TMP_Text operatorNameText;

        [Header("Character")]
        [SerializeField] private Image characterStanding;
        [SerializeField] private CanvasGroup characterGroup;

        [Header("Dialogue")]
        [SerializeField] private RectTransform dialoguePanel;
        [SerializeField] private CanvasGroup dialogueGroup;
        [SerializeField] private TMP_Text dialogueSpeakerText;
        [SerializeField] private TMP_Text dialogueBodyText;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float lobbyReadyDelay = 0.15f;
        [SerializeField, Min(0.01f)] private float labelSlideDuration = 0.24f;
        [SerializeField, Min(0f)] private float labelStagger = 0.08f;
        [SerializeField, Min(0.01f)] private float characterDuration = 0.38f;
        [SerializeField, Min(0.01f)] private float dialogueDuration = 0.25f;
        [SerializeField] private float labelSlideDistance = 520f;
        [SerializeField] private float characterSlideDistance = 110f;
        [SerializeField] private float dialogueRiseDistance = 70f;

        private readonly List<OperatorCatalogEntry> _pendingEntries = new();
        private readonly Vector2[] _labelPositions = new Vector2[3];
        private IProfileStorage _profileStorage;
        private PlayerProfile _profile;
        private Vector2 _characterPosition;
        private Vector2 _dialoguePosition;
        private bool _skipRequested;
        private bool _sequenceComplete;
        private bool _isOpen;
        private bool _presentationQueueActive;
        private int _pendingIndex;
        private AsyncOperationHandle<OperatorDefinition> _definitionHandle;
        private bool _ownsDefinitionHandle;

        private void Awake()
        {
            // 빌드 이후 원격으로 추가된 오퍼레이터까지 포함한 카탈로그로 바꾼다.
            // 원격 카탈로그가 아직 안 왔거나 추가분이 없으면 내장본을 그대로 돌려준다.
            catalog = LiveCatalogService.Resolve(catalog);
            ResolveReferences();
            CachePositions();
            SetRootVisible(false);

            if (advanceButton != null)
            {
                advanceButton.onClick.RemoveListener(HandleAdvance);
                advanceButton.onClick.AddListener(HandleAdvance);
            }
        }

        private void Start()
        {
            _profileStorage = new PlayerPrefsProfileStorage();
            if (autoPresentOnStart)
            {
                StartCoroutine(WaitForLobbyAndPresent());
            }
        }

        public void PresentNewlyUnlocked()
        {
            PresentNewlyUnlockedForStage(null);
        }

        /// <summary>
        /// 구매 직후에는 전체 미표시 목록을 재검색하지 않고 방금 획득한 대상만 큐에 넣는다.
        /// Addressables 로딩 중 추가 구매가 들어와도 코루틴을 중복 시작하지 않고 기존 큐 뒤에 붙인다.
        /// </summary>
        public void PresentOperator(string operatorId)
        {
            if (string.IsNullOrWhiteSpace(operatorId)) { return; }

            _profileStorage ??= new PlayerPrefsProfileStorage();
            _profile = _profileStorage.Load();
            OperatorCatalogEntry entry = FindCatalogEntry(operatorId);
            if (entry == null || !entry.IsUnlocked(_profile) ||
                _profile.HasPresentedOperatorAcquisition(operatorId))
            {
                return;
            }

            transform.SetAsLastSibling();
            if (!_presentationQueueActive)
            {
                _pendingEntries.Clear();
                _pendingIndex = 0;
            }

            bool alreadyQueued = _pendingEntries.Exists(candidate =>
                candidate != null && candidate.operatorId == operatorId);
            if (!alreadyQueued)
            {
                _pendingEntries.Add(entry);
            }

            StartPendingQueueIfNeeded();
        }

        /// <summary>
        /// 결과 화면에서 해당 스테이지 보상으로 해금된 오퍼레이터만 즉시 소개한다.
        /// null 또는 빈 값이면 Shop 구매처럼 모든 미표시 획득을 대상으로 한다.
        /// </summary>
        public void PresentNewlyUnlockedForStage(string stageId)
        {
            if (_presentationQueueActive) { return; }

            // Shop과 결과 화면처럼 다른 패널 위에서 호출돼도 연출이 가려지지 않아야 한다.
            _profileStorage ??= new PlayerPrefsProfileStorage();
            transform.SetAsLastSibling();
            BuildPendingQueue();
            if (!string.IsNullOrWhiteSpace(stageId))
            {
                _pendingEntries.RemoveAll(entry =>
                    entry == null ||
                    !entry.IsStageRewardFor(stageId));
            }

            StartPendingQueueIfNeeded();
        }

        /// <summary>
        /// 에디터의 합류 연출 검증에서 현재 진행 중이던 코루틴과 표시 상태를 끊고
        /// 프로필 기준 큐를 새로 만든다. 로비 진입 시 한 번만 도는 초기 검사와 분리한다.
        /// </summary>
        public void ReplayNewlyUnlockedForDebug()
        {
            CancelPresentationForDebug();
            _profileStorage ??= new PlayerPrefsProfileStorage();

            BuildPendingQueue();
            Debug.Log($"[OperatorAcquisition] 합류 연출 재검사: {_pendingEntries.Count}명", this);
            StartPendingQueueIfNeeded();
        }

        /// <summary>
        /// 에디터에서 표시 이력만 검사할 때 실행 중인 연출이 그 값을 다시 저장하지 않도록
        /// 코루틴과 캐시를 함께 비운다. 프로필 데이터 자체는 호출자가 결정한다.
        /// </summary>
        public void CancelPresentationForDebug()
        {
            StopAllCoroutines();
            ReleaseDefinitionHandle();
            _pendingEntries.Clear();
            _pendingIndex = 0;
            _isOpen = false;
            _presentationQueueActive = false;
            _sequenceComplete = false;
            _skipRequested = false;
            SetRootVisible(false);
            SetMainMenuInput(true);
        }

        private void OnDestroy()
        {
            ReleaseDefinitionHandle();
        }

        private IEnumerator WaitForLobbyAndPresent()
        {
            while (mainMenuBackground != null && !mainMenuBackground.activeInHierarchy)
            {
                yield return null;
            }

            float elapsed = 0f;
            while (elapsed < lobbyReadyDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            BuildPendingQueue();
            if (_pendingEntries.Count > 0)
            {
                _presentationQueueActive = true;
                yield return PresentNext();
            }
        }

        private void BuildPendingQueue()
        {
            catalog = LiveCatalogService.Resolve(catalog);
            _pendingEntries.Clear();
            _pendingIndex = 0;
            _profile = _profileStorage.Load();
            if (catalog == null || catalog.entries == null)
            {
                Debug.LogWarning("[OperatorAcquisition] OperatorCatalog가 연결되지 않았습니다.", this);
                return;
            }

            bool profileChanged = false;
            for (int i = 0; i < catalog.entries.Count; i++)
            {
                OperatorCatalogEntry entry = catalog.entries[i];
                if (entry == null || !entry.IsUnlocked(_profile) ||
                    _profile.HasPresentedOperatorAcquisition(entry.operatorId))
                {
                    continue;
                }

                // 첫 슬롯은 게임 시작부터 함께하는 기본 오퍼레이터다. 기존 사용자에게도
                // 신규 획득처럼 다시 소개하지 않고 이후 실제 해금 슬롯만 연출한다.
                if (i == 0 && silentlyRegisterStarterOperator)
                {
                    profileChanged |= _profile.MarkOperatorAcquisitionPresented(entry.operatorId);
                    continue;
                }

                _pendingEntries.Add(entry);
            }

            if (profileChanged)
            {
                _profileStorage.Save(_profile);
            }
        }

        private IEnumerator PresentNext()
        {
            if (_pendingIndex >= _pendingEntries.Count)
            {
                CloseOverlay();
                yield break;
            }

            OperatorCatalogEntry entry = _pendingEntries[_pendingIndex];
            OperatorDefinition definition = null;
            yield return LoadDefinition(entry, loaded => definition = loaded);
            if (definition == null)
            {
                Debug.LogWarning($"[OperatorAcquisition] {entry.operatorId} 콘텐츠를 불러오지 못해 이번 연출을 건너뜁니다.", this);
                _pendingIndex++;
                yield return PresentNext();
                yield break;
            }

            PrepareContent(entry, definition);
            _skipRequested = false;
            _sequenceComplete = false;
            _isOpen = true;
            SetRootVisible(true);
            SetMainMenuInput(false);
            yield return PlaySequence();
            _sequenceComplete = true;
        }

        private IEnumerator LoadDefinition(OperatorCatalogEntry entry,
            System.Action<OperatorDefinition> onLoaded)
        {
            ReleaseDefinitionHandle();
            if (OperatorLoadoutSession.SelectedDefinition != null &&
                OperatorLoadoutSession.SelectedDefinition.operatorId == entry.operatorId)
            {
                onLoaded(OperatorLoadoutSession.SelectedDefinition);
                yield break;
            }

            _definitionHandle = Addressables.LoadAssetAsync<OperatorDefinition>(entry.address);
            _ownsDefinitionHandle = true;
            yield return _definitionHandle;
            onLoaded(_definitionHandle.Status == AsyncOperationStatus.Succeeded
                ? _definitionHandle.Result
                : null);
        }

        private void PrepareContent(OperatorCatalogEntry entry, OperatorDefinition definition)
        {
            string displayName = !string.IsNullOrWhiteSpace(definition.displayName)
                ? definition.displayName
                : entry.displayName;
            if (operatorNameText != null) { operatorNameText.text = displayName; }
            if (dialogueSpeakerText != null) { dialogueSpeakerText.text = displayName; }

            string dialogue = "새로운 오퍼레이터가 합류했습니다.";
            Sprite standing = null;
            OperatorLineSet acquisition = definition.dialogueSet != null
                ? definition.dialogueSet.operatorAcquired
                : null;
            if (acquisition != null && acquisition.TryGetRandomLobby(out string selectedLine,
                    out Sprite selectedStanding))
            {
                dialogue = selectedLine;
                standing = selectedStanding;
            }

            if (standing == null && acquisition != null) { standing = acquisition.defaultLobbySprite; }
            if (standing == null && definition.dialogueSet != null) { standing = definition.dialogueSet.lobbyIdleSprite; }
            if (standing == null) { standing = definition.managementPortrait; }
            if (standing == null) { standing = entry.managementPortrait; }

            if (characterStanding != null)
            {
                characterStanding.sprite = standing;
                characterStanding.enabled = standing != null;
                characterStanding.preserveAspect = true;
            }

            if (dialogueBodyText != null) { dialogueBodyText.text = dialogue; }
            ResetAnimatedElements();
        }

        private IEnumerator PlaySequence()
        {
            RectTransform[] labels = { newLabel, operatorLabel, operatorNameLabel };
            for (int i = 0; i < labels.Length; i++)
            {
                yield return SlideLabel(labels[i], _labelPositions[i]);
                if (labelStagger > 0f && !_skipRequested)
                {
                    yield return WaitUnscaled(labelStagger);
                }
            }

            yield return RevealCharacter();
            yield return RevealDialogue();
            ApplyFinalState();
        }

        private IEnumerator SlideLabel(RectTransform target, Vector2 destination)
        {
            if (target == null) { yield break; }
            Vector2 start = destination + Vector2.right * labelSlideDistance;
            target.anchoredPosition = start;
            CanvasGroup group = GetOrAddCanvasGroup(target.gameObject);
            group.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < labelSlideDuration && !_skipRequested)
            {
                float t = EaseOutCubic(Mathf.Clamp01(elapsed / labelSlideDuration));
                target.anchoredPosition = Vector2.LerpUnclamped(start, destination, t);
                group.alpha = t;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            target.anchoredPosition = destination;
            group.alpha = 1f;
        }

        private IEnumerator RevealCharacter()
        {
            if (characterStanding == null || characterGroup == null) { yield break; }
            RectTransform rect = characterStanding.rectTransform;
            Vector2 start = _characterPosition + Vector2.right * characterSlideDistance;
            rect.anchoredPosition = start;
            characterGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < characterDuration && !_skipRequested)
            {
                float t = EaseOutCubic(Mathf.Clamp01(elapsed / characterDuration));
                rect.anchoredPosition = Vector2.LerpUnclamped(start, _characterPosition, t);
                characterGroup.alpha = t;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            rect.anchoredPosition = _characterPosition;
            characterGroup.alpha = 1f;
        }

        private IEnumerator RevealDialogue()
        {
            if (dialoguePanel == null || dialogueGroup == null) { yield break; }
            Vector2 start = _dialoguePosition - Vector2.up * dialogueRiseDistance;
            dialoguePanel.anchoredPosition = start;
            dialogueGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < dialogueDuration && !_skipRequested)
            {
                float t = EaseOutCubic(Mathf.Clamp01(elapsed / dialogueDuration));
                dialoguePanel.anchoredPosition = Vector2.LerpUnclamped(start, _dialoguePosition, t);
                dialogueGroup.alpha = t;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            dialoguePanel.anchoredPosition = _dialoguePosition;
            dialogueGroup.alpha = 1f;
        }

        private void HandleAdvance()
        {
            if (!_isOpen) { return; }
            if (!_sequenceComplete)
            {
                _skipRequested = true;
                ApplyFinalState();
                return;
            }

            OperatorCatalogEntry completed = _pendingEntries[_pendingIndex];
            if (_profile.MarkOperatorAcquisitionPresented(completed.operatorId))
            {
                _profileStorage.Save(_profile);
            }

            _pendingIndex++;
            _isOpen = false;
            SetRootVisible(false);
            ReleaseDefinitionHandle();
            if (_pendingIndex < _pendingEntries.Count)
            {
                StartCoroutine(PresentNext());
            }
            else
            {
                CloseOverlay();
            }
        }

        private void ResetAnimatedElements()
        {
            RectTransform[] labels = { newLabel, operatorLabel, operatorNameLabel };
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) { continue; }
                labels[i].anchoredPosition = _labelPositions[i] + Vector2.right * labelSlideDistance;
                GetOrAddCanvasGroup(labels[i].gameObject).alpha = 0f;
            }

            if (characterStanding != null)
            {
                characterStanding.rectTransform.anchoredPosition =
                    _characterPosition + Vector2.right * characterSlideDistance;
            }
            if (characterGroup != null) { characterGroup.alpha = 0f; }
            if (dialoguePanel != null) { dialoguePanel.anchoredPosition = _dialoguePosition - Vector2.up * dialogueRiseDistance; }
            if (dialogueGroup != null) { dialogueGroup.alpha = 0f; }
        }

        private void ApplyFinalState()
        {
            RectTransform[] labels = { newLabel, operatorLabel, operatorNameLabel };
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) { continue; }
                labels[i].anchoredPosition = _labelPositions[i];
                GetOrAddCanvasGroup(labels[i].gameObject).alpha = 1f;
            }

            if (characterStanding != null) { characterStanding.rectTransform.anchoredPosition = _characterPosition; }
            if (characterGroup != null) { characterGroup.alpha = 1f; }
            if (dialoguePanel != null) { dialoguePanel.anchoredPosition = _dialoguePosition; }
            if (dialogueGroup != null) { dialogueGroup.alpha = 1f; }
        }

        private IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && !_skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void CloseOverlay()
        {
            _isOpen = false;
            _presentationQueueActive = false;
            SetRootVisible(false);
            SetMainMenuInput(true);
            ReleaseDefinitionHandle();
            _pendingEntries.Clear();
            _pendingIndex = 0;
        }

        private void StartPendingQueueIfNeeded()
        {
            if (_presentationQueueActive || _pendingIndex >= _pendingEntries.Count)
            {
                return;
            }

            _presentationQueueActive = true;
            StartCoroutine(PresentNext());
        }

        private OperatorCatalogEntry FindCatalogEntry(string operatorId)
        {
            if (catalog == null || catalog.entries == null)
            {
                return null;
            }

            return catalog.entries.Find(entry => entry != null && entry.operatorId == operatorId);
        }

        private void ResolveReferences()
        {
            if (rootGroup == null) { rootGroup = GetOrAddCanvasGroup(gameObject); }
            if (advanceButton == null) { advanceButton = GetComponent<Button>(); }
            if (newLabel == null) { newLabel = transform.Find("NEW") as RectTransform; }
            if (operatorLabel == null) { operatorLabel = transform.Find("OPERATOR") as RectTransform; }
            if (operatorNameLabel == null) { operatorNameLabel = transform.Find("OperatorNameText") as RectTransform; }
            if (operatorNameText == null && operatorNameLabel != null) { operatorNameText = operatorNameLabel.GetComponent<TMP_Text>(); }
            if (characterStanding == null) { characterStanding = transform.Find("CharacterStanding")?.GetComponent<Image>(); }
            if (characterStanding != null && characterGroup == null) { characterGroup = GetOrAddCanvasGroup(characterStanding.gameObject); }
            if (dialoguePanel == null) { dialoguePanel = transform.Find("AcquisitionDialogue") as RectTransform; }
            if (dialoguePanel != null && dialogueGroup == null) { dialogueGroup = GetOrAddCanvasGroup(dialoguePanel.gameObject); }
            if (dialoguePanel != null)
            {
                if (dialogueSpeakerText == null) { dialogueSpeakerText = dialoguePanel.Find("SpeakerText")?.GetComponent<TMP_Text>(); }
                if (dialogueBodyText == null) { dialogueBodyText = dialoguePanel.Find("DialogueText")?.GetComponent<TMP_Text>(); }
            }
            if (mainMenuBackground == null && transform.parent != null)
            {
                mainMenuBackground = transform.parent.Find("MainMenuBackground")?.gameObject;
            }
        }

        private void CachePositions()
        {
            _labelPositions[0] = newLabel != null ? newLabel.anchoredPosition : Vector2.zero;
            _labelPositions[1] = operatorLabel != null ? operatorLabel.anchoredPosition : Vector2.zero;
            _labelPositions[2] = operatorNameLabel != null ? operatorNameLabel.anchoredPosition : Vector2.zero;
            _characterPosition = characterStanding != null
                ? characterStanding.rectTransform.anchoredPosition
                : Vector2.zero;
            _dialoguePosition = dialoguePanel != null ? dialoguePanel.anchoredPosition : Vector2.zero;
        }

        private void SetRootVisible(bool visible)
        {
            if (rootGroup == null) { return; }
            rootGroup.alpha = visible ? 1f : 0f;
            rootGroup.interactable = visible;
            rootGroup.blocksRaycasts = visible;
        }

        private void SetMainMenuInput(bool enabled)
        {
            if (mainMenuBackground == null) { return; }
            CanvasGroup group = mainMenuBackground.GetComponent<CanvasGroup>();
            if (group == null) { return; }
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }

        private void ReleaseDefinitionHandle()
        {
            if (_ownsDefinitionHandle && _definitionHandle.IsValid())
            {
                Addressables.Release(_definitionHandle);
            }

            _ownsDefinitionHandle = false;
            _definitionHandle = default;
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - value;
            return 1f - inverse * inverse * inverse;
        }
    }
}
