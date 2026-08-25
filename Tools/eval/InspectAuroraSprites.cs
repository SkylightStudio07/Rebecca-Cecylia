RCCom.Definitions.Operator.OperatorDefinition definition =
    UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorDefinition>(
        "Assets/Data/Operators/aurora/OperatorDefinition.asset");
if (definition == null)
{
    UnityEngine.Debug.LogError("[AuroraSprite] Definition을 찾지 못했습니다.");
}
else
{
    void LogSprite(string slot, UnityEngine.Sprite sprite)
    {
        UnityEngine.Debug.Log(sprite != null
            ? $"[AuroraSprite] {slot}: {sprite.name} rect={sprite.rect.width}x{sprite.rect.height}"
            : $"[AuroraSprite] {slot}: null");
    }

    LogSprite("shopPortrait", definition.shopPortrait);
    LogSprite("managementPortrait", definition.managementPortrait);
    if (definition.dialogueSet != null)
    {
        LogSprite("lobbyIdle", definition.dialogueSet.lobbyIdleSprite);
        System.Reflection.FieldInfo[] fields = definition.dialogueSet.GetType().GetFields(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        foreach (System.Reflection.FieldInfo field in fields)
        {
            if (!typeof(RCCom.UI.OperatorLineSet).IsAssignableFrom(field.FieldType))
            {
                continue;
            }

            RCCom.UI.OperatorLineSet lineSet = field.GetValue(definition.dialogueSet) as RCCom.UI.OperatorLineSet;
            if (lineSet == null || lineSet.entries == null)
            {
                continue;
            }

            for (int i = 0; i < lineSet.entries.Count; i++)
            {
                LogSprite(field.Name + "[" + i + "]", lineSet.entries[i].lobbySprite);
            }
        }
    }
}
