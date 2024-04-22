using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class RangeOrder : Robot
    {
        [Parameter("Quantity (Lots)", Group = "Volume", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }

        [Parameter("EMA Value", DefaultValue = 100)]
        public int emaValue { get; set; }

        [Parameter("Include Trailing Stop", DefaultValue = false)]
        public bool IncludeTrailingStop { get; set; }

        [Parameter("Trailing Stop Trigger (pips)", DefaultValue = 20)]
        public int TrailingStopTrigger { get; set; }

        [Parameter("Trailing Stop Step (pips)", DefaultValue = 10)]
        public int TrailingStopStep { get; set; }

        [Parameter("Include Manual TP & SL", Group = "TP", DefaultValue = false)]
        public bool IncludeTPSL { get; set; }

        [Parameter("Take Profit", Group = "TP", DefaultValue = 1)]
        public int takeProfit { get; set; }

        [Parameter("Stop Loss", Group = "TP", DefaultValue = 1)]
        public int stopLoss { get; set; }

        private MovingAverage ema_slow;
        private double volumeInUnits;

        protected override void OnStart()
        {
            volumeInUnits = Symbol.QuantityToVolumeInUnits(Quantity);
            ema_slow = Indicators.MovingAverage(Bars.ClosePrices, emaValue, MovingAverageType.Exponential);
        }

        protected override void OnBar()
        {
            if (Bars.LowPrices.Last(1) <= ema_slow.Result.Last(1) && Bars.HighPrices.Last(1) > ema_slow.Result.Last(1) && Bars.LastBar.Close > ema_slow.Result.Last(1))
            {
                if (!IncludeTPSL)
                    PlaceLimitOrder(TradeType.Buy, SymbolName, volumeInUnits, Bars.HighPrices.Last(1), "EMA");
                else
                    PlaceLimitOrder(TradeType.Buy, SymbolName, volumeInUnits, Bars.HighPrices.Last(1), "EMA", stopLoss, takeProfit);
            }
            else if (Bars.HighPrices.Last(1) >= ema_slow.Result.Last(1) && Bars.LowPrices.Last(1) < ema_slow.Result.Last(1) && Bars.LastBar.Close < ema_slow.Result.Last(1))
            {
                if (!IncludeTPSL)
                    PlaceLimitOrder(TradeType.Sell, SymbolName, volumeInUnits, Bars.LowPrices.Last(1), "EMA");
                else
                    PlaceLimitOrder(TradeType.Sell, SymbolName, volumeInUnits, Bars.LowPrices.Last(1), "EMA", stopLoss, takeProfit);
            }
        }

        protected override void OnTick()
        {
            if (Positions.Count > 0)
            {
                foreach (var position in Positions)
                {
                    if (position.TradeType.Equals(TradeType.Buy) && (!IncludeTrailingStop && !IncludeTPSL))
                    {
                        if (Symbol.Ask <= Bars.LowPrices.Last(1))
                        {
                            ClosePosition(position);
                        }
                    }
                    else if (position.TradeType.Equals(TradeType.Sell) && (!IncludeTrailingStop && !IncludeTPSL))
                    {
                        if (Symbol.Bid >= Bars.HighPrices.Last(1))
                        {
                            ClosePosition(position);
                        }
                    }
                }
            }
            if (IncludeTrailingStop)
            {
                SetTrailingStop();
            }
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
