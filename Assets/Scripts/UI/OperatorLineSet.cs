using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCCom.UI
{
    /// <summary>
    /// 상황 1개에 대응하는 대사 후보 묶음.
    /// 새 데이터는 entries를 사용하고, 기존 에셋의 손실을 막기 위해 legacy 필드도
    /// 잠시 보존한다. 에디터의 마이그레이션 버튼이 기존 배열을 entries로 변환한다.
    /// </summary>
    [Serializable]
    public sealed class OperatorLineSet
    {
        [Tooltip("이전 문장별 포트레잇 구현 호환용 필드")]
        [HideInInspector]
        public Sprite defaultPortraitSprite;

        [Tooltip("전투에서 이 상황이 발생했을 때 사용하는 포트레잇")]
        public Sprite portraitSprite;

        [Tooltip("로비 대사의 문장별 스프라이트가 비어 있을 때 사용할 전신 스프라이트")]
        public Sprite defaultLobbySprite;

        public List<OperatorDialogueEntry> entries = new();

        [HideInInspector]
        public string[] lines;

        public bool HasContent
        {
            get
            {
                if (entries != null)
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        if (entries[i] != null && !string.IsNullOrWhiteSpace(entries[i].text))
                        {
                            return true;
                        }
                    }
                }

                return lines != null && lines.Length > 0;
            }
        }

        public Sprite ResolveCombatPortrait()
        {
            return portraitSprite != null ? portraitSprite : defaultPortraitSprite;
        }

        /// <summary>
        /// 런타임은 구 데이터와 신 데이터를 같은 경로로 읽어야 기존 Cassia 에셋을
        /// 단계적으로 마이그레이션할 수 있다.
        /// </summary>
        public bool TryGetRandomCombat(out string text, out Sprite portrait)
        {
            if (TrySelectEntry(out OperatorDialogueEntry selected))
            {
                text = selected.text;
                portrait = ResolveCombatPortrait();
                return true;
            }

            if (TrySelectLegacyLine(out text))
            {
                portrait = ResolveCombatPortrait();
                return true;
            }

            text = null;
            portrait = null;
            return false;
        }

        public bool TryGetRandomLobby(out string text, out Sprite lobbySprite)
        {
            if (TrySelectEntry(out OperatorDialogueEntry selected))
            {
                text = selected.text;
                lobbySprite = selected.lobbySprite != null ? selected.lobbySprite : defaultLobbySprite;
                return true;
            }

            if (TrySelectLegacyLine(out text))
            {
                lobbySprite = defaultLobbySprite;
                return true;
            }

            text = null;
            lobbySprite = null;
            return false;
        }

        private bool TrySelectEntry(out OperatorDialogueEntry selected)
        {
            if (entries != null)
            {
                var validEntries = new List<OperatorDialogueEntry>();
                for (int i = 0; i < entries.Count; i++)
                {
                    OperatorDialogueEntry entry = entries[i];
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.text))
                    {
                        validEntries.Add(entry);
                    }
                }

                if (validEntries.Count > 0)
                {
                    selected = validEntries[UnityEngine.Random.Range(0, validEntries.Count)];
                    return true;
                }
            }

            selected = null;
            return false;
        }

        private bool TrySelectLegacyLine(out string text)
        {
            if (lines != null && lines.Length > 0)
            {
                text = lines[UnityEngine.Random.Range(0, lines.Length)];
                return !string.IsNullOrWhiteSpace(text);
            }

            text = null;
            return false;
        }

        public int MigrateLegacyEntries()
        {
            if ((entries != null && entries.Count > 0) || lines == null || lines.Length == 0)
            {
                return 0;
            }

            if (entries == null)
            {
                entries = new List<OperatorDialogueEntry>();
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }

                entries.Add(new OperatorDialogueEntry
                {
                    text = lines[i],
                });
            }

            return entries.Count;
        }
    }
}
