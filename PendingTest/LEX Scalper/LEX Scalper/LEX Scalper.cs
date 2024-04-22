using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

/*
+---------------------------------------------------------------------------------------------------------------------------------+
| I recommend using this Cbot on Renko charts with 10 pips brick size for the following pairs only.	|
| EURUSD, GBPUSD, AUDUSD, USDJPY, EURJPY, GBPJPY, USDCHF & USDCAD                             		|
| LEXtrend Cbot has a knack of catching the trend and letting it run and cutting short its loser’s.	|
+---------------------------------------------------------------------------------------------------------------------------------+
*/

namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class LEXtrend : Robot
    {
        #region User defined parameters
        public enum Instance
        {
            Fiber,
            Cable,
            Aussei,
            Ninja,
            Swissie,
            Loonie,
            Kiwi,
            Yuppy,
            Guppy,
            Chunnel
        }

        [Parameter("Instance Name", DefaultValue = "Fiber")]
        public Instance InstanceName { get; set; }

        [Parameter("Source")]
        public DataSeries MAsource { get; set; }
        [Parameter("BaseLine", DefaultValue = 200, MinValue = 1)]
        public int MAperiod { get; set; }
        [Parameter("Tide", DefaultValue = 20, MinValue = 1)]
        public int LongCycle { get; set; }
        [Parameter("Wave", DefaultValue = 10, MinValue = 1)]
        public int ShortCycle { get; set; }
        [Parameter("Current", DefaultValue = 5, MinValue = 1)]
        public int MACDPeriod { get; set; }


        [Parameter("Percentage Risk Model?", Group = "Money Management", DefaultValue = true)]
        public bool volPercentBool { get; set; }

        [Parameter("Risk %", Group = "Money Management", DefaultValue = 3, MinValue = 1, MaxValue = 5)]
        public int volPercent { get; set; }

        [Parameter("Volume Quantity", Group = "Money Management", DefaultValue = 5000, MinValue = 1000, Step = 1000)]
        public int volQty { get; set; }

        [Parameter("StopLoss Pips", Group = "Protection", DefaultValue = 30.0, Step = 1.0)]
        public double StopLoss { get; set; }

        [Parameter("TakeProfit Pips", Group = "Protection", DefaultValue = 70.0, Step = 1.0)]
        public double TakeProfit { get; set; }

        [Parameter("BreakEvenTrigger Pips", Group = "Protection", DefaultValue = 20, MinValue = 1)]
        public double TriggerPips { get; set; }

        [Parameter("Locked in Profit", Group = "Protection", DefaultValue = 5.0, MinValue = 1.0)]
        public double AddPips { get; set; }

        [Parameter("Allowable Slippage", Group = "Filter", DefaultValue = 2.0, MinValue = 0.5, Step = 0.1)]
        public double Slippage { get; set; }

        [Parameter("Max Allowable Spread", Group = "Filter", DefaultValue = 2.0, MinValue = 0.1, MaxValue = 100.0)]
        public double MaxSpread { get; set; }

        [Parameter("Calculate OnBar?", DefaultValue = true)]
        public bool CalculateOnBar { get; set; }
        #endregion

        #region Indicator declarations
        private ExponentialMovingAverage MA2 { get; set; }
        private ExponentialMovingAverage MA1 { get; set; }
        private MacdCrossOver _MACD { get; set; }

        private double SPREAD;
        private int volume;
        private string Comment;
        #endregion



        #region Calculate Volume
        private int CalculateVolume(double stopLossPips)
        {
            int result;
            switch (volPercentBool)
            {
                case true:
                    double costPerPip = (double)((int)(Symbol.PipValue * 10000000)) / 100;
                    double posSizeForRisk = (Account.Equity * volPercent / 100) / (stopLossPips * costPerPip);
                    double posSizeToVol = (Math.Round(posSizeForRisk, 2) * 100000);
                    Print("costperppip = {0}, posSizeFoprRisk = {1}, posSizeLotsToVol = {2}", costPerPip, posSizeForRisk, posSizeToVol);
                    result = (int)Symbol.NormalizeVolumeInUnits(posSizeToVol, RoundingMode.ToNearest);
                    result = result > 150000 ? 150000 : result;
                    Print("{0}% of Account Balance used for Volume! Volume equals {1}", volPercent, result);
                    break;
                default:
                    result = volQty;
                    Print("Volume Quantity Used! Volume equals {0}", result);
                    break;
            }
            return result;
        }
        #endregion

        #region Standard event handlers
        /// This is called when the robot first starts, it is only called once.
        protected override void OnStart()
        {
            MA2 = Indicators.ExponentialMovingAverage(MAsource, MAperiod);
            MA1 = Indicators.ExponentialMovingAverage(MAsource, (MAperiod / 2));
            _MACD = Indicators.MacdCrossOver(LongCycle, ShortCycle, MACDPeriod);
            volume = CalculateVolume(StopLoss);
            SPREAD = (double)((int)Math.Round(Symbol.Spread / Symbol.PipSize, 5));
            Comment = "Lex Scalper 619";
        }


        /// This event handler is called every tick or every time the price changes for the symbol.
        protected override void OnTick()
        {
            if (CalculateOnBar)
            {
                return;
            }
            ManagePositions();
        }

        /// a special event handler that is called each time a new bar is drawn on chart.
        /// if you want your robot to act only when the previous bar is closed, this standard handler is where you put your main trading code.
        protected override void OnBar()
        {
            if (!CalculateOnBar)
            {
                return;
            }
            ManagePositions();
            var positions = Positions.FindAll(InstanceName.ToString(), SymbolName);
            foreach (var position in positions)

                if (position.Pips < TriggerPips)
                {
                    return;
                }
            BreakEvenIfNeeded();




        }

        /// a handler that is called on stopping the cBot.
        protected override void OnStop()
        {
// unused
        }

        /// a special Robot class member that handles situations with errors.
        protected override void OnError(Error error)
        {
            Print("Error Code {0}", error.Code);
        }
        #endregion

        #region Position management

        private void ManagePositions()
        {
            /// if there is no buy position open, open one and close any sell position that is open
            if (!IsPositionOpenByType(TradeType.Buy))
            {

                if (((MA1.Result.Last(1) > MA2.Result.Last(1)) || (Bars.OpenPrices.Last(1) > MA1.Result.Last(1)) || (Bars.OpenPrices.Last(1) > MA2.Result.Last(1))) && ((Bars.ClosePrices.Last(1) > Bars.OpenPrices.Last(1) && _MACD.MACD.Last(2) < _MACD.Signal.Last(2) && _MACD.MACD.Last(1) > _MACD.Signal.Last(1)) || (_MACD.MACD.Last(1) > _MACD.Signal.Last(1) && Bars.ClosePrices.Last(2) < Bars.OpenPrices.Last(2) && Bars.ClosePrices.Last(1) > Bars.OpenPrices.Last(1))))
                {
                    OpenPosition(TradeType.Buy);
                    ClosePosition(TradeType.Sell);
                }

                if (Server.Time.DayOfWeek == DayOfWeek.Friday && Server.Time.Hour >= 20 && Server.Time.Minute >= 45)
                {
                    ClosePosition(TradeType.Sell);
                }
            }

            /// if there is no sell position open, open one and close any buy position that is open
            if (!IsPositionOpenByType(TradeType.Sell))
            {
                if (((MA1.Result.Last(1) < MA2.Result.Last(1)) || (Bars.OpenPrices.Last(1) < MA1.Result.Last(1)) || (Bars.OpenPrices.Last(1) < MA2.Result.Last(1))) && ((Bars.ClosePrices.Last(1) < Bars.OpenPrices.Last(1) && _MACD.MACD.Last(2) > _MACD.Signal.Last(2) && _MACD.MACD.Last(1) < _MACD.Signal.Last(1)) || (_MACD.MACD.Last(1) < _MACD.Signal.Last(1) && Bars.ClosePrices.Last(2) > Bars.OpenPrices.Last(2) && Bars.ClosePrices.Last(1) < Bars.OpenPrices.Last(1))))
                {
                    OpenPosition(TradeType.Sell);
                    ClosePosition(TradeType.Buy);
                }

                if (Server.Time.DayOfWeek == DayOfWeek.Friday && Server.Time.Hour >= 20 && Server.Time.Minute >= 45)
                {
                    ClosePosition(TradeType.Buy);
                }
            }
        }

        /// Call custom class method to move StopLoss to BreakEven
        private void BreakEvenIfNeeded()
        {
            var positions = Positions.FindAll(InstanceName.ToString(), SymbolName);
            foreach (var position in positions)
            {
                var desiredNetProfitInDepositAsset = AddPips * Symbol.PipValue * position.VolumeInUnits;
                var desiredGrossProfitInDepositAsset = desiredNetProfitInDepositAsset - position.Commissions * 2 - position.Swap;
                var quoteToDepositRate = Symbol.PipValue / Symbol.PipSize;
                var priceDifference = desiredGrossProfitInDepositAsset / (position.VolumeInUnits * quoteToDepositRate);
                var priceAdjustment = GetPriceAdjustmentByTradeType(position.TradeType, priceDifference);
                var breakEvenLevel = position.EntryPrice + priceAdjustment;
                var roundedBreakEvenLevel = RoundPrice(breakEvenLevel, position.TradeType);

                var stopposition = position.TradeType == TradeType.Buy ? position.EntryPrice - position.StopLoss : position.StopLoss - position.EntryPrice;
                if (stopposition > 0)
                {
                    ModifyPosition(position, roundedBreakEvenLevel, position.TakeProfit);
                    Print("{0}: Stoploss Moved to Breakeven @ {1} {2}:{3}:{4}", SymbolName, Server.Time.DayOfWeek, Server.Time.Hour, Server.Time.Minute, Server.Time.Second);
                }


            }
        }

        /// Call custom class method to send a market order || open a new position

        private void OpenPosition(TradeType type)
        {
            if (Server.Time.DayOfWeek <= DayOfWeek.Friday && Server.Time.Hour < 19)
            {

                if (SPREAD < MaxSpread)
                {
                    ExecuteMarketRangeOrder(type, this.Symbol.Name, volume, Slippage, Symbol.Bid, InstanceName.ToString(), StopLoss, TakeProfit, Comment);
                }
                Print("Open {0} position: {1} Spread:{2}", type, SymbolName, SPREAD);
            }
        }

        /// Standard event handler that triggers upon position closing.
        private void ClosePosition(TradeType type)
        {
            var p = Positions.Find(InstanceName.ToString(), SymbolName, type);

            if (p != null)
            {
                ClosePosition(p);
            }
            Print("Close {0} position: {1} Spread:{2}", type, SymbolName, SPREAD);
        }

        /// Check for opened position
        private bool IsPositionOpenByType(TradeType type)
        {
            var p = Positions.FindAll(InstanceName.ToString(), SymbolName, type);

            if (p.Count() >= 1)
            {
                return true;
            }
            return false;
        }

        private double RoundPrice(double price, TradeType tradeType)
        {
            var multiplier = Math.Pow(10, Symbol.Digits);
            if (tradeType == TradeType.Buy)
                return Math.Ceiling(price * multiplier) / multiplier;

            return Math.Floor(price * multiplier) / multiplier;
        }

        private static double GetPriceAdjustmentByTradeType(TradeType tradeType, double priceDifference)
        {
            if (tradeType == TradeType.Buy)
                return priceDifference;

            return -priceDifference;
        }

        #endregion
    }
}


