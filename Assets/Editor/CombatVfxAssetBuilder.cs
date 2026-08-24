using System;
using System.IO;
using RCCom.Effects.Tower.Concrete;
using RCCom.Effects.Unit.Concrete;
using RCCom.Runtime;
using RCCom.Runtime.Visuals;
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
        public const string HitSparkMaterialPath =
            "Assets/Data/Prefabs/VFX/HitSpark_Additive.mat";
        public const string DeathBurstPrefabPath =
            "Assets/Data/Prefabs/VFX/ParticleBurst_DeathBurst.prefab";
        public const string ShockwaveRingPrefabPath =
            "Assets/Data/Prefabs/VFX/ShockwaveRing.prefab";
        public const string ScorchDecalPrefabPath =
            "Assets/Data/Prefabs/VFX/ScorchDecal.prefab";
        public const string ScorchTexturePath = "Assets/Art/VFX/scorch-decal.png";
        public const string ShockwaveRingVisualEffectPath =
            "Assets/Data/Effects/Tower/Visual/ShockwaveRing_Splash.asset";

        private const string RangePulseAuraMaterialPath =
            "Assets/Data/Effects/Unit/Visual/RangePulseAura.mat";
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
        private const string EnemyViewPrefabPath =
            "Assets/Data/Prefabs/EnemyView_Normal.prefab";
        private const string AllyUnitViewPrefabPath =
            "Assets/Data/Prefabs/AllyUnitView.prefab";
        private const string PlayerScenePath = "Assets/Scenes/DefenseScene.unity";

        [MenuItem("RCCom/Combat VFX/Build Projectile And Hit Spark")]
        public static void BuildAndConnect()
        {
            ConfigureProjectileSprite();
            EnsureFolder(PrefabFolder);

            GameObject hitSparkPrefab = BuildHitSparkPrefab();
            GameObject fakeProjectilePrefab = BuildFakeProjectilePrefab(hitSparkPrefab);
            GameObject deathBurstPrefab = BuildDeathBurstPrefab();
            GameObject shockwaveRingPrefab = BuildShockwaveRingPrefab();
            GameObject scorchDecalPrefab = BuildScorchDecalPrefab();
            ShockwaveRingVisualEffect shockwaveVisual = BuildShockwaveRingVisualEffect();
            ConnectEffect<DamageEffect>(DamageEffectPath, fakeProjectilePrefab);
            ConnectEffect<PierceDamageEffect>(PierceDamageEffectPath, fakeProjectilePrefab);
            ConnectEffect<PoisonDamageEffect>(PoisonDamageEffectPath, fakeProjectilePrefab);
            ConnectEffect<BasicAttackEffect>(BasicAttackEffectPath, fakeProjectilePrefab);
            ConnectSplashEffect(
                fakeProjectilePrefab, deathBurstPrefab, shockwaveRingPrefab, scorchDecalPrefab, shockwaveVisual);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ConnectPlayerController(fakeProjectilePrefab);
            ConnectPrefabField<EnemyView>(EnemyViewPrefabPath, "deathParticlePrefab", deathBurstPrefab);
            ConnectPrefabField<AllyUnitView>(AllyUnitViewPrefabPath, "deathParticlePrefab", deathBurstPrefab);
            Validate();
            Debug.Log("[CombatVfxAssetBuilder] 투사체·히트 스파크·사망 버스트·충격파 링·그을림 자국 생성 및 " +
                      "네 공격 효과 + 스플래시 착탄 연출 + 아군 기본 공격 + 플레이어 + 적/아군 사망 연출 배선 완료");
        }

        /// <summary>
        /// SplashDamageEffect만 fakeProjectilePrefab 외에 착탄 연출 3종(폭발 버스트/충격파 링/
        /// 그을림 자국) 슬롯을 추가로 갖고 있어 공용 ConnectEffect&lt;TEffect&gt;로 못 묶는다.
        /// </summary>
        private static void ConnectSplashEffect(
            GameObject fakeProjectilePrefab,
            GameObject explosionBurstPrefab,
            GameObject shockwaveRingPrefab,
            GameObject scorchDecalPrefab,
            ShockwaveRingVisualEffect shockwaveVisual)
        {
            var effect = AssetDatabase.LoadAssetAtPath<SplashDamageEffect>(SplashDamageEffectPath);
            if (effect == null)
            {
                throw new InvalidOperationException(
                    $"배선할 공격 효과 SO를 찾지 못했습니다: {SplashDamageEffectPath}");
            }

            var serializedEffect = new SerializedObject(effect);
            SetObjectReference(serializedEffect, "fakeProjectilePrefab", fakeProjectilePrefab);
            SetObjectReference(serializedEffect, "explosionBurstPrefab", explosionBurstPrefab);
            SetObjectReference(serializedEffect, "shockwaveRingPrefab", shockwaveRingPrefab);
            SetObjectReference(serializedEffect, "scorchDecalPrefab", scorchDecalPrefab);
            SetObjectReference(serializedEffect, "shockwaveVisual", shockwaveVisual);
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
        }

        /// <summary>
        /// 충격파 링의 색/스트로크/글로우/글로시/불투명도/확산 시간을 담는 SO. 아군 사거리
        /// 오라(RangePulseVisualEffect)가 이미 확립한 "데이터 기반 SO" 패턴을 그대로 따른다 —
        /// 이 SO를 두는 이유 자체가 "코드/빌더 대신 인스펙터에서 값을 조정할 수 있게"이므로,
        /// 프리팹류(BuildHitSparkPrefab 등, EnsureOverwriteIsOwned로 매번 완전히 재생성됨)와는
        /// 달리 **이미 에셋이 있으면 값을 절대 건드리지 않는다** — 최초 생성 시 1회만 기본값을
        /// 심고, 그 다음부터는 개발자/디자이너가 인스펙터에서 바꾼 값을 그대로 존중한다. 빌더를
        /// 다시 돌려도 튜닝값이 덮어씌워지면 안 된다는 지적을 반영({{user}}, 2026-08-24).
        /// 기본 색은 빨간색 계열(스플래시 폭발 테마).
        /// </summary>
        private static ShockwaveRingVisualEffect BuildShockwaveRingVisualEffect()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ShockwaveRingVisualEffect>(ShockwaveRingVisualEffectPath);
            if (asset != null)
            {
                return asset;
            }

            EnsureFolder(Path.GetDirectoryName(ShockwaveRingVisualEffectPath)?.Replace('\\', '/'));

            asset = ScriptableObject.CreateInstance<ShockwaveRingVisualEffect>();
            var serializedAsset = new SerializedObject(asset);
            serializedAsset.FindProperty("color").colorValue = new Color(1f, 0.28f, 0.12f, 0.9f);
            serializedAsset.FindProperty("expandDuration").floatValue = 0.18f;
            serializedAsset.FindProperty("strokeWidth").floatValue = 0.02f;
            serializedAsset.FindProperty("glowIntensity").floatValue = 1.1f;
            serializedAsset.FindProperty("glossIntensity").floatValue = 0.3f;
            serializedAsset.FindProperty("opacity").floatValue = 0.9f;
            serializedAsset.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(asset, ShockwaveRingVisualEffectPath);
            return asset;
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name}에 {propertyName} 슬롯이 없습니다.");
            }

            property.objectReferenceValue = value;
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

        /// <summary>
        /// 원래 값(크기 0.04~0.11, 수명 0.1~0.2초, Alpha Blend 기본 머티리얼)이 실제 유닛 스케일
        /// 대비 너무 작고 옅게 보인다는 피드백을 받아, FakeProjectile 스프라이트 실제 크기
        /// (localScale 0.18 적용 후 약 0.46×0.92유닛, Sprite bounds/스케일로 직접 확인)를 기준
        /// 삼아 재조정했다({{user}} 확인, 2026-08-24). Alpha Blend 기본 머티리얼도 흐릿하게
        /// 보이는 원인 중 하나라 판단해 Additive 전용 머티리얼로 교체한다(§BuildHitSparkMaterial).
        /// </summary>
        private static void ConfigureHitSpark(ParticleSystem particleSystem)
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.32f);
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
                new ParticleSystem.Burst(0f, (short)10, (short)16)
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
            renderer.lengthScale = 1.4f;
            renderer.velocityScale = 0.08f;
            renderer.sortingOrder = 7;
            renderer.sharedMaterial = BuildHitSparkMaterial();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static GameObject BuildDeathBurstPrefab()
        {
            EnsureOverwriteIsOwned(DeathBurstPrefabPath);
            var root = new GameObject("ParticleBurst_DeathBurst");

            try
            {
                ParticleSystem particleSystem = root.AddComponent<ParticleSystem>();
                ConfigureDeathBurst(particleSystem);
                root.AddComponent<ParticleBurst>();

                GameObject prefab = SaveOwnedPrefab(root, DeathBurstPrefabPath);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 히트 스파크보다 크고·오래가고·중력으로 흩날리는 사망 전용 버스트. 같은 Additive
        /// 머티리얼을 재사용해(BuildHitSparkMaterial) 머티리얼을 또 만들지 않는다. 색상은
        /// 히트 스파크와 같은 팔레트를 써서 "피격→사망"이 같은 시각 언어로 읽히게 한다
        /// (VFX_전투_연출_설계안.md §4).
        /// </summary>
        private static void ConfigureDeathBurst(ParticleSystem particleSystem)
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.18f, 0.02f, 1f),
                new Color(1f, 0.9f, 0.2f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 32;
            main.stopAction = ParticleSystemStopAction.None;
            main.gravityModifier = 0.6f;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)14, (short)22)
            });

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
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
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 8;
            renderer.sharedMaterial = BuildHitSparkMaterial();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        /// <summary>
        /// 스플래시 착탄 충격파 링. 새 셰이더/머티리얼을 만들지 않고 아군 오라 파동에 이미 쓰이는
        /// RangePulseAura.shader 머티리얼을 그대로 참조한다(설계안 §3-③, §1 — "이미 있는 인프라
        /// 재사용" 원칙). Quad 프리미티브의 기본 Collider는 필요 없어 제거한다.
        /// </summary>
        private static GameObject BuildShockwaveRingPrefab()
        {
            EnsureOverwriteIsOwned(ShockwaveRingPrefabPath);
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Quad);
            root.name = "ShockwaveRing";

            try
            {
                Collider collider = root.GetComponent<Collider>();
                if (collider != null)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }

                Material rangePulseMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>(RangePulseAuraMaterialPath);
                if (rangePulseMaterial == null)
                {
                    throw new InvalidOperationException(
                        $"기존 RangePulseAura 머티리얼을 찾지 못했습니다: {RangePulseAuraMaterialPath}");
                }

                MeshRenderer renderer = root.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = rangePulseMaterial;
                // 지면 타일맵(DefenseScene의 Ground Tilemap, 같은 Default 레이어에 sortingOrder 0)
                // 위에 그려져야 보인다 — 음수로 두면 지면에 완전히 가려져 아예 안 보인다(실전 확인됨).
                // 그을림 자국(1)보다는 위, 캐릭터 스프라이트/히트 스파크(7)/사망 버스트(8)보다는 아래.
                renderer.sortingOrder = 2;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                root.AddComponent<ShockwaveRing>();

                return SaveOwnedPrefab(root, ShockwaveRingPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildScorchDecalPrefab()
        {
            EnsureOverwriteIsOwned(ScorchDecalPrefabPath);
            ConfigureScorchSprite();
            Sprite scorchSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ScorchTexturePath);
            if (scorchSprite == null)
            {
                throw new InvalidOperationException($"그을림 자국 Sprite를 찾을 수 없습니다: {ScorchTexturePath}");
            }

            var root = new GameObject("ScorchDecal");
            try
            {
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = scorchSprite;
                // 지면 타일맵(sortingOrder 0)보다는 위, 충격파 링(2)보다는 아래 — 바닥에 눌러 붙은
                // 자국이라 링 밑에서 은은하게 보여야 한다. 지면과 같은 레이어인데 음수를 주면
                // 지면에 완전히 가려져 아예 안 보인다(실전 확인됨 — 애초의 실수).
                renderer.sortingOrder = 1;

                root.AddComponent<ScorchDecal>();

                return SaveOwnedPrefab(root, ScorchDecalPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 그을림 자국용 소프트 원형 그라디언트 텍스처를 절차적으로 생성한다. 생성형 모델로
        /// 만든 정식 아트로 나중에 교체 가능하도록 평범한 PNG 임포트 경로를 그대로 쓴다
        /// (Sprite.Create로 만든 런타임 전용 스프라이트는 프리팹에 저장되지 않아 이 방식을
        /// 쓰지 않았다).
        /// </summary>
        private static void ConfigureScorchSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var scorchColor = new Color(0.08f, 0.05f, 0.04f);
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDistance = center.magnitude;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                    float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 1f, distance));
                    texture.SetPixel(x, y, new Color(scorchColor.r, scorchColor.g, scorchColor.b, alpha));
                }
            }

            texture.Apply();
            byte[] pngBytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);

            string fullPath = Path.Combine(Application.dataPath, ScorchTexturePath.Substring("Assets/".Length));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Application.dataPath);
            File.WriteAllBytes(fullPath, pngBytes);
            AssetDatabase.ImportAsset(ScorchTexturePath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(ScorchTexturePath) is not TextureImporter importer)
            {
                throw new InvalidOperationException(
                    $"그을림 자국 이미지를 TextureImporter로 열 수 없습니다: {ScorchTexturePath}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            // 절차적으로 대칭 생성한 텍스처라 기본 피벗(0.5, 0.5)이 이미 중심과 일치한다 —
            // ConfigureProjectileSprite처럼 SpriteEditorDataProvider로 피벗을 옮길 필요가 없다.
            importer.SaveAndReimport();
        }

        /// <summary>
        /// EnemyView/AllyUnitView처럼 SO가 아니라 프리팹 자체에 붙은 컴포넌트 필드를 배선할 때
        /// 쓰는 범용 헬퍼. 씬이 아니라 프리팹 에셋이라 PrefabUtility.LoadPrefabContents로 안전하게
        /// 연다(AGENTS.md §3-2: .prefab 텍스트 직접 편집 금지 — 반드시 에디터 API 경유).
        /// </summary>
        private static void ConnectPrefabField<TComponent>(string prefabPath, string fieldName, GameObject value)
            where TComponent : Component
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                TComponent component = root.GetComponent<TComponent>();
                if (component == null)
                {
                    throw new InvalidOperationException(
                        $"{prefabPath}에서 {typeof(TComponent).Name}을 찾지 못했습니다.");
                }

                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.FindProperty(fieldName);
                if (property == null)
                {
                    throw new InvalidOperationException(
                        $"{typeof(TComponent).Name}에 {fieldName} 슬롯이 없습니다: {prefabPath}");
                }

                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidatePrefabField<TComponent>(string prefabPath, string fieldName, GameObject expected)
            where TComponent : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            TComponent component = prefab != null ? prefab.GetComponent<TComponent>() : null;
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"{prefabPath}에서 {typeof(TComponent).Name}을 찾지 못했습니다.");
            }

            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null || property.objectReferenceValue != expected)
            {
                throw new InvalidOperationException(
                    $"{prefabPath}의 {fieldName} 배선이 올바르지 않습니다.");
            }
        }

        /// <summary>
        /// 히트 스파크 전용 Additive 머티리얼. Unity 기본 파티클 머티리얼(Default-ParticleSystem.mat)은
        /// Alpha Blend라 다른 스프라이트에 반투명하게 겹쳐 보이기만 하고 밝게 도드라지지 않는다 —
        /// "타격 스파크"가 요구하는 번쩍임에는 Additive가 맞다. 새 셰이더를 작성하지 않고 엔진 내장
        /// Legacy Particle 셰이더 + 내장 소프트 도트 텍스처만 조합한다(VFX_전투_연출_설계안.md §0-3:
        /// 신규 셰이더 작성 금지 원칙 유지).
        /// </summary>
        private static Material BuildHitSparkMaterial()
        {
            EnsureOverwriteIsOwned(HitSparkMaterialPath);

            Shader additiveShader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (additiveShader == null)
            {
                throw new InvalidOperationException(
                    "Legacy Shaders/Particles/Additive 셰이더를 찾지 못했습니다.");
            }

            Texture2D softDot = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (softDot == null)
            {
                throw new InvalidOperationException("Unity 기본 파티클 텍스처를 찾지 못했습니다.");
            }

            // 이 머티리얼은 히트 스파크·사망 버스트가 공유해 한 빌드 안에서 두 번 호출된다.
            // Delete+CreateAsset로 매번 새로 만들면 먼저 저장된 프리팹(ParticleBurst_HitSpark)의
            // sharedMaterial 참조가 삭제된 GUID를 가리키게 돼 끊어진다 — 기존 에셋을 찾으면
            // 그 자리에서 속성만 갱신해 같은 GUID를 유지한다(SaveOwnedPrefab이 프리팹에 대해
            // 하는 것과 같은 "제자리 갱신" 원칙).
            Material material = AssetDatabase.LoadAssetAtPath<Material>(HitSparkMaterialPath);
            if (material == null)
            {
                material = new Material(additiveShader) { name = "HitSpark_Additive" };
                AssetDatabase.CreateAsset(material, HitSparkMaterialPath);
                AssetDatabase.SetLabels(material, new[] { GeneratedLabel });
            }
            else
            {
                material.shader = additiveShader;
            }

            material.SetTexture("_MainTex", softDot);
            material.SetColor("_TintColor", Color.white);
            EditorUtility.SetDirty(material);

            return material;
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

            ParticleSystemRenderer sparkRenderer =
                hitSparkPrefab.GetComponent<ParticleSystemRenderer>();
            if (sparkRenderer == null || sparkRenderer.sharedMaterial == null ||
                sparkRenderer.sharedMaterial.shader.name != "Legacy Shaders/Particles/Additive" ||
                particleSystem.main.startSize.constantMin < 0.15f)
            {
                throw new InvalidOperationException(
                    "히트 스파크가 Additive 머티리얼/재조정된 크기로 배선되지 않았습니다.");
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
            ValidateEffect<PierceDamageEffect>(PierceDamageEffectPath, fakeProjectilePrefab);
            ValidateEffect<PoisonDamageEffect>(PoisonDamageEffectPath, fakeProjectilePrefab);
            ValidateEffect<BasicAttackEffect>(BasicAttackEffectPath, fakeProjectilePrefab);
            ValidatePlayerController(fakeProjectilePrefab);

            GameObject deathBurstPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeathBurstPrefabPath);
            ParticleBurst deathBurst = deathBurstPrefab != null
                ? deathBurstPrefab.GetComponent<ParticleBurst>()
                : null;
            ParticleSystem deathBurstParticleSystem = deathBurstPrefab != null
                ? deathBurstPrefab.GetComponent<ParticleSystem>()
                : null;
            if (deathBurst == null || deathBurstParticleSystem == null ||
                deathBurstParticleSystem.emission.burstCount < 1)
            {
                throw new InvalidOperationException("사망 버스트 프리팹 설정이 올바르지 않습니다.");
            }

            ValidatePrefabField<EnemyView>(EnemyViewPrefabPath, "deathParticlePrefab", deathBurstPrefab);
            ValidatePrefabField<AllyUnitView>(AllyUnitViewPrefabPath, "deathParticlePrefab", deathBurstPrefab);

            GameObject shockwaveRingPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ShockwaveRingPrefabPath);
            if (shockwaveRingPrefab == null ||
                shockwaveRingPrefab.GetComponent<ShockwaveRing>() == null ||
                shockwaveRingPrefab.GetComponent<MeshRenderer>()?.sharedMaterial == null ||
                shockwaveRingPrefab.GetComponent<MeshRenderer>().sharedMaterial.shader.name !=
                    "RCCom/Unit Visuals/Range Pulse Aura")
            {
                throw new InvalidOperationException("충격파 링 프리팹 설정이 올바르지 않습니다.");
            }

            GameObject scorchDecalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScorchDecalPrefabPath);
            SpriteRenderer scorchRenderer = scorchDecalPrefab != null
                ? scorchDecalPrefab.GetComponent<SpriteRenderer>()
                : null;
            if (scorchDecalPrefab == null || scorchDecalPrefab.GetComponent<ScorchDecal>() == null ||
                scorchRenderer == null || scorchRenderer.sprite == null)
            {
                throw new InvalidOperationException("그을림 자국 프리팹 설정이 올바르지 않습니다.");
            }

            var shockwaveVisual =
                AssetDatabase.LoadAssetAtPath<ShockwaveRingVisualEffect>(ShockwaveRingVisualEffectPath);
            if (shockwaveVisual == null)
            {
                throw new InvalidOperationException(
                    $"충격파 링 시각 SO가 없습니다: {ShockwaveRingVisualEffectPath}");
            }

            ValidateSplashEffect(
                fakeProjectilePrefab, deathBurstPrefab, shockwaveRingPrefab, scorchDecalPrefab, shockwaveVisual);

            Debug.Log("[CombatVfxAssetBuilder] 투사체·히트 스파크·사망 버스트·충격파 링·그을림 자국 에셋 검증 통과");
        }

        private static void ValidateSplashEffect(
            GameObject fakeProjectilePrefab,
            GameObject explosionBurstPrefab,
            GameObject shockwaveRingPrefab,
            GameObject scorchDecalPrefab,
            ShockwaveRingVisualEffect shockwaveVisual)
        {
            var effect = AssetDatabase.LoadAssetAtPath<SplashDamageEffect>(SplashDamageEffectPath);
            if (effect == null)
            {
                throw new InvalidOperationException($"공격 효과 SO가 없습니다: {SplashDamageEffectPath}");
            }

            var serializedEffect = new SerializedObject(effect);
            CheckObjectReference(serializedEffect, "fakeProjectilePrefab", fakeProjectilePrefab);
            CheckObjectReference(serializedEffect, "explosionBurstPrefab", explosionBurstPrefab);
            CheckObjectReference(serializedEffect, "shockwaveRingPrefab", shockwaveRingPrefab);
            CheckObjectReference(serializedEffect, "scorchDecalPrefab", scorchDecalPrefab);
            CheckObjectReference(serializedEffect, "shockwaveVisual", shockwaveVisual);
        }

        private static void CheckObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object expected)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != expected)
            {
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.GetType().Name}의 {propertyName} 배선이 올바르지 않습니다.");
            }
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
