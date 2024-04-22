using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class InsideBarBot : Robot
    {
        [Parameter("Quantity (Lots)", Group = "Volume", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }

        private double volumeInUnits;

        private double takeProfit;
        private double stopLoss;

        protected override void OnStart()
        {
            volumeInUnits = Symbol.QuantityToVolumeInUnits(Quantity);
        }

        protected override void OnBar()
        {



            //Buy Setup
            //Last 3 Candles ascending
            if (Bars.Last(3).Open >= Bars.Last(4).Close && Bars.Last(2).Open >= Bars.Last(3).Close)
            {
                if (Bars.Last(4).Close > Bars.Last(4).Open && Bars.Last(3).Close > Bars.Last(3).Open && Bars.Last(2).Close > Bars.Last(2).Open)
                {
                    takeProfit = Math.Abs((Bars.Last(1).High - Bars.Last(4).Low) / Symbol.PipSize);
                    stopLoss = Math.Abs((Bars.LastBar.Close - Bars.Last(1).Low) / Symbol.PipSize);
                    if (Bars.Last(1).Low > Bars.Last(2).Low && Bars.Last(1).High < Bars.Last(2).High)
                    {
                        if (Bars.Last(1).Open >= Bars.Last(1).Close)
                            PlaceStopLimitOrder(TradeType.Buy, SymbolName, volumeInUnits, Bars.Last(1).High, 300, "Inside Bar", takeProfit, stopLoss);
                    }
                }
            }
            else if (Bars.Last(3).Open <= Bars.Last(4).Close && Bars.Last(2).Open <= Bars.Last(3).Close)
            {
                if (Bars.Last(4).Close < Bars.Last(4).Open && Bars.Last(3).Close < Bars.Last(3).Open && Bars.Last(2).Close < Bars.Last(2).Open)
                {
                    takeProfit = Math.Abs((Bars.Last(4).High - Bars.Last(1).Low) / Symbol.PipSize);
                    stopLoss = Math.Abs((Bars.Last(1).High - Bars.LastBar.Close) / Symbol.PipSize);
                    if (Bars.Last(1).Low > Bars.Last(2).Low && Bars.Last(1).High < Bars.Last(2).High)
                    {
                        if (Bars.Last(1).Open <= Bars.Last(1).Close)
                            PlaceStopLimitOrder(TradeType.Sell, SymbolName, volumeInUnits, Bars.Last(1).Low, 300, "Inside Bar", takeProfit, stopLoss);
                    }
                }
            }

        }
    }
}
