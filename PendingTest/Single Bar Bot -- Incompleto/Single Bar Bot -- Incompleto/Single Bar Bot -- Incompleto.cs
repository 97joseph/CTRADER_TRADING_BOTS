using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SingleBarBot : Robot
    {
        [Parameter("Quantity (Lots)", Group = "Volume", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double Quantity { get; set; }

        private bool validaTick = false;
        private bool isBuy = false;
        private bool isSell = false;


        private double abertura1;
        private double fechamento1;
        private double abertura2;
        private double fechamento2;
        private double abertura3;
        private double fechamento3;
        private double tickAtual;

        private double pontoEntrada;

        private double volumeInUnits;

        protected override void OnStart()
        {
            volumeInUnits = Symbol.QuantityToVolumeInUnits(Quantity);
        }

        protected override void OnBar()
        {
            abertura1 = Bars.Last(1).Open;
            fechamento1 = Bars.Last(1).Close;
            abertura2 = Bars.Last(2).Open;
            fechamento2 = Bars.Last(2).Close;
            abertura3 = Bars.Last(3).Open;
            fechamento3 = Bars.Last(3).Close;

            double aberturaAtual = Bars.LastBar.Open;

            if (validaTick)
            {
                if (((Bars.Last(1).Close - Bars.Last(1).Open) <= 0) && isBuy)
                    Desistir();
            }


            if (abertura3 < fechamento3 && abertura2 < fechamento2 && abertura1 > fechamento1)
            {
                if (abertura2 >= fechamento3)
                {
                    validaTick = true;
                    isBuy = true;
                    pontoEntrada = Bars.HighPrices.Maximum(3);
                }
            }
        }

        protected override void OnTick()
        {
            if (validaTick)
            {
                double alvoPerda = (fechamento2 - abertura3) * (Symbol.LotSize == 1 ? 10 : Symbol.LotSize / (Symbol.LotSize == 1 ? 1 : 10));

                if (isBuy)
                {
                    tickAtual = Bars.LastBar.High;
                    if (tickAtual > pontoEntrada * 1.0005)
                    {
                        Print(pontoEntrada);
                        ExecuteMarketOrder(TradeType.Buy, SymbolName, volumeInUnits, "SingleBar", alvoPerda, alvoPerda);
                        Desistir();
                    }
                    if (tickAtual <= abertura1)
                    {
                        Desistir();
                    }

                }
                else if (isSell)
                {

                }
            }
        }

        private void Desistir()
        {
            isBuy = false;
            isSell = false;
            validaTick = false;
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
        }
    }
}
