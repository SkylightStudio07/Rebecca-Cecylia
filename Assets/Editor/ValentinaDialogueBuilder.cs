using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RCCom.UI;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 발렌티나 대사 데이터베이스의 Primary 표정을 실제 존재하는 전신/치비 파일로만 연결한다.
    /// 표정표와 에셋을 따로 고치면 Missing 참조가 다시 생기므로, 표의 행 수와 실제 파일을 모두
    /// 검증한 뒤에만 저장한다.
    /// </summary>
    public static class ValentinaDialogueBuilder
    {
        private const string DialogueSetPath = "Assets/Data/Operators/valentina/OperatorDialogueSet.asset";
        private const string DatabasePath = "Assets/Art/Character Standing Arts/발렌티나/valentina-dialogue-set.md";
        private const string StandingRoot = "Assets/Art/Character Standing Arts/발렌티나";
        private const string PortraitRoot = StandingRoot + "/portrait";

        [MenuItem("RCCom/Operators/Build Valentina Dialogue")]
        public static void Build()
        {
            OperatorDialogueSet dialogueSet = AssetDatabase.LoadAssetAtPath<OperatorDialogueSet>(DialogueSetPath);
            if (dialogueSet == null)
            {
                throw new InvalidOperationException($"발렌티나 대화 에셋을 찾지 못했습니다: {DialogueSetPath}");
            }

            List<string> expressions = ReadPrimaryExpressions();
            List<OperatorLineSet> lineSets = GetLineSets(dialogueSet);
            int entryCount = 0;
            foreach (OperatorLineSet lineSet in lineSets)
            {
                entryCount += lineSet?.entries?.Count ?? 0;
            }

            if (expressions.Count != entryCount)
            {
                throw new InvalidOperationException(
                    $"표정 데이터베이스 행 수({expressions.Count})와 대사 엔트리 수({entryCount})가 다릅니다.");
            }

            int expressionIndex = 0;
            foreach (OperatorLineSet lineSet in lineSets)
            {
                if (lineSet?.entries == null || lineSet.entries.Count == 0)
                {
                    continue;
                }

                Sprite firstStanding = null;
                Sprite firstPortrait = null;
                foreach (OperatorDialogueEntry entry in lineSet.entries)
                {
                    string expression = expressions[expressionIndex++];
                    Sprite standing = LoadRequiredSprite(StandingRoot, $"발렌티나.{expression}");
                    Sprite portrait = LoadRequiredSprite(PortraitRoot, $"발렌티나.Chibby.{expression}");
                    entry.lobbySprite = standing;
                    entry.portraitSprite = portrait;
                    firstStanding ??= standing;
                    firstPortrait ??= portrait;
                }

                // 전투 UI는 상황 단위의 포트레잇만 읽는다. 같은 슬롯의 첫 문장 표정을 기본으로
                // 삼아 Missing 상태를 없애고, 로비 폴백도 실제 존재하는 전신만 사용한다.
                lineSet.defaultLobbySprite = firstStanding;
                lineSet.portraitSprite = firstPortrait;
            }

            dialogueSet.lobbyIdleSprite = LoadRequiredSprite(StandingRoot, "발렌티나.default.png");
            dialogueSet.idleSprite = LoadRequiredSprite(PortraitRoot, "발렌티나.Chibby.default.png");
            EditorUtility.SetDirty(dialogueSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ValentinaDialogueBuilder] 표정 데이터베이스 {expressionIndex}개 행을 실제 스프라이트로 연결했습니다.");
        }

        private static List<string> ReadPrimaryExpressions()
        {
            if (!File.Exists(DatabasePath))
            {
                throw new FileNotFoundException("발렌티나 표정 데이터베이스를 찾지 못했습니다.", DatabasePath);
            }

            var expressions = new List<string>();
            // 헤더/파일 목록을 제외하고, 번호가 붙은 대사 행의 첫 스프라이트 열(Primary)만 읽는다.
            var expressionPattern = new Regex(
                @"^\|\s*\*\*\d+\*\*\s*\|.*?\|\s*`([^`]+\.png)`\s*\|",
                RegexOptions.CultureInvariant);
            foreach (string line in File.ReadLines(DatabasePath))
            {
                Match match = expressionPattern.Match(line);
                if (match.Success)
                {
                    expressions.Add(match.Groups[1].Value);
                }
            }

            return expressions;
        }

        private static Sprite LoadRequiredSprite(string root, string filename)
        {
            string path = $"{root}/{filename}";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException($"표정 데이터베이스가 가리키는 스프라이트가 없습니다: {path}");
            }

            return sprite;
        }

        private static List<OperatorLineSet> GetLineSets(OperatorDialogueSet dialogueSet)
        {
            dialogueSet.EnsureLineSets();
            return new List<OperatorLineSet>
            {
                dialogueSet.operatorAcquired,
                dialogueSet.lobbyInteraction,
                dialogueSet.lobbyReturnTogether,
                dialogueSet.lobbyReturn,
                dialogueSet.lobbyTouchUnfamiliar,
                dialogueSet.lobbyTouchFavorable,
                dialogueSet.lobbyTouchJoy,
                dialogueSet.lobbyTouchLove,
                dialogueSet.lobbyTouchEx,
                dialogueSet.gameStart,
                dialogueSet.skillUsed,
                dialogueSet.baseAttacked,
                dialogueSet.playerHit,
                dialogueSet.playerHitCritical,
                dialogueSet.insufficientGold,
                dialogueSet.slotUnavailable,
                dialogueSet.playerDied,
                dialogueSet.baseDestroyed,
            };
        }
    }
}
