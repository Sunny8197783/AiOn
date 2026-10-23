// Ports story/prologue.js: 프롤로그 — a Tuesday at the 한결물류 office: delivery cancellations on the dashboard
// (drawn on a Paint and set as the office 'dashboard' state), sorting signals into 확인된 사실 / 추정 / 소문,
// the walk home.
using System;
using System.Threading.Tasks;
using Gyeongbo.Core;
using Gyeongbo.Engine;

namespace Gyeongbo.Story
{
    public static class Prologue
    {
        // Chapter runner contract: resolves when the chapter is done (null; only the ending returns a value).
        public static Task<string> Run(Ctx ctx) => throw new NotImplementedException("port: story/prologue.js:prologue");
        // The office dashboard for alerts 0..3.
        public static Paint DashboardCanvas(int alerts) => throw new NotImplementedException("port: story/prologue.js:dashboardCanvas");
    }
}
