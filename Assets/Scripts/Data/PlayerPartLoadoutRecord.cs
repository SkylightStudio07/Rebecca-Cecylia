using System;

namespace RCCom.Data
{
    /// <summary>
    /// JsonUtility가 Dictionary를 저장하지 못하므로 슬롯명과 파츠 ID를 한 쌍으로 보존한다.
    /// 슬롯 enum은 콘텐츠 계층 타입이라 프로필 데이터가 Definition에 의존하지 않게 문자열로 둔다.
    /// </summary>
    [Serializable]
    public sealed class PlayerPartLoadoutRecord
    {
        public string slot = string.Empty;
        public string partId = string.Empty;
    }
}
