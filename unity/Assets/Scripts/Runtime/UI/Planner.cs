// Ports ui/planner.js: the preparation planner (six slots, action cards with cost, done/blocked state).
// Wall-calendar planner: time slots as tiles, actions as cards. Choosing shows the cost and what it gives up.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static class Planner
    {
        static VisualElement El(string tag, string cls, params object[] kids) => Dom.El(tag, cls, kids);

        // Resolves the picked action id, or null when closed.
        public static Task<string> Show(PlannerOptions o)
        {
            o ??= new PlannerOptions();
            var actions = (o.actions ?? new List<PlannerAction>()).Where(a => a != null).ToList();
            string day = o.day ?? "", hint = o.hint ?? "";
            int total = Math.Max(0, o.slotsTotal), used = Math.Min(total, Math.Max(0, o.slotsUsed));
            int left = total - used;
            PlannerAction selected = null;
            ModalHandle<string> m = null;

            var tiles = Enumerable.Range(0, total).Select(n => El("span", "slot" + (n < used ? " used" : ""),
                El("span", "slot-no", (n + 1).ToString()), El("span", "slot-state", n < used ? "지남" : ""))).ToList();
            var slotRow = El("div", "slot-row", tiles);
            slotRow.tooltip = "준비 시간 " + total + "칸 가운데 " + used + "칸을 썼고 " + left + "칸이 남았습니다";

            var detail = El("aside", "planner-detail");
            var cards = new List<Button>();
            foreach (var a in actions)
            {
                bool blocked = a.done || a.disabled;
                var card = (Button)El("button", "plan-card" + (a.done ? " done" : "") + (a.disabled && !a.done ? " blocked" : "") + (blocked ? " aria-disabled" : ""),
                    El("span", "plan-icon", string.IsNullOrEmpty(a.icon) ? "•" : a.icon),
                    El("span", "plan-main",
                        El("b", "plan-label", a.label ?? ""),
                        El("span", "plan-desc", a.desc ?? ""),
                        a.done ? El("span", "plan-status ok", Dom.Icon("check"), "이미 했음")
                            : a.disabled ? El("span", "plan-status no", Dom.Icon("lock"), string.IsNullOrEmpty(a.reason) ? "지금은 할 수 없음" : a.reason) : null),
                    El("span", "plan-cost", a.cost + "칸"));
                card.tooltip = a.cost + "칸 필요";
                card.RegisterCallback<PointerEnterEvent>(_ => Preview(a));
                card.RegisterCallback<FocusInEvent>(_ => { UiStyle.Toggle(card, "is-focus", true); Preview(a); });
                card.RegisterCallback<FocusOutEvent>(_ => UiStyle.Toggle(card, "is-focus", false));
                card.clicked += () => Choose(a, card);
                cards.Add(card);
            }
            var grid = El("div", "planner-grid", cards);
            grid.RegisterCallback<PointerLeaveEvent>(_ => Preview(selected));

            var board = El("div", "planner-board",
                El("div", "planner-strip"),
                El("header", "planner-head",
                    El("p", "eyebrow", Dom.Icon("calendar"), "준비 계획표"),
                    Dom.Btn("닫기", () => m.Close(null), ghost: true, className: "planner-close"),
                    El("h2", "planner-title", day),
                    El("p", "planner-sub", "남은 칸 ", El("b", "planner-left", left.ToString()), " / " + total + " · 모든 일을 다 할 수는 없습니다.")),
                slotRow,
                hint != "" ? El("p", "planner-hint", Dom.Icon("info"), El("span", null, hint)) : null,
                El("div", "planner-body", grid, detail));

            m = Dom.Modal<string>("planner", "준비 계획표 · " + day, board, OnKey);
            Preview(null);

            void Preview(PlannerAction a)
            {
                int cost = a != null && !a.done && !a.disabled ? a.cost : 0;
                for (int n = 0; n < tiles.Count; n++)
                {
                    bool on = n >= used && n < used + cost;
                    UiStyle.Toggle(tiles[n], "preview", on);
                    tiles[n].Q<Label>(className: "slot-state").text = n < used ? "지남" : on ? "사용" : "";
                }
                if (selected == null || a == selected) RenderDetail(a, a == selected && selected != null);
            }

            void RenderDetail(PlannerAction a, bool confirming)
            {
                if (a == null) { Dom.Replace(detail, El("p", "muted", "할 일을 고르면 드는 시간과 대신 못 하게 되는 일을 보여 줍니다.")); return; }
                int after = left - a.cost;
                var lost = actions.Where(b => b.id != a.id && !b.done && !b.disabled && b.cost > after).ToList();
                var parts = new List<object> { El("h3", null, El("span", null, a.icon ?? ""), " ", a.label ?? ""), El("p", null, a.desc ?? "") };
                if (a.done || a.disabled)
                    parts.Add(El("p", "plan-status no", Dom.Icon("lock"), a.done ? "이미 했습니다." : (string.IsNullOrEmpty(a.reason) ? "지금은 할 수 없습니다." : a.reason)));
                else
                {
                    parts.Add(El("dl", "plan-facts",
                        El("dt", null, "드는 시간"), El("dd", null, a.cost + "칸"),
                        El("dt", null, "남는 칸"), El("dd", null, after > 0 ? after + "칸" : "없음 · 준비 시간이 끝납니다")));
                    if (lost.Count > 0) parts.Add(El("div", "plan-lost", El("b", null, "이걸 하면 못 하게 되는 일"), El("ul", null, lost.Select(b => El("li", null, "• " + b.label)))));
                    if (confirming)
                        parts.Add(El("div", "plan-confirm",
                            Dom.Btn("이 일을 한다", () => { Dom.UiSound("confirm"); m.Close(a.id); }, primary: true, iconName: "check"),
                            Dom.Btn("다시 고르기", Back, ghost: true)));
                }
                Dom.Replace(detail, parts);
            }

            void Choose(PlannerAction a, Button card)
            {
                if (a.done || a.disabled)
                {
                    UiFx.Shake(card); Dom.UiSound("error"); selected = null;
                    foreach (var c in cards) UiStyle.RemoveClass(c, "selected");
                    RenderDetail(a, false);
                    return;
                }
                selected = a;
                foreach (var c in cards) UiStyle.Toggle(c, "selected", c == card);
                Dom.UiSound("select");
                Preview(a);
                detail.Q<Button>(className: "btn")?.Focus();
            }

            void Back()
            {
                int i = actions.IndexOf(selected);
                var card = i >= 0 ? cards[i] : null;
                selected = null;
                foreach (var c in cards) UiStyle.RemoveClass(c, "selected");
                Preview(null);
                card?.Focus();
            }

            bool OnKey(KeyDownEvent e)
            {
                var key = UiFx.Key(e);
                if (key == "Escape") { if (selected != null) Back(); else m.Close(null); return true; }
                int at = cards.IndexOf(board.panel?.focusController?.focusedElement as Button);
                if (at < 0) return false;
                int step = key == "ArrowRight" || key == "ArrowDown" ? 1 : key == "ArrowLeft" || key == "ArrowUp" ? -1 : 0;
                if (step != 0) { cards[(at + step + cards.Count) % cards.Count].Focus(); return true; }
                return false;
            }

            // Start on the first card that can be chosen.
            (cards.FirstOrDefault(c => !c.ClassListContains("aria-disabled")) ?? cards.FirstOrDefault())?.Focus();
            return m.Done;
        }
    }
}
