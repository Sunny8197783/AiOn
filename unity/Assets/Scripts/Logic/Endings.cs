// Ports game/endings.js: which ending a run reaches (G.know.path) and the lines that colour it.
// The path is the last choice (G.know.path, set in story/evac.js); the body, the stock at home, who you
// helped and whom you hurt colour it. Pure: no UI, no Unity.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gyeongbo.Logic
{
    public sealed class EndingFate
    {
        public string id, title;
        public List<string> lines = new List<string>();
    }

    public static class Endings
    {
        // path -> [title, opening line]
        static readonly Dictionary<string, string[]> PATHS = new Dictionary<string, string[]>
        {
            ["bus"] = new[] { "떠난 사람", "버스는 남쪽의 임시 주거단지로 향했다. 좁은 컨테이너 방이었지만, 밤새 포성이 들리지 않았다." },
            ["missed"] = new[] { "남겨진 사람", "집결지에 닿았을 때 버스는 이미 떠난 뒤였다. 다음 차량은 사흘 뒤라고 했다." },
            ["stay"] = new[] { "지킨 사람", "박 소장과 함께 남았다. 빈 집들의 문을 단속하고, 남은 주민들과 물탱크를 나눠 썼다." },
            ["seoyeon"] = new[] { "함께 떠난 사람", "서연의 지원 차량을 함께 기다렸다. 휠체어 리프트가 고장 나 둘이서 한참을 씨름한 끝에, 해 질 무렵 같은 차로 떠났다." },
            ["volunteer"] = new[] { "남은 손", "행정복지센터 자원봉사 명단에 이름을 적었다. 급수 줄을 정리하고, 거동이 불편한 집에 물을 날랐다." },
            ["walk"] = new[] { "스스로 떠난 사람", "버스를 기다리지 않고 걸어서 도시를 빠져나왔다. 통제선 밖 국도에는 같은 선택을 한 사람들이 길게 늘어서 있었다." },
        };

        static int StockOf(Game G, params string[] ids) => ids.Sum(id => G.items.TryGetValue(id, out var e) && e != null ? e.home + e.bag : 0);

        public static EndingFate PickEnding(Game G)
        {
            var known = G.know?.path;
            var path = known != null && PATHS.ContainsKey(known) ? known : "bus";
            var lines = new List<string> { PATHS[path][1] };
            int water = StockOf(G, "water_2l", "water_500"), food = StockOf(G, "rice_instant", "canned", "bars", "ramen");
            var helps = G.events.Count(e => e.type == "help");
            bool Has(string type, Func<GameEvent, bool> pred) => G.events.Any(e => e.type == type && pred(e));
            var leg = G.injury != null && G.injury.Contains("leg");
            if (path == "stay" || path == "missed")
            {
                lines.Add(water >= 4 && food >= 4 ? "미리 쌓아 둔 물과 식량 덕분에, 급수가 끊긴 열흘을 버틸 수 있었다."
                    : "물과 먹을 것이 금세 바닥났다. 급수차가 오는 날만 손꼽아 기다렸다.");
                if (helps >= 2) lines.Add("서로 얼굴을 아는 이웃들이 공동 부엌을 꾸렸다. 혼자였다면 버티지 못했을 것이다.");
            }
            if (path == "walk") lines.Add(leg ? "부러진 다리로 걷는 길은 끝이 없었다. 지나가던 구호 트럭이 태워 주지 않았다면 그 길 위에서 멈췄을 것이다."
                : G.stats.health < 40 ? "몸이 버티지 못해 길가에 주저앉았다가 구호소에 실려 갔다." : "사흘을 걸어 친척이 있는 남쪽 도시에 닿았다.");
            if (path == "volunteer" && G.stats.health < 40) lines.Add("다친 몸으로 무리한 탓에 한동안 앓아누웠지만, 그 일을 후회하지는 않았다.");
            if (Has("looter", e => e.Str("result") == "gave")) lines.Add("먹을 것을 나눠 준 그 사람을 몇 주 뒤 배급소에서 다시 만났다. 그는 고개를 숙여 인사했다.");
            if (Has("looter", e => e.Str("result") == "fought")) lines.Add("골목에서 주먹을 휘둘렀던 날의 기억은 오래 꿈에 나왔다.");
            if ((G.karma ?? 0) <= -3) lines.Add("이유 없이 사람을 때렸다는 소문이 단지에 돌았다. 몇몇 이웃은 끝내 눈을 마주치지 않았다.");
            if (leg) lines.Add("부러진 다리는 제대로 붙지 않았다. 지금도 비가 오면 욱신거린다.");
            if (G.events.Any(e => e.type == "help" && e.Str("who") == "seoyeon") && path != "seoyeon") lines.Add("서연에게서 짧은 문자가 왔다. \"덕분에 무사히 도착했어요.\"");
            return new EndingFate { id = path, title = PATHS[path][0], lines = lines };
        }
    }
}
