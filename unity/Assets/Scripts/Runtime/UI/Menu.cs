// Ports ui/menu.js: landing screen, pause menu, settings form (MenuSettings.cs) and the sources/credits page.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Logic;
using UnityEngine.UIElements;

namespace Gyeongbo.UI
{
    public static partial class Menu
    {
        static VisualElement El(string tag, string cls, params object[] kids) => Dom.El(tag, cls, kids);

        // [chapter, title, when · where, line] for the landing screen.
        public static readonly List<string[]> CHAPTERS = new List<string[]>
        {
            new[] { "프롤로그", "평소와 다른 화요일", "D-3 화 17:30 · 한결물류 사무실", "확인된 사실, 추정, 소문을 나눕니다." },
            new[] { "1장", "준비", "D-2 ~ D-1 · 한빛아파트와 동네", "여섯 칸의 시간으로 무엇을 준비할까요." },
            new[] { "2장", "정전과 경보", "D-day 금 밤", "불이 꺼지고, 경보가 울립니다." },
            new[] { "3장", "버티기", "D+1 ~ D+3", "정전과 단수, 그리고 이웃." },
            new[] { "4장", "남을까, 떠날까", "D+6", "조건이 바뀌면 답도 바뀝니다." },
            new[] { "5장", "공식 대피", "D+12", "가방 하나와 새로운 길." },
            new[] { "엔딩", "생존 기록", "", "도움이 된 준비와 놓친 정보." },
        };

        // Resolves 'new' | 'continue' | 'settings' | 'about'.
        public static async Task<string> Landing(bool hasSave = false)
        {
            var canvas = El("div", "landing-art");
            canvas.pickingMode = PickingMode.Ignore;
            ModalHandle<string> m = null;
            Action Choose(string id) => () => { Dom.UiSound("confirm"); m.Close(id); };
            var dot = El("span", "live-dot");
            var loop = new[] { "정보", "계획", "행동", "결과" }.Select((t, i) => El("li", null,
                i > 0 ? El("span", "loop-arrow", "→") : null, El("span", "loop-no", (i + 1).ToString()), El("span", null, t)));
            var content = El("div", "landing-wrap",
                canvas,
                El("div", "landing-shade", El("div", "landing-shade-low")),
                El("main", "landing-main",
                    El("section", "landing-hero",
                        El("p", "landing-eyebrow", dot, "이야기 중심 생존 시뮬레이션 · 가상 동네 연무동"),
                        El("h1", "landing-title",
                            El("span", "bracket", "〈"), El("span", "title-ko", "경보 이전"), El("span", "bracket", "〉"),
                            El("span", "title-en", "Before the Siren".ToUpperInvariant())),   // text-transform: uppercase
                        El("p", "landing-pitch", "전쟁 직전의 준비가, 전쟁 이후의 선택지를 바꿉니다."),
                        El("p", "landing-sub", "사재기가 아니라 정보를 확인하고, 상황에 맞게 판단하고, 이웃과 함께 버티는 이야기입니다."),
                        El("ol", "landing-loop", loop),
                        El("div", "landing-actions",
                            Dom.Btn("새로 시작", Choose("new"), primary: true, iconName: "play", className: "big" + (hasSave ? "" : " autofocus")),
                            hasSave ? Dom.Btn("이어하기", Choose("continue"), iconName: "retry", className: "big autofocus") : null,
                            Dom.Btn("설정", Choose("settings"), ghost: true, iconName: "sliders"),
                            Dom.Btn("출처와 만든 사람", Choose("about"), ghost: true, iconName: "book"))),
                    El("section", "landing-chapters",
                        El("h2", null, "이야기 구성"),
                        El("ol", null, CHAPTERS.Select(c => El("li", null,
                            El("span", "ch-no", c[0]),
                            El("span", "ch-main", El("b", null, c[1]), c[2] != "" ? El("small", null, c[2]) : null, El("span", "ch-line", c[3]))))))),
                El("footer", "landing-foot",
                    El("p", "fiction-note", Dom.Icon("info"),
                        El("span", null, "이 게임의 동네·인물·회사·재난문자·방송은 모두 가상입니다. 실제 상황이 아닙니다. 안전 행동요령은 국민안전24 등 공식 자료에서 확인한 문장만 출처와 함께 인용합니다. 폭력이나 부상 장면은 나오지 않습니다.")),
                    El("p", "keys-note", new[] { new[] { "WASD", "이동" }, new[] { "E", "살피기" }, new[] { "Tab", "수첩" }, new[] { "Esc", "메뉴" } }
                        .Select(k => El("span", null, El("kbd", null, k[0]), k[1])))));
            m = Dom.Modal<string>("landing", "경보 이전 · 시작 화면", content);
            var stopBeacon = UiFx.Pulse(dot, 2);   // ponytail: beacon ring drawn as a pulse
            var stop = MenuSkyline.Start(canvas);
            try { return await m.Done; }
            finally { stop(); stopBeacon(); }
        }

        // Resolves 'resume' | 'settings' | 'save' | 'menu'.
        public static Task<string> Pause()
        {
            ModalHandle<string> m = null;
            Action Go(string id) => () => { Dom.UiSound("confirm"); m.Close(id); };
            Button menuBtn = null;
            VisualElement confirmBox = null;
            Button leave = Dom.Btn("나가기", Go("menu"), primary: true, iconName: "exit");
            confirmBox = El("div", "pause-confirm hidden",
                El("p", null, "메인 메뉴로 나가면 마지막 체크포인트 이후의 진행은 사라집니다."),
                El("div", "row", leave, Dom.Btn("취소", () => { UiStyle.Hidden(confirmBox, true); menuBtn.Focus(); }, ghost: true)));
            confirmBox.tooltip = "메인 메뉴로 나가기 확인";
            menuBtn = Dom.Btn("메인 메뉴로", () => { UiStyle.Hidden(confirmBox, false); leave.Focus(); }, ghost: true, iconName: "exit");
            var keys = new[]
            {
                new[] { "WASD · 방향키", "이동" }, new[] { "Shift", "달리기 (기력)" }, new[] { "Space", "점프" }, new[] { "마우스 (클릭 후) · Alt+방향키", "둘러보기" },
                new[] { "E · Enter", "살피기 · 대화" }, new[] { "F", "손전등 · 휴대폰 불빛" }, new[] { "P", "휴대폰 · FM 라디오" }, new[] { "Tab", "수첩" }, new[] { "I", "소지품" },
                new[] { "M", "지도" }, new[] { "V", "1인칭 / 3인칭" }, new[] { "H", "조작법" }, new[] { "Esc", "이 메뉴" },
            };
            var keyList = El("dl", "key-list", keys.Select(k => El("div", "key-row", El("dt", null, k[0]), El("dd", null, k[1]))));
            keyList.tooltip = "조작키";
            var content = El("scroll", "pause-panel",
                El("p", "eyebrow", "일시정지"),
                El("h2", null, "잠시 멈춤"),
                El("div", "pause-actions",
                    Dom.Btn("계속하기", Go("resume"), primary: true, iconName: "play", className: "autofocus"),
                    Dom.Btn("설정", Go("settings"), iconName: "sliders"),
                    Dom.Btn("체크포인트 저장", Go("save"), iconName: "save"),
                    menuBtn),
                confirmBox,
                keyList);
            m = Dom.Modal<string>("pause", "일시정지 메뉴", content, e =>
            {
                if (UiFx.Key(e) != "Escape" || UiFx.Repeat(e)) return false;
                if (!UiStyle.IsHidden(confirmBox)) { UiStyle.Hidden(confirmBox, true); menuBtn.Focus(); }
                else m.Close("resume");
                return true;
            });
            return m.Done;
        }

        // Credits, every source with a link, the fiction disclaimer.
        public static Task About(IList<SourceEntry> sources)
        {
            var src = (sources ?? new List<SourceEntry>()).Where(s => s != null).ToList();
            var byPub = new List<KeyValuePair<string, List<SourceEntry>>>();
            foreach (var s in src)
            {
                int i = byPub.FindIndex(p => p.Key == s.publisher);
                if (i < 0) { byPub.Add(new KeyValuePair<string, List<SourceEntry>>(s.publisher, new List<SourceEntry>())); i = byPub.Count - 1; }
                byPub[i].Value.Add(s);
            }
            ModalHandle<bool> m = null;
            var scroll = El("scroll", "about-scroll",
                El("section", "about-fiction",
                    El("h3", null, Dom.Icon("info"), "가상의 이야기입니다"),
                    El("p", null, "연무동, 한빛아파트, 한결물류, 바로편의점과 모든 등장인물·사건은 가상입니다. 실제 지역·기관·인물과 관계가 없습니다."),
                    El("p", null, "게임 속 재난문자, 재난방송, 라디오 주파수, 통제 구간, 출국 제한 같은 조건은 이야기를 위해 만든 것입니다. 어떤 신호 하나가 실제 위기를 뜻하지는 않습니다."),
                    El("p", null, "게임이 알려 주는 안전 행동요령은 아래 공식 자료에서 직접 확인한 문장만 인용합니다. 실제 상황에서는 재난방송, 재난문자, 국민안전24와 지자체 안내를 따르세요.")),
                El("section", "about-sources",
                    El("h3", null, Dom.Icon("official"), "참고한 공식 자료 " + src.Count + "건"),
                    byPub.Select(p => El("div", "src-group", El("h4", null, p.Key ?? ""),
                        El("ul", null, p.Value.Select(s => El("li", null,
                            !string.IsNullOrEmpty(s.url) ? DebriefUi.Link(s.title ?? "", s.url) : El("span", null, s.title ?? ""),
                            !string.IsNullOrEmpty(s.checkedAt) ? El("small", null, " · 확인 " + s.checkedAt) : null)))))),
                El("section", "about-credits",
                    El("h3", null, Dom.Icon("people"), "만든 사람과 도구"),
                    El("ul", null,
                        El("li", null, "기획·개발: 〈경보 이전〉 제작팀 (Claude Code와 함께 제작)"),
                        El("li", null, "3D 렌더링: Unity 6 (Universal Render Pipeline)"),
                        El("li", null, "소리: 녹음 파일 없이 코드로 직접 합성"),
                        El("li", null, "글꼴: 기기에 설치된 맑은 고딕 · Apple SD Gothic Neo · Noto Sans CJK KR · 나눔고딕"))));
            scroll.focusable = true; scroll.tabIndex = 0; scroll.tooltip = "내용";
            var content = El("div", "about-panel",
                El("header", "nb-head", El("p", "eyebrow", Dom.Icon("book"), "출처와 만든 사람"), El("h2", null, "〈경보 이전〉에 대하여")),
                scroll,
                El("div", "set-actions", Dom.Btn("닫기", () => m.Close(), primary: true, className: "autofocus")));
            m = Dom.Modal<bool>("about", "출처와 만든 사람", content, e => { if (UiFx.Key(e) == "Escape") { m.Close(); return true; } return false; });
            return m.Done;
        }
    }
}
