using System;
using System.Collections;
using RCCom.Definitions.Operator;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RCCom.Runtime
{
    /// <summary>
    /// 타이틀의 여러 오퍼레이터 화면이 동일한 Addressables 검증·다운로드 절차를 공유한다.
    /// 화면마다 로딩 코드를 복제하면 한쪽만 ID 검증이나 핸들 소유권 이전을 빠뜨릴 수 있어
    /// 세션에 선택이 완료되는 지점까지 이 단일 경로로 묶는다.
    /// </summary>
    public static class OperatorContentLoader
    {
        public static IEnumerator LoadAndSelect(OperatorCatalogEntry entry,
            Action<string, float> onProgress, Action<OperatorDefinition> onSucceeded,
            Action<string> onFailed)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.operatorId) ||
                string.IsNullOrWhiteSpace(entry.address))
            {
                onFailed?.Invoke("오퍼레이터 카탈로그 정보가 올바르지 않습니다.");
                yield break;
            }

            if (OperatorLoadoutSession.SelectedDefinition != null &&
                OperatorLoadoutSession.SelectedDefinition.operatorId == entry.operatorId)
            {
                onProgress?.Invoke("준비 완료", 1f);
                onSucceeded?.Invoke(OperatorLoadoutSession.SelectedDefinition);
                yield break;
            }

            onProgress?.Invoke("콘텐츠 확인 중…", 0f);
            // 자동 해제를 허용하면 yield 복귀 시점에는 핸들이 이미 무효화되어 Status 조회가 예외를 낸다.
            AsyncOperationHandle initialization = Addressables.InitializeAsync(false);
            yield return initialization;
            bool initializationSucceeded = initialization.Status == AsyncOperationStatus.Succeeded;
            Addressables.Release(initialization);
            if (!initializationSucceeded)
            {
                onFailed?.Invoke("Addressables 초기화에 실패했습니다.");
                yield break;
            }

            AsyncOperationHandle<long> sizeHandle = Addressables.GetDownloadSizeAsync(entry.address);
            yield return sizeHandle;
            if (sizeHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(sizeHandle);
                onFailed?.Invoke("다운로드 크기를 확인하지 못했습니다. 네트워크 연결을 확인하세요.");
                yield break;
            }

            long downloadBytes = sizeHandle.Result;
            Addressables.Release(sizeHandle);

            if (downloadBytes > 0L)
            {
                AsyncOperationHandle downloadHandle = Addressables.DownloadDependenciesAsync(entry.address, false);
                while (!downloadHandle.IsDone)
                {
                    float progress = downloadHandle.GetDownloadStatus().Percent;
                    onProgress?.Invoke($"콘텐츠 다운로드 중… {progress:P0}", progress * 0.85f);
                    yield return null;
                }

                if (downloadHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    Addressables.Release(downloadHandle);
                    onFailed?.Invoke("콘텐츠 다운로드에 실패했습니다. 다시 시도하세요.");
                    yield break;
                }

                Addressables.Release(downloadHandle);
            }

            onProgress?.Invoke("오퍼레이터 불러오는 중…", 0.9f);
            AsyncOperationHandle<OperatorDefinition> definitionHandle =
                Addressables.LoadAssetAsync<OperatorDefinition>(entry.address);
            yield return definitionHandle;

            if (definitionHandle.Status != AsyncOperationStatus.Succeeded || definitionHandle.Result == null)
            {
                if (definitionHandle.IsValid())
                {
                    Addressables.Release(definitionHandle);
                }

                onFailed?.Invoke("오퍼레이터를 불러오지 못했습니다. 다시 시도하세요.");
                yield break;
            }

            if (definitionHandle.Result.operatorId != entry.operatorId)
            {
                Addressables.Release(definitionHandle);
                onFailed?.Invoke("카탈로그와 오퍼레이터 ID가 일치하지 않습니다.");
                yield break;
            }

            OperatorLoadoutSession.SelectAddressable(definitionHandle);
            onProgress?.Invoke("준비 완료", 1f);
            onSucceeded?.Invoke(definitionHandle.Result);
        }
    }
}
