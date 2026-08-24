using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.Operator
{
    /// <summary>
     /// 오퍼레이터 1명이 가진 강화 트랙 묶음. towerRoster/cardRoster와 동일하게
    /// OperatorDefinition이 직접 필드로 참조해, 오퍼레이터 Addressable 그룹에 함께 딸려간다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/Operator/Operator Upgrade Track Set")]
    public class OperatorUpgradeTrackSet : ScriptableObject
    {
        public List<OperatorUpgradeTrack> tracks = new();

        public OperatorUpgradeTrack FindByTrackId(string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId) || tracks == null)
            {
                return null;
            }

            for (int i = 0; i < tracks.Count; i++)
            {
                OperatorUpgradeTrack track = tracks[i];
                if (track != null && string.Equals(track.trackId, trackId, StringComparison.Ordinal))
                {
                    return track;
                }
            }

            return null;
        }
    }
}
