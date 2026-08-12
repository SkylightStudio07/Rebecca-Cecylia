foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:TowerRoster"))
{
    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var roster = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Tower.TowerRoster>(path);
    var entries = new System.Collections.Generic.List<string>();

    foreach (RCCom.Definitions.Tower.TowerDefinition definition in roster.towers)
    {
        entries.Add(definition == null ? "<null>" : UnityEditor.AssetDatabase.GetAssetPath(definition));
    }

    UnityEngine.Debug.Log($"[TowerRegistration] Roster {path}: {string.Join(" | ", entries)}");
}

foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:UnlockTowerCard"))
{
    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var card = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Effects.Card.Concrete.UnlockTowerCard>(path);
    var serializedCard = new UnityEditor.SerializedObject(card);
    UnityEngine.Object definition = serializedCard.FindProperty("unlockDefinition").objectReferenceValue;
    UnityEngine.Debug.Log($"[TowerRegistration] Unlock {path}: {UnityEditor.AssetDatabase.GetAssetPath(definition)}");
}
