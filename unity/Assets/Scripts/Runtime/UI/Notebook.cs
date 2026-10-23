// Ports ui/notebook.js: 수첩 (G.info by day, filtered by label) and 소지품 (home stock and bag with the load meter).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;

namespace Gyeongbo.UI
{
    public static class Notebook
    {
        public static Task Show(IList<InfoNote> entries) => throw new NotImplementedException("port: ui/notebook.js:notebook");
        public static Task Inventory(InventoryOptions o) => throw new NotImplementedException("port: ui/notebook.js:inventory");
    }
}
