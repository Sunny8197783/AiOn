// Ports levels/apartment-props.js: the radio on the living-room table, candles, the backpack by the door,
// the wallet with the ID card; state setters for them.
using System;
using UnityEngine;

namespace Gyeongbo.Levels
{
    public sealed class AptProps
    {
        public Action<object> setRadio, setCandle, setBag, setWallet;   // apartment states radio / candles / bag / wallet
        public Action<float, float> update;
    }

    public static class ApartmentProps
    {
        public static AptProps BuildProps(AptBuilder B, GameObject g, GameObject dyn) => throw new NotImplementedException("port: levels/apartment-props.js:buildProps");
    }
}
