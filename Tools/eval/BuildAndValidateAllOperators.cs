RCCom.EditorTools.OperatorAssetBuilder.BuildAll();
RCCom.EditorTools.OperatorCatalogBuilder.BuildAll();
if (!RCCom.EditorTools.OperatorAssetValidator.ValidateAll())
{
    throw new System.InvalidOperationException("오퍼레이터 에셋 검증에 실패했습니다.");
}
