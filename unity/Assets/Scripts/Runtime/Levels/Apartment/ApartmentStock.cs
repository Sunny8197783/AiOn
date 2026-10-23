// Ports levels/apartment-stock.js: the balcony storage showing what is stocked at home (state stock
// { itemId: count }, each kind as instanced packs up to its slot capacity).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class AptStock
    {
        public Action<Dictionary<string, int>> setStock;
        public List<string> ids = new List<string>();
    }

    public static class ApartmentStock
    {
        public static AptStock BuildStock(AptBuilder B, GameObject g, GameObject dyn) => throw new NotImplementedException("port: levels/apartment-stock.js:buildStock");
    }
}
