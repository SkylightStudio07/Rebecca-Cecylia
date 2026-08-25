using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 원격 서버 주소를 저장소에 하드코딩하지 않고 환경 변수에서 활성 Addressables 프로필로 주입한다.
    /// CI와 로컬 최종 빌드가 같은 진입점을 쓰게 해 수동 Profile 창 편집 누락을 막는다.
    /// </summary>
    public static class AddressablesRemoteProfileConfigurator
    {
        public const string RemoteLoadPathEnvironmentVariable = "RCCOM_REMOTE_LOAD_PATH";
        public const string RemoteBuildPathEnvironmentVariable = "RCCOM_REMOTE_BUILD_PATH";
        public const string DefaultRemoteBuildPath = "ServerData/[BuildTarget]";

        [MenuItem("RCCom/Addressables/Configure Active Remote Profile From Environment")]
        public static void ConfigureFromEnvironment()
        {
            string remoteLoadPath = Environment.GetEnvironmentVariable(RemoteLoadPathEnvironmentVariable);
            string remoteBuildPath = Environment.GetEnvironmentVariable(RemoteBuildPathEnvironmentVariable);
            ConfigureActiveProfile(remoteLoadPath, remoteBuildPath);
        }

        public static void ConfigureActiveProfile(string remoteLoadPath, string remoteBuildPath = null)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings가 없습니다.");
            }

            string normalizedLoadPath = NormalizeRemoteLoadPath(remoteLoadPath);
            string normalizedBuildPath = string.IsNullOrWhiteSpace(remoteBuildPath)
                ? DefaultRemoteBuildPath
                : remoteBuildPath.Trim().TrimEnd('/', '\\');

            AddressableAssetProfileSettings profiles = settings.profileSettings;
            if (profiles.GetProfileDataByName(AddressableAssetSettings.kRemoteLoadPath) == null ||
                profiles.GetProfileDataByName(AddressableAssetSettings.kRemoteBuildPath) == null)
            {
                throw new InvalidOperationException("Addressables 원격 경로 프로필 변수를 찾지 못했습니다.");
            }

            // SetValue의 두 번째 인자는 변수 "이름"이다(예: "Remote.LoadPath"). id(GUID)를
            // 넘기면 내부적으로 그 문자열을 이름으로 다시 검색하다 실패해, 예외 없이 콘솔
            // 오류만 남기고 아무 것도 바뀌지 않는다 — 반드시 상수 이름 그대로 넘긴다.
            profiles.SetValue(settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath, normalizedLoadPath);
            profiles.SetValue(settings.activeProfileId, AddressableAssetSettings.kRemoteBuildPath, normalizedBuildPath);
            EnsureRemoteCatalogEnabled(settings);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // SetValue는 실패해도 예외를 던지지 않으므로, 실제로 반영됐는지 다시 읽어
            // 검증하지 않으면 아래 성공 로그가 거짓말을 할 수 있다.
            string appliedLoadPath = profiles.GetValueByName(settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath);
            string appliedBuildPath = profiles.GetValueByName(settings.activeProfileId, AddressableAssetSettings.kRemoteBuildPath);
            if (appliedLoadPath != normalizedLoadPath || appliedBuildPath != normalizedBuildPath)
            {
                throw new InvalidOperationException(
                    "Addressables 원격 경로 반영에 실패했습니다. Remote.LoadPath/Remote.BuildPath 프로필 변수가 " +
                    "활성 프로필에 있는지 확인하세요.");
            }

            Debug.Log($"[AddressablesProfile] 활성 프로필 원격 경로 설정 완료: {normalizedLoadPath}");
        }

        /// <summary>
        /// 원격 카탈로그를 켠다. 이게 꺼져 있으면 Remote 그룹이라도 "번들만 원격, 카탈로그는
        /// 빌드에 내장"인 상태라 빌드 이후에 새 ID를 추가해도 플레이어가 알 수 없다 —
        /// 즉 진짜 라이브 드랍이 불가능하다. .asset YAML을 직접 켜면 AGENTS.md 규칙 위반이라
        /// 여기서 코드로만 설정한다. 이미 같은 값이면 다시 쓰지 않아 불필요한 SetDirty를 피한다.
        /// </summary>
        private static void EnsureRemoteCatalogEnabled(AddressableAssetSettings settings)
        {
            if (!settings.BuildRemoteCatalog)
            {
                settings.BuildRemoteCatalog = true;
            }

            // ProfileValueReference.GetName()은 참조가 아직 어떤 변수도 안 가리키고 있으면
            // (최초 설정 시 Id가 비어 있으면) 콘솔에 경고를 남기고 빈 문자열을 반환한다.
            // 여기서는 그 경고 없이 조용히 비교하기 위해 GetName 대신 Id를 직접 비교한다.
            string remoteBuildId = settings.profileSettings.GetProfileDataByName(AddressableAssetSettings.kRemoteBuildPath)?.Id;
            string remoteLoadId = settings.profileSettings.GetProfileDataByName(AddressableAssetSettings.kRemoteLoadPath)?.Id;

            if (settings.RemoteCatalogBuildPath.Id != remoteBuildId)
            {
                settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            }

            if (settings.RemoteCatalogLoadPath.Id != remoteLoadId)
            {
                settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            }
        }

        public static string NormalizeRemoteLoadPath(string remoteLoadPath)
        {
            if (string.IsNullOrWhiteSpace(remoteLoadPath))
            {
                throw new InvalidOperationException(
                    $"원격 로드 주소가 없습니다. {RemoteLoadPathEnvironmentVariable} 환경 변수를 설정하세요.");
            }

            string normalized = remoteLoadPath.Trim().TrimEnd('/');
            bool isHttps = normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            bool isLocalHttp = normalized.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) ||
                               normalized.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase);
            if (!isHttps && !isLocalHttp)
            {
                throw new InvalidOperationException(
                    "WebGL 원격 콘텐츠 주소는 HTTPS여야 합니다. 로컬 스파이크만 localhost HTTP를 허용합니다.");
            }

            return normalized;
        }
    }
}
