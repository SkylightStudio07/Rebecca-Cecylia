var set = UnityEditor.AssetDatabase.LoadAssetAtPath<RCCom.UI.OperatorDialogueSet>("Assets/Data/Operators/valentina/OperatorDialogueSet.asset");
if (set == null || set.lobbyIdleSprite == null || set.idleSprite == null) throw new System.Exception("발렌티나 기본 스프라이트가 비어 있습니다.");
var lineSets = new RCCom.UI.OperatorLineSet[] { set.operatorAcquired, set.lobbyInteraction, set.lobbyReturnTogether, set.lobbyReturn, set.lobbyTouchUnfamiliar, set.lobbyTouchFavorable, set.lobbyTouchJoy, set.lobbyTouchLove, set.lobbyTouchEx, set.gameStart, set.skillUsed, set.baseAttacked, set.playerHit, set.playerHitCritical, set.insufficientGold, set.slotUnavailable, set.playerDied, set.baseDestroyed };
var count = 0;
foreach (var lineSet in lineSets)
{
    if (lineSet == null || lineSet.portraitSprite == null || lineSet.defaultLobbySprite == null) throw new System.Exception("상황 기본 스프라이트가 비어 있습니다.");
    foreach (var entry in lineSet.entries)
    {
        if (entry == null || entry.lobbySprite == null || entry.portraitSprite == null) throw new System.Exception("문장별 스프라이트가 비어 있습니다.");
        count++;
    }
}
if (count != 121) throw new System.Exception($"대사 수가 예상과 다릅니다: {count}");
UnityEngine.Debug.Log($"[VerifyValentinaDialogue] 18개 상황, {count}개 대사 스프라이트 참조 검증 통과");
