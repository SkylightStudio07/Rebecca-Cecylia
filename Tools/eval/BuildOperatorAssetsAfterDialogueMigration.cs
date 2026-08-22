if (!UnityEditor.EditorApplication.ExecuteMenuItem("RCCom/Operators/Build All Operator Assets"))
{
    throw new System.InvalidOperationException("Operator Asset Builder 메뉴를 실행하지 못했습니다.");
}
