{
    const string storageKey = "RCCom.Verify.PlayerProfileProgress";
    UnityEngine.PlayerPrefs.DeleteKey(storageKey);

    try
    {
        var storage = new RCCom.Runtime.PlayerPrefsProfileStorage(storageKey);
        RCCom.Data.PlayerProfile profile = storage.Load();

        if (!profile.TryRecordBestWave(4) || profile.bestWave != 4)
        {
            throw new System.InvalidOperationException("첫 최고 웨이브가 기록되지 않았습니다.");
        }

        if (profile.TryRecordBestWave(2) || profile.TryRecordBestWave(-1) || profile.bestWave != 4)
        {
            throw new System.InvalidOperationException("최고 웨이브가 낮은 값으로 덮어써졌습니다.");
        }

        storage.Save(profile);
        RCCom.Data.PlayerProfile reloaded = storage.Load();
        if (reloaded.bestWave != 4)
        {
            throw new System.InvalidOperationException("최고 웨이브 저장 왕복에 실패했습니다.");
        }

        UnityEngine.Debug.Log("[VerifyPlayerProfileProgress] 최고 웨이브 단조 증가 및 저장 왕복 검증 통과");
    }
    finally
    {
        UnityEngine.PlayerPrefs.DeleteKey(storageKey);
        UnityEngine.PlayerPrefs.Save();
    }
}
