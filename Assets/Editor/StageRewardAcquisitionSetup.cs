using System;
using RCCom.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 로비의 합류 연출을 프리팹으로 보존하고 DefenseScene 결과 화면에도 같은 연출을 배선한다.
    /// </summary>
    public static class StageRewardAcquisitionSetup
    {
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string DefenseScenePath = "Assets/Scenes/DefenseScene.unity";
        private const string PrefabPath = "Assets/Data/Prefabs/OperatorAcquisitionOverlay.prefab";
        private const string InstanceName = "StageRewardOperatorAcquisition";

        [MenuItem("RCCom/UI/Setup Stage Reward Acquisition")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("스테이지 보상 합류 UI Setup은 Edit Mode에서만 실행할 수 있습니다.");
            }

            Scene titleScene = GetOrOpenScene(TitleScenePath, out bool closeTitleScene);
            GameObject source = FindGameObject(titleScene, "GachaGainBackground");
            if (source == null || source.GetComponent<OperatorAcquisitionUI>() == null)
            {
                if (closeTitleScene) { EditorSceneManager.CloseScene(titleScene, true); }
                throw new InvalidOperationException("TitleScene의 GachaGainBackground 합류 UI를 찾지 못했습니다.");
            }

            GameObject temporary = UnityEngine.Object.Instantiate(source);
            temporary.name = "OperatorAcquisitionOverlay";
            temporary.transform.SetParent(null, false);
            ConfigureResultAcquisition(temporary.GetComponent<OperatorAcquisitionUI>());
            CanvasGroup temporaryGroup = temporary.GetComponent<CanvasGroup>();
            if (temporaryGroup != null)
            {
                temporaryGroup.alpha = 0f;
                temporaryGroup.interactable = false;
                temporaryGroup.blocksRaycasts = false;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporary, PrefabPath);
            UnityEngine.Object.DestroyImmediate(temporary);
            if (closeTitleScene) { EditorSceneManager.CloseScene(titleScene, true); }
            if (prefab == null)
            {
                throw new InvalidOperationException($"합류 연출 프리팹을 저장하지 못했습니다: {PrefabPath}");
            }

            Scene defenseScene = GetOrOpenScene(DefenseScenePath, out bool closeDefenseScene);
            GameObject canvas = FindGameObject(defenseScene, "Canvas");
            GameResultUI resultUI = FindComponent<GameResultUI>(defenseScene);
            if (canvas == null || resultUI == null)
            {
                if (closeDefenseScene) { EditorSceneManager.CloseScene(defenseScene, true); }
                throw new InvalidOperationException("DefenseScene의 Canvas 또는 GameResultUI를 찾지 못했습니다.");
            }

            Transform existing = canvas.transform.Find(InstanceName);
            GameObject instance = existing != null
                ? existing.gameObject
                : PrefabUtility.InstantiatePrefab(prefab, defenseScene) as GameObject;
            if (instance == null)
            {
                if (closeDefenseScene) { EditorSceneManager.CloseScene(defenseScene, true); }
                throw new InvalidOperationException("DefenseScene에 합류 연출 프리팹을 배치하지 못했습니다.");
            }

            instance.name = InstanceName;
            instance.transform.SetParent(canvas.transform, false);
            instance.transform.SetAsLastSibling();
            instance.SetActive(true);
            OperatorAcquisitionUI acquisition = instance.GetComponent<OperatorAcquisitionUI>();
            ConfigureResultAcquisition(acquisition);

            SerializedObject resultSerialized = new(resultUI);
            SerializedProperty acquisitionProperty = resultSerialized.FindProperty("operatorAcquisitionUI");
            if (acquisitionProperty == null)
            {
                if (closeDefenseScene) { EditorSceneManager.CloseScene(defenseScene, true); }
                throw new InvalidOperationException("GameResultUI.operatorAcquisitionUI 필드를 찾지 못했습니다.");
            }
            acquisitionProperty.objectReferenceValue = acquisition;
            resultSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(instance);
            EditorUtility.SetDirty(resultUI);
            EditorSceneManager.MarkSceneDirty(defenseScene);
            EditorSceneManager.SaveScene(defenseScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateScene(defenseScene);
            if (closeDefenseScene) { EditorSceneManager.CloseScene(defenseScene, true); }
            Debug.Log("[StageRewardAcquisitionSetup] DefenseScene 결과 화면 합류 연출 배선 완료");
        }

        private static void ConfigureResultAcquisition(OperatorAcquisitionUI acquisition)
        {
            if (acquisition == null)
            {
                throw new InvalidOperationException("OperatorAcquisitionUI가 없습니다.");
            }

            SerializedObject serialized = new(acquisition);
            SerializedProperty mainMenu = serialized.FindProperty("mainMenuBackground");
            SerializedProperty autoPresent = serialized.FindProperty("autoPresentOnStart");
            SerializedProperty catalog = serialized.FindProperty("catalog");
            if (mainMenu == null || autoPresent == null || catalog == null ||
                catalog.objectReferenceValue == null)
            {
                throw new InvalidOperationException("결과용 OperatorAcquisitionUI 필드 연결이 올바르지 않습니다.");
            }

            mainMenu.objectReferenceValue = null;
            autoPresent.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(acquisition);
        }

        private static void ValidateScene(Scene defenseScene)
        {
            GameObject canvas = FindGameObject(defenseScene, "Canvas");
            GameResultUI resultUI = FindComponent<GameResultUI>(defenseScene);
            Transform instance = canvas != null ? canvas.transform.Find(InstanceName) : null;
            OperatorAcquisitionUI acquisition = instance != null
                ? instance.GetComponent<OperatorAcquisitionUI>()
                : null;
            if (resultUI == null || acquisition == null)
            {
                throw new InvalidOperationException("DefenseScene 결과용 합류 연출 인스턴스가 없습니다.");
            }

            SerializedObject resultSerialized = new(resultUI);
            SerializedObject acquisitionSerialized = new(acquisition);
            if (resultSerialized.FindProperty("operatorAcquisitionUI").objectReferenceValue != acquisition ||
                acquisitionSerialized.FindProperty("autoPresentOnStart").boolValue ||
                acquisitionSerialized.FindProperty("mainMenuBackground").objectReferenceValue != null)
            {
                throw new InvalidOperationException("DefenseScene 결과용 합류 연출 배선 검증에 실패했습니다.");
            }
        }

        private static Scene GetOrOpenScene(string path, out bool shouldClose)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            shouldClose = !scene.IsValid() || !scene.isLoaded;
            return shouldClose
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)
                : scene;
        }

        private static GameObject FindGameObject(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded) { return null; }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == name) { return transforms[i].gameObject; }
                }
            }
            return null;
        }

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded) { return null; }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) { return component; }
            }
            return null;
        }
    }
}
