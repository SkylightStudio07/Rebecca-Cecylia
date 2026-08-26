using System;
using System.Collections.Generic;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Stage;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace RCCom.Runtime
{
    /// <summary>
    /// LiveContent 버튼을 누른 뒤에만 원격 카탈로그를 갱신하고 로컬 목록에 추가한다.
    ///
    /// Addressables 초기화와 원격 카탈로그 갱신은 서로 다른 일이다. 빌드 설정에서 시작 시
    /// 자동 갱신을 끄고, 이 서비스가 명시적으로 CheckForCatalogUpdates/UpdateCatalogs를
    /// 호출해야만 서버를 확인한다. 따라서 타이틀 초기 화면과 오프라인 플레이는 항상 로컬
    /// 카탈로그만 보며, 버튼 요청이 성공한 세션에서만 원격 항목이 활성화된다.
    /// </summary>
    public static class LiveCatalogService
    {
        public static bool IsRunning { get; private set; }
        public static bool IsResolved { get; private set; }
        public static bool IsActivated { get; private set; }
        public static string FailureMessage { get; private set; } = string.Empty;

        /// <summary>현재 요청이 끝날 때 발생한다. 이미 끝난 뒤 구독하면 즉시 호출된다.</summary>
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
        private static OperatorCatalog _liveOperators;
        private static StageCatalog _liveStages;

        // 씬의 직렬화 카탈로그가 실수로 원격 항목을 포함한 구버전 에셋이어도 버튼 전에는
        // 노출하지 않는다. 빌더도 같은 경계를 강제하지만 런타임 방어선을 별도로 둔다.
        private static readonly Dictionary<OperatorCatalog, OperatorCatalog> LocalOperators = new();
        private static readonly Dictionary<StageCatalog, StageCatalog> LocalStages = new();
        private static readonly Dictionary<OperatorCatalog, OperatorCatalog> MergedOperators = new();
        private static readonly Dictionary<StageCatalog, StageCatalog> MergedStages = new();
        private static readonly HashSet<ScriptableObject> LocalResults = new();
        private static readonly HashSet<ScriptableObject> MergedResults = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewApplicationRun()
        {
            IsRunning = false;
            IsResolved = false;
            IsActivated = false;
            FailureMessage = string.Empty;
            _resolved = null;
            _liveOperators = null;
            _liveStages = null;
            LocalOperators.Clear();
            LocalStages.Clear();
            MergedOperators.Clear();
            MergedStages.Clear();
            LocalResults.Clear();
            MergedResults.Clear();
        }

        /// <summary>
        /// 사용자 행동으로 원격 갱신을 시작한다. 성공 뒤 재호출은 이미 적용된 결과를 유지하고,
        /// 실패 뒤 재호출은 네트워크 복구를 위해 다시 시도한다.
        /// </summary>
        public static void RequestRefresh()
        {
            if (IsRunning || IsActivated)
            {
                return;
            }

            IsRunning = true;
            IsResolved = false;
            FailureMessage = string.Empty;

            Addressables.InitializeAsync(false).Completed += initialization =>
            {
                bool initialized = initialization.Status == AsyncOperationStatus.Succeeded;
                Addressables.Release(initialization);
                if (!initialized)
                {
                    Finish(false, "온라인 콘텐츠 시스템을 초기화하지 못했습니다.");
                    return;
                }

                CheckForCatalogUpdates();
            };
        }

        public static OperatorCatalog Resolve(OperatorCatalog builtIn)
        {
            OperatorCatalog local = ResolveLocalOnly(builtIn);
            if (local == null || MergedResults.Contains(local) || !IsActivated ||
                _liveOperators == null || _liveOperators.entries == null ||
                _liveOperators.entries.Count == 0)
            {
                return local;
            }

            if (MergedOperators.TryGetValue(local, out OperatorCatalog cached) && cached != null)
            {
                return cached;
            }

            List<OperatorCatalogEntry> entries = MergeEntries(
                local.entries, _liveOperators.entries, entry => entry.operatorId, out int added);
            if (added == 0)
            {
                MergedOperators[local] = local;
                return local;
            }

            OperatorCatalog merged = CreateRuntimeCopy<OperatorCatalog>(local.name, "Live");
            merged.entries = entries;
            MergedOperators[local] = merged;
            MergedResults.Add(merged);
            Debug.Log($"[LiveCatalog] 원격 오퍼레이터 {added}명을 활성화했습니다.");
            return merged;
        }

        public static StageCatalog Resolve(StageCatalog builtIn)
        {
            StageCatalog local = ResolveLocalOnly(builtIn);
            if (local == null || MergedResults.Contains(local) || !IsActivated ||
                _liveStages == null || _liveStages.entries == null || _liveStages.entries.Count == 0)
            {
                return local;
            }

            if (MergedStages.TryGetValue(local, out StageCatalog cached) && cached != null)
            {
                return cached;
            }

            List<StageCatalogEntry> entries = MergeEntries(
                local.entries, _liveStages.entries, entry => entry.stageId, out int added);
            if (added == 0)
            {
                MergedStages[local] = local;
                return local;
            }

            StageCatalog merged = CreateRuntimeCopy<StageCatalog>(local.name, "Live");
            merged.entries = entries;
            MergedStages[local] = merged;
            MergedResults.Add(merged);
            Debug.Log($"[LiveCatalog] 원격 스테이지 {added}개를 활성화했습니다.");
            return merged;
        }

        /// <summary>
        /// 버튼이 카탈로그 갱신 뒤 실제 원격 번들까지 한 번에 받을 수 있도록 새 항목의 주소를
        /// 돌려준다. 주소 문자열만 모으며 존재 여부는 호출자가 Addressables 로케이션으로 확인한다.
        /// </summary>
        public static List<string> GetDownloadAddresses()
        {
            var addresses = new HashSet<string>(StringComparer.Ordinal);
            if (_liveOperators != null && _liveOperators.entries != null)
            {
                foreach (OperatorCatalogEntry entry in _liveOperators.entries)
                {
                    if (entry == null) { continue; }
                    AddAddress(addresses, entry.address);
                    AddAddress(addresses, entry.previewPortraitAddress);
                    AddAddress(addresses, entry.managementPortraitAddress);
                    AddAddress(addresses, entry.shopPortraitAddress);
                    AddAddress(addresses, entry.shopUpperBodyPortraitAddress);
                    AddAddress(addresses, entry.shopUpperBodyPortraitDimmedAddress);
                    AddAddress(addresses, entry.unlockRewardPortraitAddress);

                    if (entry.unitPreviews == null) { continue; }
                    foreach (var unit in entry.unitPreviews)
                    {
                        if (unit == null) { continue; }
                        AddAddress(addresses, unit.address);
                        AddAddress(addresses, unit.previewIconAddress);
                    }
                }
            }

            if (_liveStages != null && _liveStages.entries != null)
            {
                foreach (StageCatalogEntry entry in _liveStages.entries)
                {
                    if (entry == null) { continue; }
                    AddAddress(addresses, entry.address);
                    AddAddress(addresses, entry.descriptionBackgroundAddress);
                    if (entry.enemyPreviews == null) { continue; }
                    foreach (var enemy in entry.enemyPreviews)
                    {
                        if (enemy == null || string.IsNullOrWhiteSpace(enemy.enemyId)) { continue; }
                        AddAddress(addresses, BattleContentCache.EnemyAddressPrefix + enemy.enemyId);
                    }
                }
            }

            return new List<string>(addresses);
        }

        private static void CheckForCatalogUpdates()
        {
            AsyncOperationHandle<List<string>> check = Addressables.CheckForCatalogUpdates(false);
            check.Completed += completed =>
            {
                var updates = completed.Status == AsyncOperationStatus.Succeeded && completed.Result != null
                    ? new List<string>(completed.Result)
                    : new List<string>();
                bool checkSucceeded = completed.Status == AsyncOperationStatus.Succeeded;
                Addressables.Release(completed);

                if (!checkSucceeded)
                {
                    // 네트워크 조회가 실패해도 이전에 캐시된 원격 카탈로그가 있으면 사용할 수
                    // 있으므로 현재 로케이터에서 라이브 카탈로그를 한 번 찾아본다.
                    Debug.LogWarning("[LiveCatalog] 원격 카탈로그 갱신 확인에 실패해 캐시된 콘텐츠를 확인합니다.");
                    LoadLiveCatalogs();
                    return;
                }

                if (updates.Count == 0)
                {
                    LoadLiveCatalogs();
                    return;
                }

                AsyncOperationHandle<List<IResourceLocator>> update =
                    Addressables.UpdateCatalogs(updates, false);
                update.Completed += updated =>
                {
                    bool succeeded = updated.Status == AsyncOperationStatus.Succeeded;
                    Addressables.Release(updated);
                    if (!succeeded)
                    {
                        Finish(false, "온라인 카탈로그를 갱신하지 못했습니다.");
                        return;
                    }

                    LoadLiveCatalogs();
                };
            };
        }

        private static void LoadLiveCatalogs()
        {
            _liveOperators = null;
            _liveStages = null;
            LoadIfPublished<OperatorCatalog>(
                OperatorCatalog.LiveCatalogAddress,
                catalog => _liveOperators = catalog,
                () => LoadIfPublished<StageCatalog>(
                    StageCatalog.LiveCatalogAddress,
                    catalog => _liveStages = catalog,
                    () =>
                    {
                        bool found = _liveOperators != null || _liveStages != null;
                        Finish(found, found ? string.Empty : "서버에서 적용할 신규 콘텐츠를 찾지 못했습니다.");
                    }));
        }

        private static OperatorCatalog ResolveLocalOnly(OperatorCatalog source)
        {
            if (source == null || LocalResults.Contains(source) || MergedResults.Contains(source))
            {
                return source;
            }

            if (LocalOperators.TryGetValue(source, out OperatorCatalog cached) && cached != null)
            {
                return cached;
            }

            List<OperatorCatalogEntry> entries = FilterLocal(
                source.entries, entry => entry.remoteContent, out int removed);
            if (removed == 0)
            {
                LocalOperators[source] = source;
                return source;
            }

            OperatorCatalog local = CreateRuntimeCopy<OperatorCatalog>(source.name, "Local");
            local.entries = entries;
            LocalOperators[source] = local;
            LocalResults.Add(local);
            return local;
        }

        private static StageCatalog ResolveLocalOnly(StageCatalog source)
        {
            if (source == null || LocalResults.Contains(source) || MergedResults.Contains(source))
            {
                return source;
            }

            if (LocalStages.TryGetValue(source, out StageCatalog cached) && cached != null)
            {
                return cached;
            }

            List<StageCatalogEntry> entries = FilterLocal(
                source.entries, entry => entry.remoteContent, out int removed);
            if (removed == 0)
            {
                LocalStages[source] = source;
                return source;
            }

            StageCatalog local = CreateRuntimeCopy<StageCatalog>(source.name, "Local");
            local.entries = entries;
            LocalStages[source] = local;
            LocalResults.Add(local);
            return local;
        }

        private static List<TEntry> FilterLocal<TEntry>(
            List<TEntry> source, Func<TEntry, bool> isRemote, out int removed) where TEntry : class
        {
            var result = new List<TEntry>();
            removed = 0;
            if (source == null) { return result; }
            foreach (TEntry entry in source)
            {
                if (entry == null) { continue; }
                if (isRemote(entry))
                {
                    removed++;
                    continue;
                }

                result.Add(entry);
            }

            return result;
        }

        private static List<TEntry> MergeEntries<TEntry>(
            List<TEntry> local, List<TEntry> live, Func<TEntry, string> getId, out int added)
            where TEntry : class
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<TEntry>();
            if (local != null)
            {
                foreach (TEntry entry in local)
                {
                    if (entry == null) { continue; }
                    entries.Add(entry);
                    known.Add(getId(entry));
                }
            }

            added = 0;
            if (live == null) { return entries; }
            foreach (TEntry entry in live)
            {
                if (entry == null) { continue; }
                string id = getId(entry);
                if (string.IsNullOrWhiteSpace(id) || !known.Add(id)) { continue; }
                entries.Add(entry);
                added++;
            }

            return entries;
        }

        private static T CreateRuntimeCopy<T>(string sourceName, string suffix) where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            instance.name = sourceName + " (" + suffix + ")";
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
        }

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
                    if (loaded.Status == AsyncOperationStatus.Succeeded && loaded.Result != null)
                    {
                        // 앱 수명 동안 카탈로그를 계속 참조하므로 핸들을 의도적으로 유지한다.
                        onLoaded(loaded.Result);
                    }
                    else
                    {
                        Debug.LogWarning($"[LiveCatalog] 라이브 카탈로그를 받지 못했습니다: {address}");
                    }

                    onFinished();
                };
            };
        }

        private static void AddAddress(HashSet<string> addresses, string address)
        {
            if (!string.IsNullOrWhiteSpace(address))
            {
                addresses.Add(address);
            }
        }

        private static void Finish(bool activated, string failureMessage)
        {
            IsRunning = false;
            IsResolved = true;
            IsActivated = activated;
            FailureMessage = failureMessage ?? string.Empty;
            Action callbacks = _resolved;
            _resolved = null;
            callbacks?.Invoke();
        }
    }
}
