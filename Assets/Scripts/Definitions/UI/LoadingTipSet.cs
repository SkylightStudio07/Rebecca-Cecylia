using System.Collections.Generic;
using UnityEngine;

namespace RCCom.Definitions.UI
{
    /// <summary>
    /// 로딩 화면의 짧은 팁 문구 묶음. 전투 코드와 무관한 라이브 콘텐츠이므로
    /// Addressable 에셋 하나로 분리해 문구만 원격 갱신할 수 있게 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "RCCom/UI/Loading Tip Set")]
    public sealed class LoadingTipSet : ScriptableObject
    {
        [TextArea(1, 3)]
        public List<string> tips = new();

        public bool TryGetRandom(out string tip)
        {
            if (tips != null)
            {
                var validTips = new List<string>();
                for (int i = 0; i < tips.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(tips[i]))
                    {
                        validTips.Add(tips[i]);
                    }
                }

                if (validTips.Count > 0)
                {
                    tip = validTips[Random.Range(0, validTips.Count)];
                    return true;
                }
            }

            tip = null;
            return false;
        }
    }
}
