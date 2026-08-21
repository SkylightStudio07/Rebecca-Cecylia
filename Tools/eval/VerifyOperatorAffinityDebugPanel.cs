UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
    "Assets/Scenes/TitleScene.unity",
    UnityEditor.SceneManagement.OpenSceneMode.Single);
RCCom.UI.OperatorAffinityDebugPanel panel =
    UnityEngine.Object.FindFirstObjectByType<RCCom.UI.OperatorAffinityDebugPanel>(
        UnityEngine.FindObjectsInactive.Include);
if (scene.path != "Assets/Scenes/TitleScene.unity" || panel == null || !panel.gameObject.activeSelf)
{
    throw new System.InvalidOperationException("호감도 디버그 패널 씬 배치를 찾지 못했습니다.");
}

UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(panel);
string[] required =
{
    "operatorIdInput", "statusText", "affinitySlider", "applyAffinityButton",
    "decreaseAffinityButton", "increaseAffinityButton", "setUnfamiliarButton",
    "setFavorableButton", "setJoyButton", "setLoveButton", "setExButton",
    "queueParticipatedReturnButton", "queueOtherReturnButton", "clearReturnButton",
    "showDialogueButton", "refreshButton", "lobbyDialogueUi",
};
for (int i = 0; i < required.Length; i++)
{
    if (serialized.FindProperty(required[i]).objectReferenceValue == null)
    {
        throw new System.InvalidOperationException("디버그 패널 참조 누락: " + required[i]);
    }
}

var profile = new RCCom.Data.PlayerProfile();
profile.SetOperatorAffinity("cassia", 24);
if (profile.GetOperatorAffinityTier("cassia") != RCCom.Data.OperatorAffinityTier.Unfamiliar)
{
    throw new System.InvalidOperationException("24 호감도 경계가 잘못되었습니다.");
}
profile.SetOperatorAffinity("cassia", 25);
if (profile.GetOperatorAffinityTier("cassia") != RCCom.Data.OperatorAffinityTier.Favorable)
{
    throw new System.InvalidOperationException("25 호감도 경계가 잘못되었습니다.");
}
profile.SetOperatorAffinity("cassia", 50);
if (profile.GetOperatorAffinityTier("cassia") != RCCom.Data.OperatorAffinityTier.Joy)
{
    throw new System.InvalidOperationException("50 호감도 경계가 잘못되었습니다.");
}
profile.SetOperatorAffinity("cassia", 75);
if (profile.GetOperatorAffinityTier("cassia") != RCCom.Data.OperatorAffinityTier.Love)
{
    throw new System.InvalidOperationException("75 호감도 경계가 잘못되었습니다.");
}
profile.QueueBattleReturn("cassia");
if (!profile.TryClaimBattleReturn("cassia", out int participatedAmount, out bool participated) ||
    !participated || participatedAmount != RCCom.Data.PlayerProfile.ReturnAffinityWithParticipation)
{
    throw new System.InvalidOperationException("참전 귀환 +5 경로가 잘못되었습니다.");
}
profile.QueueBattleReturn("__debug_other_operator__");
if (!profile.TryClaimBattleReturn("cassia", out int otherAmount, out bool participatedOther) ||
    participatedOther || otherAmount != RCCom.Data.PlayerProfile.ReturnAffinityWithoutParticipation)
{
    throw new System.InvalidOperationException("비참전 귀환 +2 경로가 잘못되었습니다.");
}

UnityEngine.Debug.Log("[VerifyOperatorAffinityDebugPanel] 패널 배선·호감도 경계·귀환 보상 검증 통과");
