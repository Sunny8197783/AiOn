// SHARED. Options and results of the ui/*.js functions the story calls through ctx.ui (IUi). Field names
// are the JS keys. The UI groups implement the functions with exactly these types.
using System;
using System.Collections.Generic;
using Gyeongbo.Logic;
using UnityEngine.UIElements;

namespace Gyeongbo.Core
{
    // A talk choice, a card/phone action button. disabled choices show reason and cannot be picked.
    public sealed class Choice
    {
        public string id, label, reason, note;
        public bool primary, disabled;
        public Choice() { }
        public Choice(string id, string label, bool primary = false) { this.id = id; this.label = label; this.primary = primary; }
    }

    public sealed class ObjectiveStep
    {
        public string text;
        public bool done;
        public ObjectiveStep() { }
        public ObjectiveStep(string text, bool done) { this.text = text; this.done = done; }
    }

    // talk() resolves the picked choice id, or null when there were no choices.
    public sealed class TalkOptions
    {
        public string speaker = "", role = "";
        public VisualElement portrait;
        public List<string> lines = new List<string>();
        public List<Choice> choices;
    }

    // card() resolves the action id. kind: info | alert | sms | ... (ui/dialog.js KIND).
    public sealed class CardOptions
    {
        public string kind = "info", title = "", meta = "";
        public List<string> body = new List<string>();
        public List<Choice> actions;
    }

    public sealed class PhoneMessage
    {
        public string from, kind, time, text;   // kind: sms | safety | emergency | me
    }

    // phone() resolves the action id.
    public sealed class PhoneOptions
    {
        public double battery = 50;
        public string signal = "ok";
        public List<PhoneMessage> messages = new List<PhoneMessage>();
        public string mapApp;                   // null | 'fail'
        public List<Choice> actions;
    }

    public sealed class AlertOptions
    {
        public string kind = "위급재난문자", text = "", time = "";
    }

    public sealed class InventoryOptions
    {
        public Dictionary<string, ItemEntry> home, bag;
        public Dictionary<string, CatalogItem> catalog;
        public BagLoadResult load;
        public BagLimit limit;
    }

    public sealed class PlannerAction
    {
        public string id, label, desc, icon, reason;
        public int cost;
        public bool done, disabled;
    }

    // planner() resolves the picked action id, or null (closed).
    public sealed class PlannerOptions
    {
        public string day = "", hint = "";
        public int slotsTotal = 6, slotsUsed;
        public List<PlannerAction> actions = new List<PlannerAction>();
    }

    public sealed class ShopItem
    {
        public string id, name, unit, note, label;
        public int price;
        public double weightKg, volumeL;
        public string stock = "ok";             // ok | low | out
    }

    public sealed class InfoLine
    {
        public string text, label;
    }

    public sealed class ShopOptions
    {
        public string title = "진열대", budgetNote;
        public int money;
        public List<ShopItem> items = new List<ShopItem>();
        public List<InfoLine> info = new List<InfoLine>();
    }

    // null = left without buying.
    public sealed class ShopResult
    {
        public Dictionary<string, int> cart = new Dictionary<string, int>();
        public int total;
    }

    public sealed class BagItem
    {
        public string id, name, tip;
        public double weightKg, volumeL;
        public int home, bag;
        public List<string> tags = new List<string>();
        public bool ready = true;
    }

    public sealed class BagOptions
    {
        public string mode = "guided", context = "alert";
        public BagLimit limit = new BagLimit { weightKg = 8, volumeL = 25 };
        public List<BagItem> items = new List<BagItem>();
        public List<BagGoal> goals = new List<BagGoal>();
        public List<string> tips = new List<string>();
    }

    // { bag: { id: units in the bag } }
    public sealed class BagResult
    {
        public Dictionary<string, int> bag = new Dictionary<string, int>();
    }

    public sealed class RadioOptions
    {
        public string mode = "guided", device = "radio";   // device: radio | phone
        public List<Station> stations = new List<Station>();
        public double start = 89.5;
        public bool battery = true;
        public int extraSeconds;
        public Action<float?> onSignal;                     // clarity 0..1 while tuned, null when closed
    }

    public sealed class RadioNote
    {
        public string text, kind;
    }

    public sealed class RadioResult
    {
        public Station station;
        public bool official;
        public List<string> heard = new List<string>();
        public List<RadioNote> noted = new List<RadioNote>();
    }

    public sealed class ExtinguisherOptions
    {
        public string mode = "guided", fire = "small";
        public List<ExtStep> steps;                         // null = Drills.EXT_STEPS
        public List<ExtStep> choices;                       // null = Drills.EXT_CHOICES
        public bool practice;
    }

    public sealed class ExtinguisherResult
    {
        public List<string> sequence = new List<string>();
        public string result;
    }

    public sealed class DocsOptions
    {
        public string mode = "guided";
        public List<DocField> fields = new List<DocField>();
        public List<string> carry = new List<string> { "bag", "home" };
    }

    public sealed class DocsResult
    {
        public Dictionary<string, string> card = new Dictionary<string, string>();
        public string where;
    }

    public sealed class KnownMap
    {
        public List<string> closures = new List<string>();
        public Dictionary<string, string> gates = new Dictionary<string, string>();
    }

    public sealed class RouteMapOptions
    {
        public string mode = "guided", from = "home", to = "shelter";   // mode 'view' = just look
        public List<RouteOption> options = new List<RouteOption>();
        public List<string> closures = new List<string>();
        public Dictionary<string, string> gates = new Dictionary<string, string>();   // open | closed
        public KnownMap known = new KnownMap();
        public XZ? playerAt;
    }

    public sealed class RouteMapResult
    {
        public int? choice;
    }

    // failCard() resolves 'retry' or 'menu'.
    public sealed class FailCardOptions
    {
        public string title = "", seen = "", missed = "", next = "";
        public List<SourceEntry> sources = new List<SourceEntry>();
    }

    public sealed class ChapterCardOptions
    {
        public string chapter = "", title = "";
        public List<string> lines = new List<string>();
        public List<Payoff> payoffs = new List<Payoff>();
    }
}
