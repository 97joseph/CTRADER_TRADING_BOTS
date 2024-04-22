using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;


/*
Name: StopOut_cBot
Description: Bot for checking daily, weekly, monthly and overall PnL when PnL is above defined limits and if PnL is below defined limits, bot will close all pending orders and positions.
Author: GeorgeFreelanceDeveloper
CreateDate: 15.5.2023
UpdateDate: 1.1.2024
Version: 1.2.1
*/

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.FullAccess)]
    public class StopOut_cBot : Robot
    {

        // User defined properties
        [Parameter(DefaultValue = 5)]
        public double RiskPerTradePercentage { get; set; }

        [Parameter(DefaultValue = 2)]
        public int MaxDailyDrawDownMultiplier { get; set; }

        [Parameter(DefaultValue = 3)]
        public int MaxWeeklyDrawDownMultiplier { get; set; }

        [Parameter(DefaultValue = 5)]
        public int MaxMonthlyDrawDownMultiplier { get; set; }

        [Parameter(DefaultValue = 10)]
        public int MaxDrawDownMultiplier { get; set; }

        // Computed properties
        private double RiskPerTrade { get; set; }
        private double MaxDailyDrawDownAmount { get; set; }
        private double MaxWeeklyDrawDownAmount { get; set; }
        private double MaxMonthlyDrawDownAmount { get; set; }
        private double MaxDrawDownAmount { get; set; }

        //Constants
        private readonly bool enableTrace = false;
        private readonly string LogFolderPath = "c:/Logs/cBots/StopOut/";
        private readonly string LogSendersAddress = "senderaddress@email.com";
        private readonly string LogRecipientAddress = "recipientaddress@email.com";


        protected override void OnStart()
        {
            Log("Start StopOut_cBot");
            
            Log("User defined properties:");
            Log(String.Format("RiskPerTradePercentage: {0}", RiskPerTradePercentage));
            Log(String.Format("MaxDailyDrawDownMultiplier: {0}", MaxDailyDrawDownMultiplier));
            Log(String.Format("MaxWeeklyDrawDownMultiplier: {0}", MaxWeeklyDrawDownMultiplier));
            Log(String.Format("MaxMonthlyDrawDownMultiplier: {0}", MaxMonthlyDrawDownMultiplier));
            Log(String.Format("MaxDrawDownMultiplier: {0}", MaxDrawDownMultiplier));
            
            Log("Validation of User defined properties ...");
            List<String> inputErrorMessages = ValidateInputs();
            inputErrorMessages.ForEach(m => Log(m));
            if (inputErrorMessages.Any()){
                Log("App contains input validation errors and will be stop.");
                Stop();
                return;
            }

            Log("Compute properties ...");
            RiskPerTrade = Math.Round(Account.Balance * (RiskPerTradePercentage/100),2);
            MaxDailyDrawDownAmount = MaxDailyDrawDownMultiplier * RiskPerTrade * -1;
            MaxWeeklyDrawDownAmount = MaxWeeklyDrawDownMultiplier * RiskPerTrade * -1;
            MaxMonthlyDrawDownAmount = MaxMonthlyDrawDownMultiplier * RiskPerTrade * -1;
            MaxDrawDownAmount = MaxDrawDownMultiplier * RiskPerTrade * -1;

            Log("Computed properties:");
            Log(String.Format("RiskPerTrade: {0}", RiskPerTrade));
            Log(String.Format("MaxDailyDrawDownAmount: {0}", MaxDailyDrawDownAmount));
            Log(String.Format("MaxWeeklyDrawDownAmount: {0}", MaxWeeklyDrawDownAmount));
            Log(String.Format("MaxMonthlyDrawDownAmount: {0}", MaxMonthlyDrawDownAmount));
            Log(String.Format("MaxDrawDownAmount: {0}", MaxDrawDownAmount));
        }

        protected override void OnBar()
        {
            Log("Start onBar step");
            
            var dailyPnL = ComputeDailyPnL();
            var weeklyPnL = ComputeWeeklyPnL();
            var monthlyPnL = ComputeMonthlyPnL();
            var overallPnL = ComputeOverallPnL();

            if (dailyPnL < MaxDailyDrawDownAmount)
            {
                Log(String.Format("Daily loss limit reached. [dailyPnL: {0}, MaxDailyDrawDownAmount: {1}]", dailyPnL, MaxDailyDrawDownAmount));
                Log("Start close all pending orders and positions");
                CloseAllPositionsAndPendingOrders();
                LocalStorage.SetObject("MaxDailyDrawDownReach",true, LocalStorageScope.Device);
                return;
            }

            if (weeklyPnL < MaxWeeklyDrawDownAmount)
            {
                Log(String.Format("Weekly loss limit reached. [weeklyPnL: {0}, MaxWeeklyDrawDownAmount: {1}]", weeklyPnL, MaxWeeklyDrawDownAmount));
                Log("Start close all pending orders and positions");
                CloseAllPositionsAndPendingOrders();
                LocalStorage.SetObject("MaxWeeklyDrawDownReach",true, LocalStorageScope.Device);
                return;
            }

            if (monthlyPnL < MaxMonthlyDrawDownAmount)
            {
                Log(String.Format("Monthly loss limit reached. [monthlyPnL: {0}, MaxMonthlyDrawDownAmount: {1}]", monthlyPnL, MaxMonthlyDrawDownAmount));
                Log("Start close all pending orders and positions");
                CloseAllPositionsAndPendingOrders();
                LocalStorage.SetObject("MaxMonthlyDrawDownReach",true, LocalStorageScope.Device);
                return;
            }

            if (overallPnL < MaxDrawDownAmount)
            {
                Log(String.Format("Max drawdown limit reached. [overallPnL: {0}, MaxDrawDownAmount: {1}]", overallPnL, MaxDrawDownAmount));
                Log("Start close all pending orders and positions");
                CloseAllPositionsAndPendingOrders();
                LocalStorage.SetObject("MaxDrawDownReach",true, LocalStorageScope.Device);
                return;
            }

            if(enableTrace)
            {
                Log("PnLs:");
                Log(String.Format("Daily PnL: {0}", dailyPnL));
                Log(String.Format("Weekly PnL: {0}", weeklyPnL));
                Log(String.Format("Monthly PnL: {0}", monthlyPnL));    
                Log(String.Format("Overall PnL: {0}", overallPnL));
            }
            
            LocalStorage.SetObject("MaxDailyDrawDownReach",false, LocalStorageScope.Device);
            LocalStorage.SetObject("MaxWeeklyDrawDownReach",false, LocalStorageScope.Device);
            LocalStorage.SetObject("MaxMonthlyDrawDownReach",false, LocalStorageScope.Device);
            LocalStorage.SetObject("MaxDrawDownAmount",false, LocalStorageScope.Device);
            
            Log("Finished onBar step");
        }

        protected override void OnException(Exception exception)
        {
            Log(exception.ToString(), "ERROR");
        }

        private void CloseAllPositionsAndPendingOrders()
        {
            Log("Start close all pending orders and positions");
            Positions.ToList().ForEach(p => ClosePosition(p));
            PendingOrders.ToList().ForEach(o => CancelPendingOrder(o));
            Log("Finished close all positions and pending orders");
        }

        private double ComputeDailyPnL()
        {
            DateTime startOfDay = DateTime.Now.Date;
            var dailyTrades = History.ToList().Where(trade => trade.ClosingTime >= startOfDay);
            return dailyTrades.Sum(trade => trade.NetProfit);
        }

        private double ComputeWeeklyPnL()
        {
            var today = DateTime.Now;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var weeklyTrades = History.ToList().Where(trade => trade.ClosingTime >= startOfWeek);
            return weeklyTrades.Sum(trade => trade.NetProfit);
        }

        private double ComputeMonthlyPnL()
        {
            var today = DateTime.Now;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var monthlyTrades = History.ToList().Where(trade => trade.ClosingTime >= startOfMonth);
            return monthlyTrades.Sum(trade => trade.NetProfit);
        }

        private double ComputeOverallPnL()
        {
            return History.Sum(trade => trade.NetProfit);
        }

        protected override void OnStop()
        {
            Log("Finished StopOut_cBot");
        }
        
        private List<String> ValidateInputs()
        {
            var errMessages = new List<String>();
            
            if (RiskPerTradePercentage <= 0)
            {
                errMessages.Add(String.Format("WARNING: RiskPerTradePercentage must be greater than 0. [RiskPerTradePercentage: {0}]", RiskPerTradePercentage));
            }
            
            if (MaxDailyDrawDownMultiplier <= 0)
            {
                errMessages.Add(String.Format("WARNING: MaxDailyDrawDownMultiplier must be greater than 0. [MaxDailyDrawDownMultiplier: {0}]", MaxDailyDrawDownMultiplier));
            }
            
            if (MaxWeeklyDrawDownMultiplier <= 0)
            {
                 errMessages.Add(String.Format("WARNING: MaxWeeklyDrawDownMultiplier must be greater than 0. [MaxWeeklyDrawDownMultiplier: {0}]", MaxWeeklyDrawDownMultiplier));
            }
            
            if (MaxMonthlyDrawDownMultiplier <= 0)
            {
                errMessages.Add(String.Format("WARNING: MaxMonthlyDrawDownMultiplier must be greater than 0. [MaxMonthlyDrawDownMultiplier: {0}]", MaxMonthlyDrawDownMultiplier));
            }

            if (MaxDrawDownMultiplier <= 0)
            {
                errMessages.Add(String.Format("WARNING: MaxDrawDownMultiplier must be greater than 0. [MaxDrawDownMultiplier: {0}]", MaxDrawDownMultiplier));
            }
            
            return errMessages;
        }

        private void Log(string message, string level = "INFO")
        {
            string logMessage = string.Format("[{0}] {1}: {2}", 
                    DateTime.Now, 
                    level,
                    message);

            String dy = DateTime.Now.Day.ToString();
            String mn = DateTime.Now.Month.ToString();
            String yy = DateTime.Now.Year.ToString();
            string logFileName = String.Format("StopOut_{0}{1}{2}.log", yy, mn, dy);
            string logPath = LogFolderPath + logFileName;
            if(!Directory.Exists(LogFolderPath))
            {
                Directory.CreateDirectory(LogFolderPath);
            }
            
            Print(logMessage); // Log to terminal
            File.AppendAllText(logPath,logMessage + Environment.NewLine); // Log to log file

            if (level.SequenceEqual("ERROR")){
                Notifications.SendEmail(LogSendersAddress, LogRecipientAddress, "Error in StopOut cBot", logMessage);
            }
        }
        
    }
}
