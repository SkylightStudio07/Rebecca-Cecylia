namespace RCCom.Runtime.Visuals
{
    /// <summary>
    /// 표현 SO가 씬을 전역 탐색하지 않고 자신을 소유한 View와 런타임 유닛에만 접근하게 하는 경계.
    /// </summary>
    public class AllyUnitVisualContext
    {
        public AllyUnitView view;
        public AllyUnitInstance instance;
        public int sortingLayerId;
        public int sortingOrder;
    }
}
