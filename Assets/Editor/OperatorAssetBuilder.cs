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

            foreach (string recipePath in recipePaths)
            {
                TextAsset recipeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(recipePath);
                OperatorAssetRecipe recipe = JsonUtility.FromJson<OperatorAssetRecipe>(recipeAsset.text);
                ValidateRecipe(recipe, recipePath);
                BuildOperator(recipe);
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

            Debug.Log($"[OperatorAssetBuilder] 오퍼레이터 {recipePaths.Count}명 생성/갱신 및 검증 완료");
        }

        private static void BuildOperator(OperatorAssetRecipe recipe)
        {
            TowerRoster sourceTowerRoster = LoadRequired<TowerRoster>(recipe.sourceTowerRosterPath, recipe.operatorId);
            CardRoster sourceCardRoster = LoadRequired<CardRoster>(recipe.sourceCardRosterPath, recipe.operatorId);
            AllyUnitRoster sourceAllyUnitRoster = LoadOptional<AllyUnitRoster>(recipe.sourceAllyUnitRosterPath);
            OperatorDialogueSet dialogueSet = LoadRequired<OperatorDialogueSet>(recipe.dialogueSetPath, recipe.operatorId);
            Sprite selectionPortrait = LoadOptional<Sprite>(recipe.selectionPortraitPath);

            string operatorFolder = $"{OutputRoot}/{recipe.operatorId}";
            EnsureFolder(operatorFolder);

            TowerRoster towerRoster = GetOrCreateOwnedAsset<TowerRoster>($"{operatorFolder}/TowerRoster.asset");
            towerRoster.towers = new List<TowerDefinition>(sourceTowerRoster.towers);
            EditorUtility.SetDirty(towerRoster);

            CardRoster cardRoster = GetOrCreateOwnedAsset<CardRoster>($"{operatorFolder}/CardRoster.asset");
            cardRoster.cards = new List<RCCom.Effects.Card.CardEffectBase>(sourceCardRoster.cards);
            EditorUtility.SetDirty(cardRoster);

            AllyUnitRoster allyUnitRoster = null;
            if (sourceAllyUnitRoster != null)
            {
                allyUnitRoster = GetOrCreateOwnedAsset<AllyUnitRoster>($"{operatorFolder}/AllyUnitRoster.asset");
                allyUnitRoster.units = new List<AllyUnitDefinition>(sourceAllyUnitRoster.units);
                EditorUtility.SetDirty(allyUnitRoster);
            }

            OperatorDefinition definition = GetOrCreateOwnedAsset<OperatorDefinition>($"{operatorFolder}/OperatorDefinition.asset");
            definition.operatorId = recipe.operatorId;
            definition.displayName = recipe.displayName;
            definition.playStyleDescription = recipe.playStyleDescription;
            definition.selectionPortrait = selectionPortrait;
            definition.playerData = ClonePlayerData(recipe.playerData);
            definition.towerRoster = towerRoster;
            definition.cardRoster = cardRoster;
            definition.allyUnitRoster = allyUnitRoster;
            definition.dialogueSet = dialogueSet;
            definition.requiredBestWave = recipe.requiredBestWave;
            EditorUtility.SetDirty(definition);
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

        private static T GetOrCreateOwnedAsset<T>(string path) where T : ScriptableObject
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

            if (recipe.requiredBestWave < 0)
            {
                throw new InvalidOperationException($"requiredBestWave는 음수일 수 없습니다: {recipePath}");
            }
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
