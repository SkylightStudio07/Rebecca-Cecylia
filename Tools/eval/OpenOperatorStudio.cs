if (!UnityEditor.EditorApplication.ExecuteMenuItem("RCCom/Operators/Open Operator Studio"))
{
    throw new System.InvalidOperationException("Operator Studio 메뉴를 실행하지 못했습니다.");
}
UnityEngine.Debug.Log("[OperatorStudio] 창 열기 검증 완료");
