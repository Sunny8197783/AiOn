// app.js `ui`: the UI functions the story calls through ctx.ui, wired to the UI groups' statics. Shopping and
// packing happen by hand in the 3D scene (Grab3d).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;
using Gyeongbo.Logic;
using Gyeongbo.UI;

namespace Gyeongbo.App
{
    public sealed class AppUi : IUi
    {
        readonly GameEngine engine;
        Func<ShopOptions, Task<ShopResult>> shop;
        Func<BagOptions, Task<BagResult>> bag;

        public AppUi(GameEngine engine) { this.engine = engine; }

        public Task<string> Talk(TalkOptions o) => Dialog.Talk(o);
        public Task<string> Card(CardOptions o) => Dialog.Card(o);
        public Task TimeSkip(string from, string to, params string[] text) => Dialog.TimeSkip(from, to, text);
        public Task<string> Phone(PhoneOptions o) => UI.Phone.Show(o);
        public Task EmergencyAlert(AlertOptions o) => UI.Phone.EmergencyAlert(o);
        public Task Notebook(IList<InfoNote> entries) => UI.Notebook.Show(entries);
        public Task Inventory(InventoryOptions o) => UI.Notebook.Inventory(o);
        public Task<string> Planner(PlannerOptions o) => UI.Planner.Show(o);
        public Task<ShopResult> Shop(ShopOptions o) => (shop ??= Grab3d.Shop3d(engine))(o);
        public Task<BagResult> Bag(BagOptions o) => (bag ??= Grab3d.Bag3d(engine))(o);
        public Task<RadioResult> Radio(RadioOptions o) => UI.Radio.Open(o);
        public Task<ExtinguisherResult> Extinguisher(ExtinguisherOptions o) => UI.Extinguisher.Open(o);
        public Task<DocsResult> Docs(DocsOptions o) => UI.Docs.Open(o);
        public Task<RouteMapResult> Routemap(RouteMapOptions o) => RouteMap.Open(o);
        public Task<string> FailCard(FailCardOptions o) => DebriefUi.FailCard(o);
        public Task ChapterCard(ChapterCardOptions o) => DebriefUi.ChapterCard(o);
        public Task<string> Ending(EndingRecordData record, IList<SourceEntry> sources) => DebriefUi.Ending(record, sources);
    }
}
