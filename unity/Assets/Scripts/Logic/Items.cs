// Ports game/items.js: the item catalogue, starting stock, bag limit, mart sections and store prices.
using System;
using System.Collections.Generic;

namespace Gyeongbo.Logic
{
    public sealed class CatalogItem
    {
        public string name, unit, category;
        public int price;
        public double weightKg, volumeL;
        public string needsCheck;          // null | "batteries" | "charge"
        public string risk;                // null | "fire" | "spoil"
        public bool notSold, atmOnly;
        public List<string> needs = new List<string>();
        public List<string> facts = new List<string>();
    }

    public sealed class BagLimit
    {
        public double weightKg, volumeL;
    }

    public sealed class MartSection
    {
        public string label;
        public List<string> items = new List<string>();
    }

    public static class Items
    {
        // Data tables: filled by the Logic porter from game/items.js (insertion order kept).
        public static readonly Dictionary<string, CatalogItem> CATALOG = new Dictionary<string, CatalogItem>();
        public static readonly Dictionary<string, int> START_HOME = new Dictionary<string, int>();
        public static readonly BagLimit BAG_LIMIT = new BagLimit { weightKg = 8, volumeL = 25 };
        public static readonly Dictionary<string, MartSection> MART_SECTIONS = new Dictionary<string, MartSection>();
        public static readonly Dictionary<string, int> STORE_ITEMS = new Dictionary<string, int>();

        public static string ItemName(string id) => CATALOG.TryGetValue(id ?? "", out var c) ? c.name : id;
    }
}
