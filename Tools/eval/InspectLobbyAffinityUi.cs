UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
RCCom.UI.LobbyOperatorDialogueUI controller =
    UnityEngine.Object.FindFirstObjectByType<RCCom.UI.LobbyOperatorDialogueUI>(
        UnityEngine.FindObjectsInactive.Include);
if (controller == null)
{
    throw new System.InvalidOperationException("TitleScene 로비 호감도 UI를 찾지 못했습니다.");
}

var serialized = new UnityEditor.SerializedObject(controller);
UnityEngine.Object portrait = serialized.FindProperty("lobbyOperatorImage").objectReferenceValue;
UnityEngine.Object dialogue = serialized.FindProperty("dialogueSet").objectReferenceValue;
UnityEngine.Debug.Log($"[InspectLobbyAffinityUi] scene={active.path}, dialogueSet={dialogue != null}, " +
    $"portrait={portrait != null}, portraitPath={(portrait != null ? portrait.name : "None")}, " +
    $"fallbackOperatorId={serialized.FindProperty("fallbackOperatorId").stringValue}");
