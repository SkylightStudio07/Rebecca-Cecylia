if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Scenes/Editor"))
{
    UnityEditor.AssetDatabase.CreateFolder("Assets/Scenes", "Editor");
}

if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(
        RCCom.EditorTools.StageRouteAuthoringTool.TestScenePath) == null)
{
    UnityEditor.AssetDatabase.CopyAsset(
        RCCom.EditorTools.StageRouteAuthoringTool.DefenseScenePath,
        RCCom.EditorTools.StageRouteAuthoringTool.TestScenePath);
    UnityEditor.AssetDatabase.SaveAssets();
    UnityEditor.AssetDatabase.Refresh();
}

UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
    RCCom.EditorTools.StageRouteAuthoringTool.TestScenePath,
    UnityEditor.SceneManagement.OpenSceneMode.Single);
var stage = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Stage.StageDefinition>(
    "Assets/Data/Stages/CH1/ch1-01.asset");
RCCom.EditorTools.StageRouteAuthoringTool.LoadIntoOpenTestScene(stage);
RCCom.EditorTools.StageRouteAuthoringTool.CaptureFromOpenTestScene(stage);
UnityEngine.Debug.Log("[BuildStageRouteAuthoringWorkspace] 1-1 테스트 작업대 생성 완료");
