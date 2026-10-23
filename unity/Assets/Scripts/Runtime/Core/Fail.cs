// SHARED. story/runtime.js Fail: a soft fail (late, dead, exhausted, fire, lostWay) the chapter runner
// catches, shows the fail card for and retries from the last checkpoint.
using System;
using System.Collections.Generic;

namespace Gyeongbo.Core
{
    public sealed class Fail : Exception
    {
        public readonly string type;
        public readonly Dictionary<string, object> detail;

        public Fail(string type, Dictionary<string, object> detail = null) : base("fail:" + type)
        {
            this.type = type;
            this.detail = detail ?? new Dictionary<string, object>();
        }
    }
}
