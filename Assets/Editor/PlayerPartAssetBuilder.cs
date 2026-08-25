using System;
using System.Collections.Generic;
using RCCom.Definitions.PlayerPart;
using RCCom.Effects.PlayerPart;
using RCCom.Effects.PlayerPart.Concrete;
using RCCom.Runtime.Visuals;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    public static class PlayerPartAssetBuilder
    {
        public const string RecipeFolder = "Assets/Editor/PlayerPartRecipes";
        public const string DefinitionFolder = "Assets/Data/PlayerParts/Definitions";
        public const string EffectFolder = "Assets/Data/PlayerParts/Effects";
        public const string CatalogPath = "Assets/Resources/PlayerParts/PlayerPartCatalog.asset";

        [MenuItem("RCCom/Player Parts/Build All Player Part Assets")]
        public static void BuildAll()
        {
            // 외부 아트 저장소에서 복사된 PNG를 같은 호출 안에서 즉시 Sprite로 설정할 수 있게 먼저 동기화한다.
            AssetDatabase.Refresh();
            EnsureFolders();
            List<PlayerPartAssetRecipe> recipes = LoadRecipes();
            ConfigureIconImports(recipes);
            var definitions = new List<PlayerPartDefinition>();
            foreach (PlayerPartAssetRecipe recipe in recipes)
            {
                definitions.Add(BuildDefinition(recipe));
            }

            BuildCatalog(definitions);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PlayerPartAssetBuilder] 파츠 {definitions.Count}개와 카탈로그를 생성/갱신했습니다.");
        }

        public static List<PlayerPartAssetRecipe> LoadRecipes()
        {
            var recipes = new List<PlayerPartAssetRecipe>();
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { RecipeFolder });
            var paths = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                PlayerPartAssetRecipe recipe = JsonUtility.FromJson<PlayerPartAssetRecipe>(text.text);
                if (recipe == null || string.IsNullOrWhiteSpace(recipe.partId))
                {
                    throw new InvalidOperationException($"유효하지 않은 플레이어 파츠 레시피입니다: {path}");
                }

                recipes.Add(recipe);
            }

            return recipes;
        }

        public static string GetDefinitionPath(string partId) => $"{DefinitionFolder}/{partId}.asset";

        private static PlayerPartDefinition BuildDefinition(PlayerPartAssetRecipe recipe)
        {
            string path = GetDefinitionPath(recipe.partId);
            PlayerPartDefinition definition = AssetDatabase.LoadAssetAtPath<PlayerPartDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<PlayerPartDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.partId = recipe.partId;
            definition.slot = recipe.slot;
            definition.grade = recipe.grade;
            definition.archetypeId = recipe.archetypeId;
            definition.displayName = recipe.displayName;
            definition.description = recipe.description;
            definition.price = recipe.price;
            definition.statOverride = recipe.statOverride ?? new PlayerPartStatOverride();
            definition.icon = LoadOptionalSprite(ResolveIconPath(recipe));
            definition.effects = BuildEffects(recipe);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static List<PlayerPartEffectBase> BuildEffects(PlayerPartAssetRecipe recipe)
        {
            var effects = new List<PlayerPartEffectBase>();
            if (recipe.effects == null)
            {
                return effects;
            }

            for (int index = 0; index < recipe.effects.Count; index++)
            {
                PlayerPartEffectRecipe effectRecipe = recipe.effects[index];
                if (effectRecipe == null || string.IsNullOrWhiteSpace(effectRecipe.type))
                {
                    continue;
                }

                string path = $"{EffectFolder}/{recipe.partId}-effect-{index}.asset";
                PlayerPartEffectBase effect = AssetDatabase.LoadAssetAtPath<PlayerPartEffectBase>(path);
                Type expectedType = ResolveEffectType(effectRecipe.type);
                if (effect != null && effect.GetType() != expectedType)
                {
                    throw new InvalidOperationException(
                        $"생성 Effect 타입이 레시피와 다릅니다. 기존 에셋을 검토한 뒤 삭제하세요: {path}");
                }

                if (effect == null)
                {
                    effect = (PlayerPartEffectBase)ScriptableObject.CreateInstance(expectedType);
                    AssetDatabase.CreateAsset(effect, path);
                }

                ConfigureEffect(effect, effectRecipe);
                ConfigureSharedAttackVisuals(effect);
                // Unity가 파일명과 Main Object 이름이 다르다고 반복 경고하지 않도록 에셋 경로를 정본으로 삼는다.
                effect.name = System.IO.Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(effect);
                effects.Add(effect);
            }

            return effects;
        }

        private static Type ResolveEffectType(string type)
        {
            return type switch
            {
                "basic" => typeof(PlayerBasicAttackEffect),
                "multi-barrel" => typeof(PlayerMultiBarrelAttackEffect),
                "pierce" => typeof(PlayerPierceAttackEffect),
                "splash" => typeof(PlayerSplashAttackEffect),
                "chain" => typeof(PlayerChainAttackEffect),
                "overload-attack" => typeof(PlayerOverloadAttackEffect),
                "inertia" => typeof(InertiaThrusterEffect),
                "overboost" => typeof(OverboostThrusterEffect),
                "regenerative-armor" => typeof(RegenerativeArmorEffect),
                "passive-regeneration" => typeof(PassiveRegenerationEffect),
                "rechargeable-shield" => typeof(RechargeableShieldEffect),
                "one-time-revival" => typeof(OneTimeRevivalEffect),
                "core-overload" => typeof(CoreOverloadModuleEffect),
                _ => throw new InvalidOperationException($"지원하지 않는 플레이어 파츠 Effect 타입입니다: {type}"),
            };
        }

        private static void ConfigureEffect(PlayerPartEffectBase effect, PlayerPartEffectRecipe recipe)
        {
            switch (effect)
            {
                case PlayerMultiBarrelAttackEffect value:
                    value.shotCount = recipe.shotCount;
                    value.shotInterval = recipe.shotInterval;
                    value.damageMultiplierPerShot = recipe.damageMultiplier;
                    break;
                case PlayerPierceAttackEffect value:
                    value.maxTargets = recipe.maxTargets;
                    value.damageFalloffPerTarget = recipe.damageFalloff;
                    value.beamHalfAngleDegrees = recipe.beamHalfAngleDegrees;
                    break;
                case PlayerSplashAttackEffect value:
                    value.radius = recipe.radius;
                    value.splashDamageMultiplier = recipe.splashDamageMultiplier;
                    break;
                case PlayerChainAttackEffect value:
                    value.jumpCount = recipe.jumpCount;
                    value.damageFalloffPerJump = recipe.damageFalloff;
                    break;
                case PlayerOverloadAttackEffect value:
                    value.skillPierceTargets = recipe.maxTargets;
                    value.beamHalfAngleDegrees = recipe.beamHalfAngleDegrees;
                    break;
                case InertiaThrusterEffect value:
                    value.accelerationDelay = recipe.accelerationDelay;
                    value.rampDuration = recipe.rampDuration;
                    value.maximumSpeedMultiplier = recipe.maximumSpeedMultiplier;
                    break;
                case OverboostThrusterEffect value:
                    value.dashDuration = recipe.dashDuration;
                    break;
                case RegenerativeArmorEffect value:
                    value.delayAfterDamage = recipe.delayAfterDamage;
                    value.maxHealthPerSecond = recipe.maxHealthPerSecond;
                    break;
                case PassiveRegenerationEffect value:
                    value.maxHealthPerSecond = recipe.maxHealthPerSecond;
                    value.healOnSkillRatio = recipe.healOnSkillRatio;
                    break;
                case RechargeableShieldEffect value:
                    value.barrierAmount = recipe.barrierAmount;
                    value.rechargeInterval = recipe.rechargeInterval;
                    break;
                case OneTimeRevivalEffect value:
                    value.restoredHealthRatio = recipe.restoredHealthRatio;
                    value.invulnerabilityDuration = recipe.invulnerabilityDuration;
                    break;
                case CoreOverloadModuleEffect value:
                    value.maxHealthPerSecond = recipe.maxHealthPerSecond;
                    value.triggerHealthRatio = recipe.triggerHealthRatio;
                    value.instantHealRatio = recipe.instantHealRatio;
                    value.invulnerabilityDuration = recipe.invulnerabilityDuration;
                    value.cooldown = recipe.cooldown;
                    break;
            }
        }

        /// <summary>
        /// 플레이어 공격용으로 VFX 프리팹을 복제하지 않고 타워/아군이 이미 쓰는 공용 자산을
        /// 그대로 배선한다. 경로 정본도 CombatVfxAssetBuilder와 공유해 한쪽만 교체되는 일을 막는다.
        /// </summary>
        private static void ConfigureSharedAttackVisuals(PlayerPartEffectBase effect)
        {
            if (effect is PlayerPierceAttackEffect or PlayerChainAttackEffect or PlayerOverloadAttackEffect)
            {
                SetObjectReference(
                    effect,
                    "laserBeamPrefab",
                    LoadRequired<GameObject>(CombatVfxAssetBuilder.LaserBeamPrefabPath));
                SetObjectReference(
                    effect,
                    "laserBeamVisual",
                    LoadRequired<LaserBeamVisualEffect>(CombatVfxAssetBuilder.LaserBeamVisualEffectPath));
            }

            if (effect is PlayerSplashAttackEffect)
            {
                SetObjectReference(
                    effect,
                    "fakeProjectilePrefab",
                    LoadRequired<GameObject>(CombatVfxAssetBuilder.FakeProjectilePrefabPath));
                SetObjectReference(
                    effect,
                    "explosionBurstPrefab",
                    LoadRequired<GameObject>(CombatVfxAssetBuilder.DeathBurstPrefabPath));
                SetObjectReference(
                    effect,
                    "shockwaveRingPrefab",
                    LoadRequired<GameObject>(CombatVfxAssetBuilder.ShockwaveRingPrefabPath));
                SetObjectReference(
                    effect,
                    "shockwaveVisual",
                    LoadRequired<ShockwaveRingVisualEffect>(CombatVfxAssetBuilder.ShockwaveRingVisualEffectPath));
                SetObjectReference(
                    effect,
                    "scorchDecalPrefab",
                    LoadRequired<GameObject>(CombatVfxAssetBuilder.ScorchDecalPrefabPath));
            }
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"공용 전투 VFX 자산을 찾지 못했습니다: {path}");
            }

            return asset;
        }

        private static void SetObjectReference(
            PlayerPartEffectBase effect,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(effect);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{effect.GetType().Name}에 VFX 슬롯이 없습니다: {propertyName}");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildCatalog(List<PlayerPartDefinition> definitions)
        {
            definitions.Sort((left, right) =>
            {
                int slot = left.slot.CompareTo(right.slot);
                if (slot != 0) { return slot; }
                int grade = left.grade.CompareTo(right.grade);
                return grade != 0 ? grade : string.CompareOrdinal(left.partId, right.partId);
            });

            PlayerPartCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayerPartCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PlayerPartCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.parts = definitions;
            EditorUtility.SetDirty(catalog);
        }

        private static Sprite LoadOptionalSprite(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ConfigureIconImports(List<PlayerPartAssetRecipe> recipes)
        {
            var configuredPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayerPartAssetRecipe recipe in recipes)
            {
                string path = ResolveIconPath(recipe);
                if (!configuredPaths.Add(path))
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException($"상점 아이콘을 찾지 못했거나 PNG가 아닙니다: {path}");
                }

                bool changed = importer.textureType != TextureImporterType.Sprite ||
                               importer.spriteImportMode != SpriteImportMode.Single ||
                               !importer.alphaIsTransparency || importer.mipmapEnabled ||
                               importer.wrapMode != TextureWrapMode.Clamp ||
                               importer.maxTextureSize != 512 ||
                               importer.textureCompression != TextureImporterCompression.Compressed;
                if (!changed)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                // 원본 1254px를 그대로 올리면 WebGL UI 메모리가 과도하므로 상점 썸네일 상한을 둔다.
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
        }

        private static string ResolveIconPath(PlayerPartAssetRecipe recipe)
        {
            if (!string.IsNullOrWhiteSpace(recipe.iconPath))
            {
                return recipe.iconPath;
            }

            string slotFolder = recipe.slot.ToString().ToLowerInvariant();
            return $"Assets/Art/PlayerParts/Icons/{slotFolder}/{recipe.archetypeId}.png";
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "PlayerParts");
            EnsureFolder("Assets/Data/PlayerParts", "Definitions");
            EnsureFolder("Assets/Data/PlayerParts", "Effects");
            EnsureFolder("Assets", "Resources");
            EnsureFolder("Assets/Resources", "PlayerParts");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
