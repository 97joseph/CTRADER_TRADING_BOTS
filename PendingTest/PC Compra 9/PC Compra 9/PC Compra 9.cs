using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class PCCompra9 : Robot
    {
        [Parameter("Quantity (Lots)", Group = "Volume", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }

        [Parameter("Include Trailing Stop", DefaultValue = false)]
        public bool IncludeTrailingStop { get; set; }

        [Parameter("Trailing Stop Trigger (pips)", DefaultValue = 20)]
        public int TrailingStopTrigger { get; set; }

        [Parameter("Trailing Stop Step (pips)", DefaultValue = 10)]
        public int TrailingStopStep { get; set; }

        private double volumeInUnits;
        private MovingAverage mme9;
        private MovingAverage mme21;

        protected override void OnStart()
        {
            volumeInUnits = Symbol.QuantityToVolumeInUnits(Quantity);
            mme9 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 9);
            mme21 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 21);
        }

        protected override void OnTick()
        {
            if (IncludeTrailingStop)
            {
                SetTrailingStop();
            }
        }

        protected override void OnBar()
        {
            if (Positions.Count > 0)
            {
                if (Bars.Last(1).Close < mme9.Result.LastValue && Bars.LastBar.Close < Bars.Last(1).Close)
                {
                    foreach (var position in Positions)
                    {
                        ClosePosition(position);
                    }
                }
            }

            if (mme9.Result.LastValue > mme21.Result.LastValue)
            {
                if (mme9.Result.LastValue > mme9.Result.Last(1) && mme21.Result.LastValue > mme21.Result.Last(1) && mme9.Result.Last(2) > mme9.Result.Last(3) && mme21.Result.Last(2) > mme21.Result.Last(3) && mme9.Result.Last(2) > mme9.Result.Last(4) && mme21.Result.Last(2) > mme21.Result.Last(5))
                {
                    if (Bars.LastBar.Close < Bars.Last(1).Close)
                    {
                        PlaceLimitOrder(TradeType.Buy, SymbolName, volumeInUnits, Bars.HighPrices.Last(5), "mme9");
                    }
                }
            }
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
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
