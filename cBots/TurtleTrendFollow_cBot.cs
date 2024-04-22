using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

/*
Name: TurtleTrendFollow_cBot
Description: The Turtle trend following trading strategy was developed by Richard Dennis and William Eckhardt in the early 1980s. 
The story goes that Richard Dennis, a successful commodity trader, believed that trading could be taught to anyone, 
and he decided to conduct an experiment to prove his theory. He recruited a group of people, known as the "Turtles," 
and taught them his trading system.
Author: GeorgeFreelanceDeveloper
CreateDate: 3.1.2023
UpdateDate: 28.1.2023, UpdatedBy: LucyFreelanceDeveloper
Version: 0.0.6
*/

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.FullAccess)]
    public class TurtleTrendFollow_cBot : Robot
    {
        // User defined properties
        [Parameter(DefaultValue = 20)]
        public int CountPeriodForEntry1 { get; set; }
        
        [Parameter(DefaultValue = 10)]
        public int CountPeriodForStop1 { get; set; }
        
        [Parameter(DefaultValue = true)]
        public bool EnableL1 { get; set; }
        
        [Parameter(DefaultValue = 55)]
        public int CountPeriodForEntry2 { get; set; }
        
        [Parameter(DefaultValue = 20)]
        public int CountPeriodForStop2 { get; set; }
        
        [Parameter(DefaultValue = false)]
        public bool EnableL2 { get; set; }
        
        [Parameter(DefaultValue = 2.5)]
        public double RiskPercentage { get; set; }
        
        [Parameter(DefaultValue = true)]
        public bool LongOnly { get; set; }

       
        // Constants
        private readonly string LogFolderPath = "c:/Logs/cBots/TurtleTrendFollow/";
        private readonly string LogSendersAddress = "senderaddress@email.com";
        private readonly string LogRecipientAddress = "recipientaddress@email.com";
        
        // Level 1: Swing
        // Level 2: Position
        public enum Level { L1, L2 }
        
        protected override void OnStart()
        {
            Log("Start TurtleTrendFollow_cBot");

            Log("User defined properties:");
            Log($"CountPeriodForEntry1: {CountPeriodForEntry1}");
            Log($"CountPeriodForStop1: {CountPeriodForStop1}");
            Log($"EnableL1: {EnableL1}");
            Log($"CountPeriodForEntry2: {CountPeriodForEntry2}");
            Log($"CountPeriodForStop2: {CountPeriodForStop2}");
            Log($"EnableL2: {EnableL2}");
            Log($"RiskPercentage: {RiskPercentage}");

            Log("Validation of User defined properties ...");
            List<String> inputErrorMessages = ValidateInputs();
            inputErrorMessages.ForEach(m => Log(m));
            if (inputErrorMessages.Any()){
                Log("App contains input validation errors and will be stop.");
                Stop();
                return;
            }
            
            Log("Register listeners");
            Positions.Opened += PositionsOnOpened;
            Positions.Closed += PositionsOnClosed;
        }
        
        protected override void OnBar()
        {
            Log("Start OnBar");
            if(EnableL1)
            {
                ExecuteStrategyPerLevel(Level.L1);
            }
            if(EnableL2)
            {
                ExecuteStrategyPerLevel(Level.L2);
            }
            Log("Finished OnBar"); 
        }

        protected override void OnStop()
        {
            Log("Finished TurtleTrendFollow_cBot");
        }

        protected override void OnException(Exception exception)
        {
            Log(exception.ToString(), "ERROR");
        }
        
        private void ExecuteStrategyPerLevel(Level level)
        {
            Log($"ExecuteStrategyPerLevel: {level}");
            int countPeriodForEntry = level == Level.L1 ? CountPeriodForEntry1 : CountPeriodForEntry2;
            int countPeriodForStop = level == Level.L1 ? CountPeriodForStop1 : CountPeriodForStop2;
            string label = $"TurtleTrendFollow_cBot-{level}";
            
            double actualPrice = MarketData.GetTicks().Last().Ask;
            //double actualPrice = MarketData.GetBars(TimeFrame.Minute).Last().Close; // For backtest on m1 bars
            
            var barsForEntry = MarketData.GetBars(TimeFrame.Daily).SkipLast(1).ToList().TakeLast(countPeriodForEntry);
            double maxPriceLastDaysForEntry =  barsForEntry.Max(b=>b.High);
            double minPriceLastDaysForEntry = barsForEntry.Min(b=>b.Low);
            
            var barsForStop = MarketData.GetBars(TimeFrame.Daily).SkipLast(1).ToList().TakeLast(countPeriodForStop);
            double maxPriceLastDaysForStop = barsForStop.Max(b=>b.High);
            double minPriceLastDaysForStop = barsForStop.Min(b=>b.Low);
            
            Position position = Positions.Find(label);
            
            if(position == null)
            {
                if(actualPrice > maxPriceLastDaysForEntry)
                {
                    Log($"Price reach breakout zone for long (actualPrice > maxPriceLastDaysForEntry), bot will execute market long order. [actualPrice: {actualPrice}, maxPriceLastDaysForEntry: {maxPriceLastDaysForEntry}]");
                    double stopLossPips = (Math.Abs(maxPriceLastDaysForEntry - minPriceLastDaysForStop)/Symbol.PipSize);
                    double amount = ComputeTradeAmount(maxPriceLastDaysForEntry, minPriceLastDaysForStop);
                    ExecuteMarketOrder(TradeType.Buy, Symbol.Name, amount, label, stopLossPips, null);
                }
                else if (actualPrice < minPriceLastDaysForEntry && !LongOnly)
                {
                    Log($"Price reach breakout zone for short (actualPrice < minPriceLastDaysForEntry), bot will execute market short order. [actualPrice: {actualPrice}, minPriceLastDaysForEntry: {minPriceLastDaysForEntry}]");
                    double stopLossPips = (Math.Abs(minPriceLastDaysForEntry - maxPriceLastDaysForStop)/Symbol.PipSize);
                    double amount = ComputeTradeAmount(minPriceLastDaysForEntry, maxPriceLastDaysForStop);
                    ExecuteMarketOrder(TradeType.Sell, Symbol.Name, amount, label, stopLossPips, null);
                }
            }
            else
            {
                if(position.TradeType == TradeType.Buy && position.StopLoss != minPriceLastDaysForStop)
                {
                    Log($"Long position is not at minPriceLastDaysForStop (stopLossShort != minPriceLastDaysForStop), position will be modified [actualPriceOfStopLoss: {position.StopLoss}, updatedPriceOfStopLoss: {minPriceLastDaysForStop}].");
                    ModifyPosition(position, minPriceLastDaysForStop, position.TakeProfit);
                }
                if(position.TradeType == TradeType.Sell && position.StopLoss != maxPriceLastDaysForStop)
                {
                    Log($"Short position is not at maxPriceLastDaysForStop (stopLossShort != maxPriceLastDaysForStop), position will be modified [actualPriceOfStopLoss: {position.StopLoss}, updatedPriceOfStopLoss: {maxPriceLastDaysForStop}].");
                    ModifyPosition(position, maxPriceLastDaysForStop, position.TakeProfit);
                }
            }            
        }

        private void PositionsOnOpened(PositionOpenedEventArgs args)
        {
            var pos = args.Position;
            if (pos.Symbol.ToString().SequenceEqual(Symbol.Name)){
                 Log("Order was converted to position.");
                 Log($"Position id {pos.Id} opened at {pos.EntryPrice}");
            }

        }
        
        private void PositionsOnClosed(PositionClosedEventArgs args)
        {
            var pos = args.Position;
            if(pos.Symbol.ToString().SequenceEqual(Symbol.Name)){
                string profitLossMessage = pos.GrossProfit >= 0 ? "profit" : "loss";   
                Log($"Position id {pos.Id} closed with {pos.GrossProfit} {profitLossMessage}");
            }
        }
        
        private double ComputeTradeAmount(double entryPrice, double stopPrice)
        {
            double riskPerTrade = (RiskPercentage / 100) * Account.Balance;
            double move = entryPrice - stopPrice;
            double amountRaw = riskPerTrade / ((Math.Abs(move) / Symbol.PipSize) * Symbol.PipValue);
            double amount = ((int)(amountRaw / Symbol.VolumeInUnitsStep)) * Symbol.VolumeInUnitsStep;
            return amount;
        }

        private List<String> ValidateInputs()
        {
            var errMessages = new List<String>();
            
            if (CountPeriodForEntry1 <= 0)
            {
                errMessages.Add($"WARNING: CountPeriodForEntry1 must be greater than 0. [CountPeriodForEntry1: {CountPeriodForEntry1}]");
            }
            
            if (CountPeriodForStop1 <= 0)
            {
                errMessages.Add($"WARNING: CountPeriodForStop1 must be greater than 0. [CountPeriodForStop1: {CountPeriodForStop1}]");
            }
            
            if (CountPeriodForEntry2 <= 0)
            {
                errMessages.Add($"WARNING: CountPeriodForEntry2 must be greater than 0. [CountPeriodForEntry2: {CountPeriodForEntry2}]");
            }
            
            if (CountPeriodForStop2 <= 0)
            {
                errMessages.Add($"WARNING: CountPeriodForStop2 must be greater than 0. [CountPeriodForStop2: {CountPeriodForStop2}]");
            }
            
            if (RiskPercentage <= 0)
            {
                 errMessages.Add($"WARNING: RiskPercentage must be greater than 0. [RiskPercentage: {RiskPercentage}]");
            }
            
            return errMessages;
        }
        
        private void Log(String message, String level = "INFO")
        {
            string logMessage = $"[{DateTime.Now} - {Symbol.ToString()}] {level}: {message}";

            String dy = DateTime.Now.Day.ToString();
            String mn = DateTime.Now.Month.ToString();
            String yy = DateTime.Now.Year.ToString();

            string logFileName = $"TurtleTrendFollow_{Symbol.ToString()}_{yy}{mn}{dy}.log";
            string logPath = LogFolderPath + logFileName;

            if(!Directory.Exists(LogFolderPath))
            {
                Directory.CreateDirectory(LogFolderPath);
            }
            
            Print(logMessage); // Log to terminal
            File.AppendAllText(logPath,logMessage + Environment.NewLine); // Log to log file
            
            if (level.SequenceEqual("ERROR")){
                Notifications.SendEmail(LogSendersAddress, LogRecipientAddress, "Error in TurtleTrendFollow cBot", logMessage);
            }
        }
    }
}