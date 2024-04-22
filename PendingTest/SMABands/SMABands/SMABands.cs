using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SMABands : Robot
    {
        [Parameter("Quantity (Lots)", Group = "Volume", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }

        [Parameter("Simple Moving Average Value", Group = "Volume", DefaultValue = 1, MinValue = 1, Step = 1)]
        public int smaValue { get; set; }

        [Parameter("Minimun Take Pips", Group = "Take", DefaultValue = 1, MinValue = 1, Step = 1)]
        public double minTake { get; set; }

        [Parameter("Enable TakeProfit?", Group = "TakeProfit", DefaultValue = true)]
        public bool useTakeProfit { get; set; }

        [Parameter("Take Profit", Group = "TakeProfit", DefaultValue = 1, MinValue = 1, Step = 1)]
        public int takeProfit { get; set; }

        [Parameter("Enable Stop Loss?", Group = "StopLoss", DefaultValue = true)]
        public bool useStopLoss { get; set; }

        [Parameter("Stop Loss", Group = "StopLoss", DefaultValue = 1, MinValue = 1, Step = 1)]
        public int stopLoss { get; set; }


        private MovingAverage FourMinimum;
        private MovingAverage FourMaximum;
        private double LastValue { get; set; }
        private double volumeInUnits;

        protected override void OnStart()
        {
            volumeInUnits = Symbol.QuantityToVolumeInUnits(Quantity);
            FourMinimum = Indicators.SimpleMovingAverage(Bars.LowPrices, smaValue);
            FourMaximum = Indicators.SimpleMovingAverage(Bars.HighPrices, smaValue);
        }

        protected override void OnBar()
        {
            if (Positions.Count() >= 1)
            {
                if (Bars.LastBar.High >= FourMaximum.Result.LastValue)
                {
                    foreach (var position in Positions)
                    {
                        if (position.Pips > minTake)
                        {
                            ClosePosition(position);
                        }
                    }
                }
            }

            bool buyOrSell = Bars.LastBar.Close >= FourMinimum.Result.LastValue;
            ExecuteMarketOrder((buyOrSell ? TradeType.Buy : TradeType.Sell), SymbolName, volumeInUnits, "SMABands", (useStopLoss ? stopLoss : double.MaxValue), (useTakeProfit ? takeProfit : double.MaxValue));
        }
    }
}
