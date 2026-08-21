using System;
using UnityEngine;

namespace RCCom.UI
{
    /// <summary>
    /// 한 줄의 대사와 로비에서 그 줄을 말할 때 사용할 전신 스프라이트를 묶는다.
    /// 전투 포트레잇은 OperatorLineSet의 상황별 portraitSprite가 계속 담당한다.
    /// </summary>
    [Serializable]
    public sealed class OperatorDialogueEntry
    {
        [TextArea(2, 4)]
        public string text;

        [Tooltip("로비의 Canvas/OperatorImage에 표시할 문장별 전신 스프라이트")]
        public Sprite lobbySprite;

        [HideInInspector]
        public Sprite portraitSprite;
    }
}
