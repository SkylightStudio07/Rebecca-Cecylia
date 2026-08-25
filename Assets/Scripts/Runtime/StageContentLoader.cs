using System;
using System.Collections;
using RCCom.Definitions.Stage;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace RCCom.Runtime
{
    /// <summary>
    /// 스테이지 선택 화면이 StageDefinition을 확보하는 단일 경로.
    ///
    /// 카탈로그가 직접 참조를 들고 있으면(빌드에 포함된 로컬 스테이지) 그대로 쓰고, 없으면
    /// (원격 스테이지) Addressables로 내려받는다. 두 경우를 화면마다 각각 분기하면 한쪽만
    /// 진행 표시나 실패 처리를 빠뜨리게 되므로 OperatorContentLoader와 같은 모양으로 묶는다.
    /// </summary>
    public static class StageContentLoader
    {
        public static IEnumerator Load(
            StageCatalogEntry entry,
            Action<string, float> onProgress,
            Action<StageDefinition> onSucceeded,
            Action<string> onFailed)
        {
            if (entry == null)
            {
                onFailed?.Invoke("스테이지 카탈로그 정보가 올바르지 않습니다.");
                yield break;
            }

            // 빌드에 이미 들어 있는 스테이지는 Addressables 왕복 없이 즉시 넘긴다.
            if (entry.stageDefinition != null)
            {
                onProgress?.Invoke("준비 완료", 1f);
                onSucceeded?.Invoke(entry.stageDefinition);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(entry.address))
            {
                onFailed?.Invoke("스테이지 전투 데이터가 준비되지 않았습니다.");
                yield break;
            }

            onProgress?.Invoke("콘텐츠 확인 중…", 0f);
            // 자동 해제를 허용하면 yield 복귀 시점에는 핸들이 이미 무효화되어 Status 조회가 예외를 낸다.
            AsyncOperationHandle initialization = Addressables.InitializeAsync(false);
            yield return initialization;
            bool initialized = initialization.Status == AsyncOperationStatus.Succeeded;
            Addressables.Release(initialization);
            if (!initialized)
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
                    onProgress?.Invoke($"스테이지 다운로드 중… {progress:P0}", progress * 0.9f);
                    yield return null;
                }

                bool downloaded = downloadHandle.Status == AsyncOperationStatus.Succeeded;
                Addressables.Release(downloadHandle);
                if (!downloaded)
                {
                    onFailed?.Invoke("스테이지 다운로드에 실패했습니다. 다시 시도하세요.");
                    yield break;
                }
            }

            onProgress?.Invoke("스테이지 불러오는 중…", 0.95f);
            AsyncOperationHandle<StageDefinition> definitionHandle =
                Addressables.LoadAssetAsync<StageDefinition>(entry.address);
            yield return definitionHandle;

            if (definitionHandle.Status != AsyncOperationStatus.Succeeded || definitionHandle.Result == null)
            {
                if (definitionHandle.IsValid())
                {
                    Addressables.Release(definitionHandle);
                }

                onFailed?.Invoke("스테이지를 불러오지 못했습니다. 다시 시도하세요.");
                yield break;
            }

            if (definitionHandle.Result.stageId != entry.stageId)
            {
                Addressables.Release(definitionHandle);
                onFailed?.Invoke("카탈로그와 스테이지 ID가 일치하지 않습니다.");
                yield break;
            }

            // 핸들은 의도적으로 Release하지 않는다. 전투 씬이 이 Definition을 계속 참조하므로
            // 여기서 해제하면 곧바로 다시 받아야 한다(OperatorContentLoader와 같은 이유).
            onProgress?.Invoke("준비 완료", 1f);
            onSucceeded?.Invoke(definitionHandle.Result);
        }
    }
}
