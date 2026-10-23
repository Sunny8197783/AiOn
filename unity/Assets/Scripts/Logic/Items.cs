// Ports game/items.js: the item catalogue, starting stock, bag limit, mart sections and store prices.
// Prices are fictional 2026 won; weights and volumes are game approximations, not advice.
// needsCheck: what turns "bought" into "usable" (batteries in, charged, tuned). risk: why an item can hurt.
// facts: FACTS ids (game/facts.js) that the item's tip is based on.
// The tables below are generated from the JS module (values verbatim, insertion order kept).
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
        static List<string> L(params string[] s) => new List<string>(s);

        public static readonly Dictionary<string, CatalogItem> CATALOG = new Dictionary<string, CatalogItem>
        {
            ["water_2l"] = new CatalogItem { name = "생수 2L", unit = "병", price = 1100, weightKg = 2.05, volumeL = 2.2, category = "water", facts = L("home_tub", "water_sphere") },
            ["water_500"] = new CatalogItem { name = "생수 500mL", unit = "병", price = 600, weightKg = 0.52, volumeL = 0.6, category = "water", facts = L("bag_days", "water_sphere") },
            ["rice_instant"] = new CatalogItem { name = "즉석밥", unit = "개", price = 1500, weightKg = 0.22, volumeL = 0.4, category = "food", facts = L("bag_nocook") },
            ["canned"] = new CatalogItem { name = "통조림(참치·햄)", unit = "캔", price = 2800, weightKg = 0.2, volumeL = 0.25, category = "food", facts = L("bag_nocook", "home_30") },
            ["ramen"] = new CatalogItem { name = "라면", unit = "봉", price = 900, weightKg = 0.12, volumeL = 0.6, category = "food", needs = L("hot_water"), facts = L("home_30") },
            ["bars"] = new CatalogItem { name = "건빵·에너지바", unit = "봉", price = 1500, weightKg = 0.1, volumeL = 0.15, category = "food", facts = L("bag_nocook") },
            ["radio"] = new CatalogItem { name = "휴대용 라디오(건전지식)", unit = "대", price = 24000, weightKg = 0.3, volumeL = 0.6, category = "info", needsCheck = "batteries", facts = L("radio_batt", "power_prep", "radio_listen") },
            ["batteries_aa"] = new CatalogItem { name = "AA 건전지 4개", unit = "팩", price = 4500, weightKg = 0.1, volumeL = 0.05, category = "power", facts = L("radio_batt") },
            ["flashlight"] = new CatalogItem { name = "손전등(LED)", unit = "개", price = 9000, weightKg = 0.2, volumeL = 0.4, category = "light", needsCheck = "batteries", facts = L("power_prep", "candle_no") },
            ["powerbank"] = new CatalogItem { name = "보조배터리", unit = "개", price = 22000, weightKg = 0.22, volumeL = 0.15, category = "power", needsCheck = "charge", facts = L("comms_battery") },
            ["first_aid"] = new CatalogItem { name = "구급함(상비약·밴드)", unit = "개", price = 15000, weightKg = 0.5, volumeL = 1.5, category = "health", facts = L("bag_days") },
            ["meds"] = new CatalogItem { name = "복용 중인 약(1주일분)", unit = "봉투", price = 0, weightKg = 0.1, volumeL = 0.1, category = "health", notSold = true, facts = L("contact_fields", "people_unhcr") },
            ["masks"] = new CatalogItem { name = "마스크 10매", unit = "팩", price = 5000, weightKg = 0.1, volumeL = 0.4, category = "health", facts = L() },
            ["wipes"] = new CatalogItem { name = "물티슈", unit = "팩", price = 2000, weightKg = 0.4, volumeL = 0.6, category = "hygiene", facts = L() },
            ["tissue"] = new CatalogItem { name = "휴지", unit = "롤", price = 1500, weightKg = 0.15, volumeL = 0.8, category = "hygiene", facts = L() },
            ["trash_bags"] = new CatalogItem { name = "비닐봉투·쓰레기봉투", unit = "묶음", price = 2500, weightKg = 0.2, volumeL = 0.3, category = "hygiene", facts = L() },
            ["blanket"] = new CatalogItem { name = "은박 보온담요", unit = "장", price = 3000, weightKg = 0.06, volumeL = 0.2, category = "warmth", facts = L("bag_foil") },
            ["raincoat"] = new CatalogItem { name = "우의", unit = "벌", price = 3000, weightKg = 0.15, volumeL = 0.3, category = "warmth", facts = L() },
            ["clothes"] = new CatalogItem { name = "여벌 옷·양말", unit = "벌", price = 0, weightKg = 0.6, volumeL = 3, category = "warmth", notSold = true, facts = L("home_clothes") },
            ["hygiene_kit"] = new CatalogItem { name = "세면도구", unit = "세트", price = 0, weightKg = 0.3, volumeL = 0.8, category = "hygiene", notSold = true, facts = L() },
            ["whistle"] = new CatalogItem { name = "호루라기", unit = "개", price = 2000, weightKg = 0.02, volumeL = 0.02, category = "tool", facts = L() },
            ["tape"] = new CatalogItem { name = "청테이프", unit = "개", price = 2000, weightKg = 0.2, volumeL = 0.3, category = "tool", facts = L() },
            ["water_jug"] = new CatalogItem { name = "접이식 물통 10L", unit = "개", price = 7000, weightKg = 0.3, volumeL = 1, category = "water", facts = L("water_point") },
            ["candles"] = new CatalogItem { name = "양초 6개", unit = "팩", price = 3500, weightKg = 0.4, volumeL = 0.4, category = "light", risk = "fire", facts = L("candle_prep", "candle_no", "candle_rules") },
            ["gas_stove"] = new CatalogItem { name = "휴대용 가스레인지", unit = "대", price = 25000, weightKg = 1.8, volumeL = 6, category = "fire", risk = "fire", facts = L("home_stove", "shelter_rules") },
            ["butane"] = new CatalogItem { name = "부탄가스 4개", unit = "묶음", price = 6000, weightKg = 1, volumeL = 2, category = "fire", risk = "fire", facts = L("home_stove") },
            ["cash"] = new CatalogItem { name = "현금 5만 원", unit = "묶음", price = 50000, weightKg = 0.01, volumeL = 0.01, category = "docs", atmOnly = true, facts = L("bag_cash") },
            // Made at home (서류·연락처 정리), not bought.
            ["contact_card"] = new CatalogItem { name = "비상연락처 카드", unit = "장", price = 0, weightKg = 0.01, volumeL = 0.01, category = "docs", notSold = true, facts = L("contact_card", "contact_fields", "contact_meet") },
            ["doc_copies"] = new CatalogItem { name = "신분증·중요 서류 사본", unit = "묶음", price = 0, weightKg = 0.05, volumeL = 0.05, category = "docs", notSold = true, facts = L("bag_copies", "docs_take") },
            // The original, picked up from the wallet on the living-room table (story/world.js).
            ["id_card"] = new CatalogItem { name = "신분증(주민등록증)", unit = "장", price = 0, weightKg = 0.01, volumeL = 0.01, category = "docs", notSold = true, facts = L("docs_take") },
            // Already in the fridge; only safe for a limited time once the power is off.
            ["fridge_food"] = new CatalogItem { name = "냉장고 반찬", unit = "통", price = 0, weightKg = 0.4, volumeL = 0.5, category = "food", notSold = true, risk = "spoil", facts = L("food_fridge", "food_4h", "food_door") },
            ["cooked_meat"] = new CatalogItem { name = "남은 익힌 고기", unit = "통", price = 0, weightKg = 0.3, volumeL = 0.4, category = "food", notSold = true, risk = "spoil", facts = L("myth_meat", "food_4h") },
        };

        // What 정민 has at home before preparing. The flashlight has no batteries: owned is not usable.
        public static readonly Dictionary<string, int> START_HOME = new Dictionary<string, int> { ["water_2l"] = 2, ["ramen"] = 5, ["rice_instant"] = 2, ["canned"] = 1, ["flashlight"] = 1, ["meds"] = 1, ["clothes"] = 2, ["hygiene_kit"] = 1, ["tissue"] = 2, ["fridge_food"] = 4, ["cooked_meat"] = 1 };

        // A 25 L day pack; above 8 kg walking slows down (game numbers).
        public static readonly BagLimit BAG_LIMIT = new BagLimit { weightKg = 8, volumeL = 25 };

        public static readonly Dictionary<string, MartSection> MART_SECTIONS = new Dictionary<string, MartSection>
        {
            ["water"] = new MartSection { label = "생수·음료", items = L("water_2l", "water_500") },
            ["food"] = new MartSection { label = "식품", items = L("rice_instant", "canned", "ramen", "bars") },
            ["daily"] = new MartSection { label = "생활용품", items = L("wipes", "tissue", "trash_bags", "tape", "raincoat", "blanket", "water_jug", "gas_stove", "butane", "whistle") },
            ["elec"] = new MartSection { label = "전지·전기용품", items = L("radio", "batteries_aa", "flashlight", "powerbank", "candles") },
            ["health"] = new MartSection { label = "의약외품", items = L("first_aid", "masks") },
        };

        // The convenience store: fewer items, a little dearer.
        public static readonly Dictionary<string, int> STORE_ITEMS = new Dictionary<string, int> { ["water_500"] = 700, ["bars"] = 1800, ["ramen"] = 1100, ["batteries_aa"] = 5500, ["wipes"] = 2500, ["tissue"] = 1800, ["masks"] = 6000, ["candles"] = 4000 };

        public static string ItemName(string id) => CATALOG.TryGetValue(id ?? "", out var c) ? c.name : id;
    }
}
