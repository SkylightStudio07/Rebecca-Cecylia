using System;
using RCCom.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 제공된 상점 BGM을 리크루트 패널에 Editor API로 배선한다.
    /// 씬 YAML을 직접 편집하지 않고 반복 실행 가능한 설정 경로를 남기기 위한 도구다.
    /// </summary>
    public static class ShopBgmAssetBuilder
    {
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string ShopBgmPath = "Assets/Music/BGM/Rare_Item_Bgm.mp3";

        [MenuItem("RCCom/Audio/Build Recruit Shop BGM")]
        public static void BuildAndVerify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("상점 BGM 배선은 씬을 저장하므로 Edit Mode에서 실행해야 합니다.");
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ShopBgmPath);
            if (clip == null)
            {
                throw new InvalidOperationException($"상점 BGM AudioClip을 찾지 못했습니다: {ShopBgmPath}");
            }

            Scene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            LobbyShopPanelUI shopPanel = FindInScene<LobbyShopPanelUI>(scene);
            if (shopPanel == null)
            {
                throw new InvalidOperationException("TitleScene에서 LobbyShopPanelUI를 찾지 못했습니다.");
            }

            SerializedObject serializedShop = new SerializedObject(shopPanel);
            SerializedProperty bgmProperty = serializedShop.FindProperty("shopBgmClip");
            if (bgmProperty == null)
            {
                throw new InvalidOperationException("LobbyShopPanelUI.shopBgmClip 직렬화 필드를 찾지 못했습니다.");
            }

            bgmProperty.objectReferenceValue = clip;
            serializedShop.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shopPanel);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"상점 BGM 배선 씬 저장에 실패했습니다: {TitleScenePath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SerializedObject verification = new SerializedObject(shopPanel);
            AudioClip actual = verification.FindProperty("shopBgmClip").objectReferenceValue as AudioClip;
            if (actual != clip)
            {
                throw new InvalidOperationException("상점 BGM 참조가 TitleScene에 저장되지 않았습니다.");
            }

            Debug.Log($"[ShopBgmAssetBuilder] PASS — {clip.name}, {clip.length:0.00}초, Recruit 반복 BGM 배선 완료");
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
