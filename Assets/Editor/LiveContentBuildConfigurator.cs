using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 라이브 콘텐츠의 런타임 경계를 빌드 설정에도 강제한다. 자동 카탈로그 갱신이 켜져 있으면
    /// 버튼 전 서버 접속이 발생하고, 원격 번들의 전용 셰이더가 플레이어에서 스트립되면 머티리얼이
    /// 보라색으로 렌더링되므로 두 설정을 한 메뉴에서 함께 맞춘다.
    /// </summary>
    public static class LiveContentBuildConfigurator
    {
        private const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";
        private static readonly string[] RequiredShaderPaths =
        {
            "Assets/Shaders/Tower/LaserBeam.shader",
            "Assets/Shaders/Enemy/EnemyRangeAura.shader",
            "Assets/Shaders/Enemy/EnemyFrontShield.shader",
            "Assets/Shaders/Unit/RangePulseAura.shader",
        };

        [MenuItem("RCCom/Addressables/Configure Explicit Live Content Loading")]
        public static void Configure()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings를 찾지 못했습니다.");
            }

            var addressableSettings = new SerializedObject(settings);
            SerializedProperty disableUpdate =
                addressableSettings.FindProperty("m_DisableCatalogUpdateOnStart");
            if (disableUpdate == null)
            {
                throw new InvalidOperationException("Addressables 자동 카탈로그 갱신 설정을 찾지 못했습니다.");
            }

            disableUpdate.boolValue = true;
            addressableSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);

            UnityEngine.Object[] graphicsAssets = AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsPath);
            if (graphicsAssets == null || graphicsAssets.Length == 0)
            {
                throw new InvalidOperationException("GraphicsSettings 에셋을 찾지 못했습니다.");
            }

            var graphicsSettings = new SerializedObject(graphicsAssets[0]);
            SerializedProperty alwaysIncluded = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");
            if (alwaysIncluded == null)
            {
                throw new InvalidOperationException("Always Included Shaders 목록을 찾지 못했습니다.");
            }

            foreach (string shaderPath in RequiredShaderPaths)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (shader == null)
                {
                    throw new InvalidOperationException($"필수 셰이더를 찾지 못했습니다: {shaderPath}");
                }

                if (!Contains(alwaysIncluded, shader))
                {
                    int index = alwaysIncluded.arraySize;
                    alwaysIncluded.InsertArrayElementAtIndex(index);
                    alwaysIncluded.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                }
            }

            graphicsSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(graphicsAssets[0]);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LiveContentBuildConfigurator] 버튼 전 자동 갱신 차단 및 원격 셰이더 보존 설정 완료");
        }

        public static void ValidateOrThrow(AddressableAssetSettings settings)
        {
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings가 없습니다.");
            }

            var addressableSettings = new SerializedObject(settings);
            SerializedProperty disableUpdate =
                addressableSettings.FindProperty("m_DisableCatalogUpdateOnStart");
            if (disableUpdate == null || !disableUpdate.boolValue)
            {
                throw new InvalidOperationException(
                    "시작 시 카탈로그 자동 갱신이 켜져 있습니다. " +
                    "RCCom/Addressables/Configure Explicit Live Content Loading을 실행하세요.");
            }

            UnityEngine.Object[] graphicsAssets = AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsPath);
            if (graphicsAssets == null || graphicsAssets.Length == 0)
            {
                throw new InvalidOperationException("GraphicsSettings 에셋이 없습니다.");
            }

            var graphicsSettings = new SerializedObject(graphicsAssets[0]);
            SerializedProperty alwaysIncluded = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");
            var missing = new List<string>();
            foreach (string shaderPath in RequiredShaderPaths)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                if (shader == null || alwaysIncluded == null || !Contains(alwaysIncluded, shader))
                {
                    missing.Add(shaderPath);
                }
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "원격 번들에서 사용하는 셰이더가 Always Included Shaders에 없습니다:\n  " +
                    string.Join("\n  ", missing));
            }
        }

        private static bool Contains(SerializedProperty array, UnityEngine.Object target)
        {
            for (int i = 0; i < array.arraySize; i++)
            {
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == target)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
