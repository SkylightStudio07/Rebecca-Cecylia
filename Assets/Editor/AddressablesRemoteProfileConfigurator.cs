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
            var loadVariable = profiles.GetProfileDataByName(AddressableAssetSettings.kRemoteLoadPath);
            var buildVariable = profiles.GetProfileDataByName(AddressableAssetSettings.kRemoteBuildPath);
            string loadVariableId = loadVariable?.Id;
            string buildVariableId = buildVariable?.Id;
            if (string.IsNullOrEmpty(loadVariableId) || string.IsNullOrEmpty(buildVariableId))
            {
                throw new InvalidOperationException("Addressables 원격 경로 프로필 변수를 찾지 못했습니다.");
            }

            profiles.SetValue(settings.activeProfileId, loadVariableId, normalizedLoadPath);
            profiles.SetValue(settings.activeProfileId, buildVariableId, normalizedBuildPath);
            EnsureRemoteCatalogEnabled(settings);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

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

            if (settings.RemoteCatalogBuildPath.GetName(settings) != AddressableAssetSettings.kRemoteBuildPath)
            {
                settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            }

            if (settings.RemoteCatalogLoadPath.GetName(settings) != AddressableAssetSettings.kRemoteLoadPath)
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
