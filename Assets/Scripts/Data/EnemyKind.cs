namespace RCCom.Data
{
    /// <summary>
    /// 적 베이스 종류. Studio와 카탈로그에서 적의 역할을 분류하는 식별값이다.
    /// </summary>
    public enum EnemyKind
    {
        Normal = 0,
        Rusher = 1,
        Tanker = 2,
        Boss = 3,

        // JSON과 SO에는 정수로 저장되므로 기존 항목 뒤에만 추가해 직렬화 호환성을 지킨다.
        Drone = 4,
        Explode = 5,
        Heal = 6,
        HeavyTanker = 7,
    }
}
