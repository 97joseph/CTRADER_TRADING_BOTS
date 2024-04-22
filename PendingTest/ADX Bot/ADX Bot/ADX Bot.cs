using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class ADXBot : Robot
    {
        [Parameter("Quantity (Lots)", Group = "Volume", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }


        [Parameter("ADX Periods", Group = "ADX", DefaultValue = 20, MinValue = 6, Step = 1, MaxValue = 1000)]
        public int adxPeriods { get; set; }

        [Parameter("Trend MME", Group = "ADX", DefaultValue = 21, MinValue = 6, Step = 1, MaxValue = 1000)]
        public int trendMME { get; set; }

        [Parameter("Take Profit", Group = "Risk", DefaultValue = 150, MinValue = 4, Step = 1, MaxValue = 1000)]
        public int takeProfit { get; set; }

        [Parameter("Stop Loss", Group = "Risk", DefaultValue = 150, MinValue = 4, Step = 1, MaxValue = 1000)]
        public int stopLoss { get; set; }


        private DirectionalMovementSystem adx;
        private double volumeInUnits;
        private ExponentialMovingAverage mme;

        private RelativeStrengthIndex rsi;


        protected override void OnStart()
        {
            volumeInUnits = Symbol.QuantityToVolumeInUnits(Quantity);
            adx = Indicators.DirectionalMovementSystem(adxPeriods);
            mme = Indicators.ExponentialMovingAverage(Bars.ClosePrices, trendMME);
            rsi = Indicators.RelativeStrengthIndex(Bars.ClosePrices, 14);
        }

        protected override void OnBar()
        {
            if (Positions.Count == 0)
            {
                if (adx.DIMinus.Last(3) < adx.DIPlus.Last(3) && adx.DIMinus.Last(1) > adx.DIPlus.Last(1) && adx.DIMinus.LastValue > adx.DIPlus.LastValue)
                {
                    if (mme.Result.LastValue < mme.Result.Last(2))
                        if (rsi.Result.LastValue <= 45)
                            ExecuteMarketOrder(TradeType.Sell, SymbolName, volumeInUnits, "ADX Sell", stopLoss, takeProfit);
                }
                else if (adx.DIPlus.Last(3) < adx.DIMinus.Last(3) && adx.DIPlus.Last(1) > adx.DIMinus.Last(1) && adx.DIPlus.LastValue > adx.DIMinus.LastValue)
                {
                    if (mme.Result.LastValue > mme.Result.Last(2))
                        if (rsi.Result.LastValue > 50)
                            ExecuteMarketOrder(TradeType.Buy, SymbolName, volumeInUnits, "ADX Buy", stopLoss, takeProfit);
                }
            }
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
        }
    }
}
