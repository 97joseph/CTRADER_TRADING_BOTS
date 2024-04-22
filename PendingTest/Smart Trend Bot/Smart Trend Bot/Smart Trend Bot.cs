using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SmartTrendBot : Robot
    {

        [Parameter("Quantity (Lots)", Group = "Volume", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }

        [Parameter("Source", Group = "RSI")]
        public DataSeries Source { get; set; }

        [Parameter("Periods", Group = "RSI", DefaultValue = 19)]
        public int Periods { get; set; }

        [Parameter("MME Medium", Group = "RSI", DefaultValue = 21)]
        public int p_mmeMedium { get; set; }

        [Parameter("Inclinacao Entrada", Group = "RSI", DefaultValue = 0.01, Step = 0.01)]
        public double inclinacao_ent { get; set; }

        [Parameter("Include Trailing Stop", Group = "Trailing Stop", DefaultValue = false)]
        public bool IncludeTrailingStop { get; set; }

        [Parameter("Trailing Stop Trigger (pips)", Group = "Trailing Stop", DefaultValue = 5)]
        public int TrailingStopTrigger { get; set; }

        [Parameter("Trailing Stop Step (pips)", Group = "Trailing Stop", DefaultValue = 1)]
        public int TrailingStopStep { get; set; }

        [Parameter("Stop Loss", Group = "Risk", DefaultValue = 1)]
        public int stopLoss { get; set; }

        [Parameter("Take Profit", Group = "Risk", DefaultValue = 1)]
        public int takeProfit { get; set; }

        private double volumeInUnits;
        private double inclinacaoMme;
        private MovingAverage mmeMedium;
        private RelativeStrengthIndex rsi;

        protected override void OnStart()
        {
            volumeInUnits = Symbol.QuantityToVolumeInUnits(Quantity);
            mmeMedium = Indicators.MovingAverage(Bars.ClosePrices, p_mmeMedium, MovingAverageType.Exponential);
            rsi = Indicators.RelativeStrengthIndex(Source, Periods);
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
            inclinacaoMme = (((mmeMedium.Result.LastValue * 100) / mmeMedium.Result.Last(2)) - 100) * 360;

            foreach (Position position in Positions)
            {
                if (position.Label.Equals("Trend Buy") && Bars.LastBar.Close < mmeMedium.Result.LastValue)
                {
                    ClosePosition(position);
                }
                else if (position.Label.Equals("Trend Sell") && Bars.LastBar.Close > mmeMedium.Result.LastValue)
                {
                    ClosePosition(position);
                }
            }


            if (Positions.Count() == 0)
            {
                if (Bars.LastBar.Open >= Bars.Last(1).Close)
                {
                    if (inclinacaoMme >= inclinacao_ent)
                    {
                        ExecuteMarketOrder(TradeType.Buy, SymbolName, volumeInUnits, "Trend Buy", stopLoss, takeProfit);
                    }
                }
                else if (Bars.LastBar.Open < Bars.Last(1).Close)
                {
                    if (inclinacaoMme <= (inclinacao_ent * -1))
                    {
                        ExecuteMarketOrder(TradeType.Sell, SymbolName, volumeInUnits, "Trend Sell", stopLoss, takeProfit);
                    }
                }
            }
        }

        private void SetTrailingStop()
        {
            var sellPositions = Positions.FindAll("Trend", SymbolName, TradeType.Sell);

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

            var buyPositions = Positions.FindAll("Trend", SymbolName, TradeType.Buy);

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
