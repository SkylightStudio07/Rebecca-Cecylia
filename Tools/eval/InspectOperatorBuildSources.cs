string FormatAssetPaths(string typeFilter)
{
    string[] guids = UnityEditor.AssetDatabase.FindAssets(typeFilter);
    var paths = new System.Collections.Generic.List<string>();

    foreach (string guid in guids)
    {
        paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
    }

    return string.Join(" | ", paths);
}

UnityEngine.Debug.Log($"[OperatorBuildSources] TowerRoster: {FormatAssetPaths("t:TowerRoster")}");
UnityEngine.Debug.Log($"[OperatorBuildSources] CardRoster: {FormatAssetPaths("t:CardRoster")}");
UnityEngine.Debug.Log($"[OperatorBuildSources] DialogueSet: {FormatAssetPaths("t:OperatorDialogueSet")}");

UnityEngine.SceneManagement.Scene defenseScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
    "Assets/Scenes/DefenseScene.unity",
    UnityEditor.SceneManagement.OpenSceneMode.Additive);

try
{
    RCCom.Runtime.PlayerController player = null;
    RCCom.Runtime.TowerBuildController buildController = null;
    RCCom.Managers.CardManager cardManager = null;
    RCCom.UI.OperatorDialogueUI dialogueUI = null;

    foreach (UnityEngine.GameObject root in defenseScene.GetRootGameObjects())
    {
        player ??= root.GetComponentInChildren<RCCom.Runtime.PlayerController>(true);
        buildController ??= root.GetComponentInChildren<RCCom.Runtime.TowerBuildController>(true);
        cardManager ??= root.GetComponentInChildren<RCCom.Managers.CardManager>(true);
        dialogueUI ??= root.GetComponentInChildren<RCCom.UI.OperatorDialogueUI>(true);
    }

    if (player == null || buildController == null || cardManager == null || dialogueUI == null)
    {
        throw new System.InvalidOperationException("DefenseScene에서 카시아 로드아웃 원본 컴포넌트를 모두 찾지 못했습니다.");
    }

    RCCom.Data.PlayerData data = player.data;
    UnityEngine.Debug.Log(
        $"[OperatorBuildSources] PlayerData: maxHealth={data.maxHealth}, moveSpeed={data.moveSpeed}, " +
        $"hitInvulnerabilityDuration={data.hitInvulnerabilityDuration}, attackDamage={data.attackDamage}, " +
        $"attackRange={data.attackRange}, attackInterval={data.attackInterval}, projectileSpeed={data.projectileSpeed}, " +
        $"skillCooldown={data.skillCooldown}, skillRange={data.skillRange}, skillDamage={data.skillDamage}");

    var buildSerialized = new UnityEditor.SerializedObject(buildController);
    var cardSerialized = new UnityEditor.SerializedObject(cardManager);
    var dialogueSerialized = new UnityEditor.SerializedObject(dialogueUI);

    UnityEngine.Object towerRoster = buildSerialized.FindProperty("towerRoster").objectReferenceValue;
    UnityEngine.Object cardRoster = cardSerialized.FindProperty("cardRoster").objectReferenceValue;
    UnityEngine.Object dialogueSet = dialogueSerialized.FindProperty("dialogueSet").objectReferenceValue;

    UnityEngine.Debug.Log($"[OperatorBuildSources] Scene TowerRoster: {UnityEditor.AssetDatabase.GetAssetPath(towerRoster)}");
    UnityEngine.Debug.Log($"[OperatorBuildSources] Scene CardRoster: {UnityEditor.AssetDatabase.GetAssetPath(cardRoster)}");
    UnityEngine.Debug.Log($"[OperatorBuildSources] Scene DialogueSet: {UnityEditor.AssetDatabase.GetAssetPath(dialogueSet)}");

    var typedDialogueSet = dialogueSet as RCCom.UI.OperatorDialogueSet;
    UnityEngine.Debug.Log($"[OperatorBuildSources] Selection Portrait: {UnityEditor.AssetDatabase.GetAssetPath(typedDialogueSet.idleSprite)}");
}
finally
{
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(defenseScene, true);
}
