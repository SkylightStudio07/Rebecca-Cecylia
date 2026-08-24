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
            EnsureOverwriteIsOwned(PrefabPath);
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

                // 기존 프리팹과 구조가 같으면 재생성을 건너뛰어 손으로 튜닝한 값(소팅오더 등)을
                // 보존한다. 구조가 바뀐 경우에만 값을 최대한 이식한 뒤 재생성한다
                // (BuilderPrefabMerge 참고, {{user}} 지적, 2026-08-24).
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (existing != null && BuilderPrefabMerge.HasSameShape(existing, root))
                {
                    Debug.Log($"[AllyUnitViewPrefabBuilder] 구조 변경 없음, 기존 프리팹 값 보존: {PrefabPath}");
                    Validate();
                    return;
                }

                if (existing != null)
                {
                    int mergedCount = BuilderPrefabMerge.CopyTunedValues(existing, root);
                    Debug.LogWarning(
                        $"[AllyUnitViewPrefabBuilder] 구조 변경을 감지해 프리팹을 재생성합니다 " +
                        $"(값 필드 {mergedCount}개 컴포넌트에서 이식): {PrefabPath}");
                }

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

        private static void EnsureOverwriteIsOwned(string path)
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && Array.IndexOf(AssetDatabase.GetLabels(existing), GeneratedLabel) < 0)
            {
                throw new InvalidOperationException($"자동 생성 라벨이 없는 기존 에셋은 덮어쓸 수 없습니다: {path}");
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
