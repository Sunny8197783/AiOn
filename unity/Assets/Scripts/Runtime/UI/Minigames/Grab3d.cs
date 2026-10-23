// Ports ui/grab3d.js: shopping and packing by hand in the 3D scene (shelf / home stock in front, the basket or
// backpack below; click to take, right-click to put back). Same contracts as the 2D shop and bag.
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;

namespace Gyeongbo.UI
{
    public static class Grab3d
    {
        // ctx.ui.shop: resolves { cart, total } or null when left without buying.
        public static Func<ShopOptions, Task<ShopResult>> Shop3d(GameEngine engine) => throw new NotImplementedException("port: ui/grab3d.js:shop3d");
        // ctx.ui.bag: resolves { bag }.
        public static Func<BagOptions, Task<BagResult>> Bag3d(GameEngine engine) => throw new NotImplementedException("port: ui/grab3d.js:bag3d");
    }
}
