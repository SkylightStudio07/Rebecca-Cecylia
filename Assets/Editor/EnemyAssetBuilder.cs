using System;
using System.Collections.Generic;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using RCCom.Effects.Enemy;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 단일 적 빌드 결과. 어떤 에셋이 실제로 다시 쓰였는지 남겨,
    /// "빌드했더니 관계없는 파일까지 변경됐다"는 상황을 눈으로 구분할 수 있게 한다.
    /// </summary>
    public sealed class EnemyBuildReport
    {
        public string enemyId;
        public readonly List<string> changedAssets = new();
        public bool validationPassed;
    }

    /// <summary>
    /// JSON 레시피에서 적 Definition을 일괄 생성한다. OperatorAssetBuilder와 동일하게 생성물에
    /// 라벨을 붙이고 그 라벨이 없는 기존 에셋은 수정하지 않아, 같은 경로에 사람이 만든 에셋이
    /// 있어도 자동화가 조용히 덮어쓰는 사고를 막는다.
    /// </summary>
    public static class EnemyAssetBuilder
    {
        private const string RecipeFolder = "Assets/Editor/EnemyRecipes";
        private const string OutputRoot = "Assets/Data/Enemies";
        private const string GeneratedLabel = "RCCom.GeneratedEnemy";

        [MenuItem("RCCom/Enemies/Build All Enemy Assets")]
        public static void BuildAll()
        {
            List<string> recipePaths = FindRecipePaths();
            if (recipePaths.Count == 0)
            {
                // 적 레시피가 아직 하나도 없는 초기 상태다. 오퍼레이터와 달리 이 파이프라인은
                // 골격만 먼저 만들어지므로, 예외로 취급하면 마이그레이션 전까지 이 메뉴 자체가
                // 항상 실패 상태가 되어 다른 검증 루프까지 막는다.
                Debug.Log($"[EnemyAssetBuilder] 적 레시피가 없어 빌드를 건너뜁니다: {RecipeFolder}");
                return;
            }

            EnsureFolder(OutputRoot);

            var changedAssets = new List<string>();
            foreach (string recipePath in recipePaths)
            {
                TextAsset recipeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(recipePath);
                EnemyAssetRecipe recipe = JsonUtility.FromJson<EnemyAssetRecipe>(recipeAsset.text);
                ValidateRecipe(recipe, recipePath);
                BuildEnemy(recipe, changedAssets);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 에셋과 카탈로그/Addressables 그룹을 같은 레시피에서 갱신해 서로 어긋나는
            // 수작업 상태가 생기지 않게 한다.
            EnemyCatalogBuilder.BuildAll();

            if (!EnemyAssetValidator.ValidateAll(false))
            {
                throw new InvalidOperationException("적 에셋 생성 후 검증에 실패했습니다. 콘솔 오류를 확인하세요.");
            }

            Debug.Log(
                $"[EnemyAssetBuilder] 적 {recipePaths.Count}종 생성/갱신 및 검증 완료 " +
                $"(내용이 바뀐 에셋 {changedAssets.Count}개)");
        }

        /// <summary>
        /// 레시피 한 개만 다시 만든다. 작업 중이 아닌 적의 생성물과 그룹은 건드리지 않아,
        /// 변경이 없는 적의 에셋이 다시 쓰이면서 생기는 형상관리 잡음을 없앤다.
        /// 그룹 정리 같은 전체 동기화는 BuildAll이 계속 담당한다.
        /// </summary>
        public static EnemyBuildReport BuildSingle(string recipePath)
        {
            TextAsset recipeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(recipePath);
            if (recipeAsset == null)
            {
                throw new InvalidOperationException($"적 레시피를 찾지 못했습니다: {recipePath}");
            }

            EnemyAssetRecipe recipe = JsonUtility.FromJson<EnemyAssetRecipe>(recipeAsset.text);
            ValidateRecipe(recipe, recipePath);
            EnsureFolder(OutputRoot);

            var report = new EnemyBuildReport { enemyId = recipe.enemyId };
            BuildEnemy(recipe, report.changedAssets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EnemyCatalogBuilder.BuildForEnemy(recipe, report.changedAssets);

            // 카탈로그와 Addressables는 적끼리 공유하는 상태이므로, 하나만 빌드해도 검증은
            // 전체를 돌려 다른 적과 어긋난 상태를 여기서 잡는다.
            report.validationPassed = EnemyAssetValidator.ValidateAll(false);
            Debug.Log(
                $"[EnemyAssetBuilder] {recipe.enemyId} 단일 빌드 완료 " +
                $"(내용이 바뀐 에셋 {report.changedAssets.Count}개, " +
                $"검증 {(report.validationPassed ? "통과" : "실패")})");
            return report;
        }

        private static void BuildEnemy(EnemyAssetRecipe recipe, List<string> changedAssets)
        {
            Sprite sprite = LoadOptional<Sprite>(recipe.spritePath);

            var effects = new List<EnemyEffectBase>();
            if (recipe.effectPaths != null)
            {
                foreach (string effectPath in recipe.effectPaths)
                {
                    EnemyEffectBase effect = LoadOptional<EnemyEffectBase>(effectPath);
                    if (effect != null)
                    {
                        effects.Add(effect);
                    }
                }
            }

            string enemyFolder = $"{OutputRoot}/{recipe.enemyId}";
            EnsureFolder(enemyFolder);

            EnemyDefinition definition = GetOrCreateOwnedAsset<EnemyDefinition>(
                $"{enemyFolder}/EnemyDefinition.asset", changedAssets);
            ApplyIfChanged(definition, asset =>
            {
                asset.data = CloneEnemyData(recipe);
                asset.effects = effects;
                asset.sprite = sprite;
                asset.spriteForwardOffsetDegrees = recipe.spriteForwardOffsetDegrees;
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

        /// <summary>
        /// 레시피의 최상위 enemyId/displayName을 정본으로 삼아 EnemyData를 복제한다.
        /// data.enemyId/displayName을 그대로 두면 레시피 파일을 손으로 수정할 때 두 값이
        /// 어긋날 수 있어, 여기서 항상 최상위 값으로 덮어써 단일 진실 공급원을 유지한다.
        /// </summary>
        private static EnemyData CloneEnemyData(EnemyAssetRecipe recipe)
        {
            EnemyData source = recipe.data ?? new EnemyData();
            return new EnemyData
            {
                enemyId = recipe.enemyId,
                displayName = recipe.displayName,
                kind = source.kind,
                maxHealth = source.maxHealth,
                moveSpeed = source.moveSpeed,
                contactDamage = source.contactDamage,
                attackRange = source.attackRange,
                attackInterval = source.attackInterval,
                waveCost = source.waveCost,
                minWave = source.minWave,
                goldReward = source.goldReward,
                expReward = source.expReward,
            };
        }

        private static void ValidateRecipe(EnemyAssetRecipe recipe, string recipePath)
        {
            if (recipe == null)
            {
                throw new InvalidOperationException($"JSON을 읽지 못했습니다: {recipePath}");
            }

            if (string.IsNullOrWhiteSpace(recipe.enemyId))
            {
                throw new InvalidOperationException($"enemyId가 비어 있습니다: {recipePath}");
            }

            foreach (char character in recipe.enemyId)
            {
                bool isAllowed = character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';
                if (!isAllowed)
                {
                    throw new InvalidOperationException($"enemyId는 영문 소문자, 숫자, -, _만 사용할 수 있습니다: {recipe.enemyId}");
                }
            }

            if (string.IsNullOrWhiteSpace(recipe.displayName) || recipe.data == null)
            {
                throw new InvalidOperationException($"표시 이름 또는 EnemyData가 비어 있습니다: {recipePath}");
            }

            if (recipe.data.maxHealth <= 0f || recipe.data.moveSpeed < 0f || recipe.data.attackInterval <= 0f)
            {
                throw new InvalidOperationException($"EnemyData의 필수 수치가 유효하지 않습니다: {recipePath}");
            }

            if (recipe.data.waveCost < 0f || recipe.data.minWave < 0)
            {
                throw new InvalidOperationException($"웨이브 예산 수치는 음수일 수 없습니다: {recipePath}");
            }
        }

        private static List<string> FindRecipePaths()
        {
            var recipePaths = new List<string>();
            if (!AssetDatabase.IsValidFolder(RecipeFolder))
            {
                return recipePaths;
            }

            string[] recipeGuids = AssetDatabase.FindAssets("t:TextAsset", new[] { RecipeFolder });
            foreach (string guid in recipeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    recipePaths.Add(path);
                }
            }

            recipePaths.Sort(StringComparer.Ordinal);
            return recipePaths;
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
