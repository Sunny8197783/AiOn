// Ports levels/apartment-view.js: what the windows look out on — the opposite blocks with lit windows,
// the street and sky per view (day | dusk | night | blackout).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class AptView
    {
        public GameObject group;
        public Action<object> setView;
        public Action<float> update;
        public List<string> views = new List<string>();
    }

    public static class ApartmentView
    {
        public static AptView BuildView(AptBuilder B) => throw new NotImplementedException("port: levels/apartment-view.js:buildView");
    }
}
