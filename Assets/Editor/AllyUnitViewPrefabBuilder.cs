using System;
using RCCom.Runtime;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 유닛 종류별 프리팹 복제를 막기 위해 AllyUnitView 공용 프리팹 하나만 생성한다.
    /// 스프라이트는 비워 두고 런타임 Bind가 AllyUnitDefinition에서 주입하도록 유지한다.
    ///
    /// 주의 — 이 프리팹은 CombatVfxAssetBuilder도 같이 배선한다(deathExplosionPrefab/
    /// deathKnockbackVisual). 구조 변경으로 BuilderPrefabMerge가 재생성 경로를 타면 Object
    /// Reference는 항상 "이번에 새로 지은 값"을 쓰므로(구조 변경 시 옛 배선을 되살리면 안 되는
    /// BuilderPrefabMerge의 의도적 정책) 이 빌더가 모르는 그 두 필드가 null로 리셋된다 —
    /// 구조가 바뀌는 코드 변경 후에는 반드시 RCCom/Combat VFX/Build Projectile And Hit Spark도
    /// 다시 실행해서 복구해야 한다(직접 겪음, {{user}} 체력바 작업 중, 2026-08-24).
    /// </summary>
    public static class AllyUnitViewPrefabBuilder
    {
        public const string PrefabPath = "Assets/Data/Prefabs/AllyUnitView.prefab";
        private const string GeneratedLabel = "RCCom.GeneratedAllyUnitView";
        // EnemyView_Normal.prefab의 HealthBar가 쓰는 것과 같은 에셋 — 배경(검정 틴트)/채움
        // (흰 틴트) 둘 다 이 한 장을 공유한다(왼쪽 피벗이라 Fill.localScale.x로 줄어든다).
        private const string HealthBarSpritePath = "Assets/Art/UI/HP_Enemy_Bar.png";

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

                AllyUnitView view = root.AddComponent<AllyUnitView>();

                UnitHealthBar healthBar = BuildHealthBarHierarchy(root.transform);
                var serializedView = new SerializedObject(view);
                serializedView.FindProperty("healthBar").objectReferenceValue = healthBar;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

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

        /// <summary>
        /// EnemyView_Normal.prefab의 HealthBar(Background+Fill 두 SpriteRenderer)와 같은 구조를
        /// 코드로 재현한다 — 저쪽은 손으로 만들어져 있어 그대로 복제할 편집기 API가 없어서
        /// 새로 짰다. 위치/스케일은 (0,0,0)/(1,1,1)로 둔다 — AllyUnitView.Bind()가 유닛별
        /// SpriteFit 스케일에 맞춰 역보정해서 매번 다시 계산하므로 여기서 굳이 맞출 필요 없다.
        /// </summary>
        private static UnitHealthBar BuildHealthBarHierarchy(Transform parent)
        {
            Sprite barSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HealthBarSpritePath);
            if (barSprite == null)
            {
                throw new InvalidOperationException($"체력바 스프라이트를 찾지 못했습니다: {HealthBarSpritePath}");
            }

            var healthBarRoot = new GameObject("HealthBar");
            healthBarRoot.transform.SetParent(parent, false);

            var background = new GameObject("Background");
            background.transform.SetParent(healthBarRoot.transform, false);
            SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = barSprite;
            backgroundRenderer.color = Color.black;
            // 아군 스프라이트 자체의 sortingOrder(2)보다 위에 그려져야 한다.
            backgroundRenderer.sortingOrder = 3;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(healthBarRoot.transform, false);
            SpriteRenderer fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = barSprite;
            fillRenderer.color = Color.white;
            fillRenderer.sortingOrder = 4;

            UnitHealthBar healthBar = healthBarRoot.AddComponent<UnitHealthBar>();
            var serializedHealthBar = new SerializedObject(healthBar);
            serializedHealthBar.FindProperty("fillTransform").objectReferenceValue = fill.transform;
            serializedHealthBar.ApplyModifiedPropertiesWithoutUndo();

            return healthBar;
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

            UnitHealthBar healthBar = prefab.GetComponentInChildren<UnitHealthBar>(true);
            if (healthBar == null)
            {
                throw new InvalidOperationException("공용 AllyUnitView 프리팹에 체력바(UnitHealthBar)가 없습니다.");
            }

            var serializedHealthBar = new SerializedObject(healthBar);
            if (serializedHealthBar.FindProperty("fillTransform").objectReferenceValue == null)
            {
                throw new InvalidOperationException("체력바의 fillTransform 배선이 비어 있습니다.");
            }

            var serializedView = new SerializedObject(view);
            if (serializedView.FindProperty("healthBar").objectReferenceValue != healthBar)
            {
                throw new InvalidOperationException("AllyUnitView의 healthBar 슬롯이 끊어져 있습니다.");
            }

            Debug.Log($"[AllyUnitViewPrefabBuilder] 공용 View 프리팹 검증 통과: {PrefabPath}");
        }
    }
}
