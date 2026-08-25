using System;
using System.Collections.Generic;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Stage;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace RCCom.Runtime
{
    /// <summary>
    /// 플레이어 빌드 이후에 추가된 콘텐츠를 "발견"하게 해 주는 층.
    ///
    /// 배경: 오퍼레이터·스테이지 목록의 정본인 카탈로그 SO는 씬 UI가 [SerializeField]로 직접
    /// 참조해서 플레이어 데이터에 통째로 직렬화된다. Definition과 아트는 원격 그룹에 있어
    /// CDN에서 받을 수 있지만, "그런 항목이 존재한다"는 사실 자체가 빌드에 박혀 있어서 원격
    /// 번들을 아무리 잘 올려도 구 플레이어의 화면에는 끝내 나타나지 않았다. 즉 라이브 드랍의
    /// 마지막 한 칸이 비어 있었다.
    ///
    /// 이 서비스는 원격에서 "신규 항목만 담긴" 경량 카탈로그를 받아 빌드 내장 카탈로그 위에
    /// 얹는다. 병합은 **추가 전용**이다 — 이미 빌드에 있는 항목은 내장본을 그대로 쓴다.
    /// 기존 항목까지 원격본으로 갈아치우면 그 항목이 참조하는 로컬 에셋을 원격 번들이 끌어안게
    /// 되어 번들이 비대해지고, static으로 묶어둔 로컬 그룹과 교차 의존이 생긴다. 신규 항목은
    /// 정의상 원격 콘텐츠라 로컬 에셋을 직접 참조하지 않고 주소 문자열만 들고 있어 그런 의존이
    /// 애초에 생기지 않는다(빌더가 그 불변식을 빌드 시점에 검사한다).
    ///
    /// 세션 내내 유지되는 static 캐시지만 씬 재로드 시 초기화할 필요가 없다. 담고 있는 것이
    /// 세션 상태가 아니라 계정 단위 콘텐츠 목록이라, 전투 씬을 오갈 때마다 다시 받는 쪽이
    /// 오히려 잘못된 동작이다.
    /// </summary>
    public static class LiveCatalogService
    {
        /// <summary>원격 조회가 끝났는지. 실패해도(오프라인 등) 끝나면 true가 된다.</summary>
        public static bool IsResolved { get; private set; }

        /// <summary>원격 조회 완료 시 한 번 발생한다. 이미 완료된 뒤 구독하면 즉시 호출된다.</summary>
        public static event Action Resolved
        {
            add
            {
                if (IsResolved)
                {
                    value?.Invoke();
                    return;
                }

                _resolved += value;
            }
            remove => _resolved -= value;
        }

        private static Action _resolved;
        private static bool _started;

        private static OperatorCatalog _liveOperators;
        private static StageCatalog _liveStages;

        // 내장 카탈로그 인스턴스별로 병합 결과를 한 번만 만들어 재사용한다.
        private static readonly Dictionary<OperatorCatalog, OperatorCatalog> _mergedOperators = new();
        private static readonly Dictionary<StageCatalog, StageCatalog> _mergedStages = new();

        // Resolve가 자기 결과물을 다시 입력으로 받아도 두 번 병합하지 않게 한다.
        // (화면이 Awake와 Open에서 각각 Resolve를 부르는 구조라 실제로 일어난다.)
        private static readonly HashSet<ScriptableObject> _mergedResults = new();

        /// <summary>
        /// 씬 로드 전에 원격 조회를 시작한다. 코루틴 호스트가 필요 없도록 완료 콜백만 사용한다 —
        /// 이 서비스는 화면에 딸린 것이 아니라 앱 수명 전체를 사는 것이라, 어느 씬의 어떤
        /// MonoBehaviour에도 매달지 않는 편이 옳다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Begin()
        {
            if (_started)
            {
                return;
            }

            _started = true;

            // 자동 해제를 허용하면 콜백 시점에 핸들이 이미 무효화되어 Status 조회가 예외를 낸다.
            // (OperatorContentLoader가 같은 이유로 false를 넘긴다.)
            Addressables.InitializeAsync(false).Completed += initialization =>
            {
                bool initialized = initialization.Status == AsyncOperationStatus.Succeeded;
                Addressables.Release(initialization);
                if (!initialized)
                {
                    // 오프라인이거나 원격 카탈로그를 못 받은 상태. 빌드 내장 콘텐츠만으로
                    // 계속 굴러가야 하므로 실패를 조용히 흡수하고 완료로 처리한다.
                    Finish();
                    return;
                }

                LoadIfPublished<OperatorCatalog>(
                    OperatorCatalog.LiveCatalogAddress,
                    catalog => _liveOperators = catalog,
                    // 스테이지 카탈로그를 이어서 받는다. 둘 다 같은 원격 번들에 있어 두 번째
                    // 조회는 이미 받아둔 번들에서 해결되므로 순차 처리 비용이 사실상 없다.
                    () => LoadIfPublished<StageCatalog>(
                        StageCatalog.LiveCatalogAddress,
                        catalog => _liveStages = catalog,
                        Finish));
            };
        }

        /// <summary>
        /// 내장 카탈로그에 원격 신규 항목을 얹은 결과를 돌려준다. 얹을 것이 없거나 아직 조회가
        /// 끝나지 않았으면 내장본을 그대로 돌려주므로, 호출부는 결과가 null인지만 신경 쓰면 된다.
        /// </summary>
        public static OperatorCatalog Resolve(OperatorCatalog builtIn)
        {
            if (builtIn == null || _mergedResults.Contains(builtIn))
            {
                return builtIn;
            }

            if (_liveOperators == null || _liveOperators.entries == null || _liveOperators.entries.Count == 0)
            {
                return builtIn;
            }

            if (_mergedOperators.TryGetValue(builtIn, out OperatorCatalog cached) && cached != null)
            {
                return cached;
            }

            List<OperatorCatalogEntry> entries = MergeEntries(
                builtIn.entries, _liveOperators.entries, entry => entry.operatorId, out int added);
            if (added == 0)
            {
                _mergedOperators[builtIn] = builtIn;
                return builtIn;
            }

            OperatorCatalog merged = CreateRuntimeCopy<OperatorCatalog>(builtIn.name);
            merged.entries = entries;
            _mergedOperators[builtIn] = merged;
            _mergedResults.Add(merged);
            Debug.Log($"[LiveCatalog] 원격 오퍼레이터 {added}명을 카탈로그에 추가했습니다.");
            return merged;
        }

        /// <inheritdoc cref="Resolve(OperatorCatalog)"/>
        public static StageCatalog Resolve(StageCatalog builtIn)
        {
            if (builtIn == null || _mergedResults.Contains(builtIn))
            {
                return builtIn;
            }

            if (_liveStages == null || _liveStages.entries == null || _liveStages.entries.Count == 0)
            {
                return builtIn;
            }

            if (_mergedStages.TryGetValue(builtIn, out StageCatalog cached) && cached != null)
            {
                return cached;
            }

            List<StageCatalogEntry> entries = MergeEntries(
                builtIn.entries, _liveStages.entries, entry => entry.stageId, out int added);
            if (added == 0)
            {
                _mergedStages[builtIn] = builtIn;
                return builtIn;
            }

            StageCatalog merged = CreateRuntimeCopy<StageCatalog>(builtIn.name);
            merged.entries = entries;
            _mergedStages[builtIn] = merged;
            _mergedResults.Add(merged);
            Debug.Log($"[LiveCatalog] 원격 스테이지 {added}개를 카탈로그에 추가했습니다.");
            return merged;
        }

        /// <summary>
        /// 내장 목록 뒤에 "아직 모르는 ID"만 덧붙인다. 순서를 뒤섞지 않는 이유는 기존 화면의
        /// 인덱스 기반 선택 상태(저장된 선택 인덱스, 스크롤 위치)가 그대로 유지되어야 하기 때문이다.
        /// </summary>
        private static List<TEntry> MergeEntries<TEntry>(
            List<TEntry> builtIn, List<TEntry> live, Func<TEntry, string> getId, out int added)
            where TEntry : class
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<TEntry>();
            if (builtIn != null)
            {
                foreach (TEntry entry in builtIn)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    entries.Add(entry);
                    known.Add(getId(entry));
                }
            }

            added = 0;
            foreach (TEntry entry in live)
            {
                if (entry == null)
                {
                    continue;
                }

                string id = getId(entry);
                if (string.IsNullOrWhiteSpace(id) || !known.Add(id))
                {
                    continue;
                }

                entries.Add(entry);
                added++;
            }

            return entries;
        }

        /// <summary>
        /// SO 원본을 고치지 않기 위한 세션 전용 복제본(TowerDefinition의 CreateRuntimeInstance와
        /// 같은 이유). HideAndDontSave가 아니면 에디터 Play 모드에서 이 임시 에셋이 저장 대상으로 잡힌다.
        /// </summary>
        private static T CreateRuntimeCopy<T>(string sourceName) where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            instance.name = sourceName + " (Live)";
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
        }

        /// <summary>
        /// 주소를 바로 LoadAssetAsync에 넘기면, 라이브 카탈로그를 아직 한 번도 배포하지 않은
        /// 빌드에서 InvalidKeyException이 콘솔 오류로 남는다. 정상 상태를 오류로 보이게 하지
        /// 않으려고 로케이션 조회로 존재 여부를 먼저 확인한다.
        /// </summary>
        private static void LoadIfPublished<T>(string address, Action<T> onLoaded, Action onFinished)
            where T : ScriptableObject
        {
            AsyncOperationHandle<IList<IResourceLocation>> locations =
                Addressables.LoadResourceLocationsAsync(address, typeof(T));
            locations.Completed += located =>
            {
                bool exists = located.Status == AsyncOperationStatus.Succeeded &&
                              located.Result != null && located.Result.Count > 0;
                Addressables.Release(located);
                if (!exists)
                {
                    onFinished();
                    return;
                }

                Addressables.LoadAssetAsync<T>(address).Completed += loaded =>
                {
                    if (loaded.Status == AsyncOperationStatus.Succeeded)
                    {
                        // 핸들은 의도적으로 Release하지 않는다. 이 카탈로그는 앱이 사는 동안
                        // 계속 참조되므로 해제하면 곧바로 다시 받아야 한다.
                        onLoaded(loaded.Result);
                    }
                    else
                    {
                        Debug.LogWarning($"[LiveCatalog] 원격 카탈로그를 받지 못했습니다: {address}. 내장 카탈로그로 진행합니다.");
                    }

                    onFinished();
                };
            };
        }

        private static void Finish()
        {
            IsResolved = true;
            Action callbacks = _resolved;
            _resolved = null;
            callbacks?.Invoke();
        }
    }
}
