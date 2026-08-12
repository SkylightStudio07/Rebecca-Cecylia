using RCCom.Data;

namespace RCCom.Core
{
    /// <summary>
    /// 계정 데이터 저장소 교체 지점. 게임 로직은 PlayerPrefs나 향후 외부 SDK를 직접 알지 않고
    /// 이 계약만 사용하므로, 저장 백엔드를 바꿔도 프로필 소비 코드는 그대로 유지할 수 있다.
    /// </summary>
    public interface IProfileStorage
    {
        PlayerProfile Load();
        void Save(PlayerProfile profile);
    }
}
