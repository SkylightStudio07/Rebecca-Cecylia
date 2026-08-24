using System;
using System.Collections.Generic;
using RCCom.UI;
using UnityEditor;
using UnityEngine;

namespace RCCom.EditorTools
{
    /// <summary>
    /// 오로라(Aurora)의 대사 데이터 에셋(OperatorDialogueSet.asset)을 일괄 생성 및 갱신한다.
    /// 상황별 적절한 표정 전신 스프라이트와 전투 포트레잇을 데이터 기반으로 연결한다.
    /// </summary>
    public static class AuroraDialogueBuilder
    {
        private const string DialogueSetPath = "Assets/Data/Operators/aurora/OperatorDialogueSet.asset";
        private const string ArtRoot = "Assets/Art/Character Standing Arts/오로라";
        private const string StandingRoot = "Assets/Art/Character Standing Arts/오로라/레베카";

        [MenuItem("RCCom/Operators/Build Aurora Dialogue")]
        public static void Build()
        {
            OperatorDialogueSet dialogueSet = AssetDatabase.LoadAssetAtPath<OperatorDialogueSet>(DialogueSetPath);
            if (dialogueSet == null)
            {
                dialogueSet = ScriptableObject.CreateInstance<OperatorDialogueSet>();
                AssetDatabase.CreateAsset(dialogueSet, DialogueSetPath);
            }

            dialogueSet.EnsureLineSets();

            // 기본 스프라이트
            dialogueSet.lobbyIdleSprite = LoadStanding("Management.png") ?? LoadStanding("오로라.default-1.png");
            dialogueSet.idleSprite = LoadPortrait("chibby_portrait_1.png");

            // 1. 획득 연출
            SetupLineSet(
                dialogueSet.operatorAcquired,
                portrait: LoadPortrait("chibby_portrait_1.png"),
                defaultLobby: LoadStanding("오로라.smile-1.png"),
                entries: new (string, Sprite)[]
                {
                    ("캐나다 최고의 해커, 여기 등장!\n외주 인력이라지만 뭐어, 잘 부탁해!", LoadStanding("오로라.smile-1.png"))
                }
            );

            // 2. 로비 기본 클릭
            SetupLineSet(
                dialogueSet.lobbyInteraction,
                portrait: LoadPortrait("chibby_portrait_1.png"),
                defaultLobby: LoadStanding("오로라.default-1.png"),
                entries: new (string, Sprite)[]
                {
                    ("뭐야 뭐야, 감독관? 새 외주 일거리라도 가져왔어?", LoadStanding("오로라.curious-1.png")),
                    ("잠깐만~ 지금 방화벽 보안 패치 중이라 손이 바쁘거든? 3분만 대기!", LoadStanding("오로라.annoyed-1.png")),
                    ("흐응~ 지루한데... 사내 인트라넷에 지뢰찾기나 숨겨둘까?", LoadStanding("오로라.smug.png"))
                }
            );

            // 3. 참전 후 귀환
            SetupLineSet(
                dialogueSet.lobbyReturnTogether,
                portrait: LoadPortrait("chibby_portrait_2.png"),
                defaultLobby: LoadStanding("오로라.happy smile.png"),
                entries: new (string, Sprite)[]
                {
                    ("뭐, 이 정도야 아무것도 아니니까!\n감독관도 수고했어!", LoadStanding("오로라.happy smile.png")),
                    ("내 드론 디버프 폭격 봤지? 완전 사기 치트키 수준이었다니까!", LoadStanding("오로라.smug.png"))
                }
            );

            // 4. 미참전 대기 후 귀환
            SetupLineSet(
                dialogueSet.lobbyReturn,
                portrait: LoadPortrait("chibby_portrait_1.png"),
                defaultLobby: LoadStanding("오로라.bored-1.png"),
                entries: new (string, Sprite)[]
                {
                    ("어어~ 다녀왔어?\n잘 잤다~", LoadStanding("오로라.bored-1.png")),
                    ("나 없이 고생 좀 했나 보네? 다음엔 꼭 나도 데려가, 감독관.", LoadStanding("오로라.smile-2.png"))
                }
            );

            // 5. 호감도 터치 1단계: 낯섦 (0~24)
            SetupLineSet(
                dialogueSet.lobbyTouchUnfamiliar,
                portrait: LoadPortrait("chibby_portrait_7.png"),
                defaultLobby: LoadStanding("오로라.annoyed-1.png"),
                entries: new (string, Sprite)[]
                {
                    ("어어, 터치 금지! 방화벽 뚫리면 감전된다?", LoadStanding("오로라.annoyed-1.png")),
                    ("계약서 똑바로 읽어봤어? 불필요한 신체 접촉은 외주 옵션에 없거든요~", LoadStanding("오로라.annoyed-2.png")),
                    ("잡담할 시간 있으면 보안 코드나 한 번 더 확인해, 감독관.", LoadStanding("오로라.default-1.png"))
                }
            );

            // 6. 호감도 터치 2단계: 호감 (25~49)
            SetupLineSet(
                dialogueSet.lobbyTouchFavorable,
                portrait: LoadPortrait("chibby_portrait_1.png"),
                defaultLobby: LoadStanding("오로라.smug.png"),
                entries: new (string, Sprite)[]
                {
                    ("감독관은 생각보다 반응속도가 빠르네? 꽤 마음에 드는 스펙이야.", LoadStanding("오로라.smug.png")),
                    ("외주 계약 끝나도 여기 계속 있을까나~ 감독관이랑 일하는 거, 제법 재밌단 말이지.", LoadStanding("오로라.smile-1.png")),
                    ("이 헤드셋? 네 목소리 노이즈 캔슬링 안 되게 특수 주파수로 맞춰뒀지~ 후훗.", LoadStanding("오로라.happy smile.png"))
                }
            );

            // 7. 호감도 터치 3단계: 친밀 (50~74)
            SetupLineSet(
                dialogueSet.lobbyTouchJoy,
                portrait: LoadPortrait("chibby_portrait_2.png"),
                defaultLobby: LoadStanding("오로라.flustered-1.png"),
                entries: new (string, Sprite)[]
                {
                    ("자꾸 쿡쿡 찌르면 시스템 과열된다고! ...뭐, 싫다는 건 아니지만.", LoadStanding("오로라.flustered-1.png")),
                    ("내 드론들 우선 제어권, 1순위가 나고 2순위가 감독관 너인 거 알아? 특별 대우라고!", LoadStanding("오로라.smug.png")),
                    ("마침 심심했는데 잘됐다! 감독관, 나랑 밤새 협동 게임 한 판 때릴래?", LoadStanding("오로라.happy smile.png"))
                }
            );

            // 8. 호감도 터치 4단계: 연심 (75~99)
            SetupLineSet(
                dialogueSet.lobbyTouchLove,
                portrait: LoadPortrait("chibby_portrait_11.png"),
                defaultLobby: LoadStanding("오로라.blushing shyly-1.png"),
                entries: new (string, Sprite)[]
                {
                    ("내 철벽 보안 시스템에 백도어 심은 범인이 누군지 알아? ...감독관 너잖아, 바보야.", LoadStanding("오로라.blushing shyly-1.png")),
                    ("다른 오퍼레이터들이랑 그렇게 다정하게 이야기하지 마. ...치트키 써서 감독관 시야 차단해버린다?", LoadStanding("오로라.annoyed-3.png")),
                    ("손, 그렇게 덥석 잡으면... 내 심장 프로세스에 과부하 걸린단 말이야...", LoadStanding("오로라.blushing shyly-2.png"))
                }
            );

            // 9. 호감도 터치 5단계: 최고 호감도 (100)
            SetupLineSet(
                dialogueSet.lobbyTouchEx,
                portrait: LoadPortrait("chibby_portrait_12.png"),
                defaultLobby: LoadStanding("오로라.blushing shyly-3.png"),
                entries: new (string, Sprite)[]
                {
                    ("이제 감독관 없이는 로그인도 안 돼... 내 하트의 루트 권한, 영원히 네 거야.", LoadStanding("오로라.blushing shyly-3.png")),
                    ("내 모든 개인 암호키, 감독관한테만 다 넘겨줄게. 그러니까... 평생 책임져, 알았지?", LoadStanding("오로라.fidgeting shyly.png")),
                    ("삭제 불가능한 영구 데이터로 저장했어. ...사랑해, 나의 영원한 관리자님♥", LoadStanding("오로라.aroused-1.png"))
                }
            );

            // 10. 전투 개시
            SetupLineSet(
                dialogueSet.gameStart,
                portrait: LoadPortrait("chibby_portrait_2.png"),
                defaultLobby: null,
                entries: new (string, Sprite)[]
                {
                    ("접속 완료! 적들 시스템 싹 다 털어버릴 준비 됐어?", null),
                    ("자, 이번 판도 내가 하드캐리 해줄 테니까 딱 붙어있어!", null)
                }
            );

            // 11. 스킬 발동
            SetupLineSet(
                dialogueSet.skillUsed,
                portrait: LoadPortrait("chibby_portrait_3.png"),
                defaultLobby: null,
                entries: new (string, Sprite)[]
                {
                    ("오버클럭 가동! 적 방화벽 강제 셧다운!", null),
                    ("루트 권한 탈취 완료! 디버프 잔뜩 걸어줄 테니까 다 쓸어버려!", null),
                    ("치트 엔진 풀가동! 어디 한번 버텨봐라!", null)
                }
            );

            // 12. 거점 피격
            SetupLineSet(
                dialogueSet.baseAttacked,
                portrait: LoadPortrait("chibby_portrait_4.png"),
                defaultLobby: null,
                entries: new (string, Sprite)[]
                {
                    ("야! 메인 서버 털리잖아! 백업 안 해뒀단 말이야!", null),
                    ("거점 핑 튀어! 당장 디도스 공격 차단해!", null),
                    ("방어선 뚫린다! 기지 방화벽 터지기 전에 막아!", null)
                }
            );

            // 13. 플레이어 피격 (일반)
            SetupLineSet(
                dialogueSet.playerHit,
                portrait: LoadPortrait("chibby_portrait_5.png"),
                defaultLobby: null,
                entries: new (string, Sprite)[]
                {
                    ("기체 쉴드 경고! 무빙 좀 신경 써 봐!", null),
                    ("피격 감지! 어어, 에러 로그 쌓인다!", null),
                    ("비틀거리지 마! 핑 렉 걸린 거 아니지?", null)
                }
            );

            // 14. 플레이어 피격 (위기 - 체력 30% 이하)
            SetupLineSet(
                dialogueSet.playerHitCritical,
                portrait: LoadPortrait("chibby_portrait_6.png"),
                defaultLobby: null,
                entries: new (string, Sprite)[]
                {
                    ("치명적 시스템 오류! 당장 빠져나와, 바보 감독관!!", null),
                    ("강제 로그아웃 직전이야! 죽으면 진짜 가만 안 둬!", null),
                    ("하드웨어 한계치 돌파! 후퇴해! 명령이야!!", null)
                }
            );

            // 15. 건설 실패 - 골드 부족
            SetupLineSet(
                dialogueSet.insufficientGold,
                portrait: LoadPortrait("chibby_portrait_7.png"),
                defaultLobby: null,
                entries: new (string, Sprite)[]
                {
                    ("자금 락 걸렸어! 무과금 유저도 아니고 돈 좀 더 모아와~", null),
                    ("잔액 0원! 나 천재 해커지만 골드 복사 치트는 안 쓴다고?", null)
                }
            );

            // 16. 건설 실패 - 슬롯 부족
            SetupLineSet(
                dialogueSet.slotUnavailable,
                portrait: LoadPortrait("chibby_portrait_8.png"),
                defaultLobby: null,
                entries: new (string, Sprite)[]
                {
                    ("포트 만석이야! 트래픽 초과로 더 못 꽂아!", null),
                    ("슬롯 꽉 찼어! 업그레이드 카드로 슬롯부터 확장하고 와!", null)
                }
            );

            // 17. 플레이어 사망
            SetupLineSet(
                dialogueSet.playerDied,
                portrait: LoadPortrait("chibby_portrait_9.png"),
                defaultLobby: null,
                entries: new (string, Sprite)[]
                {
                    ("연결 끊김... 거짓말이지? 야! 눈 떠봐... 제발...!!", null),
                    ("비정상 종료... 안 돼, 감독관! 복구 코드 실행해... 제발...", null)
                }
            );

            // 18. 거점 파괴
            SetupLineSet(
                dialogueSet.baseDestroyed,
                portrait: LoadPortrait("chibby_portrait_10.png"),
                defaultLobby: null,
                entries: new (string, Sprite)[]
                {
                    ("메인 코어 파괴... 시스템 올 셧다운이야. ...감독관, 얼른 도망쳐!", null),
                    ("방화벽 완전 붕괴... 크윽, 이번 판은 리셋하고 재접속하자...", null)
                }
            );

            EditorUtility.SetDirty(dialogueSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AuroraDialogueBuilder] 오로라(Aurora) OperatorDialogueSet 에셋 생성 및 대사/스프라이트 설정 완료!");
        }

        private static void SetupLineSet(
            OperatorLineSet lineSet,
            Sprite portrait,
            Sprite defaultLobby,
            (string text, Sprite lobbySprite)[] entries)
        {
            if (lineSet == null) return;
            lineSet.portraitSprite = portrait;
            lineSet.defaultLobbySprite = defaultLobby;
            lineSet.entries = new List<OperatorDialogueEntry>();

            if (entries != null)
            {
                foreach (var item in entries)
                {
                    lineSet.entries.Add(new OperatorDialogueEntry
                    {
                        text = item.text,
                        lobbySprite = item.lobbySprite
                    });
                }
            }
        }

        private static Sprite LoadStanding(string filename)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{StandingRoot}/{filename}");
        }

        private static Sprite LoadPortrait(string filename)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/{filename}");
        }
    }
}
