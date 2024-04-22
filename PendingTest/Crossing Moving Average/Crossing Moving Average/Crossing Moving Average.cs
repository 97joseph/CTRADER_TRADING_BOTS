using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class CrossingMovingAverage : Robot
    {
        [Parameter("Quantity (Lots)", Group = "Volume", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }

        [Parameter("MME Fast", Group = "MME", DefaultValue = 13)]
        public int mmeFast { get; set; }

        [Parameter("MME Ultra Slow", Group = "MME", DefaultValue = 66)]
        public int mmeSlow { get; set; }

        [Parameter("MME Medium", Group = "MME", DefaultValue = 21)]
        public int mmeMedium { get; set; }


        [Parameter("Take Profit", Group = "Risk Management", DefaultValue = 0)]
        public int takeProfit { get; set; }

        [Parameter("Stop Loss", Group = "Risk Management", DefaultValue = 5)]
        public int stopLoss { get; set; }


        [Parameter("Include Trailing Stop", Group = "Trailing Stop", DefaultValue = false)]
        public bool IncludeTrailingStop { get; set; }

        [Parameter("Trailing Stop Trigger (pips)", Group = "Trailing Stop", DefaultValue = 5)]
        public int TrailingStopTrigger { get; set; }

        [Parameter("Trailing Stop Step (pips)", Group = "Trailing Stop", DefaultValue = 1)]
        public int TrailingStopStep { get; set; }


        private MovingAverage i_MA_slow;
        private MovingAverage i_MA_fast;
        private MovingAverage i_MA_medium;

        private double volumeInUnits;

        protected override void OnStart()
        {
            volumeInUnits = Symbol.QuantityToVolumeInUnits(Quantity);

            i_MA_slow = Indicators.MovingAverage(Bars.ClosePrices, mmeSlow, MovingAverageType.Exponential);
            i_MA_fast = Indicators.MovingAverage(Bars.ClosePrices, mmeFast, MovingAverageType.Exponential);
            i_MA_medium = Indicators.MovingAverage(Bars.ClosePrices, mmeMedium, MovingAverageType.Exponential);
        }

        protected override void OnBar()
        {
            if (i_MA_fast.Result.Last(5) < i_MA_medium.Result.Last(5) && i_MA_medium.Result.Last(5) < i_MA_slow.Result.Last(5) && i_MA_fast.Result.LastValue > i_MA_slow.Result.LastValue && i_MA_medium.Result.LastValue > i_MA_slow.Result.LastValue)
            {
                ExecuteMarketOrder(TradeType.Buy, SymbolName, volumeInUnits, "Scalp", stopLoss, takeProfit);
            }
            if (i_MA_fast.Result.Last(5) > i_MA_medium.Result.Last(5) && i_MA_medium.Result.Last(5) > i_MA_slow.Result.Last(5) && i_MA_fast.Result.LastValue < i_MA_slow.Result.LastValue && i_MA_medium.Result.LastValue < i_MA_slow.Result.LastValue)
            {
                ExecuteMarketOrder(TradeType.Sell, SymbolName, volumeInUnits, "Scalp", stopLoss, takeProfit);
            }
        }

        protected override void OnTick()
        {
            if (IncludeTrailingStop)
            {
                SetTrailingStop();
            }
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
        }

        private void SetTrailingStop()
        {
            var sellPositions = Positions.FindAll("Scalp", SymbolName, TradeType.Sell);

            foreach (Position position in sellPositions)
            {
                double distance = position.EntryPrice - Symbol.Ask;

                if (distance < TrailingStopTrigger * Symbol.PipSize)
                    continue;

                double newStopLossPrice = Symbol.Ask + TrailingStopStep * Symbol.PipSize;

                if (position.StopLoss == null || newStopLossPrice < position.StopLoss)
                {
                    ModifyPosition(position, newStopLossPrice, position.TakeProfit);
                }
            }

            var buyPositions = Positions.FindAll("Scalp", SymbolName, TradeType.Buy);

            foreach (Position position in buyPositions)
            {
                double distance = Symbol.Bid - position.EntryPrice;

                if (distance < TrailingStopTrigger * Symbol.PipSize)
                    continue;

                double newStopLossPrice = Symbol.Bid - TrailingStopStep * Symbol.PipSize;
                if (position.StopLoss == null || newStopLossPrice > position.StopLoss)
                {
                    ModifyPosition(position, newStopLossPrice, position.TakeProfit);
                }
            }
        }
    }
}
