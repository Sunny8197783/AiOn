// three.js Sprite stand-in for the minigames' 3D labels (grab3d price tags, notecheck pins): a quad carrying a
// Paint texture, drawn over everything (depthTest false), turned to face the camera each frame.
using Gyeongbo.Engine;
using UnityEngine;

namespace Gyeongbo.UI
{
    internal sealed class MgSprite
    {
        public readonly GameObject go;
        public readonly Material mat;
        public readonly Texture2D tex;
        readonly Mesh mesh;

        // center: sprite.center (0.5, 0.5 = middle). Size comes from Scale (sprite.scale).
        public MgSprite(Paint p, float centerX = 0.5f, float centerY = 0.5f, int renderOrder = 0, float opacity = 1)
        {
            tex = p.ToTexture(true, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            mat = Gyeongbo.Engine.Mat.Unlit(new MatSpec { map = tex, transparent = true, depthTest = false, depthWrite = false, side = "double", opacity = opacity });
            mat.renderQueue = 3000 + renderOrder;
            mesh = Geo.Plane(1, 1).Translate(0.5f - centerX, 0.5f - centerY, 0).ToMesh("sprite");
            go = Geo.Obj(mesh, mat, "sprite", false, false);
        }

        public void Scale(float x, float y) => go.transform.localScale = new Vector3(x, y, 1);

        // Screen-aligned like THREE.Sprite: the quad's front (+z, which reads correctly) faces the camera.
        public void Face(Camera cam)
        {
            if (cam == null) return;
            go.transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
        }

        public void Dispose()
        {
            if (go != null) Object.Destroy(go);
            Object.Destroy(mat); Object.Destroy(tex); Object.Destroy(mesh);
        }
    }
}
