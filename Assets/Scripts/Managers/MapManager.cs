using System;
using System.Collections.Generic;
using RCCom.Core;
using RCCom.Data;
using RCCom.Definitions.Tower;
using RCCom.Runtime;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RCCom.Managers
{
    /// <summary>
    /// 게임 흐름 단계 매니저 중 하나 (ARCHITECTURE.md 5단계 원칙: "오브젝트 타입"별 매니저는
    /// 안 만들지만 "게임 흐름 단계"별 매니저는 4개 — GameManager/MapManager/WaveManager/CardManager).
    ///
    /// 그리드 기반 타워 설치 슬롯과 적 이동 웨이포인트 경로를 관리한다. {{user}} 지침: 타워
    /// 설치는 그리드(Tilemap) 기준이지만 적/플레이어 이동은 그리드와 무관해야 하므로, 이 둘을
    /// 서로 다른 데이터로 분리해서 갖는다 — 설치 슬롯은 slotTilemap의 셀 좌표, 웨이포인트는
    /// 그냥 Transform 배열(자유 좌표, 그리드 셀과 무관).
    /// </summary>
    public class MapManager : MonoBehaviour
    {
        [Header("타워 설치 그리드 (슬롯이 칠해진 타일맵 레이어)")]
        [SerializeField] private Tilemap slotTilemap;

        [Header("공용 타워 프리팹 (종류별로 따로 안 두고 1개 재사용, Build(definition)으로 종류 주입)")]
        [SerializeField] private TowerInstance towerPrefab;

        [Header("적 이동 경로 (그리드 무관, 자유 좌표 — 씬에 배치한 오브젝트 순서대로)")]
        [SerializeField] private Transform[] waypoints;

        [Header("경로 곡선 보간 (0 = 기존 직선 유지, 1 = 최대 부드러움)")]
        [Tooltip("0이면 웨이포인트 배열을 그대로 사용한다(완전 하위호환). 0보다 크면 구심 Catmull-Rom 곡선을 " +
                 "따라 촘촘한 정점으로 베이킹한다. 유선형 맵은 0.6~0.8, 직선/격자형 맵은 0을 권장.")]
        [Range(0f, 1f)]
        [SerializeField] private float pathSmoothness = 0f;

        [Tooltip("베이킹된 정점 사이의 최대 간격(월드 유닛). 작을수록 부드럽지만 적 이동 속도가 " +
                 "프레임레이트에 비례해 미세하게 느려진다(정점 1개당 평균 step/2 손실). 0.5 권장, 0.25 미만 금지.")]
        [Range(0.25f, 5f)]
        [SerializeField] private float maxPointSpacing = 0.5f;

        [Header("타워 종류별 설치 슬롯 제한 (GDD 기본값, 카드로 확장 가능)")]
        [SerializeField] private int maxAttackTowers = 3;
        [SerializeField] private int maxSkillTowers = 1;
        [SerializeField] private int maxPowerTowers = 2;

        private readonly Dictionary<Vector3Int, TowerInstance> _occupiedSlots = new();
        private int _attackTowerCount;
        private int _skillTowerCount;
        private int _powerTowerCount;

        private Vector2[] _waypointPositions;

        /// <summary>HUD의 슬롯 텍스트 3종이 참조 — 종류별 "남은" 설치 가능 수.</summary>
        public int AttackSlotsRemaining => maxAttackTowers - _attackTowerCount;
        public int SkillSlotsRemaining => maxSkillTowers - _skillTowerCount;
        public int PowerSlotsRemaining => maxPowerTowers - _powerTowerCount;

        /// <summary>
        /// 적 이동용 웨이포인트 목록 (스테이지당 고정이라 Awake에서 한 번만 캐싱).
        /// WaveManager가 EnemyInstance 스폰 시 이 목록을 넘겨줄 것.
        /// </summary>
        public IReadOnlyList<Vector2> Waypoints => _waypointPositions;

        private void Awake()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                _waypointPositions = Array.Empty<Vector2>();
                Debug.LogError("[Map] waypoints가 비어 있음 — 적/아군 이동 경로를 만들 수 없다.");
                return;
            }

            // 인스펙터에서 슬롯을 비워둔 실수를 여기서 걸러낸다. 그대로 두면 waypoints[i].position이
            // NRE를 던지므로, null 슬롯은 건너뛰고 로그로 알린 뒤 나머지로 경로를 만든다.
            List<Vector2> rawPoints = new List<Vector2>(waypoints.Length);
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null)
                {
                    Debug.LogError($"[Map] waypoints[{i}] 슬롯이 비어 있음 — 건너뜀.");
                    continue;
                }

                rawPoints.Add(waypoints[i].position);
            }

            // Awake에서 1회만 베이킹하고 이후 불변으로 캐싱한다 — 매 프레임 재계산하지 않는다.
            // (AllyUnitTargeting의 정적 캐시가 "같은 리스트 인스턴스가 제자리에서 변형되지
            // 않는다"를 전제하므로, 이 불변성은 성능 최적화 이상의 의미를 가진다.)
            _waypointPositions = PathSmoothing.GenerateSmoothPath(rawPoints, pathSmoothness, maxPointSpacing);

            Debug.Log($"[Map] 경로 베이킹: 제어점 {rawPoints.Count}개 → 정점 {_waypointPositions.Length}개 " +
                      $"(smoothness {pathSmoothness}, spacing {maxPointSpacing})");
        }

        /// <summary>
        /// 씬 뷰에서 곡선이 배경 도로 아트를 벗어나는지 Play 모드 없이 검수하기 위한 프리뷰.
        /// 런타임 파일이라 UnityEditor는 쓸 수 없으므로 Gizmos API만으로 그린다 — 이 메서드
        /// 자체가 에디터에서만 호출되니 UnityEditor 없이도 목적을 달성할 수 있다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return;
            }

            List<Vector2> rawPoints = new List<Vector2>(waypoints.Length);
            foreach (Transform wp in waypoints)
            {
                if (wp != null)
                {
                    rawPoints.Add(wp.position);
                }
            }

            if (rawPoints.Count == 0)
            {
                return;
            }

            // 원본 제어점: 디자이너가 실제로 배치한 좌표를 곡선과 겹쳐 보여줘 비교 기준으로 삼는다.
            Gizmos.color = Color.red;
            foreach (Vector2 p in rawPoints)
            {
                Gizmos.DrawSphere(p, 0.12f);
            }

            // 플레이 모드에서는 Awake가 이미 베이킹한 결과를 그대로 쓰고, 에디트 모드에서는
            // 슬라이더를 움직일 때마다 그 자리에서 다시 베이킹한다 — 필드에 캐싱하지 않는다.
            // 이 메서드는 에디터에서만 호출되므로(MaxTotalPoints 상한도 있어) 매번 계산해도
            // 무방하고, 그래야 에디트 모드에서 값 변경이 즉시 반영된다.
            IReadOnlyList<Vector2> path = Application.isPlaying
                ? _waypointPositions
                : PathSmoothing.GenerateSmoothPath(rawPoints, pathSmoothness, maxPointSpacing);

            if (path == null || path.Count == 0)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Gizmos.DrawLine(path[i], path[i + 1]);
            }

            // 시작점(적 스폰)과 끝점(거점/아군 스폰)은 색을 구분해 방향성을 한눈에 알 수 있게 한다.
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(path[0], 0.2f);

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(path[path.Count - 1], 0.2f);
        }

        /// <summary>월드 좌표가 속한 셀을 반환한다. 슬롯 타일맵에 실제로 칠해진 셀이어야 유효하다.</summary>
        public bool TryGetSlotCell(Vector3 worldPosition, out Vector3Int cell)
        {
            cell = slotTilemap.WorldToCell(worldPosition);
            return slotTilemap.HasTile(cell);
        }

        /// <summary>설치 프리뷰(커서 따라다니는 스프라이트)가 셀 중심 좌표를 필요로 해서 공개.</summary>
        public Vector3 GetCellCenterWorld(Vector3Int cell) => slotTilemap.GetCellCenterWorld(cell);

        public bool CanBuild(TowerKind kind, Vector3Int cell)
        {
            if (!slotTilemap.HasTile(cell) || _occupiedSlots.ContainsKey(cell))
            {
                return false;
            }

            return kind switch
            {
                TowerKind.Attack => _attackTowerCount < maxAttackTowers,
                TowerKind.Skill => _skillTowerCount < maxSkillTowers,
                TowerKind.Power => _powerTowerCount < maxPowerTowers,
                _ => false,
            };
        }

        /// <summary>
        /// 공용 타워 프리팹을 슬롯 셀 중심 위치에 건설하고 definition을 주입한다. 호출 전
        /// CanBuild로 가능 여부를 확인해야 한다 (여기서는 재확인하지 않음 — 예: 타워 설치 UI가
        /// 버튼 활성/비활성을 CanBuild로 미리 걸러야 함).
        /// </summary>
        public TowerInstance Build(TowerDefinition definition, Vector3Int cell)
        {
            Vector3 worldPosition = slotTilemap.GetCellCenterWorld(cell);
            TowerInstance instance = Instantiate(towerPrefab, worldPosition, Quaternion.identity);
            instance.Build(definition);

            _occupiedSlots[cell] = instance;
            IncrementCount(definition.Data.kind);

            return instance;
        }

        /// <summary>업그레이드 카드(예: "공격 타워 증설")가 호출해 슬롯 한도를 늘린다.</summary>
        public void IncreaseMaxSlots(TowerKind kind, int amount)
        {
            switch (kind)
            {
                case TowerKind.Attack:
                    maxAttackTowers += amount;
                    break;
                case TowerKind.Skill:
                    maxSkillTowers += amount;
                    break;
                case TowerKind.Power:
                    maxPowerTowers += amount;
                    break;
            }
        }

        /// <summary>해당 셀에 지어진 타워가 있으면 반환한다 (철거 기능용).</summary>
        public bool TryGetTowerAt(Vector3Int cell, out TowerInstance instance) =>
            _occupiedSlots.TryGetValue(cell, out instance);

        /// <summary>
        /// 해당 셀의 타워를 철거한다. 비용 확인/차감은 호출부(TowerBuildController)의 책임 —
        /// 여기서는 슬롯/카운터 정리와 실제 파괴만 담당한다.
        /// </summary>
        public void RemoveTower(Vector3Int cell)
        {
            if (!_occupiedSlots.TryGetValue(cell, out TowerInstance instance))
            {
                return;
            }

            _occupiedSlots.Remove(cell);
            DecrementCount(instance.Data.kind);
            Destroy(instance.gameObject);
        }

        private void IncrementCount(TowerKind kind)
        {
            switch (kind)
            {
                case TowerKind.Attack:
                    _attackTowerCount++;
                    break;
                case TowerKind.Skill:
                    _skillTowerCount++;
                    break;
                case TowerKind.Power:
                    _powerTowerCount++;
                    break;
            }
        }

        private void DecrementCount(TowerKind kind)
        {
            switch (kind)
            {
                case TowerKind.Attack:
                    _attackTowerCount--;
                    break;
                case TowerKind.Skill:
                    _skillTowerCount--;
                    break;
                case TowerKind.Power:
                    _powerTowerCount--;
                    break;
            }
        }
    }
}
