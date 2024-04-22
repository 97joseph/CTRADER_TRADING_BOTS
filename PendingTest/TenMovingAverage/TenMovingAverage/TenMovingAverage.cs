using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class TenMovingAverage : Robot
    {
        [Parameter("Quantity (Lots)", Group = "Volume", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }

        [Parameter("Enable TakeProfit?", Group = "TakeProfit", DefaultValue = true)]
        public bool useTakeProfit { get; set; }

        [Parameter("Take Profit", Group = "TakeProfit", DefaultValue = 1, MinValue = 1, Step = 1)]
        public double takeProfit { get; set; }

        [Parameter("Enable Stop Loss?", Group = "StopLoss", DefaultValue = true)]
        public bool useStopLoss { get; set; }

        [Parameter("Stop Loss", Group = "StopLoss", DefaultValue = 1, MinValue = 1, Step = 1)]
        public double stopLoss { get; set; }

        private MovingAverage tenMinimum;
        private MovingAverage tenMaximum;

        private double volumeInUnits;

        protected override void OnStart()
        {
            volumeInUnits = Symbol.QuantityToVolumeInUnits(Quantity);
            tenMinimum = Indicators.MovingAverage(Bars.LowPrices, 10, MovingAverageType.Simple);
            tenMaximum = Indicators.MovingAverage(Bars.HighPrices, 10, MovingAverageType.Simple);
        }

        protected override void OnBar()
        {
            if (Positions.Count() > 0)
            {
                if (Bars.LastBar.High >= tenMaximum.Result.LastValue && !useTakeProfit)
                {
                    foreach (var position in Positions)
                    {
                        ClosePosition(position);
                    }
                }
            }

            if (Bars.LastBar.Close <= tenMinimum.Result.LastValue && Positions.Count() == 0)
            {
                ExecuteMarketOrder(TradeType.Buy, SymbolName, volumeInUnits, "TenMov", (useStopLoss ? stopLoss : double.MaxValue), (useTakeProfit ? takeProfit : double.MaxValue));
            }
        }
    }
}
