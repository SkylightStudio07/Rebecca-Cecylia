using System;
using System.Collections;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Stage;
using RCCom.Definitions.Unit;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RCCom.Runtime
{
    /// <summary>
    /// TitleScene에서 내려받은 전투 콘텐츠와 Addressables 핸들을 애플리케이션 실행 동안 보관한다.
    /// 씬 재로드는 도메인 리로드가 아니므로, Retry 때 같은 Definition을 다시 로드하지 않고
    /// 이미 검증한 핸들과 Definition을 재사용한다.
    /// </summary>
    public static class BattleContentCache
    {
        public const string EnemyAddressPrefix = "enemy/";
        public const string AllyUnitAddressPrefix = "ally-unit/";

        private static readonly Dictionary<string, EnemyDefinition> EnemyDefinitions =
            new(StringComparer.Ordinal);
        private static readonly Dictionary<string, AllyUnitDefinition> AllyUnitDefinitions =
            new(StringComparer.Ordinal);
        private static readonly Dictionary<string, AsyncOperationHandle<EnemyDefinition>> EnemyHandles =
            new(StringComparer.Ordinal);
        private static readonly Dictionary<string, AsyncOperationHandle<AllyUnitDefinition>> AllyUnitHandles =
            new(StringComparer.Ordinal);
        private static readonly Dictionary<EnemyRoster, EnemyRoster> RuntimeEnemyRosters = new();
        private static readonly Dictionary<AllyUnitRoster, AllyUnitRoster> RuntimeAllyUnitRosters = new();
        private static readonly Dictionary<(AllyUnitDefinition source, string operatorId), AllyUnitDefinition>
            RuntimeUpgradedUnitDefinitions = new();

        private static bool _addressablesInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewApplicationRun()
        {
            ReleaseHandles();
            EnemyDefinitions.Clear();
            AllyUnitDefinitions.Clear();
            EnemyHandles.Clear();
            AllyUnitHandles.Clear();
            RuntimeEnemyRosters.Clear();
            RuntimeAllyUnitRosters.Clear();
            ClearRuntimeUpgradeCache();
            _addressablesInitialized = false;
        }

        /// <summary>
        /// 강화 레벨은 같은 애플리케이션 실행 중에도 상점에서 바뀔 수 있으므로 전투 씬마다
        /// 복제본을 다시 계산한다. 복제한 효과 SO까지 함께 파괴해 Retry 누적 메모리도 남기지 않는다.
        /// </summary>
        public static void ClearRuntimeUpgradeCache()
        {
            foreach (KeyValuePair<(AllyUnitDefinition source, string operatorId), AllyUnitDefinition> pair
                     in RuntimeUpgradedUnitDefinitions)
            {
                AllyUnitDefinition source = pair.Key.source;
                AllyUnitDefinition upgraded = pair.Value;
                if (upgraded == null || ReferenceEquals(upgraded, source))
                {
                    continue;
                }

                if (upgraded.effects != null)
                {
                    for (int i = 0; i < upgraded.effects.Count; i++)
                    {
                        UnityEngine.Object effect = upgraded.effects[i];
                        if (effect != null && !ContainsReference(source != null ? source.effects : null, effect))
                        {
                            UnityEngine.Object.Destroy(effect);
                        }
                    }
                }

                UnityEngine.Object.Destroy(upgraded);
            }

            RuntimeUpgradedUnitDefinitions.Clear();
        }

        private static bool ContainsReference(List<RCCom.Effects.Unit.AllyUnitEffectBase> effects,
            UnityEngine.Object target)
        {
            if (effects == null)
            {
                return false;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                if (ReferenceEquals(effects[i], target))
                {
                    return true;
                }
            }

            return false;
        }

        public static EnemyDefinition ResolveEnemy(string enemyId)
        {
            return !string.IsNullOrWhiteSpace(enemyId) &&
                   EnemyDefinitions.TryGetValue(enemyId, out EnemyDefinition definition)
                ? definition
                : null;
        }

        public static AllyUnitDefinition ResolveAllyUnit(string unitId)
        {
            return !string.IsNullOrWhiteSpace(unitId) &&
                   AllyUnitDefinitions.TryGetValue(unitId, out AllyUnitDefinition definition)
                ? definition
                : null;
        }

        /// <summary>
        /// Addressables가 채우는 비직렬화 목록을 전투 씬에서 사용할 세션 전용 Roster로 만든다.
        /// 원본 SO의 직렬화 필드는 건드리지 않고, 같은 원본에 대한 클론만 재사용한다.
        /// </summary>
        public static EnemyRoster CreateRuntimeEnemyRoster(EnemyRoster source)
        {
            if (source == null)
            {
                return null;
            }

            if (!RuntimeEnemyRosters.TryGetValue(source, out EnemyRoster runtimeRoster) ||
                runtimeRoster == null)
            {
                runtimeRoster = ScriptableObject.CreateInstance<EnemyRoster>();
                runtimeRoster.name = $"{source.name} (Runtime)";
                runtimeRoster.hideFlags = HideFlags.DontSave;
                RuntimeEnemyRosters[source] = runtimeRoster;
            }

            runtimeRoster.enemyIds = CopyStrings(source.enemyIds);
            runtimeRoster.enemies.Clear();
            foreach (string enemyId in GetEnemyIds(source))
            {
                EnemyDefinition definition = ResolveEnemy(enemyId) ?? source.FindById(enemyId);
                if (definition != null && !runtimeRoster.enemies.Contains(definition))
                {
                    runtimeRoster.enemies.Add(definition);
                }
            }

            return runtimeRoster;
        }

        /// <summary>
        /// 오퍼레이터 Roster를 전투 세션용 클론으로 만든다. 캐시가 아직 비어 있는 개발용
        /// Foundation 검증에서는 원본의 비직렬화 목록을 보조 경로로 사용한다.
        /// </summary>
        public static AllyUnitRoster CreateRuntimeAllyUnitRoster(AllyUnitRoster source)
        {
            return CreateRuntimeAllyUnitRoster(source, null, null, null);
        }

        /// <summary>
        /// 오퍼레이터 강화 레벨을 함께 반영하는 버전. operatorId/upgradeTracks/profile 중 하나라도
        /// 비어 있으면 강화 계산을 건너뛰고 기존 무인자 오버로드와 동일하게 동작한다 — 미강화
        /// 상태·구버전 호출부 모두 회귀 없이 그대로 유지된다.
        /// </summary>
        public static AllyUnitRoster CreateRuntimeAllyUnitRoster(
            AllyUnitRoster source,
            string operatorId,
            OperatorUpgradeTrackSet upgradeTracks,
            PlayerProfile profile)
        {
            if (source == null)
            {
                return null;
            }

            if (!RuntimeAllyUnitRosters.TryGetValue(source, out AllyUnitRoster runtimeRoster) ||
                runtimeRoster == null)
            {
                runtimeRoster = ScriptableObject.CreateInstance<AllyUnitRoster>();
                runtimeRoster.name = $"{source.name} (Runtime)";
                runtimeRoster.hideFlags = HideFlags.DontSave;
                RuntimeAllyUnitRosters[source] = runtimeRoster;
            }

            runtimeRoster.unitIds = CopyStrings(source.unitIds);
            runtimeRoster.units.Clear();
            foreach (string unitId in GetAllyUnitIds(source))
            {
                AllyUnitDefinition definition = ResolveAllyUnit(unitId) ?? source.FindById(unitId);
                definition = ResolveUpgradedUnitDefinition(definition, operatorId, upgradeTracks, profile);
                if (definition != null && !runtimeRoster.units.Contains(definition))
                {
                    runtimeRoster.units.Add(definition);
                }
            }

            // 기존 Foundation 검증처럼 ID 없이 비직렬화 목록만 주입한 호출도 같은 계약으로 정규화한다.
            if (runtimeRoster.unitIds.Count == 0)
            {
                foreach (AllyUnitDefinition definition in runtimeRoster.units)
                {
                    if (definition != null && definition.data != null &&
                        !runtimeRoster.unitIds.Contains(definition.data.unitId))
                    {
                        runtimeRoster.unitIds.Add(definition.data.unitId);
                    }
                }
            }

            return runtimeRoster;
        }

        /// <summary>
        /// 같은 원본 Definition + operatorId 조합은 씬 안에서 한 번만 강화 복제본을 만든다.
        /// 강화 대상이 없으면(트랙 없음·레벨 0 등) OperatorUpgradeApplier가 원본을 그대로 돌려주므로
        /// 캐시에도 원본이 그대로 들어가 불필요한 할당이 생기지 않는다.
        /// </summary>
        private static AllyUnitDefinition ResolveUpgradedUnitDefinition(
            AllyUnitDefinition source,
            string operatorId,
            OperatorUpgradeTrackSet upgradeTracks,
            PlayerProfile profile)
        {
            if (source == null || upgradeTracks == null || profile == null ||
                string.IsNullOrWhiteSpace(operatorId))
            {
                return source;
            }

            var key = (source, operatorId);
            if (RuntimeUpgradedUnitDefinitions.TryGetValue(key, out AllyUnitDefinition cached) &&
                cached != null)
            {
                return cached;
            }

            AllyUnitDefinition upgraded =
                OperatorUpgradeApplier.CreateUpgradedUnitDefinition(source, operatorId, upgradeTracks, profile);
            RuntimeUpgradedUnitDefinitions[key] = upgraded;
            return upgraded;
        }

        public static IEnumerator PreloadAllyUnits(
            AllyUnitRoster roster,
            Action<string, float> onProgress,
            Action<string> onFailed)
        {
            List<string> unitIds = GetAllyUnitIds(roster);
            yield return PreloadAllyUnitIds(unitIds, onProgress, onFailed);
        }

        public static IEnumerator PreloadEnemies(
            EnemyRoster roster,
            Action<string, float> onProgress,
            Action<string> onFailed)
        {
            List<string> enemyIds = GetEnemyIds(roster);
            yield return PreloadEnemyIds(enemyIds, onProgress, onFailed);
        }

        public static IEnumerator PreloadEnemiesForStage(
            StageDefinition stage,
            Action<string, float> onProgress,
            Action<string> onFailed)
        {
            var enemyIds = new List<string>();
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            if (stage != null && stage.waves != null)
            {
                foreach (StageWaveDefinition wave in stage.waves)
                {
                    if (wave == null || wave.spawns == null)
                    {
                        continue;
                    }

                    foreach (StageEnemySpawn spawn in wave.spawns)
                    {
                        if (spawn != null && !string.IsNullOrWhiteSpace(spawn.enemyId) &&
                            uniqueIds.Add(spawn.enemyId))
                        {
                            enemyIds.Add(spawn.enemyId);
                        }
                    }
                }
            }

            yield return PreloadEnemyIds(enemyIds, onProgress, onFailed);
        }

        private static IEnumerator PreloadAllyUnitIds(
            List<string> unitIds,
            Action<string, float> onProgress,
            Action<string> onFailed)
        {
            string initializationError = null;
            yield return EnsureAddressablesInitialized(error => initializationError = error);
            if (!string.IsNullOrWhiteSpace(initializationError))
            {
                Fail(initializationError, onFailed);
                yield break;
            }

            if (unitIds.Count == 0)
            {
                onProgress?.Invoke("아군 유닛 콘텐츠 준비 완료", 1f);
                yield break;
            }

            for (int i = 0; i < unitIds.Count; i++)
            {
                string unitId = unitIds[i];
                if (AllyUnitDefinitions.ContainsKey(unitId))
                {
                    onProgress?.Invoke($"아군 유닛 준비 {i + 1}/{unitIds.Count}", (i + 1f) / unitIds.Count);
                    continue;
                }

                string address = AllyUnitAddressPrefix + unitId;
                AsyncOperationHandle<AllyUnitDefinition> handle =
                    Addressables.LoadAssetAsync<AllyUnitDefinition>(address);
                yield return handle;
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    ReleaseIfValid(handle);
                    Fail($"아군 유닛 콘텐츠를 불러오지 못했습니다: {unitId}", onFailed);
                    yield break;
                }

                AllyUnitDefinition definition = handle.Result;
                if (definition.data == null || !string.Equals(definition.data.unitId, unitId,
                        StringComparison.Ordinal))
                {
                    ReleaseIfValid(handle);
                    Fail($"아군 유닛 ID가 카탈로그와 일치하지 않습니다: {unitId}", onFailed);
                    yield break;
                }

                AllyUnitDefinitions[unitId] = definition;
                AllyUnitHandles[unitId] = handle;
                onProgress?.Invoke($"아군 유닛 준비 {i + 1}/{unitIds.Count}", (i + 1f) / unitIds.Count);
            }
        }

        private static IEnumerator PreloadEnemyIds(
            List<string> enemyIds,
            Action<string, float> onProgress,
            Action<string> onFailed)
        {
            string initializationError = null;
            yield return EnsureAddressablesInitialized(error => initializationError = error);
            if (!string.IsNullOrWhiteSpace(initializationError))
            {
                Fail(initializationError, onFailed);
                yield break;
            }

            if (enemyIds.Count == 0)
            {
                onProgress?.Invoke("적 콘텐츠 준비 완료", 1f);
                yield break;
            }

            for (int i = 0; i < enemyIds.Count; i++)
            {
                string enemyId = enemyIds[i];
                if (EnemyDefinitions.ContainsKey(enemyId))
                {
                    onProgress?.Invoke($"적 콘텐츠 준비 {i + 1}/{enemyIds.Count}", (i + 1f) / enemyIds.Count);
                    continue;
                }

                string address = EnemyAddressPrefix + enemyId;
                AsyncOperationHandle<EnemyDefinition> handle =
                    Addressables.LoadAssetAsync<EnemyDefinition>(address);
                yield return handle;
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    ReleaseIfValid(handle);
                    Fail($"적 콘텐츠를 불러오지 못했습니다: {enemyId}", onFailed);
                    yield break;
                }

                EnemyDefinition definition = handle.Result;
                if (definition.data == null || !string.Equals(definition.data.enemyId, enemyId,
                        StringComparison.Ordinal))
                {
                    ReleaseIfValid(handle);
                    Fail($"적 ID가 카탈로그와 일치하지 않습니다: {enemyId}", onFailed);
                    yield break;
                }

                EnemyDefinitions[enemyId] = definition;
                EnemyHandles[enemyId] = handle;
                onProgress?.Invoke($"적 콘텐츠 준비 {i + 1}/{enemyIds.Count}", (i + 1f) / enemyIds.Count);
            }
        }

        private static IEnumerator EnsureAddressablesInitialized(Action<string> onFailed)
        {
            if (_addressablesInitialized)
            {
                yield break;
            }

            AsyncOperationHandle initialization = Addressables.InitializeAsync(false);
            yield return initialization;
            if (initialization.Status != AsyncOperationStatus.Succeeded)
            {
                ReleaseIfValid(initialization);
                onFailed?.Invoke("Addressables 초기화에 실패했습니다.");
                yield break;
            }

            _addressablesInitialized = true;
            ReleaseIfValid(initialization);
        }

        private static List<string> GetEnemyIds(EnemyRoster roster)
        {
            var ids = new List<string>();
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            if (roster != null && roster.enemyIds != null)
            {
                foreach (string enemyId in roster.enemyIds)
                {
                    if (!string.IsNullOrWhiteSpace(enemyId) && uniqueIds.Add(enemyId))
                    {
                        ids.Add(enemyId);
                    }
                }
            }

            if (ids.Count == 0 && roster != null && roster.enemies != null)
            {
                foreach (EnemyDefinition definition in roster.enemies)
                {
                    string enemyId = definition != null && definition.data != null
                        ? definition.data.enemyId
                        : null;
                    if (!string.IsNullOrWhiteSpace(enemyId) && uniqueIds.Add(enemyId))
                    {
                        ids.Add(enemyId);
                    }
                }
            }

            return ids;
        }

        private static List<string> GetAllyUnitIds(AllyUnitRoster roster)
        {
            var ids = new List<string>();
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            if (roster != null && roster.unitIds != null)
            {
                foreach (string unitId in roster.unitIds)
                {
                    if (!string.IsNullOrWhiteSpace(unitId) && uniqueIds.Add(unitId))
                    {
                        ids.Add(unitId);
                    }
                }
            }

            if (ids.Count == 0 && roster != null && roster.units != null)
            {
                foreach (AllyUnitDefinition definition in roster.units)
                {
                    string unitId = definition != null && definition.data != null
                        ? definition.data.unitId
                        : null;
                    if (!string.IsNullOrWhiteSpace(unitId) && uniqueIds.Add(unitId))
                    {
                        ids.Add(unitId);
                    }
                }
            }

            return ids;
        }

        private static List<string> CopyStrings(List<string> source)
        {
            return source == null ? new List<string>() : new List<string>(source);
        }

        private static void Fail(string message, Action<string> onFailed)
        {
            Debug.LogError($"[BattleContentCache] {message}");
            onFailed?.Invoke(message);
        }

        private static void ReleaseHandles()
        {
            foreach (AsyncOperationHandle<EnemyDefinition> handle in EnemyHandles.Values)
            {
                ReleaseIfValid(handle);
            }

            foreach (AsyncOperationHandle<AllyUnitDefinition> handle in AllyUnitHandles.Values)
            {
                ReleaseIfValid(handle);
            }
        }

        private static void ReleaseIfValid(AsyncOperationHandle handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
    }
}
