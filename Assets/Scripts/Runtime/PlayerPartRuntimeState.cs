using UnityEngine;

namespace RCCom.Runtime
{
    /// <summary>
    /// Effect SO가 여러 플레이어/씬에서 공유돼도 상태가 섞이지 않도록 플레이어 인스턴스가
    /// 소유하는 범용 상태 슬롯. 구체 Effect는 필요한 필드만 의미를 부여한다.
    /// </summary>
    public sealed class PlayerPartRuntimeState
    {
        public float timer;
        public float value;
        public int counter;
        public bool flag;
        public Vector2 direction;
    }
}
