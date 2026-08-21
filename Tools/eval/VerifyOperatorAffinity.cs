const string storageKey = "RCCom.Verify.OperatorAffinity";
UnityEngine.PlayerPrefs.DeleteKey(storageKey);
UnityEngine.PlayerPrefs.Save();

try
{
    var storage = new RCCom.Runtime.PlayerPrefsProfileStorage(storageKey);
    RCCom.Data.PlayerProfile profile = storage.Load();
    profile.SetOperatorAffinity("cassia", 24);
    if (profile.GetOperatorAffinityTier("cassia") != RCCom.Data.OperatorAffinityTier.Unfamiliar)
    {
        throw new System.InvalidOperationException("24 호감도가 낯섦 단계가 아닙니다.");
    }

    profile.SetOperatorAffinity("cassia", 25);
    if (profile.GetOperatorAffinityTier("cassia") != RCCom.Data.OperatorAffinityTier.Favorable)
    {
        throw new System.InvalidOperationException("25 호감도가 호감 단계가 아닙니다.");
    }

    profile.SetOperatorAffinity("cassia", 100);
    if (profile.GetOperatorAffinity("cassia") != 100 ||
        profile.GetOperatorAffinityTier("cassia") != RCCom.Data.OperatorAffinityTier.Love)
    {
        throw new System.InvalidOperationException("호감도 100 정규화 또는 사랑 단계 판정에 실패했습니다.");
    }

    profile.SetOperatorAffinity("cassia", 0);
    profile.QueueBattleReturn("cassia");
    if (!profile.TryClaimBattleReturn("cassia", out int granted, out bool participated) ||
        granted != RCCom.Data.PlayerProfile.ReturnAffinityWithParticipation || !participated ||
        profile.GetOperatorAffinity("cassia") != 5 || profile.pendingReturnCount != 0)
    {
        throw new System.InvalidOperationException("참전 귀환 +5 소비에 실패했습니다.");
    }

    profile.QueueBattleReturn("cassia");
    if (!profile.TryClaimBattleReturn("other", out granted, out participated) ||
        granted != RCCom.Data.PlayerProfile.ReturnAffinityWithoutParticipation || participated ||
        profile.GetOperatorAffinity("other") != 2)
    {
        throw new System.InvalidOperationException("비참전 오퍼레이터 귀환 +2 소비에 실패했습니다.");
    }

    storage.Save(profile);
    RCCom.Data.PlayerProfile reloaded = storage.Load();
    if (reloaded.GetOperatorAffinity("other") != 2 ||
        reloaded.schemaVersion != RCCom.Data.PlayerProfile.CurrentSchemaVersion)
    {
        throw new System.InvalidOperationException("호감도 저장 왕복에 실패했습니다.");
    }

    UnityEngine.Debug.Log("[VerifyOperatorAffinity] 호감도 경계·귀환 보상·저장 왕복 검증 통과");
}
finally
{
    UnityEngine.PlayerPrefs.DeleteKey(storageKey);
    UnityEngine.PlayerPrefs.Save();
}
