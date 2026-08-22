var dialogueSet = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.UI.OperatorDialogueSet>(
    "Assets/Data/Prefabs/UI/New Operator Dialogue Set.asset");
if (dialogueSet == null)
{
    throw new System.InvalidOperationException("Cassia OperatorDialogueSet을 찾지 못했습니다.");
}

dialogueSet.EnsureLineSets();
var lineSets = new RCCom.UI.OperatorLineSet[]
{
    dialogueSet.lobbyInteraction, dialogueSet.lobbyReturnTogether, dialogueSet.lobbyReturn,
    dialogueSet.lobbyTouchUnfamiliar, dialogueSet.lobbyTouchFavorable, dialogueSet.lobbyTouchJoy,
    dialogueSet.lobbyTouchLove, dialogueSet.lobbyTouchEx, dialogueSet.gameStart, dialogueSet.skillUsed,
    dialogueSet.baseAttacked, dialogueSet.playerHit, dialogueSet.playerHitCritical,
    dialogueSet.insufficientGold, dialogueSet.slotUnavailable, dialogueSet.playerDied,
    dialogueSet.baseDestroyed,
};

int migrated = 0;
for (int i = 0; i < lineSets.Length; i++)
{
    RCCom.UI.OperatorLineSet lineSet = lineSets[i];
    if (lineSet == null)
    {
        continue;
    }

    if (lineSet.portraitSprite == null)
    {
        lineSet.portraitSprite = lineSet.defaultPortraitSprite;
    }

    migrated += lineSet.MigrateLegacyEntries();
}

UnityEditor.EditorUtility.SetDirty(dialogueSet);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.AssetDatabase.Refresh();
UnityEngine.Debug.Log($"[MigrateOperatorDialogueLines] {migrated}개 대사 엔트리 마이그레이션 완료");
