UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(
    out UnityEditor.AddressableAssets.Build.AddressablesPlayerBuildResult rccomResult);
UnityEngine.Debug.Log(string.IsNullOrEmpty(rccomResult.Error)
    ? $"[BuildAddressablesContent] 완료 ({rccomResult.Duration:F1}s)"
    : $"[BuildAddressablesContent] 실패: {rccomResult.Error}");
