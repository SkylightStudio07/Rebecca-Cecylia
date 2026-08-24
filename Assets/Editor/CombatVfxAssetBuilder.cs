using System;
using RCCom.Effects.Tower.Concrete;
using RCCom.Effects.Unit.Concrete;
using RCCom.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 전투 투사체 스프라이트 임포트부터 공용 프리팹과 세 공격 효과 SO 배선까지 한 번에
    /// 재현한다. 프리팹을 효과별로 복제하지 않는 이유는 색·동작 차이가 필요해질 때도
    /// SO의 프리팹 슬롯 조립만 바꿔 전투 코드는 그대로 유지하기 위해서다.
    /// </summary>
    public static class CombatVfxAssetBuilder
    {
        public const string ProjectileSpritePath =
            "Assets/Art/VFX/fake-projectile-orange.png";
        public const string FakeProjectilePrefabPath =
            "Assets/Data/Prefabs/VFX/FakeProjectile.prefab";
        public const string HitSparkPrefabPath =
            "Assets/Data/Prefabs/VFX/ParticleBurst_HitSpark.prefab";

        private const string PrefabFolder = "Assets/Data/Prefabs/VFX";
        private const string GeneratedLabel = "RCCom.GeneratedCombatVfx";
        private const string DamageEffectPath =
            "Assets/Data/Effects/Tower/DamageEffect_Default.asset";
        private const string SplashDamageEffectPath =
            "Assets/Data/Effects/Tower/Splash Damage Effect.asset";
        private const string PierceDamageEffectPath =
            "Assets/Data/Effects/Tower/Pierce Damage Effect.asset";
        private const string PoisonDamageEffectPath =
            "Assets/Data/Effects/Tower/Poison Damage Effect.asset";
        private const string BasicAttackEffectPath =
            "Assets/Data/Effects/Unit/BasicAttackEffect.asset";
        private const string PlayerScenePath = "Assets/Scenes/DefenseScene.unity";

        [MenuItem("RCCom/Combat VFX/Build Projectile And Hit Spark")]
        public static void BuildAndConnect()
        {
            ConfigureProjectileSprite();
            EnsureFolder(PrefabFolder);

            GameObject hitSparkPrefab = BuildHitSparkPrefab();
            GameObject fakeProjectilePrefab = BuildFakeProjectilePrefab(hitSparkPrefab);
            ConnectEffect<DamageEffect>(DamageEffectPath, fakeProjectilePrefab);
            ConnectEffect<SplashDamageEffect>(SplashDamageEffectPath, fakeProjectilePrefab);
            ConnectEffect<PierceDamageEffect>(PierceDamageEffectPath, fakeProjectilePrefab);
            ConnectEffect<PoisonDamageEffect>(PoisonDamageEffectPath, fakeProjectilePrefab);
            ConnectEffect<BasicAttackEffect>(BasicAttackEffectPath, fakeProjectilePrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ConnectPlayerController(fakeProjectilePrefab);
            Validate();
            Debug.Log("[CombatVfxAssetBuilder] 투사체·히트 스파크 생성 및 네 공격 효과 + 아군 기본 공격 + 플레이어 배선 완료");
        }

        private static void ConfigureProjectileSprite()
        {
            AssetDatabase.ImportAsset(ProjectileSpritePath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(ProjectileSpritePath) is not TextureImporter importer)
            {
                throw new InvalidOperationException(
                    $"투사체 이미지를 TextureImporter로 열 수 없습니다: {ProjectileSpritePath}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            // 피벗 데이터는 importer 내부 데이터이므로 Sprite Editor provider의 지원 여부를
            // 확인한 뒤에만 수정한다. 지원하지 않는 importer에 강제로 쓰면 메타데이터가
            // 손상될 수 있어 즉시 중단한다.
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            ISpriteEditorDataProvider provider =
                factories.GetSpriteEditorDataProviderFromObject(importer);
            if (provider == null)
            {
                throw new InvalidOperationException(
                    "투사체 importer가 Sprite Editor data provider를 제공하지 않습니다.");
            }

            provider.InitSpriteEditorDataProvider();
            ISpriteFrameEditCapability frameEdit =
                provider.GetDataProvider<ISpriteFrameEditCapability>();
            if (frameEdit == null ||
                !frameEdit.GetEditCapability().HasCapability(EEditCapability.EditPivot))
            {
                throw new InvalidOperationException(
                    "투사체 importer가 피벗 편집을 지원하지 않아 작업을 중단했습니다.");
            }

            SpriteRect[] spriteRects = provider.GetSpriteRects();
            if (spriteRects == null || spriteRects.Length != 1)
            {
                throw new InvalidOperationException(
                    "투사체 스프라이트는 중앙 피벗을 가진 Single Sprite여야 합니다.");
            }

            spriteRects[0].alignment = (int)SpriteAlignment.Center;
            spriteRects[0].pivot = new Vector2(0.5f, 0.5f);
            provider.SetSpriteRects(spriteRects);
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static GameObject BuildHitSparkPrefab()
        {
            EnsureOverwriteIsOwned(HitSparkPrefabPath);
            var root = new GameObject("ParticleBurst_HitSpark");

            try
            {
                ParticleSystem particleSystem = root.AddComponent<ParticleSystem>();
                ConfigureHitSpark(particleSystem);
                root.AddComponent<ParticleBurst>();

                GameObject prefab = SaveOwnedPrefab(root, HitSparkPrefabPath);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureHitSpark(ParticleSystem particleSystem)
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.18f, 0.02f, 1f),
                new Color(1f, 0.9f, 0.2f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 32;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)8, (short)12)
            });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.04f;
            shape.arc = 360f;

            var lifetimeGradient = new Gradient();
            lifetimeGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.35f), 0f),
                    new GradientColorKey(new Color(1f, 0.12f, 0.01f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(lifetimeGradient);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.8f;
            renderer.velocityScale = 0.12f;
            renderer.sortingOrder = 7;
            Material defaultParticleMaterial =
                AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            if (defaultParticleMaterial == null)
            {
                throw new InvalidOperationException("Unity 기본 파티클 Material을 찾지 못했습니다.");
            }

            renderer.sharedMaterial = defaultParticleMaterial;
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static GameObject BuildFakeProjectilePrefab(GameObject hitSparkPrefab)
        {
            EnsureOverwriteIsOwned(FakeProjectilePrefabPath);
            Sprite projectileSprite =
                AssetDatabase.LoadAssetAtPath<Sprite>(ProjectileSpritePath);
            if (projectileSprite == null)
            {
                throw new InvalidOperationException(
                    $"임포트된 투사체 Sprite를 찾을 수 없습니다: {ProjectileSpritePath}");
            }

            var root = new GameObject("FakeProjectile");
            try
            {
                root.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = projectileSprite;
                renderer.sortingOrder = 6;

                FakeProjectile projectile = root.AddComponent<FakeProjectile>();
                var serializedProjectile = new SerializedObject(projectile);
                serializedProjectile.FindProperty("hitSparkPrefab").objectReferenceValue =
                    hitSparkPrefab;
                serializedProjectile.ApplyModifiedPropertiesWithoutUndo();

                return SaveOwnedPrefab(root, FakeProjectilePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject SaveOwnedPrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool succeeded);
            if (!succeeded || prefab == null)
            {
                throw new InvalidOperationException($"전투 VFX 프리팹 저장에 실패했습니다: {path}");
            }

            AssetDatabase.SetLabels(prefab, new[] { GeneratedLabel });
            EditorUtility.SetDirty(prefab);
            return prefab;
        }

        private static void ConnectEffect<TEffect>(string effectPath, GameObject prefab)
            where TEffect : ScriptableObject
        {
            TEffect effect = AssetDatabase.LoadAssetAtPath<TEffect>(effectPath);
            if (effect == null)
            {
                throw new InvalidOperationException(
                    $"배선할 공격 효과 SO를 찾지 못했습니다: {effectPath}");
            }

            var serializedEffect = new SerializedObject(effect);
            SerializedProperty property =
                serializedEffect.FindProperty("fakeProjectilePrefab");
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"공격 효과에 fakeProjectilePrefab 슬롯이 없습니다: {effectPath}");
            }

            property.objectReferenceValue = prefab;
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
        }

        /// <summary>
        /// PlayerController는 프리팹이 아니라 DefenseScene에 직접 배치된 씬 오브젝트라 SO 배선과
        /// 달리 씬을 열어 컴포넌트를 찾아야 한다. 원래 열려 있던 씬 목록을 기억해뒀다가 끝나면
        /// 되돌려, 이 메뉴 실행이 에디터에 열려 있던 작업 씬을 조용히 바꿔버리지 않게 한다.
        /// </summary>
        private static void ConnectPlayerController(GameObject prefab)
        {
            string[] originallyOpenScenePaths = GetOpenScenePaths();
            bool alreadyOpen = Array.IndexOf(originallyOpenScenePaths, PlayerScenePath) >= 0;

            Scene scene = alreadyOpen
                ? SceneManager.GetSceneByPath(PlayerScenePath)
                : EditorSceneManager.OpenScene(PlayerScenePath, OpenSceneMode.Additive);

            try
            {
                PlayerController player = FindPlayerController(scene);
                if (player == null)
                {
                    throw new InvalidOperationException(
                        $"{PlayerScenePath}에서 PlayerController를 찾지 못했습니다.");
                }

                var serializedPlayer = new SerializedObject(player);
                SerializedProperty property = serializedPlayer.FindProperty("fakeProjectilePrefab");
                if (property == null)
                {
                    throw new InvalidOperationException("PlayerController에 fakeProjectilePrefab 슬롯이 없습니다.");
                }

                property.objectReferenceValue = prefab;
                serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!alreadyOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static PlayerController FindPlayerController(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                PlayerController player = root.GetComponentInChildren<PlayerController>(true);
                if (player != null)
                {
                    return player;
                }
            }

            return null;
        }

        private static string[] GetOpenScenePaths()
        {
            var paths = new string[SceneManager.sceneCount];
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = SceneManager.GetSceneAt(i).path;
            }

            return paths;
        }

        [MenuItem("RCCom/Combat VFX/Validate Projectile And Hit Spark")]
        public static void Validate()
        {
            ValidateSpriteImport();

            GameObject hitSparkPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(HitSparkPrefabPath);
            ParticleBurst burst = hitSparkPrefab != null
                ? hitSparkPrefab.GetComponent<ParticleBurst>()
                : null;
            ParticleSystem particleSystem = hitSparkPrefab != null
                ? hitSparkPrefab.GetComponent<ParticleSystem>()
                : null;
            if (burst == null || particleSystem == null || particleSystem.main.loop ||
                particleSystem.main.playOnAwake ||
                particleSystem.main.stopAction != ParticleSystemStopAction.None ||
                particleSystem.emission.burstCount < 1)
            {
                throw new InvalidOperationException(
                    "ParticleBurst 히트 스파크 프리팹 설정이 올바르지 않습니다.");
            }

            GameObject fakeProjectilePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(FakeProjectilePrefabPath);
            FakeProjectile projectile = fakeProjectilePrefab != null
                ? fakeProjectilePrefab.GetComponent<FakeProjectile>()
                : null;
            SpriteRenderer renderer = fakeProjectilePrefab != null
                ? fakeProjectilePrefab.GetComponent<SpriteRenderer>()
                : null;
            if (projectile == null || renderer == null || renderer.sprite == null)
            {
                throw new InvalidOperationException(
                    "FakeProjectile 프리팹의 컴포넌트 또는 Sprite 연결이 올바르지 않습니다.");
            }

            var serializedProjectile = new SerializedObject(projectile);
            if (serializedProjectile.FindProperty("hitSparkPrefab").objectReferenceValue !=
                hitSparkPrefab)
            {
                throw new InvalidOperationException(
                    "FakeProjectile의 hitSparkPrefab 슬롯이 끊어져 있습니다.");
            }

            ValidateEffect<DamageEffect>(DamageEffectPath, fakeProjectilePrefab);
            ValidateEffect<SplashDamageEffect>(SplashDamageEffectPath, fakeProjectilePrefab);
            ValidateEffect<PierceDamageEffect>(PierceDamageEffectPath, fakeProjectilePrefab);
            ValidateEffect<PoisonDamageEffect>(PoisonDamageEffectPath, fakeProjectilePrefab);
            ValidateEffect<BasicAttackEffect>(BasicAttackEffectPath, fakeProjectilePrefab);
            ValidatePlayerController(fakeProjectilePrefab);
            Debug.Log("[CombatVfxAssetBuilder] 투사체·히트 스파크 에셋 검증 통과");
        }

        private static void ValidatePlayerController(GameObject expectedPrefab)
        {
            string[] originallyOpenScenePaths = GetOpenScenePaths();
            bool alreadyOpen = Array.IndexOf(originallyOpenScenePaths, PlayerScenePath) >= 0;

            Scene scene = alreadyOpen
                ? SceneManager.GetSceneByPath(PlayerScenePath)
                : EditorSceneManager.OpenScene(PlayerScenePath, OpenSceneMode.Additive);

            try
            {
                PlayerController player = FindPlayerController(scene);
                if (player == null)
                {
                    throw new InvalidOperationException(
                        $"{PlayerScenePath}에서 PlayerController를 찾지 못했습니다.");
                }

                var serializedPlayer = new SerializedObject(player);
                SerializedProperty property = serializedPlayer.FindProperty("fakeProjectilePrefab");
                if (property == null || property.objectReferenceValue != expectedPrefab)
                {
                    throw new InvalidOperationException(
                        "PlayerController의 fakeProjectilePrefab 배선이 올바르지 않습니다.");
                }
            }
            finally
            {
                if (!alreadyOpen)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateSpriteImport()
        {
            if (AssetImporter.GetAtPath(ProjectileSpritePath) is not TextureImporter importer ||
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !importer.alphaIsTransparency || importer.mipmapEnabled)
            {
                throw new InvalidOperationException(
                    "투사체 스프라이트 임포트 설정이 올바르지 않습니다.");
            }
        }

        private static void ValidateEffect<TEffect>(string path, GameObject expectedPrefab)
            where TEffect : ScriptableObject
        {
            TEffect effect = AssetDatabase.LoadAssetAtPath<TEffect>(path);
            if (effect == null)
            {
                throw new InvalidOperationException($"공격 효과 SO가 없습니다: {path}");
            }

            var serializedEffect = new SerializedObject(effect);
            SerializedProperty property = serializedEffect.FindProperty("fakeProjectilePrefab");
            if (property == null || property.objectReferenceValue != expectedPrefab)
            {
                throw new InvalidOperationException(
                    $"공격 효과의 fakeProjectilePrefab 배선이 올바르지 않습니다: {path}");
            }
        }

        private static void EnsureOverwriteIsOwned(string path)
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && Array.IndexOf(AssetDatabase.GetLabels(existing), GeneratedLabel) < 0)
            {
                throw new InvalidOperationException(
                    $"자동 생성 라벨이 없는 기존 에셋은 덮어쓸 수 없습니다: {path}");
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
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
