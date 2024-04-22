using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class OpClExpMov : Robot
    {
        [Parameter("Quantity (Lots)", Group = "Volume", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }

        [Parameter("Take Profit", Group = "TPSL", DefaultValue = 200)]
        public int takeProfit { get; set; }

        [Parameter("EMA", Group = "MME", DefaultValue = 20)]
        public int mmeSlow { get; set; }

        [Parameter("Include Trailing Stop", DefaultValue = false, Group = "Trailing Stop")]
        public bool IncludeTrailingStop { get; set; }

        [Parameter("Trailing Stop Trigger (pips)", DefaultValue = 20, Group = "Trailing Stop")]
        public int TrailingStopTrigger { get; set; }

        [Parameter("Trailing Stop Step (pips)", DefaultValue = 10, Group = "Trailing Stop")]
        public int TrailingStopStep { get; set; }

        private double volumeInUnits, open, close, highValue, lowValue;
        private MovingAverage mmeexp;

        protected override void OnStart()
        {
            volumeInUnits = Symbol.QuantityToVolumeInUnits(Quantity);
            mmeexp = Indicators.MovingAverage(Bars.ClosePrices, mmeSlow, MovingAverageType.Exponential);
        }

        protected override void OnTick()
        {
            open = Bars.Last(1).Open;
            close = Bars.Last(1).Close;
            highValue = Bars.Last(1).High;
            lowValue = Bars.Last(1).Low;
            double stopLoss = Math.Abs((highValue - lowValue) / Symbol.PipSize);

            if (IncludeTrailingStop)
            {
                SetTrailingStop();
            }


            if (Positions.Count.Equals(0))
            {
                if (open <= mmeexp.Result.LastValue && close >= mmeexp.Result.LastValue && Symbol.Bid.Equals(highValue))
                {
                    logTransaction();
                    ExecuteMarketOrder(TradeType.Buy, SymbolName, volumeInUnits, "EMA", stopLoss, takeProfit);
                }
                else if (open >= mmeexp.Result.LastValue && close <= mmeexp.Result.LastValue && Symbol.Ask.Equals(lowValue))
                {
                    logTransaction();
                    ExecuteMarketOrder(TradeType.Sell, SymbolName, volumeInUnits, "EMA", stopLoss, takeProfit);
                }
            }

        }

        private void logTransaction()
        {
            Print("-----Begin-----");
            Print("Open: " + open);
            Print("Close: " + close);
            Print("High: " + highValue);
            Print("Low: " + lowValue);
            Print("Hour: " + TimeInUtc);
            Print("-----End------");
        }

        private void SetTrailingStop()
        {
            var sellPositions = Positions.FindAll("EMA", SymbolName, TradeType.Sell);

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

            var buyPositions = Positions.FindAll("EMA", SymbolName, TradeType.Buy);

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
