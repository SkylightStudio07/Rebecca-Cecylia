using System.Collections;
using System.Collections.Generic;
using RCCom.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.UI;

namespace RCCom.UI
{
    /// <summary>
    /// 타이틀 메인 메뉴의 "LiveContent" 버튼.
    ///
    /// 이 버튼이 원격 서버 접속의 유일한 시작점이다. 부팅 시에는 Addressables의 로컬
    /// 카탈로그만 사용하고, 클릭 뒤 카탈로그 갱신과 신규 번들 다운로드가 모두 끝난 다음에만
    /// <see cref="LiveCatalogService"/>가 원격 항목을 선택 화면에 노출한다.
    /// </summary>
    public sealed class LiveContentButton : MonoBehaviour
    {
        private const string LoadingMessage = "온라인 서비스에서 신규 콘텐츠를 받아오고 있습니다.";

        [SerializeField] private Button button;
        [SerializeField] private LiveContentLoadingPanel loadingPanel;

        [Header("Timing")]
        [Tooltip("이미 조회가 끝나 있어도 패널이 최소 이만큼은 보이도록 한다. 없으면 캐시된 상태에서 깜빡이듯 사라진다.")]
        [SerializeField, Min(0f)] private float minimumVisibleDuration = 0.6f;
        [Tooltip("오프라인 등으로 원격 조회가 끝나지 않을 때, 로컬 콘텐츠만으로 진행 허용하기까지 기다리는 최대 시간")]
        [SerializeField, Min(1f)] private float maxWaitSeconds = 8f;

        private bool _isRunning;

        private void Awake()
        {
            if (button == null) { button = GetComponent<Button>(); }
            if (button != null) { button.onClick.AddListener(OnClick); }
        }

        private void OnDestroy()
        {
            if (button != null) { button.onClick.RemoveListener(OnClick); }
        }

        public void OnClick()
        {
            if (_isRunning)
            {
                return;
            }

            if (loadingPanel == null)
            {
                Debug.LogError("[LiveContentButton] 로딩 패널이 연결되지 않았습니다.", this);
                return;
            }

            StartCoroutine(RunLiveContentCheck());
        }

        private IEnumerator RunLiveContentCheck()
        {
            _isRunning = true;
            SetButtonInteractable(false);

            yield return loadingPanel.FadeIn(LoadingMessage);

            float coveredAt = Time.unscaledTime;
            LiveCatalogService.RequestRefresh();

            bool resolved = LiveCatalogService.IsResolved;
            while (!resolved && Time.unscaledTime - coveredAt < maxWaitSeconds)
            {
                yield return null;
                resolved = LiveCatalogService.IsResolved;
            }

            if (resolved && LiveCatalogService.IsActivated)
            {
                yield return DownloadDiscoveredContent();
            }
            else if (!resolved)
            {
                Debug.LogWarning("[LiveContentButton] 원격 콘텐츠 조회가 지연되어 로컬 콘텐츠로 계속 진행합니다.", this);
                loadingPanel.SetMessage("온라인 서비스 응답이 지연되고 있습니다. 로컬 콘텐츠로 계속합니다.");
            }
            else
            {
                loadingPanel.SetMessage(LiveCatalogService.FailureMessage);
            }

            float remainingHold = minimumVisibleDuration - (Time.unscaledTime - coveredAt);
            if (remainingHold > 0f)
            {
                yield return new WaitForSecondsRealtime(remainingHold);
            }

            yield return loadingPanel.FadeOut();

            SetButtonInteractable(!LiveCatalogService.IsActivated);
            _isRunning = false;
        }

        private IEnumerator DownloadDiscoveredContent()
        {
            List<string> addresses = LiveCatalogService.GetDownloadAddresses();
            var existingAddresses = new List<object>();
            for (int i = 0; i < addresses.Count; i++)
            {
                yield return CollectExistingAddress(addresses[i], existingAddresses);
            }

            if (existingAddresses.Count == 0)
            {
                loadingPanel.SetMessage("새로 적용할 온라인 콘텐츠가 없습니다.");
                yield break;
            }

            AsyncOperationHandle downloadHandle = Addressables.DownloadDependenciesAsync(
                existingAddresses, Addressables.MergeMode.Union, false);
            long lastBytes = 0L;
            float lastSampleTime = Time.unscaledTime;
            while (!downloadHandle.IsDone)
            {
                DownloadStatus status = downloadHandle.GetDownloadStatus();
                float elapsed = Time.unscaledTime - lastSampleTime;
                if (elapsed > 0f && status.TotalBytes > 0)
                {
                    float bytesPerSecond = (status.DownloadedBytes - lastBytes) / elapsed;
                    loadingPanel.SetProgressText(FormatProgress(
                        status.DownloadedBytes, status.TotalBytes, bytesPerSecond));
                }

                lastBytes = status.DownloadedBytes;
                lastSampleTime = Time.unscaledTime;
                yield return null;
            }

            bool succeeded = downloadHandle.Status == AsyncOperationStatus.Succeeded;
            Addressables.Release(downloadHandle);
            loadingPanel.SetMessage(succeeded
                ? "신규 콘텐츠를 적용했습니다."
                : "일부 콘텐츠를 받지 못했습니다. 선택 시 다시 시도합니다.");
        }

        /// <summary>
        /// 주소를 바로 다운로드에 넘기면, 라이브 카탈로그를 아직 배포하지 않은 빌드에서
        /// InvalidKeyException이 뜬다(LiveCatalogService.LoadIfPublished와 같은 이유).
        /// 로케이션 조회로 존재를 먼저 확인해 그 경우를 조용히 걸러낸다.
        /// </summary>
        private static IEnumerator CollectExistingAddress(string address, List<object> destination)
        {
            AsyncOperationHandle<IList<IResourceLocation>> locations =
                Addressables.LoadResourceLocationsAsync(address);
            yield return locations;

            if (locations.Status == AsyncOperationStatus.Succeeded &&
                locations.Result != null && locations.Result.Count > 0)
            {
                destination.Add(address);
            }

            Addressables.Release(locations);
        }

        private static string FormatProgress(long downloadedBytes, long totalBytes, float bytesPerSecond)
        {
            // 구분자는 ASCII만 쓴다 — "·" 같은 특수문자는 주 폰트(Pretendard) 정적 아틀라스에
            // 없어 폴백 폰트로 새고, 에디터에서 그 폴백 에셋에 동적 글리프가 구워져 계속
            // 커밋 diff가 생긴다(실제로 한 번 겪었다).
            return $"{FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes)} | " +
                   $"{FormatBytes((long)Mathf.Max(0f, bytesPerSecond))}/s";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L)
            {
                return $"{bytes / (1024f * 1024f):0.0}MB";
            }

            if (bytes >= 1024L)
            {
                return $"{bytes / 1024f:0.0}KB";
            }

            return $"{bytes}B";
        }

        private void SetButtonInteractable(bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }
}
