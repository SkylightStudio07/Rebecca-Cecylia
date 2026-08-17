using UnityEngine;

namespace RCCom.Definitions.Unit
{
    /// <summary>
    /// 아군·적 교전에서 공유하는 거리 설정. SO에는 씬별 설정만 두고 타깃·쿨다운 같은
    /// 인스턴스 상태는 저장하지 않는다 — 여러 전투 인스턴스가 하나의 SO를 공유하기 때문이다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Ally Unit/Unit Combat Settings")]
    public class UnitCombatSettings : ScriptableObject
    {
        private const float DefaultContactRange = 0.75f;
        private const float DefaultSeparationMargin = 0.05f;

        [Header("교전 거리")]
        [SerializeField] private float contactRange = DefaultContactRange;
        [SerializeField] private float separationMargin = DefaultSeparationMargin;

        /// <summary>양 진영이 이 거리 안에 들어오면 이동을 멈춘다.</summary>
        public float ContactRange => contactRange > 0f ? contactRange : DefaultContactRange;

        /// <summary>아군 최종 대기점이 적 생성점에서 추가로 유지할 여유 거리.</summary>
        public float SeparationMargin => separationMargin > 0f ? separationMargin : DefaultSeparationMargin;
    }
}
