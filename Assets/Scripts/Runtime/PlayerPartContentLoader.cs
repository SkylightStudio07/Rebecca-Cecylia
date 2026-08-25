using System.Collections.Generic;
using RCCom.Definitions.PlayerPart;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace RCCom.Runtime
{
    /// <summary>
    /// 파츠 카탈로그를 어드레서블 주소로 우선 조회하고, 아직 콘텐츠가 빌드되지 않았거나(개발
    /// 중) 초기화·조회가 실패하면 조용히 호출부의 폴백(Resources 내장 카탈로그)으로 넘어간다.
    /// LiveCatalogService와 같은 원칙 — 앱 수명 전체를 사는 조회이며 실패를 오류로 보이지 않게
    /// 흡수한다 — 을 카탈로그 하나짜리 조회에 맞춰 축소한 버전이다.
    /// </summary>
    public static class PlayerPartContentLoader
    {
        public const string CatalogAddress = "playerparts/catalog";

        /// <summary>원격 조회가 아니라도 초기화·조회가 끝났는지. 실패해도(빌드 전 등) true가 된다.</summary>
        public static bool IsResolved { get; private set; }

        private static PlayerPartCatalog _addressableCatalog;
        private static bool _started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewApplicationRun()
        {
            IsResolved = false;
            _addressableCatalog = null;
            _started = false;
        }

        /// <summary>
        /// 씬 로드 전에 조회를 시작한다. 로컬 그룹 조회라 대개 첫 전투 씬 진입 전에 끝나므로,
        /// 대부분의 실행에서 ComposePlayerLoadout 시점에는 이미 결과가 채워져 있다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Begin()
        {
            if (_started)
            {
                return;
            }

            _started = true;

            // 자동 해제를 허용하면 콜백 시점에 핸들이 이미 무효화되어 Status 조회가 예외를 낸다
            // (LiveCatalogService/BattleContentCache와 같은 이유로 false를 넘긴다).
            Addressables.InitializeAsync(false).Completed += initialization =>
            {
                bool initialized = initialization.Status == AsyncOperationStatus.Succeeded;
                Addressables.Release(initialization);
                if (!initialized)
                {
                    Finish();
                    return;
                }

                LoadIfPublished();
            };
        }

        /// <summary>
        /// 어드레서블 카탈로그가 준비됐으면 그것을, 아니면(아직 조회 중이거나 실패) 호출부가
        /// 넘긴 fallback을 돌려준다. fallback이 null이어도 안전하다.
        /// </summary>
        public static PlayerPartCatalog Resolve(PlayerPartCatalog fallback)
        {
            return _addressableCatalog != null ? _addressableCatalog : fallback;
        }

        /// <summary>
        /// 주소를 바로 LoadAssetAsync에 넘기면, 어드레서블 콘텐츠를 아직 한 번도 빌드하지 않은
        /// 개발 중 환경에서 InvalidKeyException이 콘솔 오류로 남는다. 정상적인 "아직 없음"
        /// 상태를 오류로 보이게 하지 않으려고 로케이션 조회로 존재 여부를 먼저 확인한다.
        /// </summary>
        private static void LoadIfPublished()
        {
            AsyncOperationHandle<IList<IResourceLocation>> locations =
                Addressables.LoadResourceLocationsAsync(CatalogAddress, typeof(PlayerPartCatalog));
            locations.Completed += located =>
            {
                bool exists = located.Status == AsyncOperationStatus.Succeeded &&
                              located.Result != null && located.Result.Count > 0;
                Addressables.Release(located);
                if (!exists)
                {
                    Finish();
                    return;
                }

                Addressables.LoadAssetAsync<PlayerPartCatalog>(CatalogAddress).Completed += loaded =>
                {
                    if (loaded.Status == AsyncOperationStatus.Succeeded && loaded.Result != null)
                    {
                        // 앱이 사는 동안 계속 참조되므로 의도적으로 Release하지 않는다
                        // (LiveCatalogService의 원격 카탈로그 핸들과 같은 정책).
                        _addressableCatalog = loaded.Result;
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[PlayerPartContentLoader] 파츠 카탈로그를 어드레서블로 받지 못했습니다. " +
                            "Resources 폴백으로 진행합니다.");
                    }

                    Finish();
                };
            };
        }

        private static void Finish()
        {
            IsResolved = true;
        }
    }
}
