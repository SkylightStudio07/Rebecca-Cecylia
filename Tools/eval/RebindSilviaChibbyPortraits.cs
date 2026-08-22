string portraitFolder = "Assets/Art/Character Standing Arts/실비아/portrait";
UnityEngine.Sprite LoadPortrait(string expression)
{
    string path = $"{portraitFolder}/실비아.Chibby.{expression}.png";
    UnityEngine.Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);
    if (sprite == null)
    {
        throw new System.InvalidOperationException($"Chibby Sprite를 찾지 못했습니다: {path}");
    }

    return sprite;
}

RCCom.UI.OperatorDialogueSet dialogueSet = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.UI.OperatorDialogueSet>(
    "Assets/Data/Operators/racing/OperatorDialogueSet.asset");
RCCom.Definitions.Operator.OperatorDefinition definition =
    UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorDefinition>(
        "Assets/Data/Operators/racing/OperatorDefinition.asset");
RCCom.Definitions.Operator.OperatorCatalog catalog =
    UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.Definitions.Operator.OperatorCatalog>(
        "Assets/Data/Operators/OperatorCatalog.asset");

if (dialogueSet == null || definition == null || catalog == null)
{
    throw new System.InvalidOperationException("실비아 포트릿 배선 대상 에셋을 모두 찾지 못했습니다.");
}

dialogueSet.EnsureLineSets();
dialogueSet.gameStart.portraitSprite = LoadPortrait("giggling");
dialogueSet.skillUsed.portraitSprite = LoadPortrait("evil smile");
dialogueSet.baseAttacked.portraitSprite = LoadPortrait("angry-1");
dialogueSet.playerHit.portraitSprite = LoadPortrait("confused");
dialogueSet.playerHitCritical.portraitSprite = LoadPortrait("depressed");
dialogueSet.insufficientGold.portraitSprite = LoadPortrait("annoyed");
dialogueSet.slotUnavailable.portraitSprite = LoadPortrait("disgusted");
dialogueSet.playerDied.portraitSprite = LoadPortrait("crying with eyes open");
dialogueSet.baseDestroyed.portraitSprite = LoadPortrait("crying with eyes closed");

UnityEngine.Sprite selectionPortrait = LoadPortrait("default-1");
definition.selectionPortrait = selectionPortrait;

bool catalogEntryFound = false;
if (catalog.entries != null)
{
    for (int i = 0; i < catalog.entries.Count; i++)
    {
        RCCom.Definitions.Operator.OperatorCatalogEntry entry = catalog.entries[i];
        if (entry != null && entry.operatorId == "racing")
        {
            entry.previewPortrait = selectionPortrait;
            catalogEntryFound = true;
            break;
        }
    }
}

if (!catalogEntryFound)
{
    throw new System.InvalidOperationException("OperatorCatalog에서 racing 항목을 찾지 못했습니다.");
}

UnityEditor.EditorUtility.SetDirty(dialogueSet);
UnityEditor.EditorUtility.SetDirty(definition);
UnityEditor.EditorUtility.SetDirty(catalog);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();

UnityEngine.Debug.Log(
    $"[RebindSilviaChibbyPortraits] gameStart={dialogueSet.gameStart.portraitSprite.name}, " +
    $"skillUsed={dialogueSet.skillUsed.portraitSprite.name}, " +
    $"baseAttacked={dialogueSet.baseAttacked.portraitSprite.name}, " +
    $"playerHit={dialogueSet.playerHit.portraitSprite.name}, " +
    $"playerHitCritical={dialogueSet.playerHitCritical.portraitSprite.name}, " +
    $"insufficientGold={dialogueSet.insufficientGold.portraitSprite.name}, " +
    $"slotUnavailable={dialogueSet.slotUnavailable.portraitSprite.name}, " +
    $"playerDied={dialogueSet.playerDied.portraitSprite.name}, " +
    $"baseDestroyed={dialogueSet.baseDestroyed.portraitSprite.name}, " +
    $"selection={definition.selectionPortrait.name}, catalogPreview={selectionPortrait.name}");
