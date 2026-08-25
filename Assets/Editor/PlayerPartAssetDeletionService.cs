using System;
using System.Collections.Generic;
using RCCom.Definitions.PlayerPart;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 파츠 레시피와 자동 생성물(Definition·Effect)을 한 트랜잭션처럼 정리한다.
    /// EnemyAssetDeletionService와 같은 결로, 레시피만 지우면 카탈로그가 고아 참조를
    /// 들고 있게 되는 상황을 막는다. 파츠는 적과 달리 개별 Addressables 그룹이 없고
    /// (전체가 PlayerParts-Local 카탈로그 하나에 묶임) 스테이지 편성 참조도 없다.
    /// </summary>
    public static class PlayerPartAssetDeletionService
    {
        public static void Delete(string partId)
        {
            if (string.IsNullOrWhiteSpace(partId))
            {
                throw new ArgumentException("삭제할 partId가 비어 있습니다.", nameof(partId));
            }

            PlayerPartAssetRecipe recipe = FindRecipe(partId, out string recipePath);
            if (recipe == null)
            {
                throw new InvalidOperationException($"레시피를 찾지 못했습니다: {partId}");
            }

            if (recipe.grade == PlayerPartGrade.Common)
            {
                throw new InvalidOperationException(
                    $"Common 등급 파츠는 슬롯 폴백이라 삭제할 수 없습니다: {partId}");
            }

            DeleteEffectAssets(recipe);
            DeleteAssetIfExists(PlayerPartAssetBuilder.GetDefinitionPath(partId));
            AssetDatabase.DeleteAsset(recipePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 남은 레시피를 정본으로 카탈로그를 다시 조립해, 삭제된 항목의 참조가
            // 카탈로그나 Addressables에 남지 않게 한다.
            PlayerPartAssetBuilder.BuildAll();
            Debug.Log($"[PlayerPartAssetDeletionService] 파츠 삭제 완료: {partId} (상점 아이콘 원본은 보존)");
        }

        private static PlayerPartAssetRecipe FindRecipe(string partId, out string foundPath)
        {
            foundPath = null;
            string[] guids = AssetDatabase.FindAssets(
                "t:TextAsset", new[] { PlayerPartAssetBuilder.RecipeFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                PlayerPartAssetRecipe recipe = text == null
                    ? null
                    : JsonUtility.FromJson<PlayerPartAssetRecipe>(text.text);
                if (recipe != null && string.Equals(recipe.partId, partId, StringComparison.Ordinal))
                {
                    foundPath = path;
                    return recipe;
                }
            }

            return null;
        }

        private static void DeleteEffectAssets(PlayerPartAssetRecipe recipe)
        {
            if (recipe.effects == null)
            {
                return;
            }

            for (int index = 0; index < recipe.effects.Count; index++)
            {
                DeleteAssetIfExists($"{PlayerPartAssetBuilder.EffectFolder}/{recipe.partId}-effect-{index}.asset");
            }
        }

        private static void DeleteAssetIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
