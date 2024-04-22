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
using System.Text.RegularExpressions;

/*
Name: LevelTrader_cBot
Description: An automated bot for controlling trades. The bot helps reduce risk by adjusting positions when prices move favorably, cancel pending order when trade early reaction and eliminates open positions during sudden price spikes.
Author: GeorgeFreelanceDeveloper
Updated by: LucyFreelanceDeveloper
CreateDate: 1.8.2023
UpdateDate: 30.12.2023
Version: 1.2.4
*/
namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.FullAccess)]
    public class LevelTrader_cBot : Robot
    {
        
        // User defined properties
        [Parameter(DefaultValue = 0)]
        public double EntryPrice {get; set;}
        
        [Parameter(DefaultValue = 0)]
        public double StopLossPrice {get; set;}
        
        [Parameter("Type", DefaultValue = TradeDirectionType.LONG)]
        public TradeDirectionType Direction {get; set;}
        
        [Parameter(DefaultValue = 1.5)]
        public double RiskRevardRatio {get; set;}
        
        [Parameter(DefaultValue = 5)]
        public double RiskPercentage {get; set;}
        
        [Parameter(DefaultValue = false)]
        public Boolean IsEnableTrailingStop {get; set;}

        [Parameter(DefaultValue = 0.5)]
        public double TrailingStopLossLevel1Percentage {get; set;}

        [Parameter(DefaultValue = 0.7)]
        public double TrailingStopLossLevel2Percentage {get; set;}
        
        [Parameter(DefaultValue = 60)]
        public int PlaceTradeDelayInMinutes {get; set;}
        
        [Parameter(DefaultValue = 1)]
        public int MaxAllowedOpenTrades { get; set; }
        
        // Example 2023/01/15
        [Parameter(DefaultValue = "")]
        public string ExpirationDateString {get; set;}
        
        
        // Constants
        private Regex ExpirationDatePattern = new Regex(@"^\d{4}/\d{2}/\d{2}$");
        private readonly double PercentageBeforeEntry = 0.33;
        private readonly bool enableTrace = false;
        private readonly string LogFolderPath = "c:/Logs/cBots/LevelTrader/";
        private readonly string LogSendersAddress = "senderaddress@email.com";
        private readonly string LogRecipientAddress = "recipientaddress@email.com";
        
        // Ids
        private int PendingOrderId {get; set;}
        private String TradeId {get; set;}
        
        // Computed properties
        private double Move {get; set;}
        private double Amount {get; set;}
        private double BeforeEntryPrice {get; set;}
        private double TakeProfitPriceOneToOne {get; set;} //Risk Reward Ratio 1:1
        private double RiskPerTrade {get; set;}
        private double TrailingStopLossLevel1Price {get; set;}
        private double TrailingStopLossLevel2Price {get; set;}
        private double StopLossPips {get; set;}
        private double TakeProfitPips {get; set;}
        private double StopLossLevel1Price {get; set;}
        private double StopLossLevel2Price {get; set; }
        
        // Timestamps
        private DateTime? ExpirationDate {get; set;}
        private DateTime ReachProfitTargetTimestamp {get; set;}
        private DateTime ReachBeforeEntryPriceTimestamp {get; set;}
        
        // States
        private bool ReachProfitTargetOneToOne {get; set;}
        private bool ReachBeforeEntryPrice {get; set;}
        private bool ReachTrailingStopLossLevel1Price {get; set;}
        private bool ReachTrailingStopLossLevel2Price {get; set;}
        private bool IsActivePosition {get; set;}
              
        public enum TradeDirectionType
        {
            LONG,
            SHORT
        }

        protected override void OnStart()
        {
            Log("Start LevelTrader_cBot");

            Log("User defined properties:");
            Log(String.Format("EntryPrice: {0}", EntryPrice));
            Log(String.Format("StopLossPrice: {0}", StopLossPrice));
            Log(String.Format("Direction: {0}", Direction));
            Log(String.Format("RiskRevardRatio: {0}", RiskRevardRatio));
            Log(String.Format("RiskPercentage: {0}", RiskPercentage));
            Log(String.Format("IsEnableTrailingStop: {0}", IsEnableTrailingStop));
            Log(String.Format("TrailingStopLossLevel1Percentage: {0}", TrailingStopLossLevel1Percentage));
            Log(String.Format("TrailingStopLossLevel2Percentage: {0}", TrailingStopLossLevel2Percentage));
            Log(String.Format("PlaceTradeDelayInMinutes: {0}", PlaceTradeDelayInMinutes));
            Log(String.Format("MaxAllowedOpenTrades: {0}", MaxAllowedOpenTrades));
            Log(String.Format("ExpirationDateString: {0}", ExpirationDateString));
            
            Log("Validation of User defined properties ...");
            List<String> inputErrorMessages = ValidateInputs();
            inputErrorMessages.ForEach(m => Log(m));
            if (inputErrorMessages.Any()){
                Log("App contains input validation errors and will be stop.");
                Stop();
                return;
            }

            Log("Compute properties ... ");
            TradeId = System.Guid.NewGuid().ToString();
            Move = EntryPrice - StopLossPrice;
            TakeProfitPriceOneToOne = EntryPrice + Move;
            RiskPerTrade = (RiskPercentage / 100) * Account.Balance;
            double AmountRaw = RiskPerTrade / ((Math.Abs(Move) / Symbol.PipSize) * Symbol.PipValue);
            Amount = ((int)(AmountRaw / Symbol.VolumeInUnitsStep)) * Symbol.VolumeInUnitsStep;
            BeforeEntryPrice = EntryPrice + (Move * PercentageBeforeEntry);
            
            StopLossPips = (Math.Abs(Move)/Symbol.PipSize);
            StopLossLevel1Price = EntryPrice - (Move * 0.8);
            StopLossLevel2Price = EntryPrice;
            
            TakeProfitPips = ((Math.Abs(Move)/Symbol.PipSize)) * RiskRevardRatio;
            
            TrailingStopLossLevel1Price = EntryPrice + (Move * TrailingStopLossLevel1Percentage);
            TrailingStopLossLevel2Price = EntryPrice + (Move * TrailingStopLossLevel2Percentage);
            ExpirationDate = ExpirationDateString == String.Empty ? null : DateTime.Parse(ExpirationDateString);
            
            Log("Computed properties:");
            Log(String.Format("TradeId: {0}", TradeId));
            Log(String.Format("Move: {0}", Move));
            Log(String.Format("TakeProfitPriceOneToOne : {0}", TakeProfitPriceOneToOne));
            Log(String.Format("Account.Balance: {0}", Account.Balance));
            Log(String.Format("RiskPerTrade: {0}", RiskPerTrade));
            Log(String.Format("Amount raw: {0}", AmountRaw));
            Log(String.Format("Min step volume: {0}", Symbol.VolumeInUnitsMin));
            Log(String.Format("Amount: {0}", Amount));
            Log(String.Format("Amount: {0} lots", Symbol.VolumeInUnitsToQuantity(Amount)));
            Log(String.Format("BeforeEntryPrice: {0}", BeforeEntryPrice));
            Log(String.Format("TrailingStopLossLevel1Price: {0}", TrailingStopLossLevel1Price));
            Log(String.Format("TrailingStopLossLevel2Price: {0}", TrailingStopLossLevel2Price));
            Log(String.Format("StopLossPips: {0}", StopLossPips));
            Log(String.Format("StopLossLevel1Price: {0}", StopLossLevel1Price));
            Log(String.Format("StopLossLevel2Price: {0}", StopLossLevel2Price));
            Log(String.Format("TakeProfitPips: {0}", TakeProfitPips));
            Log(String.Format("ExpirationDate: {0}", ExpirationDate));

            Log("Validate of computed properties");
            var errMessages = ValidateComputeValues();
            errMessages.ForEach(m=>Log(m));
            if (errMessages.Any())
            {
                Log("App contains compute values validation errors and will be stop.");
                Stop();
                return;
            }
            
            Log("Register listeners");
            Positions.Opened += PositionsOnOpened;
            Positions.Closed += PositionsOnClosed;
        }

        protected override void OnBar()
        {
            Log("Start onBar step");
            
            if (ExpirationDate != null && DateTime.Now > ExpirationDate)
            {
                Log("Time of trade expired, bot will stop.");
                Stop();
                return;
            }
            
            
            Bar lastBar = MarketData.GetBars(TimeFrame.Minute, Symbol.Name).Last();

            if (IsActivePosition)
            {
                if (IsEnableTrailingStop &&
                    !ReachTrailingStopLossLevel1Price && 
                    WasReachPriceLevel(lastBar, TrailingStopLossLevel1Price, Direction == TradeDirectionType.LONG ))
                {
                    Log("Price reach TrailingStopLossLevel1Price.");
                    ReachTrailingStopLossLevel1Price = true;
                    SetStopLoss(StopLossLevel1Price);
                    return;
                }

                if (IsEnableTrailingStop &&
                    !ReachTrailingStopLossLevel2Price && 
                    ReachTrailingStopLossLevel1Price && 
                    WasReachPriceLevel(lastBar, TrailingStopLossLevel2Price, Direction == TradeDirectionType.LONG))
                {
                    Log("Price reach TrailingStopLossLevel2Price.");
                    ReachTrailingStopLossLevel2Price = true;
                    SetStopLoss(StopLossLevel2Price);
                    return;
                }
            } else 
            {
                if (!ReachProfitTargetOneToOne && 
                    WasReachPriceLevel(lastBar, TakeProfitPriceOneToOne, Direction==TradeDirectionType.SHORT))
                {
                    Log("Price reach ProfitTargetOneToOne.");
                    ReachProfitTargetOneToOne = true;
                    ReachProfitTargetTimestamp = DateTime.Now;
                }

                if (ReachProfitTargetOneToOne && 
                    !ReachBeforeEntryPrice &&
                    WasReachPriceLevel(lastBar, BeforeEntryPrice, Direction==TradeDirectionType.SHORT))
                {
                    Log("Price reach BeforeEntryPrice.");
                    ReachBeforeEntryPrice = true;
                    ReachBeforeEntryPriceTimestamp = DateTime.Now;

                    if(CountOpenTrades() >= MaxAllowedOpenTrades){
                        Log("On exchange is open max allowed trades, order do not place on exchange.");
                        Stop();
                        return;
                    }
                    
                    if(ReachBeforeEntryPriceTimestamp.Subtract(ReachProfitTargetTimestamp).TotalMinutes < PlaceTradeDelayInMinutes)
                    {
                        Log("Most fast movement to level, order do not place on exchange.");
                        Stop();
                        return;
                    }

                    Log("Place limit order");
                    TradeResult result = PlaceLimitOrder();
                    Log(String.Format("Response PlaceLimitOrder: {0}",result));
                    PendingOrderId = result.PendingOrder.Id;
                }
                
                if (ReachBeforeEntryPrice &&
                    WasReachPriceLevel(lastBar, TakeProfitPriceOneToOne, Direction == TradeDirectionType.LONG))
                {
                    Log("Price reach ProfitTargetOneToOne after hit BeforeEntryPrice.");
                    Log("Cancel pending order if exist.");
                    CancelLimitOrder();
                    Stop();
                    return;
                }

                foreach (Position pos in Positions)
                {
                    if (TradeId.SequenceEqual(pos.Comment)){
                        Log("Pending order was converted to position.");
                        Log(String.Format("Position opened at {0}", pos.EntryPrice));
                        IsActivePosition = true;
                    }
                }
            }
            
            if(enableTrace)
            {
                Log(String.Format("ReachProfitTargetOneToOne: {0}", ReachProfitTargetOneToOne));
                Log(String.Format("ReachBeforeEntryPrice: {0}", ReachBeforeEntryPrice));
                Log(String.Format("IsActivePosition: {0}",IsActivePosition));
                Log(String.Format("ReachTrailingStopLossLevel1Price: {0}", ReachTrailingStopLossLevel1Price));
                Log(String.Format("ReachTrailingStopLossLevel2Price: {0}", ReachTrailingStopLossLevel2Price));
            }
   
            Log("Finished onBar step");
        }

        protected override void OnStop()
        {
            Log("Finished LevelTrader_cBot");
        }

        protected override void OnException(Exception exception)
        {
            Log(exception.ToString(), "ERROR");
        }
        
        private void PositionsOnOpened(PositionOpenedEventArgs args)
        {
            var pos = args.Position;
            if (TradeId.SequenceEqual(pos.Comment)){
                 Log("Pending order was converted to position.");
                 Log(String.Format("Position opened at {0}", pos.EntryPrice));
                 IsActivePosition = true;
            }

        }
        
        private void PositionsOnClosed(PositionClosedEventArgs args)
        {
            var pos = args.Position;
            if(TradeId.SequenceEqual(pos.Comment)){
                string profitLossMessage = pos.GrossProfit >= 0 ? "profit" : "loss";   
                Log(String.Format("Position closed with {0} {1}", pos.GrossProfit, profitLossMessage));
                Stop();
            }
        }
        
        private bool WasReachPriceLevel(Bar lastBar, double priceLevel, bool up){
            return up ? lastBar.High >= priceLevel : lastBar.Low <= priceLevel;
        }
        
        private void SetStopLoss(double price){
            var position = Positions.FirstOrDefault(pos => TradeId.SequenceEqual(pos.Comment));
            
            if (position == null)
            {
                Log("Error: Position with TradeId: {0} does not exists.", TradeId);
                return;
            }
            
            position.ModifyStopLossPrice(price);
        }
        
        private TradeResult PlaceLimitOrder()
        {
           TradeType orderTradeType = Direction == TradeDirectionType.LONG ? TradeType.Buy : TradeType.Sell;
           string symbolName = Symbol.Name;
           double volumeInUnits = Amount;
           double limitPrice = EntryPrice;
           string label = "";
           double stopLossPips = StopLossPips;
           double takeProfitPips = TakeProfitPips;
           DateTime? expiryTime = null;
           string comment = TradeId;
           bool hasTrailingStop = false;
           StopTriggerMethod stopLossTriggerMethod = StopTriggerMethod.Trade;

           return  PlaceLimitOrder(orderTradeType, symbolName, volumeInUnits, limitPrice, label, stopLossPips, takeProfitPips,
           expiryTime, comment, hasTrailingStop, stopLossTriggerMethod);
        }
        
        private void CancelLimitOrder()
        {     
            var orderToCancel = PendingOrders.FirstOrDefault(order => order.Id == PendingOrderId);
            if (orderToCancel == null)
            {
                Log(String.Format("Pending order with id {0} does not exists.", PendingOrderId), "ERROR");
                return;
            }
            
            CancelPendingOrder(orderToCancel);
        }
        
        private int CountOpenTrades()
        {
            return Positions.Count + PendingOrders.Count;
        }
        
        
        private List<String> ValidateInputs()
        {
            var errMessages = new List<String>();
            
            if (EntryPrice <= 0)
            {
                errMessages.Add(String.Format("WARNING: EntryPrice must be greater than 0. [EntryPrice: {0}]", EntryPrice));
            }
            
            if (StopLossPrice <= 0)
            {
                errMessages.Add(String.Format("WARNING: StopLossPrice must be greater than 0. [StopLossPrice: {0}]", StopLossPrice));
            }
            
            if (RiskRevardRatio < 1)
            {
                errMessages.Add(String.Format("WARNING: RiskRevardRatio must be greater or equal 1. [RiskRevardRatio: {0}]", RiskRevardRatio));
            }
            
            if (RiskPercentage <= 0)
            {
                 errMessages.Add(String.Format("WARNING: RiskPercentage must be greater than 0. [RiskPercentage: {0}]", RiskPercentage));
            }
            
            if (PlaceTradeDelayInMinutes < 0)
            {
                errMessages.Add(String.Format("WARNING: PlaceTradeDelayInMinutes must be greater than 0. [PlaceTradeDelayInMinutes: {0}]", PlaceTradeDelayInMinutes));
            }
            
            if (MaxAllowedOpenTrades <= 0)
            {
                errMessages.Add(String.Format("WARNING: MaxAllowedOpenTrades must be greater than 0. [MaxAllowedOpenTrades: {0}]", PlaceTradeDelayInMinutes));
            }
            
            if (Direction == TradeDirectionType.LONG && EntryPrice < StopLossPrice)
            {
                errMessages.Add(String.Format("WARNING: EntryPrice must be greater than stopLossPrice for LONG direction. [EntryPrice: {0}, StopLossPrice{1}]", EntryPrice, StopLossPrice));
            }
            
            if (Direction == TradeDirectionType.SHORT && EntryPrice > StopLossPrice)
            {
                errMessages.Add(String.Format("WARNING: EntryPrice must be lower than stopLossPrice for SHORT direction. [EntryPrice: {0}, StopLossPrice{1}]", EntryPrice, StopLossPrice));
            }
            
            if (ExpirationDateString != String.Empty && !ExpirationDatePattern.IsMatch(ExpirationDateString))
            {
                errMessages.Add(String.Format("WARNING: ExpirationDateString must contains valid date in format YYYY/MM/DD example 2000/01/01: [ExpirationDateString: {0}]", ExpirationDateString));
            }

            if (TrailingStopLossLevel1Percentage <= 0.0 && TrailingStopLossLevel1Percentage >= 1.0)
            {
                errMessages.Add(String.Format("WARNING: TrailingStopLossLevel1Percentage must be between 0.0 and 1.0 (0 => 0%, 1 => 100%). [TrailingStopLossLevel1Percentage: {0}]", TrailingStopLossLevel1Percentage));
            }

            if (TrailingStopLossLevel2Percentage <= 0.0 && TrailingStopLossLevel2Percentage >= 1.0)
            {
                errMessages.Add(String.Format("WARNING: TrailingStopLossLevel2Percentage must be between 0.0 and 1.0 (0 => 0%, 1 => 100%). [TrailingStopLossLevel2Percentage: {0}]", TrailingStopLossLevel2Percentage));
            }
            return errMessages;
        }
        
        private List<String> ValidateComputeValues()
        {
            var errMessages = new List<String>();
             
            if (Amount < Symbol.VolumeInUnitsMin)
            {
                errMessages.Add(String.Format("WARNING: Trade volume is less than minimum tradable amount: [Amount: {0}, MinTradableAmount: {1}]", Amount, Symbol.VolumeInUnitsMin));
            }
            
            if (Amount > Symbol.VolumeInUnitsMax)
            {
                errMessages.Add(String.Format("WARNING: Trade volume is greater than maximum tradable amount: [Amount: {0}, MaxTradableAmount: {1}]", Amount, Symbol.VolumeInUnitsMax));
            }
            
            var firstTier = Symbol.DynamicLeverage[0];
            double amountInAccountCurrency = Amount * EntryPrice;
            double expectedMargin = amountInAccountCurrency/firstTier.Leverage;
            if (expectedMargin > Account.Balance)
            {
                errMessages.Add(String.Format("WARNING: Expected margin is greater that account balance: [ExpectedMargin: {0}, AccountBalance: {1}]", expectedMargin, Account.Balance));
            }
             
            return errMessages;
        }

        private void Log(string message, string level = "INFO")
        {        
            string logMessage = string.Format("[{0} - {1} - {2}] {3}: {4}", 
                    DateTime.Now,
                    Symbol.ToString(), 
                    Direction.ToString(),
                    level,
                    message);

            String dy = DateTime.Now.Day.ToString();
            String mn = DateTime.Now.Month.ToString();
            String yy = DateTime.Now.Year.ToString();
            string logFileName = String.Format("LevelTrader_{0}_{1}_{2}{3}{4}.log", Symbol.ToString(), Direction.ToString(), yy, mn, dy);
            string logPath = LogFolderPath + logFileName;
            if(!Directory.Exists(LogFolderPath))
            {
                Directory.CreateDirectory(LogFolderPath);
            }
            
            Print(logMessage); // Log to terminal
            File.AppendAllText(logPath,logMessage + Environment.NewLine); // Log to log file

            if (level.SequenceEqual("ERROR")){
                Notifications.SendEmail(LogSendersAddress, LogRecipientAddress, "Error in LevelTrader cBot", logMessage);
            }
        }

    }
}
