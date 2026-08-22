string portraitFolder = "Assets/Art/Character Standing Arts/실비아/portrait";
UnityEngine.Sprite LoadExpected(string expression)
{
    string path = $"{portraitFolder}/실비아.Chibby.{expression}.png";
    UnityEngine.Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);
    if (sprite == null)
    {
        throw new System.InvalidOperationException($"Sprite 로드 실패: {path}");
    }

    return sprite;
}

void AssertPortrait(string label, RCCom.UI.OperatorLineSet lineSet, string expression)
{
    UnityEngine.Sprite expected = LoadExpected(expression);
    if (lineSet == null || lineSet.portraitSprite != expected)
    {
        throw new System.InvalidOperationException(
            $"{label} 포트릿 불일치: expected={expected.name}, actual={(lineSet == null || lineSet.portraitSprite == null ? "<null>" : lineSet.portraitSprite.name)}");
    }
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
    throw new System.InvalidOperationException("실비아 검증 대상 에셋을 모두 찾지 못했습니다.");
}

AssertPortrait("gameStart", dialogueSet.gameStart, "giggling");
AssertPortrait("skillUsed", dialogueSet.skillUsed, "evil smile");
AssertPortrait("baseAttacked", dialogueSet.baseAttacked, "angry-1");
AssertPortrait("playerHit", dialogueSet.playerHit, "confused");
AssertPortrait("playerHitCritical", dialogueSet.playerHitCritical, "depressed");
AssertPortrait("insufficientGold", dialogueSet.insufficientGold, "annoyed");
AssertPortrait("slotUnavailable", dialogueSet.slotUnavailable, "disgusted");
AssertPortrait("playerDied", dialogueSet.playerDied, "crying with eyes open");
AssertPortrait("baseDestroyed", dialogueSet.baseDestroyed, "crying with eyes closed");

UnityEngine.Sprite selectionPortrait = LoadExpected("default-1");
if (definition.selectionPortrait != selectionPortrait)
{
    throw new System.InvalidOperationException("OperatorDefinition.selectionPortrait 연결이 올바르지 않습니다.");
}

bool catalogPreviewMatches = false;
for (int i = 0; i < catalog.entries.Count; i++)
{
    RCCom.Definitions.Operator.OperatorCatalogEntry entry = catalog.entries[i];
    if (entry != null && entry.operatorId == "racing")
    {
        catalogPreviewMatches = entry.previewPortrait == selectionPortrait;
        break;
    }
}
if (!catalogPreviewMatches)
{
    throw new System.InvalidOperationException("OperatorCatalog racing previewPortrait 연결이 올바르지 않습니다.");
}

string[] guids = UnityEditor.AssetDatabase.FindAssets("Chibby", new[] { portraitFolder });
if (guids.Length != 27)
{
    throw new System.InvalidOperationException($"Chibby 포트릿 수가 예상과 다릅니다: {guids.Length}");
}

for (int i = 0; i < guids.Length; i++)
{
    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
    UnityEngine.Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(path);
    UnityEditor.TextureImporter importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
    UnityEngine.Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(path);
    if (texture == null || importer == null || sprite == null || importer.spritesheet == null ||
        importer.spritesheet.Length != 1 || importer.spritesheet[0].rect != new UnityEngine.Rect(0f, 0f, texture.width, texture.height))
    {
        throw new System.InvalidOperationException($"Chibby Sprite importer 검증 실패: {path}");
    }
}

UnityEngine.Debug.Log("[VerifySilviaChibbyPortraits] 실비아 Chibby 27개 importer와 9개 전투 포트릿/선택 포트릿 검증 통과");
