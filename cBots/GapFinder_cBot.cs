using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None)]
    public class DailyGapFinder : Robot
    {
    
        private readonly bool enableTrace = false;
        
        private List<Double> gap_percentages = new List<Double>();
        
        public class Gap
        {
            public double Percentage {get; set;}
            public DateTime Date {get; set; }

            public override string ToString()
            {
                return String.Format("Date: {0}, Percentage: {1}", Date, Percentage);
            }
        }
        
        private List<Gap> gaps = new List<Gap>();

        protected override void OnStart()
        {
        }

        protected override void OnBar()
        {
            List<Bar> lastBars = MarketData.GetBars(TimeFrame.Daily, Symbol.Name).TakeLast(2).ToList();
            
            Bar bar1 = lastBars.ElementAt(0);
            Bar bar2 = lastBars.ElementAt(1);
            
            if (enableTrace)
            {
                Print("Bar 1: {0}", bar1);
                Print("Bar 2: {0}", bar2);
            }

            if(bar1.Close != bar2.Open)
            {
                double diff = Math.Abs(bar1.Close - bar2.Open);
                double gap_percentage = Math.Round(diff/bar1.Close*100, 3);
                
                Gap gap = new Gap();
                gap.Percentage = gap_percentage;
                gap.Date = bar2.OpenTime;
                Print(gap);
                gaps.Add(gap);
            }
        }
        
        protected override void OnStop(){
            Print("-----------------");
            Print("Result");
            Print("-----------------");
            
            Print("Average gap is: {0} %", gaps.Average(gap => gap.Percentage));
            
            // Sort the list in descending order
            gaps.Sort((a, b) => b.Percentage.CompareTo(a.Percentage));
            
            List<Gap> top20 = gaps.Take(20).ToList();
            
            Print("Top 20 gaps:");
            foreach (Gap gap in top20)
            {
                Print(gap);
            }
            
        }
        
       
    }
}