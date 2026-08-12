const string storageKey = "RCCom.PlayerProfile.CodexVerification";

try
{
    UnityEngine.PlayerPrefs.DeleteKey(storageKey);
    UnityEngine.PlayerPrefs.Save();

    var storage = new RCCom.Runtime.PlayerPrefsProfileStorage(storageKey);
    RCCom.Data.PlayerProfile initial = storage.Load();

    if (initial.bestWave != 0 || initial.selectedOperatorId != string.Empty)
    {
        throw new System.InvalidOperationException("저장이 없는 프로필의 기본값이 올바르지 않습니다.");
    }

    var expected = new RCCom.Data.PlayerProfile
    {
        schemaVersion = 999,
        bestWave = 7,
        selectedOperatorId = "codex-verification",
    };

    storage.Save(expected);
    RCCom.Data.PlayerProfile loaded = storage.Load();

    if (loaded.schemaVersion != RCCom.Data.PlayerProfile.CurrentSchemaVersion ||
        loaded.bestWave != expected.bestWave ||
        loaded.selectedOperatorId != expected.selectedOperatorId)
    {
        throw new System.InvalidOperationException("프로필 저장/불러오기 왕복 결과가 일치하지 않습니다.");
    }

    UnityEngine.Debug.Log("[ProfileVerification] PlayerPrefs 저장/불러오기 왕복 검증 통과");
}
finally
{
    UnityEngine.PlayerPrefs.DeleteKey(storageKey);
    UnityEngine.PlayerPrefs.Save();
}
