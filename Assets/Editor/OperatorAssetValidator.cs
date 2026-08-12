using System;
using System.Collections.Generic;
using RCCom.Definitions.Card;
using RCCom.Definitions.Enemy;
using RCCom.Definitions.Operator;
using RCCom.Definitions.Tower;
using RCCom.Effects.Card.Concrete;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 오퍼레이터 패키지의 필수 참조와 Definition/Roster 누락을 한 번에 검사한다.
    /// 타워는 해금 카드에만 연결되어 초기 Roster에 없는 것이 정상일 수 있으므로,
    /// 어느 TowerRoster에도 없고 UnlockTowerCard에도 연결되지 않은 경우만 누락으로 본다.
    /// </summary>
    public static class OperatorAssetValidator
    {
        [MenuItem("RCCom/Operators/Validate Operator Assets")]
        public static void ValidateMenu()
        {
            if (!ValidateAll())
            {
                throw new InvalidOperationException("오퍼레이터/Definition 검증에 실패했습니다. 콘솔 오류를 확인하세요.");
            }
        }

        public static bool ValidateAll(bool logSuccess = true)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            ValidateOperators(errors);
            ValidateTowerRegistration(errors, warnings);
            ValidateEnemyRegistration(warnings);

            foreach (string error in errors)
            {
                Debug.LogError($"[OperatorAssetValidator] {error}");
            }

            foreach (string warning in warnings)
            {
                Debug.LogWarning($"[OperatorAssetValidator] {warning}");
            }

            if (errors.Count == 0 && logSuccess)
            {
                Debug.Log($"[OperatorAssetValidator] 필수 참조 검증 통과 (경고 {warnings.Count}건)");
            }

            return errors.Count == 0;
        }

        private static void ValidateOperators(List<string> errors)
        {
            List<OperatorDefinition> definitions = FindAssets<OperatorDefinition>();
            if (definitions.Count == 0)
            {
                errors.Add("OperatorDefinition 에셋이 하나도 없습니다.");
                return;
            }

            var operatorIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (OperatorDefinition definition in definitions)
            {
                string path = AssetDatabase.GetAssetPath(definition);

                if (string.IsNullOrWhiteSpace(definition.operatorId))
                {
                    errors.Add($"operatorId가 비어 있습니다: {path}");
                }
                else if (!operatorIds.Add(definition.operatorId))
                {
                    errors.Add($"operatorId가 중복됩니다: {definition.operatorId}");
                }

                if (string.IsNullOrWhiteSpace(definition.displayName))
                {
                    errors.Add($"displayName이 비어 있습니다: {path}");
                }

                if (definition.selectionPortrait == null)
                {
                    errors.Add($"선택 화면 초상화가 연결되지 않았습니다: {path}");
                }

                if (definition.playerData == null || definition.playerData.maxHealth <= 0f ||
                    definition.playerData.moveSpeed <= 0f || definition.playerData.attackInterval <= 0f)
                {
                    errors.Add($"PlayerData의 필수 수치가 유효하지 않습니다: {path}");
                }

                if (definition.towerRoster == null)
                {
                    errors.Add($"TowerRoster가 연결되지 않았습니다: {path}");
                }
                else
                {
                    ValidateTowerRoster(definition.towerRoster, errors);
                }

                if (definition.cardRoster == null)
                {
                    errors.Add($"CardRoster가 연결되지 않았습니다: {path}");
                }
                else
                {
                    ValidateCardRoster(definition.cardRoster, errors);
                }

                if (definition.dialogueSet == null || definition.dialogueSet.idleSprite == null)
                {
                    errors.Add($"DialogueSet 또는 기본 초상화가 연결되지 않았습니다: {path}");
                }

                if (definition.requiredBestWave < 0)
                {
                    errors.Add($"requiredBestWave가 음수입니다: {path}");
                }
            }
        }

        private static void ValidateTowerRoster(TowerRoster roster, List<string> errors)
        {
            string path = AssetDatabase.GetAssetPath(roster);
            if (roster.towers == null || roster.towers.Count == 0)
            {
                errors.Add($"TowerRoster가 비어 있습니다: {path}");
                return;
            }

            var towerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TowerDefinition tower in roster.towers)
            {
                if (tower == null)
                {
                    errors.Add($"TowerRoster에 null 항목이 있습니다: {path}");
                    continue;
                }

                string towerId = tower.Data?.towerId;
                if (string.IsNullOrWhiteSpace(towerId))
                {
                    errors.Add($"towerId가 비어 있습니다: {AssetDatabase.GetAssetPath(tower)}");
                }
                else if (!towerIds.Add(towerId))
                {
                    errors.Add($"한 TowerRoster 안에서 towerId가 중복됩니다: {towerId} ({path})");
                }
            }
        }

        private static void ValidateCardRoster(CardRoster roster, List<string> errors)
        {
            string path = AssetDatabase.GetAssetPath(roster);
            if (roster.cards == null || roster.cards.Count == 0)
            {
                errors.Add($"CardRoster가 비어 있습니다: {path}");
                return;
            }

            var cards = new HashSet<UnityEngine.Object>();
            foreach (UnityEngine.Object card in roster.cards)
            {
                if (card == null)
                {
                    errors.Add($"CardRoster에 null 항목이 있습니다: {path}");
                }
                else if (!cards.Add(card))
                {
                    errors.Add($"CardRoster에 같은 카드가 중복 등록됐습니다: {AssetDatabase.GetAssetPath(card)} ({path})");
                }
            }
        }

        private static void ValidateTowerRegistration(List<string> errors, List<string> warnings)
        {
            List<TowerDefinition> definitions = FindAssets<TowerDefinition>();
            List<TowerRoster> rosters = FindAssets<TowerRoster>();
            List<UnlockTowerCard> unlockCards = FindAssets<UnlockTowerCard>();
            var registered = new HashSet<TowerDefinition>();

            foreach (TowerRoster roster in rosters)
            {
                if (roster.towers == null)
                {
                    continue;
                }

                foreach (TowerDefinition definition in roster.towers)
                {
                    if (definition != null)
                    {
                        registered.Add(definition);
                    }
                }
            }

            foreach (UnlockTowerCard card in unlockCards)
            {
                var serializedCard = new SerializedObject(card);
                TowerDefinition definition = serializedCard.FindProperty("unlockDefinition").objectReferenceValue as TowerDefinition;
                if (definition == null)
                {
                    errors.Add($"UnlockTowerCard의 unlockDefinition이 비어 있습니다: {AssetDatabase.GetAssetPath(card)}");
                }
                else
                {
                    registered.Add(definition);
                }
            }

            foreach (TowerDefinition definition in definitions)
            {
                if (!registered.Contains(definition))
                {
                    warnings.Add($"어느 TowerRoster/UnlockTowerCard에도 등록되지 않은 TowerDefinition: {AssetDatabase.GetAssetPath(definition)}");
                }
            }
        }

        private static void ValidateEnemyRegistration(List<string> warnings)
        {
            List<EnemyDefinition> definitions = FindAssets<EnemyDefinition>();
            List<EnemyRoster> rosters = FindAssets<EnemyRoster>();
            var registered = new HashSet<EnemyDefinition>();

            foreach (EnemyRoster roster in rosters)
            {
                if (roster.enemies == null)
                {
                    continue;
                }

                foreach (EnemyDefinition definition in roster.enemies)
                {
                    if (definition != null)
                    {
                        registered.Add(definition);
                    }
                }
            }

            foreach (EnemyDefinition definition in definitions)
            {
                if (!registered.Contains(definition))
                {
                    warnings.Add($"어느 EnemyRoster에도 등록되지 않은 EnemyDefinition: {AssetDatabase.GetAssetPath(definition)}");
                }
            }
        }

        private static List<T> FindAssets<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var assets = new List<T>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }
    }
}
