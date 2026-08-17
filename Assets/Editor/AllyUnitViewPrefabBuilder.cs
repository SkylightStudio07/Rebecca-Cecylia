using System;
using RCCom.Runtime;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 유닛 종류별 프리팹 복제를 막기 위해 AllyUnitView 공용 프리팹 하나만 생성한다.
    /// 스프라이트는 비워 두고 런타임 Bind가 AllyUnitDefinition에서 주입하도록 유지한다.
    /// </summary>
    public static class AllyUnitViewPrefabBuilder
    {
        public const string PrefabPath = "Assets/Data/Prefabs/AllyUnitView.prefab";
        private const string GeneratedLabel = "RCCom.GeneratedAllyUnitView";

        [MenuItem("RCCom/Ally Units/Build Common View Prefab")]
        public static void Build()
        {
            var root = new GameObject("AllyUnitView");

            try
            {
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = null;

                // 기존 EnemyView와 같은 기본 Sorting Layer/Order를 사용해 같은 경로 위에서
                // 아군만 타일맵 뒤로 숨는 시각적 불일치를 피한다.
                renderer.sortingLayerID = 0;
                renderer.sortingOrder = 2;

                root.AddComponent<AllyUnitView>();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool succeeded);
                if (!succeeded || prefab == null)
                {
                    throw new InvalidOperationException($"공용 AllyUnitView 프리팹 저장에 실패했습니다: {PrefabPath}");
                }

                AssetDatabase.SetLabels(prefab, new[] { GeneratedLabel });
                EditorUtility.SetDirty(prefab);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Validate();
                Debug.Log($"[AllyUnitViewPrefabBuilder] 공용 View 프리팹 생성 완료: {PrefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [MenuItem("RCCom/Ally Units/Validate Common View Prefab")]
        public static void Validate()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"공용 AllyUnitView 프리팹이 없습니다: {PrefabPath}");
            }

            AllyUnitView view = prefab.GetComponent<AllyUnitView>();
            SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
            if (view == null || renderer == null)
            {
                throw new InvalidOperationException("공용 AllyUnitView 프리팹에 AllyUnitView 또는 SpriteRenderer가 없습니다.");
            }

            if (renderer.sprite != null)
            {
                throw new InvalidOperationException("공용 AllyUnitView 프리팹에는 유닛별 스프라이트를 저장하면 안 됩니다.");
            }

            if (prefab.GetComponentsInChildren<AllyUnitView>(true).Length != 1)
            {
                throw new InvalidOperationException("공용 프리팹에는 AllyUnitView가 정확히 하나만 있어야 합니다.");
            }

            Debug.Log($"[AllyUnitViewPrefabBuilder] 공용 View 프리팹 검증 통과: {PrefabPath}");
        }
    }
}
