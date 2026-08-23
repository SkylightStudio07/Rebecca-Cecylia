using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RCCom.Data;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Stage;
using RCCom.Effects.Enemy;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 레거시 3종 EnemyDefinition(enemyId "1"/"2"/"3") · 오타난 EnemyRoaster.asset ·
    /// CH1 스테이지 7개의 하드 참조를 새 레시피 파이프라인 규약(enemy-normal/enemy-rusher/
    /// enemy-tanker 슬러그, enemyId 문자열)으로 전환하는 일회성 마이그레이터.
    ///
    /// 반드시 Dry Run으로 먼저 확인할 것 — 실제 실행은 MoveAsset·SerializedObject 조작을
    /// 포함해 되돌리기 어렵다. 실행 전 커밋된 상태에서만 돌릴 것(문제 발생 시 git으로 복구).
    /// </summary>
    public static class EnemyRecipeMigrator
    {
        private const string LegacyRosterPath = "Assets/Data/Prefabs/EnemyRoaster.asset";
        private const string NewRosterPath = "Assets/Data/Definition/EnemyRoster.asset";
        private const string RecipeFolder = "Assets/Editor/EnemyRecipes";
        private const string OutputRoot = "Assets/Data/Enemies";
        private const string GeneratedLabel = "RCCom.GeneratedEnemy";

        // spawn 한 줄: "- enemy: {fileID: 11400000, guid: <32자리 hex>, type: 2}"
        // StageEnemySpawn.enemy가 이미 enemyId(string)로 바뀐 뒤라 SerializedProperty로는
        // 더 이상 이 값을 읽을 수 없다 — 디스크의 원본 YAML 텍스트에만 남아 있는 마지막
        // 단서라, 이 마이그레이터에 한해 "쓰기"가 아닌 "읽기" 목적으로만 정규식으로 스캔한다.
        // AGENTS.md가 금지하는 것은 .asset 텍스트 "편집"이며, 실제 반영은 전부 아래
        // ApplyStagePlan에서 SerializedObject를 통해서만 한다.
        private static readonly Regex SpawnEnemyGuidPattern = new(
            @"-\s*enemy:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]{32}),\s*type:\s*\d+\}",
            RegexOptions.Compiled);

        private static readonly string[] StagePaths =
        {
            "Assets/Data/Stages/CH1/ch1-01.asset",
            "Assets/Data/Stages/CH1/ch1-02.asset",
            "Assets/Data/Stages/CH1/ch1-03.asset",
            "Assets/Data/Stages/CH1/ch1-04.asset",
            "Assets/Data/Stages/CH1/ch1-05.asset",
            "Assets/Data/Stages/CH1/ch1-06.asset",
            "Assets/Data/Stages/CH1/ch1-07.asset",
        };

        private sealed class LegacyEnemy
        {
            public string legacyPath;
            public string slug;
        }

        private static readonly LegacyEnemy[] LegacyEnemies =
        {
            new() { legacyPath = "Assets/Data/Definition/EnemyDefinition_Normal.asset", slug = "enemy-normal" },
            new() { legacyPath = "Assets/Data/Definition/EnemyDefinition_Speed.asset", slug = "enemy-rusher" },
            new() { legacyPath = "Assets/Data/Definition/EnemyDefinition_Tanker.asset", slug = "enemy-tanker" },
        };

        private sealed class StagePlan
        {
            public string stagePath;
            public readonly List<int> waveSpawnCounts = new();
            public readonly List<string> extractedGuidsInOrder = new();
            public readonly List<string> resolvedSlugsInOrder = new();
            public readonly List<string> unresolvedGuids = new();
            public int liveSpawnCount;

            public bool IsConsistent => unresolvedGuids.Count == 0 && extractedGuidsInOrder.Count == liveSpawnCount;
        }

        [MenuItem("RCCom/Enemies/Migrate Legacy Enemy Assets (Dry Run)")]
        public static void DryRun()
        {
            Run(dryRun: true);
        }

        [MenuItem("RCCom/Enemies/Migrate Legacy Enemy Assets")]
        public static void MigrateReal()
        {
            Run(dryRun: false);
        }

        private static void Run(bool dryRun)
        {
            string tag = dryRun ? "[Dry Run] " : string.Empty;
            Debug.Log($"[EnemyRecipeMigrator] ===== {(dryRun ? "DRY RUN" : "REAL RUN")} 시작 =====");

            // 1) 레거시 GUID → 슬러그 매핑. 스테이지 에셋의 예전 enemy 참조를 enemyId
            // 문자열로 바꿀 때 이 매핑이 유일한 단서다.
            var guidToSlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (LegacyEnemy legacy in LegacyEnemies)
            {
                string guid = AssetDatabase.AssetPathToGUID(legacy.legacyPath);
                if (string.IsNullOrEmpty(guid))
                {
                    throw new InvalidOperationException($"레거시 적 에셋을 찾지 못했습니다: {legacy.legacyPath}");
                }

                guidToSlug[guid] = legacy.slug;
            }

            // 2) 레시피 JSON 계산 — 읽기만 하는 단계라 Dry Run에서도 실제 내용을 그대로 로그로 보여준다.
            var recipes = new List<(LegacyEnemy legacy, string json)>();
            for (int i = 0; i < LegacyEnemies.Length; i++)
            {
                LegacyEnemy legacy = LegacyEnemies[i];
                EnemyDefinition legacyDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(legacy.legacyPath);
                if (legacyDefinition == null)
                {
                    throw new InvalidOperationException($"레거시 적 에셋을 로드하지 못했습니다: {legacy.legacyPath}");
                }

                EnemyAssetRecipe recipe = BuildRecipe(legacy.slug, i, legacyDefinition);
                string json = JsonUtility.ToJson(recipe, true);
                recipes.Add((legacy, json));

                Debug.Log($"[EnemyRecipeMigrator] {tag}레시피 생성 예정: {RecipeFolder}/{legacy.slug}.json\n{json}");
            }

            // 3) 스테이지 7개의 enemy(GUID) → enemyId(슬러그) 변환 계획.
            var stagePlans = new List<StagePlan>();
            foreach (string stagePath in StagePaths)
            {
                StagePlan plan = BuildStagePlan(stagePath, guidToSlug);
                stagePlans.Add(plan);
                LogStagePlan(plan, tag);
            }

            int inconsistentCount = stagePlans.FindAll(p => !p.IsConsistent).Count;
            if (inconsistentCount > 0 && !dryRun)
            {
                throw new InvalidOperationException(
                    "스테이지 spawn 개수와 추출된 enemy 참조 개수/식별이 일치하지 않는 파일이 있어 마이그레이션을 " +
                    "중단합니다. 콘솔의 [EnemyRecipeMigrator] 오류 로그를 확인하세요.");
            }

            if (dryRun)
            {
                Debug.Log(
                    $"[EnemyRecipeMigrator] ===== DRY RUN 완료 — 어떤 파일도 변경되지 않았습니다. " +
                    $"레시피 {recipes.Count}개 / 스테이지 {stagePlans.Count}개 검토 완료 (불일치 {inconsistentCount}건) =====");
                return;
            }

            // ---- 여기서부터 실제 변경 ----
            EnsureFolder(RecipeFolder);
            foreach ((LegacyEnemy legacy, string json) in recipes)
            {
                File.WriteAllText($"{RecipeFolder}/{legacy.slug}.json", json);
            }
            AssetDatabase.Refresh();
            Debug.Log($"[EnemyRecipeMigrator] 레시피 {recipes.Count}개 작성 완료: {RecipeFolder}");

            foreach (LegacyEnemy legacy in LegacyEnemies)
            {
                string destFolder = $"{OutputRoot}/{legacy.slug}";
                EnsureFolder(destFolder);
                string destPath = $"{destFolder}/EnemyDefinition.asset";
                string moveError = AssetDatabase.MoveAsset(legacy.legacyPath, destPath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    throw new InvalidOperationException(
                        $"EnemyDefinition 이동 실패로 마이그레이션을 중단합니다: {legacy.legacyPath} → {destPath} ({moveError})");
                }

                EnemyDefinition movedDefinition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(destPath);
                AssetDatabase.SetLabels(movedDefinition, new[] { GeneratedLabel });
                Debug.Log($"[EnemyRecipeMigrator] 이동 완료: {legacy.legacyPath} → {destPath} (라벨 {GeneratedLabel} 적용)");
            }

            string rosterMoveError = AssetDatabase.MoveAsset(LegacyRosterPath, NewRosterPath);
            if (!string.IsNullOrEmpty(rosterMoveError))
            {
                throw new InvalidOperationException(
                    $"EnemyRoster 이동 실패로 마이그레이션을 중단합니다: {LegacyRosterPath} → {NewRosterPath} ({rosterMoveError})");
            }

            EnemyRoster roster = AssetDatabase.LoadAssetAtPath<EnemyRoster>(NewRosterPath);
            roster.enemyIds = new List<string>();
            foreach (LegacyEnemy legacy in LegacyEnemies)
            {
                roster.enemyIds.Add(legacy.slug);
            }

            EditorUtility.SetDirty(roster);
            Debug.Log($"[EnemyRecipeMigrator] 이동 완료: {LegacyRosterPath} → {NewRosterPath} (오타/폴더 정정, enemyIds 재기록)");

            foreach (StagePlan plan in stagePlans)
            {
                ApplyStagePlan(plan);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[EnemyRecipeMigrator] ===== REAL RUN 완료 =====");
        }

        private static EnemyAssetRecipe BuildRecipe(string slug, int catalogOrder, EnemyDefinition legacyDefinition)
        {
            EnemyData legacyData = legacyDefinition.data ?? new EnemyData();

            var effectPaths = new List<string>();
            if (legacyDefinition.effects != null)
            {
                foreach (EnemyEffectBase effect in legacyDefinition.effects)
                {
                    if (effect != null)
                    {
                        effectPaths.Add(AssetDatabase.GetAssetPath(effect));
                    }
                }
            }

            return new EnemyAssetRecipe
            {
                enemyId = slug,
                catalogOrder = catalogOrder,
                displayName = legacyData.displayName,
                spritePath = legacyDefinition.sprite != null
                    ? AssetDatabase.GetAssetPath(legacyDefinition.sprite)
                    : string.Empty,
                // 레거시 3종은 스프라이트가 위쪽을 바라보게 그려져 있어 원본 값이 -90이다.
                // 레시피 기본값(90)으로 덮이면 이동 방향과 스프라이트가 어긋나므로 그대로 옮긴다.
                spriteForwardOffsetDegrees = legacyDefinition.spriteForwardOffsetDegrees,
                effectPaths = effectPaths,
                remoteContent = false,
                data = new EnemyData
                {
                    enemyId = slug,
                    displayName = legacyData.displayName,
                    kind = legacyData.kind,
                    maxHealth = legacyData.maxHealth,
                    moveSpeed = legacyData.moveSpeed,
                    contactDamage = legacyData.contactDamage,
                    // 레거시 3종은 전부 근접(접촉 피해) 전용이라 사거리 개념이 없다 — attackRange가
                    // 0이고 attackInterval이 기본값 1인 것은 원본 에셋에 해당 키가 아예 없어서
                    // (필드가 나중에 추가됨) 생기는 결손이 아니라 의도된 값이다(사용자 확인 완료).
                    // ContactDamageEffect가 실제 피해를 담당하므로 이 두 필드는 원거리 공격 로직에서
                    // 참조되지 않는다 — 나중에 "복구"하려 하지 말 것.
                    attackRange = legacyData.attackRange,
                    attackInterval = legacyData.attackInterval,
                    waveCost = legacyData.waveCost,
                    minWave = legacyData.minWave,
                    goldReward = legacyData.goldReward,
                    expReward = legacyData.expReward,
                },
            };
        }

        private static StagePlan BuildStagePlan(string stagePath, Dictionary<string, string> guidToSlug)
        {
            var plan = new StagePlan { stagePath = stagePath };

            string rawText = File.ReadAllText(stagePath);
            foreach (Match match in SpawnEnemyGuidPattern.Matches(rawText))
            {
                string guid = match.Groups[1].Value;
                plan.extractedGuidsInOrder.Add(guid);
                if (guidToSlug.TryGetValue(guid, out string slug))
                {
                    plan.resolvedSlugsInOrder.Add(slug);
                }
                else
                {
                    plan.resolvedSlugsInOrder.Add(null);
                    plan.unresolvedGuids.Add(guid);
                }
            }

            StageDefinition definition = AssetDatabase.LoadAssetAtPath<StageDefinition>(stagePath);
            if (definition != null && definition.waves != null)
            {
                foreach (StageWaveDefinition wave in definition.waves)
                {
                    int count = wave?.spawns != null ? wave.spawns.Count : 0;
                    plan.waveSpawnCounts.Add(count);
                    plan.liveSpawnCount += count;
                }
            }

            return plan;
        }

        private static void LogStagePlan(StagePlan plan, string tag)
        {
            if (plan.unresolvedGuids.Count > 0)
            {
                Debug.LogError(
                    $"[EnemyRecipeMigrator] {tag}{plan.stagePath}: 알 수 없는 enemy GUID {plan.unresolvedGuids.Count}건 " +
                    $"({string.Join(", ", plan.unresolvedGuids)}) — 레거시 3종 이외의 참조는 지원하지 않습니다.");
            }

            if (plan.extractedGuidsInOrder.Count != plan.liveSpawnCount)
            {
                Debug.LogError(
                    $"[EnemyRecipeMigrator] {tag}{plan.stagePath}: 추출된 enemy 참조 {plan.extractedGuidsInOrder.Count}개가 " +
                    $"실제 spawn 개수 {plan.liveSpawnCount}개와 다릅니다 — 순서 대응을 신뢰할 수 없어 이 파일은 건너뜁니다.");
                return;
            }

            int flatIndex = 0;
            for (int waveIndex = 0; waveIndex < plan.waveSpawnCounts.Count; waveIndex++)
            {
                for (int spawnIndex = 0; spawnIndex < plan.waveSpawnCounts[waveIndex]; spawnIndex++)
                {
                    Debug.Log(
                        $"[EnemyRecipeMigrator] {tag}{plan.stagePath} WAVE {waveIndex + 1:00} spawn {spawnIndex + 1}: " +
                        $"{plan.extractedGuidsInOrder[flatIndex]} → enemyId \"{plan.resolvedSlugsInOrder[flatIndex]}\"");
                    flatIndex++;
                }
            }
        }

        private static void ApplyStagePlan(StagePlan plan)
        {
            var stageObject = new SerializedObject(AssetDatabase.LoadAssetAtPath<StageDefinition>(plan.stagePath));
            SerializedProperty waves = stageObject.FindProperty("waves");
            int flatIndex = 0;
            for (int waveIndex = 0; waveIndex < waves.arraySize; waveIndex++)
            {
                SerializedProperty spawns = waves.GetArrayElementAtIndex(waveIndex).FindPropertyRelative("spawns");
                for (int spawnIndex = 0; spawnIndex < spawns.arraySize; spawnIndex++)
                {
                    string slug = plan.resolvedSlugsInOrder[flatIndex];
                    spawns.GetArrayElementAtIndex(spawnIndex).FindPropertyRelative("enemyId").stringValue = slug;
                    flatIndex++;
                }
            }

            stageObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(stageObject.targetObject);
            Debug.Log($"[EnemyRecipeMigrator] 스폰 변환 적용 완료: {plan.stagePath} ({flatIndex}건)");
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
