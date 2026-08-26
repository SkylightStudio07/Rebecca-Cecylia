// ch1-06~08을 원격 전용 콘텐츠로 전환한다.
// LiveContent 버튼 시연을 위해 챕터 후반부를 "빌드 이후에 받는" 콘텐츠로 재분류하는 일회성 작업.
string[] stageIds = { "ch1-06", "ch1-07", "ch1-08" };
foreach (string stageId in stageIds)
{
    string path = $"Assets/Data/Stages/CH1/{stageId}.asset";
    var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Stage.StageDefinition>(path);
    if (definition == null)
    {
        UnityEngine.Debug.LogError($"StageDefinition을 찾지 못했습니다: {path}");
        continue;
    }

    definition.remoteContent = true;
    UnityEditor.EditorUtility.SetDirty(definition);
    UnityEngine.Debug.Log($"[MarkCh1LateStagesRemote] {stageId}.remoteContent = true");
}

UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
