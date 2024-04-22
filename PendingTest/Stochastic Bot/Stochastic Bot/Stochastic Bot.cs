using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class StochasticBot : Robot
    {
        [Parameter("Quantity (Lots)", Group = "Volume", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }

        [Parameter("Take Profit", Group = "Risk", DefaultValue = 150, MinValue = 4, Step = 1, MaxValue = 1000)]
        public int takeProfit { get; set; }

        [Parameter("Stop Loss", Group = "Risk", DefaultValue = 150, MinValue = 4, Step = 1, MaxValue = 1000)]
        public int stopLoss { get; set; }

        [Parameter("Percent K", Group = "Stochastic", DefaultValue = 150, MinValue = 4, Step = 1, MaxValue = 1000)]
        public int percentK { get; set; }

        [Parameter("Percent K Slow", Group = "Stochastic", DefaultValue = 150, MinValue = 4, Step = 1, MaxValue = 1000)]
        public int percentKSlow { get; set; }

        [Parameter("Percent D", Group = "Stochastic", DefaultValue = 150, MinValue = 4, Step = 1, MaxValue = 1000)]
        public int percentD { get; set; }


        private StochasticOscillator stochasticOscillator;
        private double volumeInUnits;



        protected override void OnStart()
        {
            volumeInUnits = Symbol.QuantityToVolumeInUnits(Quantity);
            stochasticOscillator = Indicators.StochasticOscillator(percentK, percentKSlow, percentD, MovingAverageType.Exponential);
        }

        protected override void OnBar()
        {
            if (stochasticOscillator.PercentK.Last(2) < stochasticOscillator.PercentD.Last(2))
            {
                if (stochasticOscillator.PercentK.LastValue > stochasticOscillator.PercentD.LastValue)
                {
                    Print(stochasticOscillator.PercentK.LastValue);
                    ExecuteMarketOrder(TradeType.Buy, SymbolName, volumeInUnits, "Stochastic", stopLoss, takeProfit);
                }
            }
            if (stochasticOscillator.PercentK.Last(2) > stochasticOscillator.PercentD.Last(2))
            {
                if (stochasticOscillator.PercentK.LastValue < stochasticOscillator.PercentD.LastValue)
                {
                    ExecuteMarketOrder(TradeType.Sell, SymbolName, volumeInUnits, "Stochastic", stopLoss, takeProfit);
                }
            }
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
        }
    }
}
