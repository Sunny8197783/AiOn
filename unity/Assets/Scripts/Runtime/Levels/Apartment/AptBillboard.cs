// THREE.Sprite stand-in for the apartment's candle flame and halo: a unit quad scaled (sx, sy) that turns to face
// the camera every frame. Kept out of Kit.Merge so it can move.
// ponytail: faces Camera.main only (null -> stays as is); a shared Engine sprite helper (or a VFX graph) can replace it.
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class AptBillboard : MonoBehaviour
    {
        public static GameObject Make(Material mat, float sx, float sy)
        {
            var s = Geo.Obj(Geo.Plane(1, 1), mat, "sprite", false, false);
            s.transform.localScale = new Vector3(sx, sy, 1);
            Kit.Keep(s);
            s.AddComponent<AptBillboard>();
            return s;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            // Geo planes face +z; turn the face toward the camera.
            if (cam != null) transform.rotation = cam.transform.rotation * Quaternion.Euler(0, 180, 0);
        }
    }
}
