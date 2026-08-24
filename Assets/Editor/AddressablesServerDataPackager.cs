using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// Addressables 원격 콘텐츠 빌드 결과(ServerData/[BuildTarget])를 업로드용 zip으로 묶는다.
    /// 우리 GBaaS(unity-webgl-issuetracker)의 콘텐츠 업로드 API는 zip 최상위의 단일 래퍼 폴더
    /// (serverdata/streamingassets, 대소문자 무관)를 자동으로 벗겨내고 그 아래를 채널에 그대로
    /// 설치한다. 따라서 ServerData 폴더의 "내용물"이 아니라 ServerData 폴더 "자체"를 압축해,
    /// zip 안에 [BuildTarget] 디렉터리가 살아 있게 만든다 — 그 레벨을 지우면 업로드는 성공해도
    /// 런타임 카탈로그/번들 URL이 전부 404가 된다.
    /// </summary>
    public static class AddressablesServerDataPackager
    {
        public const string DefaultZipFileName = "ServerData.zip";
        private const string BuildTargetToken = "[BuildTarget]";

        [MenuItem("RCCom/Addressables/Package ServerData Zip")]
        public static void PackageMenu()
        {
            string zipPath = PackageServerDataZip();
            Debug.Log($"[AddressablesServerDataPackager] 완료: {zipPath}");
            EditorUtility.RevealInFinder(zipPath);
        }

        /// <summary>
        /// 활성 Addressables 프로필의 Remote.BuildPath가 가리키는 폴더(기본값 ServerData)를
        /// 통째로 압축한다. outputZipPath를 비우면 프로젝트 루트의 ServerData.zip에 만든다.
        /// </summary>
        public static string PackageServerDataZip(string outputZipPath = null)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables Settings가 없습니다.");
            }

            string serverDataRoot = ResolveServerDataRoot(settings);
            if (!Directory.Exists(serverDataRoot))
            {
                throw new InvalidOperationException(
                    $"ServerData 폴더가 없습니다. 먼저 Addressables 원격 콘텐츠를 빌드하세요: {serverDataRoot}");
            }

            string[] files = Directory.GetFiles(serverDataRoot, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                throw new InvalidOperationException($"ServerData 폴더가 비어 있습니다: {serverDataRoot}");
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string zipPath = string.IsNullOrWhiteSpace(outputZipPath)
                ? Path.Combine(projectRoot, DefaultZipFileName)
                : outputZipPath;

            if (File.Exists(zipPath))
            {
                // 매번 새로 만든다 — 이전 빌드 잔여 엔트리가 새 zip에 섞여 들어가는
                // 사고(예: 삭제된 유닛의 옛 번들이 다시 올라가는 것)를 막는다.
                File.Delete(zipPath);
            }

            string wrapperFolderName = new DirectoryInfo(serverDataRoot).Name;
            string tempZipPath = zipPath + ".tmp";
            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }

            try
            {
                using (var archive = ZipFile.Open(tempZipPath, ZipArchiveMode.Create))
                {
                    foreach (string file in files)
                    {
                        string relativePath = Path.GetRelativePath(serverDataRoot, file).Replace('\\', '/');
                        string entryName = $"{wrapperFolderName}/{relativePath}";
                        archive.CreateEntryFromFile(file, entryName, System.IO.Compression.CompressionLevel.Optimal);
                    }
                }

                File.Move(tempZipPath, zipPath);
            }
            finally
            {
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }
            }

            long zipSizeBytes = new FileInfo(zipPath).Length;
            Debug.Log(
                $"[AddressablesServerDataPackager] {wrapperFolderName} 폴더 {files.Length}개 파일을 압축했습니다 " +
                $"-> {zipPath} ({zipSizeBytes:N0} bytes)");
            return zipPath;
        }

        /// <summary>
        /// Remote.BuildPath 프로필 값(예: "ServerData/[BuildTarget]")에서 [BuildTarget] 토큰
        /// 앞부분만 잘라 실제로 압축할 폴더의 절대 경로를 만든다. 경로를 하드코딩하지 않아
        /// RCCOM_REMOTE_BUILD_PATH로 값이 바뀌어도 같은 진입점이 항상 맞는 폴더를 찾는다.
        /// </summary>
        private static string ResolveServerDataRoot(AddressableAssetSettings settings)
        {
            string remoteBuildPath = settings.profileSettings.GetValueByName(
                settings.activeProfileId, AddressableAssetSettings.kRemoteBuildPath);
            if (string.IsNullOrWhiteSpace(remoteBuildPath))
            {
                throw new InvalidOperationException("Remote.BuildPath 프로필 값이 비어 있습니다.");
            }

            int tokenIndex = remoteBuildPath.IndexOf(BuildTargetToken, StringComparison.Ordinal);
            string wrapperRelativePath = tokenIndex >= 0
                ? remoteBuildPath.Substring(0, tokenIndex)
                : remoteBuildPath;
            wrapperRelativePath = wrapperRelativePath.Trim().TrimEnd('/', '\\');

            if (string.IsNullOrEmpty(wrapperRelativePath))
            {
                throw new InvalidOperationException(
                    $"Remote.BuildPath에서 상위 폴더 이름을 찾지 못했습니다: {remoteBuildPath}");
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, wrapperRelativePath));
        }
    }
}
