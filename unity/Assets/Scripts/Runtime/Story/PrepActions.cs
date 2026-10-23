// Ports story/prep-actions.js: the preparation actions — the mart run, checking the shelter and routes, the
// home equipment check, the contact card, the neighbours, packing the bag — and the helpers later chapters
// reuse (applyHome, goOut, bagItems, applyBag).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;

namespace Gyeongbo.Story
{
    public static class PrepActions
    {
        public static readonly string[] SEOYEON_CARD = { "보행 보조기를 씁니다", "평지와 완만한 경사로는 혼자 갑니다", "계단과 가파른 내리막은 혼자 내려가지 않습니다", "보조기와 떨어지지 않습니다", "도움이 필요하면 제가 말합니다" };
        public const double RADIO_FREQ = 102.1;   // game frequency of the disaster-broadcast station (fictional)

        // Shows home stock, bag, radio and wallet in the apartment from G (level states).
        public static void ApplyHome(Ctx ctx) => throw new NotImplementedException("port: story/prep-actions.js:applyHome");
        public static Task GoHome(Ctx ctx, double carryKg = 0) => throw new NotImplementedException("port: story/prep-actions.js:goHome");
        // Leave the flat for the town with an objective; resolves once in town.
        public static Task GoOut(Ctx ctx, string title, string body) => throw new NotImplementedException("port: story/prep-actions.js:goOut");
        // Bag UI items from G (owned items with weights, tips when asked, tags, ready = checked or no check needed).
        public static List<BagItem> BagItems(Game G, bool withTips = false) => throw new NotImplementedException("port: story/prep-actions.js:bagItems");
        public static Task DoMart(Ctx ctx, int slot) => throw new NotImplementedException("port: story/prep-actions.js:doMart");
        public static Task DoShelterCheck(Ctx ctx) => throw new NotImplementedException("port: story/prep-actions.js:doShelterCheck");
        public static Task DoEquip(Ctx ctx) => throw new NotImplementedException("port: story/prep-actions.js:doEquip");
        public static Task DoDocs(Ctx ctx) => throw new NotImplementedException("port: story/prep-actions.js:doDocs");
        public static Task DoNeighbors(Ctx ctx) => throw new NotImplementedException("port: story/prep-actions.js:doNeighbors");
        public static Task DoBag(Ctx ctx) => throw new NotImplementedException("port: story/prep-actions.js:doBag");
        // Moves items between home and bag so the bag holds exactly target { id: units }.
        public static void ApplyBag(Game G, IDictionary<string, int> target) => throw new NotImplementedException("port: story/prep-actions.js:applyBag");
    }
}
