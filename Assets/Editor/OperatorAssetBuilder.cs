using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Card;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Tower;
using RCCom.Definitions.Unit;
using RCCom.UI;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 단일 오퍼레이터 빌드 결과. 어떤 에셋이 실제로 다시 쓰였는지 남겨,
    /// "빌드했더니 관계없는 파일까지 변경됐다"는 상황을 눈으로 구분할 수 있게 한다.
    /// </summary>
    public sealed class OperatorBuildReport
    {
        public string operatorId;
        public readonly List<string> changedAssets = new();
        public bool validationPassed;
    }

    /// <summary>
    /// JSON 레시피에서 오퍼레이터 Definition과 전용 Tower/Card Roster를 일괄 생성한다.
    /// 생성물에 라벨을 붙이고 그 라벨이 없는 기존 에셋은 수정하지 않아, 같은 경로에 사람이
    /// 만든 에셋이 있어도 자동화가 조용히 덮어쓰는 사고를 막는다.
    /// </summary>
    public static class OperatorAssetBuilder
    {
        private const string RecipeFolder = "Assets/Editor/OperatorRecipes";
        private const string OutputRoot = "Assets/Data/Operators";
        private const string GeneratedLabel = "RCCom.GeneratedOperator";

        [MenuItem("RCCom/Operators/Build All Operator Assets")]
        public static void BuildAll()
        {
            string[] recipeGuids = AssetDatabase.FindAssets("t:TextAsset", new[] { RecipeFolder });
            var recipePaths = new List<string>();

            foreach (string guid in recipeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    recipePaths.Add(path);
                }
            }

            recipePaths.Sort(StringComparer.Ordinal);
            if (recipePaths.Count == 0)
            {
                throw new InvalidOperationException($"오퍼레이터 레시피가 없습니다: {RecipeFolder}");
            }

            EnsureFolder(OutputRoot);

            var changedAssets = new List<string>();
            foreach (string recipePath in recipePaths)
            {
                TextAsset recipeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(recipePath);
                OperatorAssetRecipe recipe = JsonUtility.FromJson<OperatorAssetRecipe>(recipeAsset.text);
                ValidateRecipe(recipe, recipePath);
                BuildOperator(recipe, changedAssets);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 에셋과 카탈로그/Addressables 그룹을 같은 레시피에서 갱신해 서로 어긋나는
            // 수작업 상태가 생기지 않게 한다.
            OperatorCatalogBuilder.BuildAll();

            if (!OperatorAssetValidator.ValidateAll(false))
            {
                throw new InvalidOperationException("오퍼레이터 에셋 생성 후 검증에 실패했습니다. 콘솔 오류를 확인하세요.");
            }

            Debug.Log(
                $"[OperatorAssetBuilder] 오퍼레이터 {recipePaths.Count}명 생성/갱신 및 검증 완료 " +
                $"(내용이 바뀐 에셋 {changedAssets.Count}개)");
        }

        /// <summary>
        /// 레시피 한 개만 다시 만든다. 작업 중이 아닌 오퍼레이터의 생성물과 그룹은 건드리지
        /// 않아, 변경이 없는 캐릭터의 에셋이 다시 쓰이면서 생기는 형상관리 잡음을 없앤다.
        /// 그룹 정리 같은 전체 동기화는 BuildAll이 계속 담당한다.
        /// </summary>
        public static OperatorBuildReport BuildSingle(string recipePath)
        {
            TextAsset recipeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(recipePath);
            if (recipeAsset == null)
            {
                throw new InvalidOperationException($"오퍼레이터 레시피를 찾지 못했습니다: {recipePath}");
            }

            OperatorAssetRecipe recipe = JsonUtility.FromJson<OperatorAssetRecipe>(recipeAsset.text);
            ValidateRecipe(recipe, recipePath);
            EnsureFolder(OutputRoot);

            var report = new OperatorBuildReport { operatorId = recipe.operatorId };
            BuildOperator(recipe, report.changedAssets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            OperatorCatalogBuilder.BuildForOperator(recipe, report.changedAssets);

            // 카탈로그와 Addressables는 오퍼레이터끼리 공유하는 상태이므로, 한 명만 빌드해도
            // 검증은 전체를 돌려 다른 오퍼레이터와 어긋난 상태를 여기서 잡는다.
            report.validationPassed = OperatorAssetValidator.ValidateAll(false);
            Debug.Log(
                $"[OperatorAssetBuilder] {recipe.operatorId} 단일 빌드 완료 " +
                $"(내용이 바뀐 에셋 {report.changedAssets.Count}개, " +
                $"검증 {(report.validationPassed ? "통과" : "실패")})");
            return report;
        }

        private static void BuildOperator(OperatorAssetRecipe recipe, List<string> changedAssets)
        {
            TowerRoster sourceTowerRoster = LoadRequired<TowerRoster>(recipe.sourceTowerRosterPath, recipe.operatorId);
            CardRoster sourceCardRoster = LoadRequired<CardRoster>(recipe.sourceCardRosterPath, recipe.operatorId);
            AllyUnitRoster sourceAllyUnitRoster = LoadOptional<AllyUnitRoster>(recipe.sourceAllyUnitRosterPath);
            OperatorDialogueSet dialogueSet = LoadRequired<OperatorDialogueSet>(recipe.dialogueSetPath, recipe.operatorId);
            Sprite selectionPortrait = LoadOptional<Sprite>(recipe.selectionPortraitPath);
            Sprite managementPortrait = LoadOptional<Sprite>(recipe.managementPortraitPath);
            Sprite shopPortrait = LoadOptional<Sprite>(recipe.shopPortraitPath);
            Sprite shopUpperBodyPortrait = LoadOptional<Sprite>(recipe.shopUpperBodyPortraitPath);
            Sprite shopUpperBodyPortraitDimmed = LoadOptional<Sprite>(recipe.shopUpperBodyPortraitDimmedPath);
            Sprite unlockRewardPortrait = LoadOptional<Sprite>(recipe.unlockRewardPortraitPath);

            string operatorFolder = $"{OutputRoot}/{recipe.operatorId}";
            EnsureFolder(operatorFolder);

            TowerRoster towerRoster = GetOrCreateOwnedAsset<TowerRoster>(
                $"{operatorFolder}/TowerRoster.asset", changedAssets);
            ApplyIfChanged(
                towerRoster,
                asset => asset.towers = new List<TowerDefinition>(sourceTowerRoster.towers),
                changedAssets);

            CardRoster cardRoster = GetOrCreateOwnedAsset<CardRoster>(
                $"{operatorFolder}/CardRoster.asset", changedAssets);
            ApplyIfChanged(
                cardRoster,
                asset => asset.cards = new List<RCCom.Effects.Card.CardEffectBase>(sourceCardRoster.cards),
                changedAssets);

            AllyUnitRoster allyUnitRoster = null;
            if (sourceAllyUnitRoster != null)
            {
                allyUnitRoster = GetOrCreateOwnedAsset<AllyUnitRoster>(
                    $"{operatorFolder}/AllyUnitRoster.asset", changedAssets);
                ApplyIfChanged(
                    allyUnitRoster,
                    asset =>
                    {
                        // 오퍼레이터 패키지가 유닛 Definition을 암묵적으로 끌고 가지 않도록
                        // 생성 Roster에는 ID만 복제한다. units는 스키마 전환 중인 기존
                        // 생성물의 GUID를 비워 다음 저장에서 완전히 제거한다.
                        asset.unitIds = new List<string>(sourceAllyUnitRoster.unitIds);
                        asset.units = new List<AllyUnitDefinition>();
                    },
                    changedAssets);
            }

            OperatorUpgradeTrackSet upgradeTrackSet = GetOrCreateOwnedAsset<OperatorUpgradeTrackSet>(
                $"{operatorFolder}/OperatorUpgradeTrackSet.asset", changedAssets);
            ApplyIfChanged(
                upgradeTrackSet,
                asset => asset.tracks = CloneUpgradeTracks(recipe.upgradeTracks),
                changedAssets);

            OperatorDefinition definition = GetOrCreateOwnedAsset<OperatorDefinition>(
                $"{operatorFolder}/OperatorDefinition.asset", changedAssets);
            ApplyIfChanged(definition, asset =>
            {
                asset.operatorId = recipe.operatorId;
                asset.displayName = recipe.displayName;
                asset.playStyleDescription = recipe.playStyleDescription;
                asset.selectionPortrait = selectionPortrait;
                asset.managementPortrait = managementPortrait;
                asset.shopPortrait = shopPortrait;
                asset.shopUpperBodyPortrait = shopUpperBodyPortrait;
                asset.shopUpperBodyPortraitDimmed = shopUpperBodyPortraitDimmed;
                asset.alternateName = recipe.alternateName ?? string.Empty;
                asset.shopDialogue = recipe.shopDialogue ?? string.Empty;
                asset.codename = recipe.codename ?? string.Empty;
                asset.role = recipe.role ?? string.Empty;
                asset.faction = recipe.faction ?? string.Empty;
                asset.height = recipe.height ?? string.Empty;
                asset.birthday = recipe.birthday ?? string.Empty;
                asset.speciality = recipe.speciality ?? string.Empty;
                asset.weapon = recipe.weapon ?? string.Empty;
                asset.origin = recipe.origin ?? string.Empty;
                asset.bondRecords = CloneBondRecords(recipe.bondRecords);
                asset.unlockRewardPortrait = unlockRewardPortrait;
                asset.playerData = ClonePlayerData(recipe.playerData);
                asset.towerRoster = towerRoster;
                asset.cardRoster = cardRoster;
                asset.allyUnitRoster = allyUnitRoster;
                asset.upgradeTracks = upgradeTrackSet;
                asset.dialogueSet = dialogueSet;
                asset.unlockType = recipe.unlockType;
                asset.requiredBestWave = recipe.requiredBestWave;
                asset.purchasePrice = recipe.purchasePrice;
                asset.requiredStageId = recipe.requiredStageId ?? string.Empty;
                asset.unlockConditions = CloneUnlockConditions(recipe.unlockConditions);
            }, changedAssets);
        }

        /// <summary>
        /// 값이 실제로 달라졌을 때만 더티 플래그를 세운다. 내용이 같은데도 저장되면 에셋
        /// 파일이 다시 쓰이면서 형상관리에 의미 없는 변경으로 잡히고, 나중에 병합 충돌을 만든다.
        /// </summary>
        private static void ApplyIfChanged<T>(T asset, Action<T> mutate, List<string> changedAssets)
            where T : UnityEngine.Object
        {
            string before = EditorJsonUtility.ToJson(asset);
            mutate(asset);
            if (EditorJsonUtility.ToJson(asset) == before)
            {
                return;
            }

            EditorUtility.SetDirty(asset);
            RecordChange(AssetDatabase.GetAssetPath(asset), changedAssets);
        }

        internal static void RecordChange(string path, List<string> changedAssets)
        {
            if (changedAssets == null || string.IsNullOrEmpty(path) || changedAssets.Contains(path))
            {
                return;
            }

            changedAssets.Add(path);
        }

        private static T LoadRequired<T>(string path, string operatorId) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"[{operatorId}] 필수 {typeof(T).Name} 에셋을 찾지 못했습니다: {path}");
            }

            return asset;
        }

        private static T LoadOptional<T>(string path) where T : UnityEngine.Object
        {
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static T GetOrCreateOwnedAsset<T>(string path, List<string> changedAssets)
            where T : ScriptableObject
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null)
            {
                if (existing is not T typedAsset)
                {
                    throw new InvalidOperationException($"생성 대상 경로에 다른 타입의 에셋이 있습니다: {path}");
                }

                string[] labels = AssetDatabase.GetLabels(typedAsset);
                if (Array.IndexOf(labels, GeneratedLabel) < 0)
                {
                    throw new InvalidOperationException($"자동 생성 라벨이 없는 기존 에셋은 덮어쓸 수 없습니다: {path}");
                }

                return typedAsset;
            }

            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SetLabels(created, new[] { GeneratedLabel });
            RecordChange(path, changedAssets);
            return created;
        }

        private static PlayerData ClonePlayerData(PlayerData source)
        {
            return new PlayerData
            {
                maxHealth = source.maxHealth,
                moveSpeed = source.moveSpeed,
                hitInvulnerabilityDuration = source.hitInvulnerabilityDuration,
                attackDamage = source.attackDamage,
                attackRange = source.attackRange,
                attackInterval = source.attackInterval,
                projectileSpeed = source.projectileSpeed,
                skillCooldown = source.skillCooldown,
                skillRange = source.skillRange,
                skillDamage = source.skillDamage,
            };
        }

        private static void ValidateRecipe(OperatorAssetRecipe recipe, string recipePath)
        {
            if (recipe == null)
            {
                throw new InvalidOperationException($"JSON을 읽지 못했습니다: {recipePath}");
            }

            if (string.IsNullOrWhiteSpace(recipe.operatorId))
            {
                throw new InvalidOperationException($"operatorId가 비어 있습니다: {recipePath}");
            }

            foreach (char character in recipe.operatorId)
            {
                bool isAllowed = character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';
                if (!isAllowed)
                {
                    throw new InvalidOperationException($"operatorId는 영문 소문자, 숫자, -, _만 사용할 수 있습니다: {recipe.operatorId}");
                }
            }

            if (string.IsNullOrWhiteSpace(recipe.displayName) || recipe.playerData == null)
            {
                throw new InvalidOperationException($"표시 이름 또는 PlayerData가 비어 있습니다: {recipePath}");
            }

            if (recipe.requiredBestWave < 0 || recipe.purchasePrice < 0)
            {
                throw new InvalidOperationException($"해금 수치는 음수일 수 없습니다: {recipePath}");
            }

            if (recipe.unlockType == OperatorUnlockType.CommodityPurchase && recipe.purchasePrice <= 0)
            {
                throw new InvalidOperationException($"골드 구매 가격은 1 이상이어야 합니다: {recipePath}");
            }

            if (recipe.unlockType == OperatorUnlockType.StageClearReward &&
                string.IsNullOrWhiteSpace(recipe.requiredStageId))
            {
                throw new InvalidOperationException($"스테이지 보상 Stage ID가 비어 있습니다: {recipePath}");
            }

            if (recipe.upgradeTracks != null)
            {
                if (recipe.upgradeTracks.Count != 5)
                {
                    throw new InvalidOperationException(
                        $"오퍼레이터 강화 트랙은 정확히 5개여야 합니다: {recipePath}");
                }

                var trackIds = new HashSet<string>(StringComparer.Ordinal);
                var modifierTargets = new HashSet<string>(StringComparer.Ordinal);
                AllyUnitRoster unitRoster = string.IsNullOrWhiteSpace(recipe.sourceAllyUnitRosterPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<AllyUnitRoster>(recipe.sourceAllyUnitRosterPath);
                foreach (OperatorUpgradeTrack track in recipe.upgradeTracks)
                {
                    if (track == null || string.IsNullOrWhiteSpace(track.trackId))
                    {
                        throw new InvalidOperationException($"강화 트랙 trackId가 비어 있습니다: {recipePath}");
                    }

                    if (!trackIds.Add(track.trackId))
                    {
                        throw new InvalidOperationException(
                            $"강화 트랙 trackId가 중복됩니다: {track.trackId} ({recipePath})");
                    }

                    if (track.maxLevel < 1)
                    {
                        throw new InvalidOperationException(
                            $"강화 트랙 maxLevel은 1 이상이어야 합니다: {track.trackId} ({recipePath})");
                    }

                    if (string.IsNullOrWhiteSpace(track.displayName) || track.levelCosts == null ||
                        track.levelCosts.Count != track.maxLevel || track.modifiers == null ||
                        track.modifiers.Count == 0)
                    {
                        throw new InvalidOperationException(
                            $"강화 이름·비용표·modifier 구성이 불완전합니다: {track.trackId} ({recipePath})");
                    }

                    if (track.requiredAffinityByLevel != null &&
                        track.requiredAffinityByLevel.Count != 0 &&
                        track.requiredAffinityByLevel.Count != track.maxLevel)
                    {
                        throw new InvalidOperationException(
                            $"호감도 조건표 길이는 0 또는 maxLevel이어야 합니다: {track.trackId} ({recipePath})");
                    }

                    int previousAffinity = 0;
                    for (int levelIndex = 0; levelIndex < track.maxLevel; levelIndex++)
                    {
                        if (track.levelCosts[levelIndex] <= 0)
                        {
                            throw new InvalidOperationException(
                                $"강화 비용은 1 이상이어야 합니다: {track.trackId} Lv{levelIndex + 1} ({recipePath})");
                        }

                        if (track.requiredAffinityByLevel != null && track.requiredAffinityByLevel.Count > 0)
                        {
                            int affinity = track.requiredAffinityByLevel[levelIndex];
                            if (affinity < previousAffinity || affinity < 0 ||
                                affinity > PlayerProfile.MaxOperatorAffinity)
                            {
                                throw new InvalidOperationException(
                                    $"호감도 조건은 0~100 사이에서 감소하지 않아야 합니다: {track.trackId} ({recipePath})");
                            }

                            previousAffinity = affinity;
                        }
                    }

                    foreach (OperatorUpgradeModifier modifier in track.modifiers)
                    {
                        if (modifier == null || modifier.levelDeltas == null ||
                            modifier.levelDeltas.Count != track.maxLevel ||
                            modifier.hasMinValue && modifier.hasMaxValue && modifier.minValue > modifier.maxValue)
                        {
                            throw new InvalidOperationException(
                                $"modifier의 레벨표 또는 범위가 잘못됐습니다: {track.trackId} ({recipePath})");
                        }

                        bool deployTarget = modifier.targetKind is
                            OperatorUpgradeTargetKind.DeployStartingCommandPoints or
                            OperatorUpgradeTargetKind.DeployMaxCommandPoints or
                            OperatorUpgradeTargetKind.DeployCommandPointRecoveryPerSecond;
                        if (deployTarget != string.IsNullOrWhiteSpace(modifier.targetUnitId))
                        {
                            throw new InvalidOperationException(
                                $"지휘 포인트 modifier만 Unit ID를 비워야 합니다: {track.trackId} ({recipePath})");
                        }

                        if (!deployTarget && (unitRoster == null ||
                            unitRoster.unitIds == null || !unitRoster.unitIds.Contains(modifier.targetUnitId)))
                        {
                            throw new InvalidOperationException(
                                $"modifier Unit ID가 오퍼레이터 Roster에 없습니다: {modifier.targetUnitId} ({recipePath})");
                        }

                        string targetKey = modifier.targetKind + "\n" + modifier.targetUnitId;
                        if (!modifierTargets.Add(targetKey))
                        {
                            throw new InvalidOperationException(
                                $"같은 강화 대상이 여러 트랙에 중복됩니다: {track.trackId} ({recipePath})");
                        }
                    }
                }
            }

            ValidateUnlockConditions(recipe, recipePath);
        }

        private static List<OperatorUpgradeTrack> CloneUpgradeTracks(List<OperatorUpgradeTrack> source)
        {
            var clone = new List<OperatorUpgradeTrack>();
            if (source == null)
            {
                return clone;
            }

            foreach (OperatorUpgradeTrack track in source)
            {
                if (track == null)
                {
                    continue;
                }

                clone.Add(new OperatorUpgradeTrack
                {
                    trackId = track.trackId,
                    displayName = track.displayName,
                    description = track.description,
                    category = track.category,
                    maxLevel = track.maxLevel,
                    levelCosts = track.levelCosts == null
                        ? new List<int>()
                        : new List<int>(track.levelCosts),
                    requiredAffinityByLevel = track.requiredAffinityByLevel == null
                        ? new List<int>()
                        : new List<int>(track.requiredAffinityByLevel),
                    modifiers = CloneUpgradeModifiers(track.modifiers),
                });
            }

            return clone;
        }

        internal static List<OperatorUnlockCondition> CloneUnlockConditions(
            List<OperatorUnlockCondition> source)
        {
            var clone = new List<OperatorUnlockCondition>();
            if (source == null)
            {
                return clone;
            }

            foreach (OperatorUnlockCondition condition in source)
            {
                if (condition == null)
                {
                    continue;
                }

                clone.Add(new OperatorUnlockCondition
                {
                    type = condition.type,
                    requiredBestWave = condition.requiredBestWave,
                    purchasePrice = condition.purchasePrice,
                    requiredStageId = condition.requiredStageId ?? string.Empty,
                });
            }

            return clone;
        }

        internal static List<OperatorBondRecord> CloneBondRecords(List<OperatorBondRecord> source)
        {
            var clone = new List<OperatorBondRecord>();
            for (int i = 0; i < 5; i++)
            {
                OperatorBondRecord record = source != null && i < source.Count ? source[i] : null;
                clone.Add(new OperatorBondRecord
                {
                    description = record != null ? record.description ?? string.Empty : string.Empty,
                });
            }

            return clone;
        }

        private static void ValidateUnlockConditions(OperatorAssetRecipe recipe, string recipePath)
        {
            if (recipe.unlockConditions == null || recipe.unlockConditions.Count == 0)
            {
                return;
            }

            var types = new HashSet<OperatorUnlockType>();
            foreach (OperatorUnlockCondition condition in recipe.unlockConditions)
            {
                if (condition == null || !types.Add(condition.type))
                {
                    throw new InvalidOperationException($"해금 조건이 비어 있거나 종류가 중복됩니다: {recipePath}");
                }

                if (condition.requiredBestWave < 0 || condition.purchasePrice < 0)
                {
                    throw new InvalidOperationException($"해금 조건 수치는 음수일 수 없습니다: {recipePath}");
                }

                if (condition.type == OperatorUnlockType.CommodityPurchase && condition.purchasePrice <= 0)
                {
                    throw new InvalidOperationException($"상점 구매 조건의 가격은 1 이상이어야 합니다: {recipePath}");
                }

                if (condition.type == OperatorUnlockType.StageClearReward &&
                    string.IsNullOrWhiteSpace(condition.requiredStageId))
                {
                    throw new InvalidOperationException($"스테이지 보상 조건의 Stage ID가 비어 있습니다: {recipePath}");
                }
            }
        }

        private static List<OperatorUpgradeModifier> CloneUpgradeModifiers(
            List<OperatorUpgradeModifier> source)
        {
            var clone = new List<OperatorUpgradeModifier>();
            if (source == null)
            {
                return clone;
            }

            foreach (OperatorUpgradeModifier modifier in source)
            {
                if (modifier == null)
                {
                    continue;
                }

                clone.Add(new OperatorUpgradeModifier
                {
                    targetKind = modifier.targetKind,
                    targetUnitId = modifier.targetUnitId,
                    levelDeltas = modifier.levelDeltas == null
                        ? new List<float>()
                        : new List<float>(modifier.levelDeltas),
                    isInteger = modifier.isInteger,
                    hasMinValue = modifier.hasMinValue,
                    minValue = modifier.minValue,
                    hasMaxValue = modifier.hasMaxValue,
                    maxValue = modifier.maxValue,
                });
            }

            return clone;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new ArgumentException("에셋 폴더는 Assets 아래여야 합니다.", nameof(folderPath));
            }

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
