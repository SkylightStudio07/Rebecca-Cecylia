using System;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 보관된 콘텐츠 상태(addressables_content_state.bin)가 어떤 플레이어 빌드의 것인지
    /// 사람이 읽을 수 있게 남기는 메타데이터. bin 파일 자체는 바이너리라 열어봐도 어느
    /// 빌드와 짝인지 알 수 없어서, 라이브 드랍 전에 "지금 서비스 중인 빌드가 이것이 맞는가"를
    /// 확인할 방법이 필요하다.
    ///
    /// JsonUtility로 직렬화하므로 필드는 public이어야 하고 프로퍼티는 쓰지 않는다.
    /// </summary>
    [Serializable]
    public sealed class AddressablesReleaseManifest
    {
        public string version = string.Empty;
        public string buildTarget = string.Empty;
        public string unityVersion = string.Empty;
        public string archivedAtUtc = string.Empty;

        /// <summary>
        /// 콘텐츠 업데이트는 이 두 경로가 원본 플레이어와 같을 때만 성립한다.
        /// 다르면 구 플레이어는 여전히 옛 주소를 보고 있어 새 카탈로그를 영영 못 받는다.
        /// </summary>
        public string remoteLoadPath = string.Empty;

        public string remoteCatalogLoadPath = string.Empty;
    }
}
