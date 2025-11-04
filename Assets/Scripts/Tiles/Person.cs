using UnityEngine;

namespace Tiles
{
    public sealed class Person : MonoBehaviour
    {
        public void WarpTo(Transform anchor)
        {
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
        }
    }
}