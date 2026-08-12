{
    string normalized = RCCom.EditorTools.AddressablesRemoteProfileConfigurator.NormalizeRemoteLoadPath(
        " https://cdn.example.com/rccom/[BuildTarget]/ ");
    if (normalized != "https://cdn.example.com/rccom/[BuildTarget]")
    {
        throw new System.InvalidOperationException("원격 로드 주소 정규화에 실패했습니다.");
    }

    bool rejectedUnsafeAddress = false;
    try
    {
        RCCom.EditorTools.AddressablesRemoteProfileConfigurator.NormalizeRemoteLoadPath(
            "http://cdn.example.com/rccom");
    }
    catch (System.InvalidOperationException)
    {
        rejectedUnsafeAddress = true;
    }

    if (!rejectedUnsafeAddress)
    {
        throw new System.InvalidOperationException("HTTPS가 아닌 원격 서버 주소를 허용했습니다.");
    }

    RCCom.EditorTools.AddressablesBuildValidator.ValidateOrThrow(UnityEditor.BuildTarget.WebGL);
    UnityEngine.Debug.Log("[VerifyAddressablesAutomation] URL 계약 및 WebGL 사전 검증 통과");
}
