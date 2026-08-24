using System;
using RCCom.Managers;
using RCCom.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 공용 UI 선택음을 두 플레이 씬의 SoundManager와 모든 일반 Button에 반복 가능하게 배선한다.
    /// 씬 YAML을 직접 편집하지 않고 Editor API만 사용하기 위한 재현 가능한 자동화 경로다.
    /// </summary>
    public static class UISelectSoundAssetBuilder
    {
        private const string ClipPath = "Assets/Music/Effects/Ui_select.wav";
        private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
        private const string DefenseScenePath = "Assets/Scenes/DefenseScene.unity";

        [MenuItem("RCCom/UI/Build UI Select Sound")]
        public static void BuildAndVerify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("UI 선택음 배선은 씬을 저장하므로 Edit Mode에서 실행해야 합니다.");
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip == null)
            {
                throw new InvalidOperationException($"UI 선택음 AudioClip을 찾지 못했습니다: {ClipPath}");
            }

            int titleButtons = WireScene(TitleScenePath, clip);
            int defenseButtons = WireScene(DefenseScenePath, clip);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[UISelectSoundAssetBuilder] PASS — Title {titleButtons}개, Defense {defenseButtons}개 버튼 배선 완료");
        }

        private static int WireScene(string scenePath, AudioClip clip)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            SoundManager soundManager = FindInScene<SoundManager>(scene);
            if (soundManager == null)
            {
                throw new InvalidOperationException($"{scenePath}에서 SoundManager를 찾지 못했습니다.");
            }

            SerializedObject serializedSound = new SerializedObject(soundManager);
            serializedSound.FindProperty("uiSelectClip").objectReferenceValue = clip;
            serializedSound.FindProperty("uiSelectCutoffDuration").floatValue = 1f;
            serializedSound.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(soundManager);

            int buttonCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Button[] buttons = root.GetComponentsInChildren<Button>(true);
                foreach (Button button in buttons)
                {
                    if (button.GetComponent<UISelectSoundEmitter>() == null)
                    {
                        button.gameObject.AddComponent<UISelectSoundEmitter>();
                    }

                    EditorUtility.SetDirty(button.gameObject);
                    buttonCount++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"UI 선택음 배선 씬 저장에 실패했습니다: {scenePath}");
            }

            VerifyScene(scene, clip, buttonCount);
            return buttonCount;
        }

        private static void VerifyScene(Scene scene, AudioClip expectedClip, int expectedButtonCount)
        {
            SoundManager soundManager = FindInScene<SoundManager>(scene);
            SerializedObject serializedSound = new SerializedObject(soundManager);
            AudioClip actualClip = serializedSound.FindProperty("uiSelectClip").objectReferenceValue as AudioClip;
            if (actualClip != expectedClip)
            {
                throw new InvalidOperationException($"{scene.path} SoundManager의 UI 선택음 참조가 저장되지 않았습니다.");
            }

            int emitterCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                emitterCount += root.GetComponentsInChildren<UISelectSoundEmitter>(true).Length;
            }

            if (emitterCount != expectedButtonCount)
            {
                throw new InvalidOperationException(
                    $"{scene.path} 버튼/선택음 Emitter 수가 다릅니다: Button={expectedButtonCount}, Emitter={emitterCount}");
            }
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
