UnityEngine.GameObject target = UnityEngine.GameObject.Find("Canvas/RawImage");
if (target == null) { throw new System.InvalidOperationException("Canvas/RawImage를 찾지 못했습니다."); }
UnityEngine.UI.RawImage image = target.GetComponent<UnityEngine.UI.RawImage>();
string components = string.Empty;
UnityEngine.Component[] all = target.GetComponents<UnityEngine.Component>();
for (int i = 0; i < all.Length; i++)
{
    if (i > 0) { components += ","; }
    components += all[i] != null ? all[i].GetType().FullName : "Missing";
}
throw new System.InvalidOperationException("sibling=" + target.transform.GetSiblingIndex() +
    ", texture=" + (image != null && image.texture != null ? image.texture.name : "null") +
    ", components=" + components);
