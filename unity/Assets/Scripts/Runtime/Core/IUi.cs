// SHARED. ctx.ui: the UI functions the story calls (app.js `ui` object). App/AppUi implements it by calling
// the UI groups' static functions (Dialog, Phone, Notebook, Planner, DebriefUi, Bag/Grab3d, Radio, ...).
using System.Collections.Generic;
using System.Threading.Tasks;
using Gyeongbo.Logic;

namespace Gyeongbo.Core
{
    public interface IUi
    {
        Task<string> Talk(TalkOptions o);
        Task<string> Card(CardOptions o);
        Task TimeSkip(string from, string to, params string[] text);
        Task<string> Phone(PhoneOptions o);
        Task EmergencyAlert(AlertOptions o);
        Task Notebook(IList<InfoNote> entries);
        Task Inventory(InventoryOptions o);
        Task<string> Planner(PlannerOptions o);
        Task<ShopResult> Shop(ShopOptions o);          // ui/grab3d.js shop3d (by hand in the 3D scene)
        Task<BagResult> Bag(BagOptions o);             // ui/grab3d.js bag3d
        Task<RadioResult> Radio(RadioOptions o);
        Task<ExtinguisherResult> Extinguisher(ExtinguisherOptions o);
        Task<DocsResult> Docs(DocsOptions o);
        Task<RouteMapResult> Routemap(RouteMapOptions o);
        Task<string> FailCard(FailCardOptions o);
        Task ChapterCard(ChapterCardOptions o);
        Task<string> Ending(EndingRecordData record, IList<SourceEntry> sources);
    }
}
