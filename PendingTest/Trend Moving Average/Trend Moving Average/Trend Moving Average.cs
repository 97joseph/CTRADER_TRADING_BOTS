using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class TrendMovingAverage : Robot
    {

        [Parameter("Quantity (Lots)", Group = "Volume", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }

        [Parameter("Source", Group = "RSI")]
        public DataSeries Source { get; set; }

        [Parameter("Periods", Group = "RSI", DefaultValue = 19)]
        public int Periods { get; set; }

        private RelativeStrengthIndex rsi;

        [Parameter("MME Slow", Group = "RSI", DefaultValue = 16)]
        public int mmeSlow { get; set; }

        [Parameter("MME Fast", Group = "RSI", DefaultValue = 12)]
        public int mmeFast { get; set; }

        [Parameter("MME Ultra Slow", Group = "RSI", DefaultValue = 196)]
        public int mmeUltraSlow { get; set; }

        [Parameter("Take Profit", Group = "RSI", DefaultValue = 200)]
        public int takeProfit { get; set; }

        [Parameter("Stop Loss", Group = "RSI", DefaultValue = 95)]
        public int stopLoss { get; set; }

        [Parameter("Include Trailing Stop", DefaultValue = false)]
        public bool IncludeTrailingStop { get; set; }

        [Parameter("Trailing Stop Trigger (pips)", DefaultValue = 20)]
        public int TrailingStopTrigger { get; set; }

        [Parameter("Trailing Stop Step (pips)", DefaultValue = 10)]
        public int TrailingStopStep { get; set; }



        private MovingAverage i_MA_ultraSlow;
        private MovingAverage i_MA_slow;
        private MovingAverage i_MA_fast;


        private double volumeInUnits;

        protected override void OnTick()
        {
            if (IncludeTrailingStop)
            {
                SetTrailingStop();
            }
        }

        protected override void OnStart()
        {
            volumeInUnits = Symbol.QuantityToVolumeInUnits(Quantity);
            rsi = Indicators.RelativeStrengthIndex(Source, Periods);
            i_MA_slow = Indicators.MovingAverage(Bars.ClosePrices, mmeSlow, MovingAverageType.Exponential);
            i_MA_ultraSlow = Indicators.MovingAverage(Bars.ClosePrices, mmeUltraSlow, MovingAverageType.Exponential);
            i_MA_fast = Indicators.MovingAverage(Bars.ClosePrices, mmeFast, MovingAverageType.Exponential);
        }

        protected override void OnBar()
        {
            if (Positions.Count() == 0)
            {
                if (rsi.Result.LastValue > 25 && rsi.Result.LastValue < 75)
                {
                    if (i_MA_fast.Result.LastValue > i_MA_slow.Result.LastValue)
                    {
                        ExecuteMarketOrder(TradeType.Buy, SymbolName, volumeInUnits, "RSI", stopLoss, takeProfit);
                    }
                    else if (i_MA_fast.Result.LastValue < i_MA_slow.Result.LastValue)
                    {
                        ExecuteMarketOrder(TradeType.Sell, SymbolName, volumeInUnits, "RSI", stopLoss, takeProfit);
                    }
                }
            }
        }

        private void SetTrailingStop()
        {
            var sellPositions = Positions.FindAll("RSI", SymbolName, TradeType.Sell);

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

            var buyPositions = Positions.FindAll("RSI", SymbolName, TradeType.Buy);

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
