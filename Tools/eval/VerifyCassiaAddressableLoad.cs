UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle initialization =
    UnityEngine.AddressableAssets.Addressables.InitializeAsync();
initialization.WaitForCompletion();
if (initialization.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
{
    throw new System.InvalidOperationException("Addressables 초기화에 실패했습니다.");
}

UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<RCCom.Definitions.Operator.OperatorDefinition> handle =
    UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<RCCom.Definitions.Operator.OperatorDefinition>("operator/cassia");
RCCom.Definitions.Operator.OperatorDefinition definition = handle.WaitForCompletion();
if (handle.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ||
    definition == null || definition.operatorId != "cassia")
{
    if (handle.IsValid())
    {
        UnityEngine.AddressableAssets.Addressables.Release(handle);
    }

    throw new System.InvalidOperationException("Addressables 주소로 Cassia Definition을 불러오지 못했습니다.");
}

UnityEngine.AddressableAssets.Addressables.Release(handle);
UnityEngine.Debug.Log("[OperatorAddressableVerification] operator/cassia 로드 및 릴리스 검증 통과");
