using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScottPlot;
using System.IO;
using Discord.WebSocket;
using System.Globalization;
using System.Drawing;
using System.Reactive;
using System.Runtime.InteropServices;
using Amazon.Auth.AccessControlPolicy.ActionIdentifiers;
using Newtonsoft.Json;
using OpenQA.Selenium.Interactions.Internal;

namespace MoongBot.Core.Manager
{
    public class CoinMarketManager
    {
        public CoinMarketManager()
        {
            LoadSubscribers();
        }
        public enum EventType
        {
            PriceSurge,        // 가격 급등
            PriceDrop,         // 가격 급락
            PriceSurgeAndFall, // 급등 후 급락
            PricePlungeAndSurge, // 급락 후 급등
            Delisting,         // 상장폐지
        }       

        private static DatabaseManager _dbManager = new DatabaseManager();
        private readonly Random _random = new Random();

        private Dictionary<int, (double OpenPrice, double HighPrice, double LowPrice, double ClosePrice)> _coinTrends = new();
        private Dictionary<int, EventType> afternoonEventCoins = new Dictionary<int, EventType>();
        private static Dictionary<int, double> prePriceCoins = new Dictionary<int, double>();
        private static HashSet<int> _eventCoins = new HashSet<int>();        
        public static HashSet<ulong> _subscribedUsers = new HashSet<ulong>();
        private static readonly string SubscriptionFilePath = Path.Combine("jsonFiles", "newsSubscribers.json");
        public const double TransactionFeeRate = 0.02;

        private static bool isAfternoonEventTriggered = false;
        private static bool isNightEventTriggered = false;
        private static bool isNewsSent = false;
        private static bool isFUNewsSent = false;

        public async Task<(Embed, MessageComponent)> GetCoinMarketStatusEmbedAsync()
        {
            var coins = await _dbManager.GetAllCoinsAsync(); // 모든 코인 정보 가져오기

            var embedBuilder = new EmbedBuilder()
            {
                Title = "코인 시세 현황",
                Color = Discord.Color.Green
            };

            var componentBuilder = new ComponentBuilder();
            var usCulture = new CultureInfo("en-US");

            foreach (var (coinId, coinName, currentPrice) in coins)
            {
                // 이전 가격 가져오기
                double previousPrice = currentPrice;
                if (prePriceCoins.ContainsKey(coinId))
                {
                    previousPrice = prePriceCoins[coinId];
                }                               

                // 시세 변동 계산
                double priceChange = ((currentPrice - previousPrice) / previousPrice) * 100;

                // 화살표 및 시세 변동 표시
                string priceChangeArrow = priceChange >= 0 ? ":chart_with_upwards_trend:" : ":chart_with_downwards_trend:";
                if (priceChange == 0)
                {
                    priceChangeArrow = "";
                }
                string priceChangeText = $"{priceChangeArrow}  {Math.Abs(priceChange):0.00}%";

                // 코인 이름, 가격, 시세 변동 정보를 필드로 추가                
                embedBuilder.AddField($"{coinName}", $"{currentPrice.ToString("C2", usCulture)}   {priceChangeText}", inline: false);

                // 각 코인에 대한 구매 버튼 추가
                var button = new ButtonBuilder()
                    .WithLabel(coinName + " 매수")
                    .WithCustomId($"buy_coin_{coinId}")
                    .WithStyle(ButtonStyle.Primary);

                componentBuilder.WithButton(button);
            }

            return (embedBuilder.Build(), componentBuilder.Build());
        }
        public async Task StartPriceUpdateAsync(int intervalMilliseconds = 120000) // 2분마다 가격 변동
        {
            while (true)
            {                
                // 모든 코인에 대해 가격 변동 적용 (2분마다)
                for (int i = 0; i < 5; i++) // 10분 = 2분 * 5
                {
                    var allCoins = await _dbManager.GetAllCoinsAsync(); // 모든 코인 가져오기

                    foreach (var (coinId, coinName, currentPrice) in allCoins)
                    {
                        prePriceCoins[coinId] = currentPrice;
                        await _dbManager.SavePreviousPriceAsync(coinId, currentPrice);  // 초기 변동 전 가격 저장

                        if (!_coinTrends.ContainsKey(coinId))
                        {                          
                            _coinTrends[coinId] = (currentPrice, currentPrice, currentPrice, currentPrice);
                        }

                        var (eventCount, isSurge, isDelisted) = await _dbManager.GetCoinEventAsync(coinId);
                        double riseValue = _random.NextDouble() * 0.1 + 1.05; // 12% ~ 25%
                        double fallingValue = 1 - (_random.NextDouble() * 0.07 + 0.07); // 15% ~ 22%
                        double newPrice = currentPrice;

                        var (openPrice, highPrice, lowPrice, closePrice) = _coinTrends[coinId];

                        // 가격 변동 적용
                        if (isDelisted)
                        {
                            // 상장폐지된 코인은 가격이 하락만 발생하도록
                            fallingValue = 1 - (_random.NextDouble() * 0.05 + 0.01); // 1% ~ 6% 하락
                            newPrice = currentPrice * fallingValue;
                        }
                        else
                        {
                            if (!_eventCoins.Contains(coinId))
                            {
                                newPrice = ApplyPriceChange(currentPrice); // 변동폭 적용 
                            }                                                                                                          
                            if (eventCount > 0 && isSurge && newPrice > currentPrice)  // 가격이 상승하는 경우에만 증폭
                            {
                                Console.WriteLine($"{coinId}번 코인에 급등 이벤트 실행");
                                newPrice *= riseValue; // 급등의 경우
                                eventCount--; // 이벤트 횟수를 줄임
                            }
                            else if (eventCount > 0 && !isSurge && newPrice < currentPrice) // 하락 중일 때만 급락 이벤트 적용
                            {
                                Console.WriteLine($"{coinId}번 코인에 급락 이벤트 실행");
                                newPrice *= fallingValue; // 급락의 경우
                                eventCount--; // 이벤트 횟수를 줄임
                            }
                        }                                               

                        // High와 Low 값을 갱신
                        highPrice = Math.Max(highPrice, newPrice);
                        lowPrice = Math.Min(lowPrice, newPrice);

                        if(i == 0)
                        {
                            openPrice = newPrice;
                        }

                        if (i == 4)
                        {
                            closePrice = newPrice;
                        }

                        await _dbManager.UpdateCoinPriceAsync(coinId, newPrice);

                        _coinTrends[coinId] = (openPrice, highPrice, lowPrice, closePrice);
                        if(eventCount > 0)
                        {
                            await _dbManager.SaveCoinEventAsync(coinId, eventCount, isSurge, isDelisted);
                        }
                        else if(eventCount == 0 && !isDelisted)
                        {
                            await _dbManager.DeleteCoinEventAsync(coinId);
                        }
                                                

                        await CheckAutoTradeForCoinAsync(coinId, newPrice);

                        if (i == 4)
                        {
                            DateTime currentTime = DateTime.Now;

                            // OHLC 데이터를 저장
                            await _dbManager.SaveCoinPriceHistoryAsync(coinId, openPrice, highPrice, lowPrice, closePrice, currentTime);

                            // 10일이 지난 데이터 삭제
                            await _dbManager.DeleteOldCoinPriceHistoryAsync(coinId);

                            _coinTrends.Remove(coinId);
                        }
                    }                   
                    await Task.Delay(intervalMilliseconds);
                }
            }
        }

        // 가격 변동을 적용 (상승 또는 하락에 따라 변동폭을 적용)
        private double ApplyPriceChange(double currentPrice)
        {
            bool isPositiveChange = _random.Next(2) == 0; // 0이면 true, 1이면 false
            double changePercentage;           

            if (isPositiveChange || currentPrice < 10)
            {
                changePercentage = _random.NextDouble() * (0.08 - 0.0001) + 0.0001;
            }
            else
            {
                changePercentage = -(_random.NextDouble() * (0.075 - 0.0001) + 0.0001);
            }
            return currentPrice + (currentPrice * changePercentage);
        }
        // 코인 매수
        public async Task<string> BuyCoinAsync(ulong userId, string coinName, double inputAmount)
        {
            var (coinId, currentPrice, symbol, fullCoinName) = await _dbManager.GetCoinPriceAsync(coinName);

            // 수수료 반영 후 실제 구매 가능한 금액 계산
            double totalQuantity = Math.Floor((inputAmount / (currentPrice * (1 + TransactionFeeRate))) * 100) / 100;

            // 총 매수 비용 계산 (거래 수수료 포함)
            double totalCost = totalQuantity * currentPrice * (1 + TransactionFeeRate);

            // 사용자의 잔액 확인
            double userDollar = await _dbManager.GetUserDollarAsync(userId);
            if (userDollar < totalCost)
            {
                return $"<@{userId}> 잔액이 부족합니다.";
            }
            if (totalQuantity < 0.01)
            {
                return $"<@{userId}> {inputAmount}로 구매가능한 코인의 수량이 없습니다.";
            }

            // 코인 매수 기록
            await _dbManager.BuyUserCoinAsync(userId, coinId, totalQuantity, currentPrice);

            // 유저의 달러 차감
            await _dbManager.UseDollarAsync(userId, totalCost);
            double balance = await _dbManager.GetUserDollarAsync(userId);

            var usCulture = new CultureInfo("en-US");
            return $"<@{userId}>님!! {fullCoinName} 코인을 {totalQuantity:N2}개 매수하였습니다. 사용금액 : {totalCost.ToString("C2", usCulture)}, 잔액 : {balance} :dollar:";
        }

        // 코인 매도
        public async Task<string> SellCoinAsync(ulong userId, string coinName, string quantity)
        {
            var (coinId, currentPrice, symbol, fullCoinName) = await _dbManager.GetCoinPriceAsync(coinName);

            // 유저가 보유한 코인의 가격별 수량을 가져옴
            var (searchCoinId, totalQuantity, averagePrice) = await _dbManager.GetUserCoinHoldingsForSpecificCoinAsync(userId, coinId);

            double remainingQuantity = 0;

            if (double.TryParse(quantity, out double userSellQuantity))
            {
                if (userSellQuantity < 0.01)
                {
                    return $"<@{userId}> 0.01 이상의 수량을 입력해주세요.";
                }
                else if(userSellQuantity > totalQuantity)
                {
                    remainingQuantity = totalQuantity;
                }
                else
                {
                    remainingQuantity = userSellQuantity;
                }              
            }
            else if (quantity.Equals("*"))
            {              
                remainingQuantity = totalQuantity;
            }
            
            if (searchCoinId == -1 || totalQuantity < 0.01)
            {
                return $"<@{userId}> 해당 코인을 보유하고있지 않습니다.";
            }
                
            double sellQuantity = Math.Min(remainingQuantity, totalQuantity);

            // 코인 매도 처리
            await _dbManager.SellUserCoinAsync(userId, searchCoinId, sellQuantity, currentPrice);

            double totalValue = currentPrice * sellQuantity;

            double balance = await _dbManager.GetUserDollarAsync(userId);

            var usCulture = new CultureInfo("en-US");
            return $"<@{userId}>님!! {fullCoinName} 코인을 {sellQuantity:N2}개 매도하였습니다. +{totalValue.ToString("C2", usCulture)} 매도후 <@{userId}>님의 잔액 : {balance} :dollar:";
        }

        public async Task<(Embed, MessageComponent)> GetUserCoinHoldingsEmbedAsync(ulong userId, string userName)
        {
            // 1. 유저가 보유한 모든 코인 가져오기
            var holdings = await _dbManager.GetUserCoinHoldingsForAllCoinsAsync(userId);

            var componentBuilder = new ComponentBuilder();
            var embedBuilder = new EmbedBuilder()
            {
                Title = $"{userName} 님의 포트폴리오",
                Color = Discord.Color.Blue
            };

            // 현재 이용자의 잔고
            double balance = await _dbManager.GetUserDollarAsync(userId);
            var usCulture = new CultureInfo("en-US");

            embedBuilder.AddField("잔고", $"{balance:N2} :dollar:", inline: false);

            // 2. 보유 코인 리스트가 없을 경우
            if (holdings.Count == 0)
            {
                embedBuilder.AddField("보유 코인이 없습니다.", "현재 보유하고 있는 코인이 없습니다.", inline: false);
            }
            else
            {
                // 3. 코인 보유 현황을 필드로 추가
                foreach (var (coinName, totalQuantity, averagePrice) in holdings)
                {                    
                    if(totalQuantity < 0.01)
                    {
                        var (deleteCoinName, deleteCoinId) = await _dbManager.GetCoinIdByNameAsync(coinName);

                        if(deleteCoinName != null)
                            await _dbManager.DeleteUserCoinHoldingAsync(userId , deleteCoinId);

                        continue;
                    }
                    // 현재 코인의 최신 시세 가져오기
                    var (coinId, currentPrice, symbol, fullCoinName) = await _dbManager.GetCoinPriceAsync(coinName);

                    // 총 투자 금액 (수량 * 평균 매수가)
                    double totalInvested = totalQuantity * averagePrice;

                    // 현재 평가 금액 (수량 * 현재 가격)
                    double currentValuation = totalQuantity * currentPrice;

                    // 총 수익/손실 (현재 평가 금액 - 총 투자 금액)
                    double profitOrLoss = currentValuation - totalInvested;

                    // 수익률 계산
                    double profitPercentage = (profitOrLoss / totalInvested) * 100;

                    // 코인별 정보 추가
                    embedBuilder.AddField(
                        $"{coinName}",
                        $"수량: {totalQuantity:N2} ({symbol})\n" +
                        $"평균 매수가: {averagePrice.ToString("C2", usCulture)}\n" +
                        $"현재 가격: {currentPrice.ToString("C2", usCulture)}\n" +
                        $"총 투자 금액: {totalInvested.ToString("C2", usCulture)}\n" +
                        $"현재 평가 금액: {currentValuation.ToString("C2", usCulture)}\n" +
                        $"수익/손실: {(profitOrLoss > 0 ? ":chart_with_upwards_trend:" : ":chart_with_downwards_trend:")} {profitOrLoss.ToString("C2", usCulture)} ({profitPercentage:+0.00;-0.00}%)",
                        inline: false
                    );

                    // 각 코인에 대한 판매 버튼 추가
                    var button = new ButtonBuilder()
                        .WithLabel(coinName + " 매도")
                        .WithCustomId($"sell_coin_{coinId}")
                        .WithStyle(ButtonStyle.Primary);

                    componentBuilder.WithButton(button);
                }
            }
            

            return (embedBuilder.Build(), componentBuilder.Build());
        }
        public async Task<(bool, string)> SetAutoTradeAsync(ulong userId, string coinName, double targetPrice, double quantity, bool isBuying)
        {
            var (coinId, currentPrice, symbol, fullCoinName) = await _dbManager.GetCoinPriceAsync(coinName);
            // 자동 매매 조건 저장 (DB 또는 메모리 저장)
            var (isSuccess, result ) = await _dbManager.SaveAutoTradeConditionAsync(userId, fullCoinName, targetPrice, quantity, isBuying);
            return (isSuccess, result);
        }

        public async Task CheckAutoTradeForCoinAsync(int coinId, double currentPrice)
        {
            try
            {
                string name = await _dbManager.GetCoinNameByIdAsync(coinId);
                var autoTrades = await _dbManager.GetAutoTradeConditionsByCoinIdAsync(name);

                foreach (var (userId, coinName, targetPrice, quantity, isBuying) in autoTrades)
                {
                    bool isTrade = false;
                    if (isBuying && currentPrice <= targetPrice && !isTrade) // 매수 조건: 현재 가격이 목표 가격 이하일 때
                    {
                        Console.WriteLine($"{userId}에 해당하는 유저의 코인 구매 로직 실행");
                        isTrade = true;
                        bool isSuccess = await _dbManager.DeleteAutoTradeConditionAsync(userId, coinName, isBuying);
                        if (isSuccess)
                        {
                            double price = quantity * currentPrice * (1 + TransactionFeeRate);
                            string result = await BuyCoinAsync(userId, coinName, price);
                            await EventManager.AutoTradeNotification(result);
                        }
                    }
                    else if (!isBuying && currentPrice >= targetPrice && !isTrade) // 매도 조건: 현재 가격이 목표 가격 이상일 때
                    {
                        Console.WriteLine($"{userId}에 해당하는 유저의 코인 판매 로직 실행");
                        isTrade = true;
                        bool isSuccess = await _dbManager.DeleteAutoTradeConditionAsync(userId, coinName, isBuying);
                        if (isSuccess)
                        {
                            string sellQuantity = quantity.ToString();
                            string result = await SellCoinAsync(userId, coinName, sellQuantity);
                            await EventManager.AutoTradeNotification(result);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                await ExceptionManager.HandleExceptionAsync(ex);
            }        
        }

        public async Task<(string, string)> GenerateCandlestickWithSMAChartAsync(string coinName, int period = 1)
        {
            try
            {
                // 데이터베이스에서 Coin ID를 가져옵니다.
                var (searchCoinName, coinId) = await _dbManager.GetCoinIdByNameAsync(coinName);

                if (searchCoinName == null)
                {
                    return ("잘못된 코인명", "");
                }
                // 기간에 따른 데이터 선택
                DateTime endTime = DateTime.Now;
                DateTime startTime = endTime.AddDays(-period);

                var priceHistory = await _dbManager.GetCoinPriceHistoryAsync(coinId, startTime, endTime);

                if (priceHistory.Count == 0)
                {
                    return ("데이터 부족", "");  // 데이터가 없을 경우 빈 문자열 반환
                }

                // OHLC 데이터 준비
                List<OHLC> ohlcList = new List<OHLC>();

                TimeSpan timeSpanPerCandle = TimeSpan.FromMinutes(10 * period);

                DateTime currentStartTime = priceHistory.First().Timestamp; // 첫 데이터의 시작 시간
                double open = priceHistory.First().Open;
                double high = priceHistory.First().High;
                double low = priceHistory.First().Low;
                double close = priceHistory.First().Close;

                foreach (var price in priceHistory)
                {
                    if (price.Timestamp < currentStartTime + timeSpanPerCandle)
                    {
                        // 기존 캔들스틱에 데이터를 추가
                        high = Math.Max(high, price.High);
                        low = Math.Min(low, price.Low);
                        close = price.Close;
                    }
                    else
                    {
                        // 새로운 캔들스틱 생성
                        ohlcList.Add(new OHLC(open, high, low, close, currentStartTime, timeSpanPerCandle));

                        // 다음 캔들스틱 준비
                        currentStartTime += timeSpanPerCandle;
                        open = price.Open;
                        high = price.High;
                        low = price.Low;
                        close = price.Close;
                    }
                }
                ohlcList.Add(new OHLC(open, high, low, close, currentStartTime, timeSpanPerCandle));

                // ScottPlot 차트 생성
                var plt = new Plot(600, 400);
                var candlePlot = plt.AddCandlesticks(ohlcList.ToArray());

                candlePlot.ColorUp = System.Drawing.Color.Red;
                candlePlot.ColorDown = System.Drawing.Color.Blue;

                if (ohlcList.Count > 8)
                {
                    // 단기 SMA (8 구간) 계산
                    var sma8 = candlePlot.GetSMA(8); // 8 기간 SMA
                    plt.AddScatterLines(sma8.xs, sma8.ys, System.Drawing.Color.DarkGreen, 2, label: "8-period SMA");
                }
                else
                {
                    Console.WriteLine("SMA 8을 계산하기에 충분한 데이터가 없습니다.");
                }

                // X축 레이블 설정 (공백 없이 데이터가 있는 시간만으로 X축 설정)
                var timestamps = ohlcList.Select(o => o.DateTime.ToOADate()).ToArray();

                // 시작과 끝 부분에 여유 공간을 추가 (X축 범위의 5% 추가)
                if (timestamps.Any())
                {
                    double minTimestamp = timestamps.First();
                    double maxTimestamp = timestamps.Last();
                    double padding = (maxTimestamp - minTimestamp) * 0.05;

                    // X축 범위를 설정하여 여유 공간을 추가
                    plt.SetAxisLimitsX(minTimestamp - padding, maxTimestamp + padding);
                }

                // X축과 Y축 라벨 설정
                plt.Title($"{searchCoinName} 차트 - ({period}일)");
                plt.XAxis.DateTimeFormat(true);  // X축을 날짜로 표시
                plt.XAxis.Label("시간");
                plt.YAxis.Label("가격 ($)");

                // 범례 추가 (단기 SMA 구분)
                plt.Legend(true, Alignment.UpperLeft);

                // 차트를 이미지 파일로 저장
                string filePath = Path.Combine(Directory.GetCurrentDirectory(), $"{coinName}_candlestick_sma_chart_{period}.png");
                plt.SaveFig(filePath);

                return (filePath, searchCoinName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GenerateCandlestickWithSMAChartAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return ("", "");
            }
        }

        public async Task<(string, string)> SendCoinPriceChartAsync(IMessageChannel channel, string coinName, int period)
        {
            try
            {              
                var (chartPath, searchCoinName) = await GenerateCandlestickWithSMAChartAsync(coinName, period);

                if (chartPath.Equals(""))
                {
                    return ("", $"차트 생성 중 문제가 발생했습니다.");
                }
                else if (chartPath.Equals("데이터 부족"))
                {
                    return ("", $"차트를 생성할 데이터가 충분히 쌓이지 않았습니다. 다음에 시도해주세요.");
                }
                else if (chartPath.Equals("잘못된 코인명"))
                {
                    return ("", $"잘못된 코인명을 입력했습니다.");
                }
                else
                {
                    if (!string.IsNullOrEmpty(chartPath))
                    {
                        return (chartPath, $"{searchCoinName}의 시세 차트입니다.");                      
                    }
                    else
                    {
                        return ("", $"차트 생성 중 문제가 발생했습니다.");
                    }
                }              
            }
            catch (Exception ex)
            {
                await ExceptionManager.HandleExceptionAsync (ex);
                return ("", $"차트 생성 중 문제가 발생했습니다.");
            }
        }

        private async Task TriggerEventAsync(bool isAfternoon)
        {
            var allCoins = await _dbManager.GetAllCoinsAsync();
            var selectedCoins = new List<(int CoinId, string CoinName, double CurrentPrice)>();           

            if (isAfternoon)
            {
                // 2~4개의 코인을 선택하여 급등/급락 이벤트 적용
                int coinCount = _random.Next(2, 5);
                var availableCoins = new List<(int CoinId, string CoinName, double CurrentPrice)>(allCoins); // 코인 리스트 복사본

                // 급등 또는 급락 이벤트 중 랜덤 선택
                var eventType = EventType.PriceSurge;
                int randomEvent = _random.Next(10);

                if (randomEvent < 4)
                {
                    eventType = EventType.PriceDrop;
                }
                else if (randomEvent == 8)
                {
                    eventType = EventType.PriceSurgeAndFall;
                }
                else if (randomEvent == 9)
                {           
                    eventType = EventType.PricePlungeAndSurge;
                }

                List<Task> eventTasks = new List<Task>();

                for (int i = 0; i < coinCount; i++)
                {
                    if (availableCoins.Count == 0) break; // 더 이상 선택할 코인이 없을 경우 종료

                    var selectedCoin = availableCoins[_random.Next(availableCoins.Count)];
                    selectedCoins.Add(selectedCoin);
                    availableCoins.Remove(selectedCoin); // 선택된 코인을 리스트에서 제거하여 중복 방지                   

                    // 해당 코인을 afternoonEventCoins에 추가 (코인ID와 발생한 이벤트 타입 저장)
                    afternoonEventCoins[selectedCoin.CoinId] = eventType;

                    // 이벤트 적용
                    if (eventType == EventType.PriceSurge)
                    {
                        eventTasks.Add(HandlePriceSurgeEventAsync(selectedCoin.CoinId));
                    }
                    else if (eventType == EventType.PriceDrop)
                    {
                        eventTasks.Add(HandlePriceDropEventAsync(selectedCoin.CoinId));
                    }
                    else if (eventType == EventType.PriceSurgeAndFall)
                    {
                        eventTasks.Add(HandlePriceSurgeAndFallEventAsync(selectedCoin.CoinId));
                    }
                    else
                    {
                        eventTasks.Add(HandlePriceDropAndSurgeEventAsync(selectedCoin.CoinId));
                    }                        
                }
                await Task.WhenAll(eventTasks);
            }
            else
            {
                int coinCount = _random.Next(2, 5);
                var availableCoins = allCoins.Where(c => !afternoonEventCoins.ContainsKey(c.CoinId)).ToList(); // 오후 이벤트 제외 코인

                for (int i = 0; i < coinCount; i++)
                {
                    if (availableCoins.Count == 0) break;

                    var selectedCoin = availableCoins[_random.Next(availableCoins.Count)];
                    selectedCoins.Add(selectedCoin);
                    availableCoins.Remove(selectedCoin); // 중복 선택 방지
                }
                
                int eventNum;
                EventType eventType = afternoonEventCoins.Values.FirstOrDefault();
                if(eventType == EventType.PriceSurge)
                {
                    eventNum = 0;
                }
                else if (eventType == EventType.PriceDrop)
                {
                    eventNum = 1;
                }
                else if(eventType == EventType.PriceSurgeAndFall)
                {
                    eventNum = 2;
                }
                else if(eventType == EventType.PricePlungeAndSurge)
                {
                    eventNum = 3;
                }
                else
                {
                    eventNum = _random.Next(4);
                }
                List<Task> eventTasks = new List<Task>();
                // afternoonEventCoins.Values에서 첫 번째 이벤트 타입에 따라 반대 이벤트 적용
                if (eventNum == 1 || eventNum == 2)
                {
                    // 오후에 급등했으면 selectedCoins에 대해 급락 이벤트 발생
                    foreach (var (coinId, coinName, currentPrice) in selectedCoins)
                    {
                        eventTasks.Add(HandlePriceSurgeEventAsync(coinId));
                    }
                }
                else 
                {
                    // 오후에 급락했으면 selectedCoins에 대해 급등 이벤트 발생
                    foreach (var (coinId, coinName, currentPrice) in selectedCoins)
                    {
                        eventTasks.Add(HandlePriceDropEventAsync(coinId));
                    }
                }

                await Task.WhenAll(eventTasks);

                // 밤 이벤트가 끝난 후, 오후에 발생한 코인 기록을 초기화             
                afternoonEventCoins.Clear();
            }
        }

        public async Task HandlePriceSurgeEventAsync(int coinId)
        {
            Console.WriteLine("급등 이벤트 실행");
            int eventCount = _random.Next(3, 11); // 3~10번의 급등 이벤트
            bool isSurge = true; // 급등 이벤트

            // 급등 이벤트를 추가
            await _dbManager.SaveCoinEventAsync(coinId, eventCount, isSurge, false);


            if (!isNewsSent)
            {
                isNewsSent = true;

                string randomSurgeNews = GetRandomSurgeNews();
                string randomReporter = reporterNames[_random.Next(reporterNames.Count)];
                string currentTime = DateTime.Now.ToString("MM월 dd일 HH:mm");

                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"📈 {currentTime} - 호롤로 뉴스")
                    .WithDescription($"{randomSurgeNews}")
                    .WithColor(Discord.Color.Green)
                    .WithFooter($"호롤일보 {randomReporter} 기자 babo@holol.com");

                // 뉴스 메시지 전송
                if (_subscribedUsers.Count > 0)
                {
                    // 구독한 유저들의 멘션 문자열 생성
                    string userMentions = string.Join(" ", _subscribedUsers.Select(userId => $"<@{userId}>"));

                    // 멘션된 유저들과 함께 뉴스 전송
                    await SendMarketEventAlertAsync(embedBuilder.Build(), userMentions);
                }
                else
                {
                    // 구독자가 없으면 그냥 뉴스만 전송
                    await SendMarketEventAlertAsync(embedBuilder.Build());
                }
                
            }          
        }    
        public async Task HandlePriceDropEventAsync(int coinId)
        {
            Console.WriteLine("급락 이벤트 실행");
            int eventCount = _random.Next(3, 11); // 3~10번의 급락 이벤트
            bool isSurge = false; // 급락 이벤트

            // 급락 이벤트를 추가
            await _dbManager.SaveCoinEventAsync(coinId, eventCount, isSurge, false);

            if (!isNewsSent)
            {
                isNewsSent = true;
                string randomPlungeNews = GetRandomPlungeNews();
                string randomReporter = reporterNames[_random.Next(reporterNames.Count)];
                string currentTime = DateTime.Now.ToString("MM월 dd일 HH:mm");

                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"📉 {currentTime} - 호롤로 뉴스")
                    .WithDescription($"{randomPlungeNews}")
                    .WithColor(Discord.Color.Red)
                    .WithFooter($"호롤일보 {randomReporter} 기자 babo@holol.com");

                // 뉴스 메시지 전송
                if (_subscribedUsers.Count > 0)
                {
                    // 구독한 유저들의 멘션 문자열 생성
                    string userMentions = string.Join(" ", _subscribedUsers.Select(userId => $"<@{userId}>"));

                    // 멘션된 유저들과 함께 뉴스 전송
                    await SendMarketEventAlertAsync(embedBuilder.Build(), userMentions);
                }
                else
                {
                    // 구독자가 없으면 그냥 뉴스만 전송
                    await SendMarketEventAlertAsync(embedBuilder.Build());
                }
                
            }                
        }

        public async Task AddCoinEventAsync(string coinName, double price)
        {
            var usCulture = new CultureInfo("en-US");

            string listedNews = "📢 새로운 코인 상장 안내\n\n" + $"거래소에 새로운 코인이 상장되었습니다. 이번에 상장된 코인은 혁신적인 기술로 주목받고 있는 **\'{coinName}\'**입니다.\n" + "거래 가능 시간은 상장 직후부터 가능합니다. 코인 거래에 앞서 해당 코인에 대한 연구와 충분한 검토를 진행하시길 권장드립니다.\n\n" + $"상장 가격 : {price.ToString("C2", usCulture)}\n\n";
            string randomReporter = reporterNames[_random.Next(reporterNames.Count)];
            string currentTime = DateTime.Now.ToString("MM월 dd일 HH:mm");

            var embedBuilder = new EmbedBuilder()
                .WithTitle($":newspaper: {currentTime} - 호롤로 뉴스")
                .WithDescription($"{listedNews}")
                .WithColor(Discord.Color.Green)
                .WithFooter($"호롤일보 {randomReporter} 기자 babo@holol.com");

            // 뉴스 메시지 전송
            if (_subscribedUsers.Count > 0)
            {
                // 구독한 유저들의 멘션 문자열 생성
                string userMentions = string.Join(" ", _subscribedUsers.Select(userId => $"<@{userId}>"));

                // 멘션된 유저들과 함께 뉴스 전송
                await SendMarketEventAlertAsync(embedBuilder.Build(), userMentions);
            }
            else
            {
                // 구독자가 없으면 그냥 뉴스만 전송
                await SendMarketEventAlertAsync(embedBuilder.Build());
            }
        }

        public async Task DeleteCoinEventAsync(string coinName)
        {
            string randomNews = GetRandomDelistingNews(coinName);

            string randomDelistingNews = $"📢 {coinName} 상장폐지 안내\n" + randomNews;
            string randomReporter = reporterNames[_random.Next(reporterNames.Count)];
            string currentTime = DateTime.Now.ToString("MM월 dd일 HH:mm");

            var embedBuilder = new EmbedBuilder()
                .WithTitle($":newspaper: {currentTime} - 호롤로 뉴스")
                .WithDescription($"{randomDelistingNews}")
                .WithColor(Discord.Color.Red)
                .WithFooter($"호롤일보 {randomReporter} 기자 babo@holol.com");


            if (_subscribedUsers.Count > 0)
            {
                // 구독한 유저들의 멘션 문자열 생성
                string userMentions = string.Join(" ", _subscribedUsers.Select(userId => $"<@{userId}>"));

                // 멘션된 유저들과 함께 뉴스 전송
                await SendMarketEventAlertAsync(embedBuilder.Build(), userMentions);
            }
            else
            {
                // 구독자가 없으면 그냥 뉴스만 전송
                await SendMarketEventAlertAsync(embedBuilder.Build());
            }


            var (searchCoinName, coinId) = await _dbManager.GetCoinIdByNameAsync(coinName);            
            await _dbManager.SaveCoinEventAsync(coinId, 0, false, true);

            double latestPrice = await _dbManager.GetCoinCurrentPriceAsync(coinId);
            double priceDropPercentage = _random.NextDouble() * (38 - 17) + 17;
            priceDropPercentage = Math.Round(priceDropPercentage, 2);
            double newPrice = latestPrice * (1 - (priceDropPercentage / 100));
            await _dbManager.UpdateCoinPriceAsync(coinId, newPrice);

            TimeSpan totalDelay = TimeSpan.FromHours(12); // 총 대기 시간
            TimeSpan delayInterval = TimeSpan.FromMinutes(30); // 30분마다 남은 시간 출력
            TimeSpan remainingTime = totalDelay;

            // 24시간 동안 10분 간격으로 로그를 출력하며 대기
            while (remainingTime > TimeSpan.Zero)
            {
                await Task.Delay(delayInterval);
                remainingTime -= delayInterval;

                // 남은 시간을 콘솔에 출력
                Console.WriteLine($"{coinName} 상장 폐지까지 남은 시간: {remainingTime.Hours}시간 {remainingTime.Minutes}분");
            }

            await _dbManager.DeleteCoinAsync(coinId);
            await _dbManager.DeleteCoinEventAsync(coinId);

            string deletionMessage = $"{coinName} 코인이 상장 폐지되었습니다.";
            await SendMarketEventAlertAsync(new EmbedBuilder()
                .WithTitle(":exclamation: 상장 폐지 완료")
                .WithDescription(deletionMessage)
                .WithColor(Discord.Color.DarkRed)
                .Build());
        }

        // 급등후 급락 이벤트
        public async Task HandlePriceSurgeAndFallEventAsync(int coinId)
        {
            Console.WriteLine("급등후 급락 이벤트 실행");
            int eventCount = _random.Next(3, 8); // 3~7번의 급등 이벤트
            int beforeEventCount = eventCount;
            bool isSurge = true; // 급등 이벤트

            // 급등 이벤트를 추가
            await _dbManager.SaveCoinEventAsync(coinId, eventCount, isSurge, false);

            string randomSurgeNews = GetRandomSurgeNews();
            string randomReporter = reporterNames[_random.Next(reporterNames.Count)];
            string beforeTime = DateTime.Now.ToString("MM월 dd일 HH:mm");

            if (!isNewsSent)
            {
                isNewsSent = true;
                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"📈 {beforeTime} - 호롤로 뉴스")
                    .WithDescription($"{randomSurgeNews}")
                    .WithColor(Discord.Color.Green)
                    .WithFooter($"호롤일보 {randomReporter} 기자 babo@holol.com");

                // 뉴스 메시지 전송
                if (_subscribedUsers.Count > 0)
                {
                    // 구독한 유저들의 멘션 문자열 생성
                    string userMentions = string.Join(" ", _subscribedUsers.Select(userId => $"<@{userId}>"));

                    // 멘션된 유저들과 함께 뉴스 전송
                    await SendMarketEventAlertAsync(embedBuilder.Build(), userMentions);
                }
                else
                {
                    // 구독자가 없으면 그냥 뉴스만 전송
                    await SendMarketEventAlertAsync(embedBuilder.Build());
                }

                
            }
            

            while (eventCount > 0)
            {
                await Task.Delay(TimeSpan.FromMinutes(10));

                var (EventCount, IsSurge, IsDelisted) = await _dbManager.GetCoinEventAsync(coinId);
                eventCount = EventCount;
            }

            eventCount = beforeEventCount + _random.Next(1, 4); // 급등 횟수에 1 ~ 3의 숫자를 더하기
            isSurge = false; // 급락 이벤트

            // 급락 이벤트를 추가
            await _dbManager.SaveCoinEventAsync(coinId, eventCount, isSurge, false);

            string currentTime = DateTime.Now.ToString("MM월 dd일 HH:mm");

            if (!isFUNewsSent)
            {
                isFUNewsSent = true;
                var plungeEmbedBuilder = new EmbedBuilder()
                .WithTitle($"📉 {currentTime} - 호롤로 뉴스 정정보도")
                .WithDescription($"{beforeTime}에 보도되었던 호롤시장의 호재 뉴스가 사실이 아닌것으로 확인되었습니다. 여러 소셜 미디어와 뉴스 매체를 통해 파트너십 체결을 통해 막대한 성장을 이루고 있다는 가짜 정보를 기반으로 퍼져나갔으나 사실무근으로 밝혀졌습니다. 이로 인해 투자자들의 패닉셀이 일어나고 있습니다. 전문가들은 이번 가짜 뉴스에 따른 변동성이 다소 과장된 측면이 있음을 언급하며, 시간이 지나면서 시장이 안정될 가능성이 높다고 보고 있습니다.")
                .WithColor(Discord.Color.Red)
                .WithFooter($"호롤일보 {randomReporter} 기자 babo@holol.com");

                // 뉴스 메시지 전송
                if (_subscribedUsers.Count > 0)
                {
                    // 구독한 유저들의 멘션 문자열 생성
                    string userMentions = string.Join(" ", _subscribedUsers.Select(userId => $"<@{userId}>"));

                    // 멘션된 유저들과 함께 뉴스 전송
                    await SendMarketEventAlertAsync(plungeEmbedBuilder.Build(), userMentions);
                }
                else
                {
                    // 구독자가 없으면 그냥 뉴스만 전송
                    await SendMarketEventAlertAsync(plungeEmbedBuilder.Build());
                }
                
            }              
        }

        public async Task HandlePriceDropAndSurgeEventAsync(int coinId)
        {
            Console.WriteLine("급락후 급등 이벤트 실행");
            int eventCount = _random.Next(3, 8); // 3~7번의 급락 이벤트
            int beforeEventCount = eventCount;
            bool isSurge = false; // 급락 이벤트

            // 급락 이벤트를 추가
            await _dbManager.SaveCoinEventAsync(coinId, eventCount, isSurge, false);

            string randomPlungeNews = GetRandomPlungeNews();
            string randomReporter = reporterNames[_random.Next(reporterNames.Count)];
            string beforeTime = DateTime.Now.ToString("MM월 dd일 HH:mm");

            if (!isNewsSent)
            {
                isNewsSent = true;
                var embedBuilder = new EmbedBuilder()
                    .WithTitle($"📉 {beforeTime} - 호롤로 뉴스")
                    .WithDescription($"{randomPlungeNews}")
                    .WithColor(Discord.Color.Red)
                    .WithFooter($"호롤일보 {randomReporter} 기자 babo@holol.com");

                // 뉴스 메시지 전송
                if (_subscribedUsers.Count > 0)
                {
                    // 구독한 유저들의 멘션 문자열 생성
                    string userMentions = string.Join(" ", _subscribedUsers.Select(userId => $"<@{userId}>"));

                    // 멘션된 유저들과 함께 뉴스 전송
                    await SendMarketEventAlertAsync(embedBuilder.Build(), userMentions);
                }
                else
                {
                    // 구독자가 없으면 그냥 뉴스만 전송
                    await SendMarketEventAlertAsync(embedBuilder.Build());
                }

                
            }


            while (eventCount > 0)
            {
                await Task.Delay(TimeSpan.FromMinutes(10));

                var (EventCount, IsSurge, IsDelisted) = await _dbManager.GetCoinEventAsync(coinId);
                eventCount = EventCount;
            }

            eventCount = beforeEventCount + _random.Next(1, 4); // 급락 횟수에 1 ~ 3의 숫자를 더하기
            isSurge = true; // 급등 이벤트

            // 급등 이벤트를 추가
            await _dbManager.SaveCoinEventAsync(coinId, eventCount, isSurge, false);
          
            if (!isFUNewsSent)
            {
                isFUNewsSent = true;
                string currentTime = DateTime.Now.ToString("MM월 dd일 HH:mm");

                var SurgeEmbedBuilder = new EmbedBuilder()
                .WithTitle($"📈 {currentTime} - 호롤코인 반등뉴스")
                .WithDescription($"{beforeTime}에 보도되었던 시장 악재로 인해 급락했던 코인들이 빠르게 반등하고 있습니다. 전문가들은 이번 반등이 예고되지 않은 외부 호재에 의해 촉발된 것으로 분석하고 있으며, 이번 반등이 더 강한 상승세로 이어질 가능성이 있다고 보고 있습니다. 글로벌 투자은행 **호롤은행**과 **요정은행**이 호롤코인의 여러 암호화폐에 대규모 투자 계획을 발표했는데 이것을 반등의 주요 원인으로 보고있습니다. 이번 반등은 많은 전문가들이 \"투자 기회\"로 보고 있으며, 추가 상승 가능성에 대한 기대가 커지고 있습니다.")
                .WithColor(Discord.Color.Green)
                .WithFooter($"호롤일보 {randomReporter} 기자 babo@holol.com");

                // 뉴스 메시지 전송
                if (_subscribedUsers.Count > 0)
                {
                    // 구독한 유저들의 멘션 문자열 생성
                    string userMentions = string.Join(" ", _subscribedUsers.Select(userId => $"<@{userId}>"));

                    // 멘션된 유저들과 함께 뉴스 전송
                    await SendMarketEventAlertAsync(SurgeEmbedBuilder.Build(), userMentions);
                }
                else
                {
                    // 구독자가 없으면 그냥 뉴스만 전송
                    await SendMarketEventAlertAsync(SurgeEmbedBuilder.Build());
                }
                
            }
        }
        public async Task RunGreatDepressionEventAsync()
        {
            // 랜덤으로 대공황 뉴스 선택            
            string selectedNews = depressionNews[_random.Next(depressionNews.Count)];
            string currentTime = DateTime.Now.ToString("MM월 dd일 HH:mm");
            string randomReporter = reporterNames[_random.Next(reporterNames.Count)];

            // 대공황 뉴스 출력
            var embedBuilder = new EmbedBuilder()
                .WithTitle($"📉 {currentTime} 대공황 뉴스 속보")
                .WithDescription(selectedNews)
                .WithColor(Discord.Color.Red)
                .WithFooter($"호롤일보 {randomReporter} 기자 babo@holol.com");


            if (_subscribedUsers.Count > 0)
            {
                // 구독한 유저들의 멘션 문자열 생성
                string userMentions = string.Join(" ", _subscribedUsers.Select(userId => $"<@{userId}>"));

                // 멘션된 유저들과 함께 뉴스 전송
                await SendMarketEventAlertAsync(embedBuilder.Build(), userMentions);
            }
            else
            {
                // 구독자가 없으면 그냥 뉴스만 전송
                await SendMarketEventAlertAsync(embedBuilder.Build());
            }

            // 코인 시장에 대공황 효과 적용 (랜덤으로 몇 개의 코인 급락)
            var allCoins = await _dbManager.GetAllCoinsAsync();
            int affectedCoinCount = _random.Next(7, 11); // 7~10개의 코인에 급락 효과 적용
            var selectedCoins = allCoins.OrderBy(_ => _random.Next()).Take(affectedCoinCount).ToList();

            foreach (var coin in selectedCoins)
            {
                _eventCoins.Add(coin.CoinId);
            }

            int count = 10;           
            while (count > 0)
            {
                string coinNews = "";
                foreach (var coin in selectedCoins)
                {
                    double latestPrice = await _dbManager.GetCoinCurrentPriceAsync(coin.CoinId);
                    if(latestPrice < 0)
                    {
                        latestPrice = coin.CurrentPrice;
                    }
                    prePriceCoins[coin.CoinId] = latestPrice;
                    await _dbManager.SavePreviousPriceAsync(coin.CoinId, latestPrice);
                    currentTime = DateTime.Now.ToString("MM월 dd일 HH:mm");

                    double priceDropPercentage;
                    // 코인 가격을 랜덤하게 8% ~ 15% 하락시킴 처음은 21% ~ 40%
                    if (count == 10)
                    {
                        priceDropPercentage = _random.NextDouble() * (40 - 21) + 21;
                        
                    }
                    else
                    {
                        priceDropPercentage = _random.NextDouble() * (15 - 8) + 8;
                    }
                    priceDropPercentage = Math.Round(priceDropPercentage, 2);
                    double newPrice = latestPrice * (1 - (priceDropPercentage / 100));

                    // 가격 업데이트
                    await _dbManager.UpdateCoinPriceAsync(coin.CoinId, newPrice);
                    string coinName = coin.CoinName;
                    // 코인 가격 변화 알림
                    coinNews += $"**{coinName}**의 가격이 **{priceDropPercentage:N2}%** 하락하여 **{newPrice:N2}**:dollar: 로 떨어졌습니다!\n";                  
                }
                var DepEmbedBuilder = new EmbedBuilder()
                .WithTitle($"📉 {currentTime} 대공황 현황")
                .WithDescription(coinNews)
                .WithColor(Discord.Color.Red)
                .WithFooter($"호롤일보 {randomReporter} 기자 babo@holol.com");

                await SendMarketEventAlertAsync(DepEmbedBuilder.Build());

                count--;
                await Task.Delay(TimeSpan.FromMinutes(2));
            }

            foreach (var coin in selectedCoins)
            {
                _eventCoins.Remove(coin.CoinId);
            }

            var randomCoin = allCoins[_random.Next(allCoins.Count)];
            double priceSurgePercentage = _random.NextDouble() * (31 - 12) + 12;
            priceSurgePercentage = Math.Round(priceSurgePercentage, 2);

            string selectedCoinName = await _dbManager.GetCoinNameByIdAsync(randomCoin.CoinId);
            double rcLatestPrice = await _dbManager.GetCoinCurrentPriceAsync(randomCoin.CoinId);

            double rcNewPrice = rcLatestPrice * (1 + (priceSurgePercentage / 100));
            await _dbManager.UpdateCoinPriceAsync(randomCoin.CoinId, rcNewPrice);
            currentTime = DateTime.Now.ToString("MM월 dd일 HH:mm");

            var SurEmbedBuilder = new EmbedBuilder()
                .WithTitle($"📈 {currentTime} 패닉 매도 끝, 암호화폐 시장 반등?")
                .WithDescription($"대공황 사태로 인한 극심한 하락세를 겪은 호롤로시장의 코인들이 반등하며 회복세에 진입했습니다. 전문가들은 패닉 매도세가 진정되고, 새로운 개발 로드맵이 발표된게 이번 상승의 주요 원인이라고 분석하고 있습니다. 암호화폐 시장이 전반적으로 회복 조짐을 보이고있고 {selectedCoinName}은 {priceSurgePercentage:N2}% 상승률을 기록하며 그 선두에 서 있습니다.")
                .WithColor(Discord.Color.Green)
                .WithFooter($"호롤일보 {randomReporter} 기자 babo@holol.com");            

            if (_subscribedUsers.Count > 0)
            {
                // 구독한 유저들의 멘션 문자열 생성
                string userMentions = string.Join(" ", _subscribedUsers.Select(userId => $"<@{userId}>"));

                // 멘션된 유저들과 함께 뉴스 전송
                await SendMarketEventAlertAsync(SurEmbedBuilder.Build(), userMentions);
            }
            else
            {
                // 구독자가 없으면 그냥 뉴스만 전송
                await SendMarketEventAlertAsync(SurEmbedBuilder.Build());
            }
            await HandlePriceSurgeEventAsync(randomCoin.CoinId);
        }

        public async Task TestSurgeAsync()
        {
            double priceSurgePercentage = _random.NextDouble() * (31 - 12) + 12;
            priceSurgePercentage = Math.Round(priceSurgePercentage, 2);

            string selectedCoinName = "밍스테리움";
            double rcLatestPrice = await _dbManager.GetCoinCurrentPriceAsync(10);

            double rcNewPrice = rcLatestPrice * (1 + (priceSurgePercentage / 100));
            await _dbManager.UpdateCoinPriceAsync(10, rcNewPrice);
            string currentTime = DateTime.Now.ToString("MM월 dd일 HH:mm");

            var SurEmbedBuilder = new EmbedBuilder()
                .WithTitle($"📈 {currentTime} 패닉 매도 끝, 암호화폐 시장 반등?")
                .WithDescription($"대공황 사태로 인한 극심한 하락세를 겪은 호롤로시장의 코인들이 반등하며 회복세에 진입했습니다. 전문가들은 패닉 매도세가 진정되고, 새로운 개발 로드맵이 발표된게 이번 상승의 주요 원인이라고 분석하고 있습니다. 암호화폐 시장이 전반적으로 회복 조짐을 보이고있고 {selectedCoinName}은 {priceSurgePercentage:N2}% 상승률을 기록하며 그 선두에 서 있습니다.")
                .WithColor(Discord.Color.Green)
                .WithFooter($"호롤일보 김띵또 기자 babo@holol.com");

            if (_subscribedUsers.Count > 0)
            {
                // 구독한 유저들의 멘션 문자열 생성
                string userMentions = string.Join(" ", _subscribedUsers.Select(userId => $"<@{userId}>"));

                // 멘션된 유저들과 함께 뉴스 전송
                await SendMarketEventAlertAsync(SurEmbedBuilder.Build(), userMentions);
            }
            else
            {
                // 구독자가 없으면 그냥 뉴스만 전송
                await SendMarketEventAlertAsync(SurEmbedBuilder.Build());
            }
            await HandlePriceSurgeEventAsync(10);
            await HandlePriceSurgeEventAsync(1);
            await HandlePriceSurgeEventAsync(6);
            await HandlePriceSurgeEventAsync(7);
            await HandlePriceSurgeEventAsync(17);
        }
        public async Task StartEventSchedulerAsync()
        {
            while (true)
            {
                var currentTime = DateTime.Now;

                if (currentTime.Hour >= 2 && currentTime.Hour < 12)
                {
                    // 02:00~12:00은 이벤트가 발생하지 않도록 대기
                    isAfternoonEventTriggered = false;
                    isNightEventTriggered = false;
                    await Task.Delay(TimeSpan.FromHours(12 - currentTime.Hour));                   
                    continue;
                }

                currentTime = DateTime.Now;

                // 오후: 12시~17시 랜덤 시간으로 이벤트 발생 (급등 또는 급락)
                if ((currentTime.Hour >= 12 && currentTime.Hour < 17) && !isAfternoonEventTriggered)
                {
                    int randomDelay = _random.Next(1, 300);
                    await Task.Delay(TimeSpan.FromMinutes(randomDelay));

                    isNewsSent = false;
                    isFUNewsSent = false;
                    isAfternoonEventTriggered = true;
                    await TriggerEventAsync(true); // 오후 이벤트 발생
                    Console.WriteLine("오후 코인 이벤트 실행");
                }
                // 밤: 21시~02시에 이벤트 발생 (급락)
                else if ((currentTime.Hour >= 18 && currentTime.Hour < 23) && !isNightEventTriggered )
                {
                    int randomDelay = _random.Next(1, 240);
                    await Task.Delay(TimeSpan.FromMinutes(randomDelay));

                    isNewsSent = false;
                    isFUNewsSent = false;
                    isNightEventTriggered = true;
                    await TriggerEventAsync(false); // 밤 이벤트 발생
                    Console.WriteLine("밤 코인 이벤트 실행");
                }
                else
                {
                    await Task.Delay(TimeSpan.FromHours(1));
                }
            }
        }

        public async Task SendMarketEventAlertAsync(Embed embed, string userMentions = "")
        {
            await EventManager.NewsNotification(embed, userMentions);
        }
        public async Task<(string, int)> GetProfitRankingEmbedAsync(SocketGuild guild, int page)
        {
            try
            {
                // 수익/손해 기록 가져오기
                var profitRankings = await _dbManager.GetUserProfitRankingAsync();
                var allUsersHoldings = new Dictionary<ulong, double>();

                // 모든 유저의 보유 코인 가져오기
                var allHoldings = await _dbManager.GetAllUserCoinHoldingsAsync();

                // 각 유저의 보유 코인 가치를 계산하고, UserId와 함께 딕셔너리에 추가
                foreach (var holding in allHoldings)
                {
                    var (userId, coinHoldings) = holding;
                    double totalHoldingsValue = 0.0;

                    foreach (var (coinName, totalQuantity, averagePrice) in coinHoldings)
                    {
                        var (coinId, currentPrice, symbol, fullCoinName) = await _dbManager.GetCoinPriceAsync(coinName);
                        totalHoldingsValue += (totalQuantity * currentPrice) - (totalQuantity * averagePrice);
                    }

                    allUsersHoldings[userId] = totalHoldingsValue;
                }

                // 유저별 총 수익/손실을 계산
                var rankingsWithNetProfit = new List<(ulong UserId, string Nickname, double TotalNetProfit, double TotalHoldingsValue)>();

                // 수익/손해 기록과 보유 코인 가치를 병합
                foreach (var userProfit in profitRankings)
                {
                    double totalProfit = userProfit.TotalProfit;
                    double holdingsValue = allUsersHoldings.ContainsKey(userProfit.UserId) ? allUsersHoldings[userProfit.UserId] : 0.0;

                    rankingsWithNetProfit.Add((userProfit.UserId, $"<@{userProfit.UserId}>", totalProfit + holdingsValue, holdingsValue));
                }

                // 보유 중인 코인만 있는 유저도 랭킹에 포함
                foreach (var userHolding in allUsersHoldings)
                {
                    if (!profitRankings.Any(r => r.UserId == userHolding.Key))
                    {
                        rankingsWithNetProfit.Add((userHolding.Key, $"<@{userHolding.Key}>", userHolding.Value, userHolding.Value));
                    }
                }

                // 총 수익이 큰 순서대로 정렬
                rankingsWithNetProfit = rankingsWithNetProfit
                    .OrderByDescending(r => r.TotalNetProfit)
                    .ToList();

                int totalUsers = rankingsWithNetProfit.Count;
                int rank = (page - 1) * 10 + 1;  // 페이지에 따라 시작 순위가 달라짐

                // 페이지에 맞는 10명의 랭킹 정보 가져오기
                rankingsWithNetProfit = rankingsWithNetProfit.Skip((page - 1) * 10).Take(10).ToList();

                // 임베드 메시지에 순위대로 출력
                var rankingsBuilder = new StringBuilder();

                foreach (var (userId, nickname, totalNetProfit, totalHoldingsValue) in rankingsWithNetProfit)
                {
                    rankingsBuilder.AppendLine($"{rank}위 {nickname} : {totalNetProfit:N2} :dollar: (보유 코인 평가 금액: {totalHoldingsValue:N2})");
                    rank++;
                }

                return (rankingsBuilder.ToString(), totalUsers);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetProfitRankingEmbedAsync에서 에러 발생 : {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return (null, 0);
            }
        }
        private string GetRandomSurgeNews()
        {
            // 기업 이름 랜덤 선택
            string randomCompanyName = companyNames[_random.Next(companyNames.Count)];

            // 은행 이름 랜덤 선택
            string randomBankName = bankNames[_random.Next(bankNames.Count)];

            // 첫 뉴스의 ~억 값 랜덤 (5 ~ 20억)
            int randomInvestment = _random.Next(5, 21);

            // 뉴스 리스트에서 랜덤 선택
            string selectedNews = surgeNewsList[_random.Next(surgeNewsList.Count)];

            // 뉴스 텍스트에 값 대입
            if (selectedNews.StartsWith("~(는)은"))
            {
                selectedNews = selectedNews.Replace("~(는)은", $"{randomCompanyName}(는)은")
                                           .Replace("~억", $"{randomInvestment}억");
            }
            else if (selectedNews.StartsWith("~(은)는"))
            {
                selectedNews = selectedNews.Replace("~(은)는", $"{randomBankName}(은)는");
            }

            return selectedNews;
        }

        private string GetRandomPlungeNews()
        {
            // 기업 이름 랜덤 선택
            string randomCompanyName = companyNames[_random.Next(companyNames.Count)];

            // 은행 이름 랜덤 선택
            string randomBankName = bankNames[_random.Next(bankNames.Count)];

            // 첫 뉴스의 ~억 값 랜덤 (5 ~ 20억)
            int randomInvestment = _random.Next(5, 21);

            // 뉴스 리스트에서 랜덤 선택
            string selectedNews = dropNewsList[_random.Next(dropNewsList.Count)];

            // 뉴스 텍스트에 값 대입
            if (selectedNews.StartsWith("~(는)은"))
            {
                selectedNews = selectedNews.Replace("~(는)은", $"{randomCompanyName}(는)은")
                                           .Replace("~억", $"{randomInvestment}억");
            }
            else if (selectedNews.StartsWith("~(은)는"))
            {
                selectedNews = selectedNews.Replace("~(은)는", $"{randomBankName}(은)는");
            }

            return selectedNews;
        }

        private string GetRandomDelistingNews(string coinName)
        {
            // 뉴스 리스트에서 랜덤 선택
            string selectedNews = delistingNewsList[_random.Next(delistingNewsList.Count)];

            string currentTime = DateTime.Now.AddHours(12).ToString("MM월 dd일 HH:mm");

            selectedNews = selectedNews.Replace("[코인 이름]", coinName);

            selectedNews = selectedNews.Replace("[폐지 날짜]", currentTime);

            return selectedNews;
        }

        public async Task CoinPriceSurgeEventAsync(int coinId)
        {
            int eventCount = _random.Next(3, 6); // 3~5번의 급등 이벤트
            bool isSurge = true; // 급등 이벤트

            // 이벤트를 DB에 저장
            await _dbManager.SaveCoinEventAsync(coinId, eventCount, isSurge, false);

            Console.WriteLine($"{coinId} 코인에 대한 급등 이벤트가 {eventCount}회로 설정되었습니다.");
        }

        public async Task CoinPricePlungeEventAsync(int coinId)
        {
            int eventCount = _random.Next(3, 6); // 3~5번의 급락 이벤트
            bool isSurge = false; // 급락 이벤트

            // 이벤트를 DB에 저장
            await _dbManager.SaveCoinEventAsync(coinId, eventCount, isSurge, false);

            Console.WriteLine($"{coinId} 코인에 대한 급락 이벤트가 {eventCount}회로 설정되었습니다.");
        }

        // 구독한 사용자 불러오기
        private void LoadSubscribers()
        {
            if (File.Exists(SubscriptionFilePath))
            {
                var jsonData = File.ReadAllText(SubscriptionFilePath);
                _subscribedUsers = JsonConvert.DeserializeObject<HashSet<ulong>>(jsonData) ?? new HashSet<ulong>();
            }
        }

        // 구독한 사용자 저장하기
        private async Task SaveSubscribersAsync()
        {
            var jsonData = JsonConvert.SerializeObject(_subscribedUsers, Formatting.Indented);
            await File.WriteAllTextAsync(SubscriptionFilePath, jsonData); // 비동기 파일 쓰기
        }



        // 사용자 구독 추가
        public async Task<bool> SubscribeUser(ulong userId)
        {
            if (!_subscribedUsers.Contains(userId))
            {
                _subscribedUsers.Add(userId);
                await SaveSubscribersAsync();
                return true;
            }
            return false;
        }

        // 사용자 구독 해제
        public async Task<bool> UnsubscribeUser(ulong userId)
        {
            if (_subscribedUsers.Contains(userId))
            {
                _subscribedUsers.Remove(userId);
                await SaveSubscribersAsync();
                return true;
            }
            return false;
        }

        public static async Task ResetCoinRecord()
        {
            Console.WriteLine("코인 기록 삭제중...");
            bool deleteCoinHolding = await _dbManager.DeleteAllUserCoinHoldingsAsync();
            bool deleteProfit = await _dbManager.DeleteAllUserProfitRecordsAsync();
            bool deleteBalances = await _dbManager.ResetDollarBalancesAsync();
            bool deleteAutoTrades = await _dbManager.DeleteAllAutoTradeConditionAsync();
            bool isSuccess = deleteCoinHolding && deleteProfit && deleteBalances && deleteAutoTrades;

            if (deleteCoinHolding)
            {
                Console.WriteLine("유저 코인 보유 기록 삭제 완료");
            }
            if (deleteProfit)
            {
                Console.WriteLine("유저 코인 거래 기록 삭제 완료");
            }
            if (deleteBalances)
            {
                Console.WriteLine("유저 보유 달러 삭제 완료");
            }
            if (deleteAutoTrades)
            {
                Console.WriteLine("자동매매 기록 삭제 완료");
            }
            if (isSuccess)
            {
                Console.WriteLine("초기화 로직 성공");
            }
            else
            {
                Console.WriteLine("초기화 로직중 실패 요소 존재");
            }
        }

        // 급등 뉴스 메시지 목록
        private readonly List<string> surgeNewsList = new List<string>
        {
            "~(는)은 특정 코인들에 약 ~억 달러를 투자한 것으로 밝혀졌습니다. 최고경영자 ㅇㅇ은 향후 자산의 일부를 더 투자할 수 있다고 밝혔으며, 가까운 미래에 자사의 제품의 결제 수단으로 코인을 용인할 것으로 기대한다며 말을 덧붙였습니다.",
            "~(는)은 디지털 화폐(CBDC)를 도입할 것이고 가까운 미래에 시험 운영을 진행하겠다고 발표했습니다. 이 발표로 인해 호롤로 서버 내 암호화폐 산업 전반에 긍정적인 영향을 미칠것이며, 현재 코인 시장의 특정 코인들의 가격이 상승하고있는 요인일 것이라는 전문가의 분석이 있었습니다.",
            "증권거래위원회(SEC)가 코인 선물 기반 ETF를 승인한다는 발표가 있었습니다. 이번 발표로 인해 전문가들은 호롤코인이 전통적인 금융 시장에서 정식 투자 수단으로 자리 잡을 가능성이 높아져 암호화폐 시장 전반에 긍정적인 형향을 미칠 것이라 전망해 투자자들의 관심을 받고 있습니다.",
            "최근 호롤 코인 시장에서의 특정 코인들의 거래량이 급증하여, 해당 코인 가격이 지속적으로 상승하고 있다는 소식입니다. 전문가들은 이 거래량 급증이 기관 투자자와 대형 펀드의 대규모 매입 때문일 가능성이 크다고 분석하고 있습니다. 해당 코인의 기술적 혁신과 향후 로드맵에 대한 기대감이 투자 심리를 자극한 것으로 보입니다. 이로 인해 투자자들은 더욱더 매수에 나서고 있으며, 가격 상승세는 당분간 계속될 것으로 예상됩니다.",
            "코인 관련 규제 완화 소식으로 인해 투자자들의 매수세가 급격히 늘고있습니다. 최근 발표된 정부의 가상화폐 관련 규제 완화 정책이 투자자들 사이에서 긍정적으로 받아들여지고 있습니다. 이번 규제 완화에는 가상자산 거래소의 허가 요건 완화, 해외 거래소에 대한 접근 제한 해제, 그리고 개인 투자자 보호 강화 방안이 포함되어 있습니다.",
            "호롤일보, 버섯뉴스 등 주요 미디어에서 호롤 코인의 미래 가능성을 언급하며 투자 열기가 뜨겁습니다. 최근 미디어에서 특정 코인의 혁신적인 기술과 잠재력을 집중 조명했습니다. 전문가들은 \"해당 코인이 곧 주요 거래서에 상장될 가능성이 크다\"고 발언하여 더욱 투자자들의 관심을 받고 있습니다. "
        };

        // 급락 뉴스 메시지 목록
        private readonly List<string> dropNewsList = new List<string>
        {
            "~(는)은 최근 대규모 해킹 사건을 겪으며 심각한 보안 문제를 노출했습니다. 해킹으로 인해 약 ~억 달러 규모의 자산이 탈취된 것으로 알려졌으며, 이로 인해 투자자들의 불안감이 커지며 코인 가격이 급락하고 있습니다. 최고경영자는 \"빠른 시일 내에 시스템을 복구하겠다\"고 밝혔으나, 시장의 반응은 여전히 냉담한 상황입니다.",
            "~(는)은 최근 발표한 회계 보고서에서 상당한 손실을 보고하며, 암호화폐 관련 투자 자산의 ~억 상당의 대규모 매각을 시사했습니다. 이에 따라 시장에서는 해당 기업이 보유한 코인의 대량 매도가 시작되었고, 가격이 급락하고 있습니다. 전문가들은 \"이러한 매각이 시장 전체에 큰 영향을 미칠 것\"이라고 경고하고 있습니다.",
            "최근 정부가 발표한 새로운 규제로 인해 특정 코인들이 거래 중단 위기에 처해있습니다. 규제를 도입하며, 코인 거래소에 대해 강경한 입장을 밝힘에 따라 가격이 급격히 하락하고 있습니다. 투자자들은 패닉 셀링에 나서고 있으며, 이 상황이 얼마나 지속될지에 대한 불확실성이 커지고 있습니다.",
            "특정 코인들이 기술적 결함을 겪으며 시장에서 신뢰를 잃고 있습니다. 해당 코인의 주요 기술적 문제가 공개되면서, 개발팀이 이를 해결하지 못할 경우 프로젝트가 중단될 수 있다는 우려가 제기되고 있습니다. 이에 따라 투자자들은 빠르게 손절에 나서며 가격이 하락하고 있습니다.",
            "~(는)은 암호화폐 대출 및 거래 서비스를 축소할 계획을 발표하였습니다. 또한 최근 글로벌 주요 은행들이 암호화폐 관련 규제를 강화할 것이라는 소식이 전해져 암호화폐 시장 전반에 부정적인 영향을 미치고 있습니다. 전문가들은 \"이 규제로 인해 코인 시장의 유동성이 줄어들 것\"이라고 경고하고 있습니다.",
            "암호화폐의 주요 거래소 상장 폐지 소식이 전해지면서 시장에서 특정 코인에 대한 신뢰가 급격히 하락하고 있습니다. 상장 폐지 이유는 불분명하지만, 투자자들은 불안감에 빠르게 매도에 나서며 가격이 폭락하고 있습니다.",
            "최근 주요 기관 투자자들이 코인에 대한 대규모 매도를 시작하며, 시장에서 가격이 급격히 하락하고 있습니다. 전문가들은 \"기관 투자자들의 매도는 더 큰 하락세의 신호일 수 있다\"고 분석하며, 투자자들은 경계심을 높이고 있습니다."
        };

        private readonly List<string> delistingNewsList = new List<string>
        {
            "**호롤 코인**은 최근 규제 강화에 따라 [코인 이름]을(를) 상장 폐지하기로 결정했습니다. \r\n이번 조치는 정부 규제에 따른 것이며, **호롤 코인**은 투자자 보호를 위한 조치라고 밝혔습니다. \r\n상장 폐지 후 [코인 이름]의 출금은 [폐지 날짜]까지 가능하니 투자자 여러분께서는 해당 일정에 유의하시기 바랍니다.",
            "**호롤 코인**은 지속적인 거래량 감소로 인해 [코인 이름]의 상장 폐지를 결정했습니다. \r\n이번 결정은 지난 몇 달 동안 [코인 이름]의 거래량이 크게 줄어들었기 때문으로, [폐지 날짜] 이후로는 거래 및 출금이 불가능해질 예정입니다. \r\n투자자 여러분께서는 폐지 전에 필요한 조치를 취하시길 바랍니다.",
            "**띵띵 프로잭트**의 개발 중단으로 인해 **호롤 코인**은 [코인 이름]을(를) 상장 폐지하기로 결정했습니다. \r\n프로젝트 측은 자금 부족 및 개발팀 해산을 공식 발표하였으며, 이로 인해 해당 코인의 거래 및 지원이 종료됩니다. 상장 폐지 후 [폐지 날짜]까지 출금이 가능하니 서둘러 조치하시기 바랍니다.",
            "**호롤 코인**은 [코인 이름]에서 발견된 심각한 보안 문제로 인해 상장 폐지를 결정했습니다. \r\n거래소 측은 해당 보안 결함이 사용자 자산에 위협을 줄 수 있다는 판단 하에 빠르게 조치를 취했으며, 해당 코인의 거래와 입출금은 [폐지 날짜]까지 가능합니다. \r\n투자자 여러분께서는 조속히 대응하시길 권장드립니다.",
            "[코인 이름]의 기술적 문제로 인해 **호롤 코인**에서 상장 폐지를 발표했습니다. \r\n블록체인 네트워크의 불안정성 및 거래 처리 지연 문제가 지속적으로 발생하면서, 거래소 측은 고객 자산 보호를 위해 상장 폐지를 결정했습니다. 해당 코인의 거래는 [폐지 날짜]까지 가능합니다.",
            "**호롤 코인**은 최근 법적 분쟁에 연루된 [코인 이름]의 상장 폐지를 발표했습니다. \r\n법적 문제로 인해 거래소 측은 더 이상 해당 코인을 거래할 수 없다고 판단했으며, 투자자들에게 신속한 출금을 권장하고 있습니다. 상장 폐지 날짜는 [폐지 날짜]로 설정되었으며, 이후 모든 거래가 중단됩니다.",
            "**띵띵 프로잭트**의 재정적 어려움으로 인해 **호롤 코인**은 [코인 이름]의 상장 폐지를 결정했습니다. \r\n이번 결정은 해당 프로젝트의 지속 가능성에 의문이 제기된 후 내려졌으며, 코인 보유자들은 [폐지 날짜]까지 자산을 이동할 수 있습니다. 투자자 여러분께서는 상장 폐지 전까지 반드시 필요한 조치를 취하시기 바랍니다."
        };

        private readonly List<string> depressionNews = new List<string>
        {
            "전 세계 경제가 큰 충격을 받고 있습니다. 뭉뭉금융, 햄스터뱅크 등 주요 글로벌 은행들의 대규모 파산 소식과 함께 금융시장이 무너지고 있으며, 코인 시장 역시 극심한 변동성을 보이고 있습니다. 투자자들이 패닉에 빠지며 주요 암호화폐가 일제히 급락하고 있습니다. 전문가들은 이번 사태가 1929년 대공황 이후 최악의 경제 위기라고 평가하고 있습니다.",
            "미국 연방준비제도의 대규모 금리 인상과 경제 불황이 겹치면서 전 세계적으로 경제가 무너지고 있습니다. 암호화폐 시장도 예외는 아니며, 특히 비트코인과 이더리움은 40% 이상 급락했습니다. 많은 투자자들이 공포에 질려 자산을 매도하고 있으며, 경제 전문가들은 이번 사태가 장기화될 가능성이 있다고 경고하고 있습니다.",
            "중국 경제의 급격한 성장 둔화와 함께 부동산 거품이 붕괴되면서 글로벌 경제에 심각한 타격을 주고 있습니다. 암호화폐 시장 또한 이러한 영향으로 인해 큰 폭으로 하락하고 있으며, 특히 호롤로 시장에서 활발히 거래되던 코인들이 급락 중입니다. 전문가들은 중국의 상황이 세계 경제에 미치는 영향이 지대할 것으로 예상하고 있습니다.",
            "유럽의 주요 은행들이 부채 문제로 인해 파산 위기에 처하면서 금융 시장이 큰 혼란에 빠졌습니다. 암호화폐 시장 또한 이러한 유럽발 금융 위기의 여파로 큰 폭의 하락세를 보이고 있으며, 많은 투자자들이 패닉셀을 진행하고 있습니다. 이에 따라 비트코인은 35% 하락하며 최근 몇 년간의 최저치를 기록했습니다.",
            "전 세계적인 인플레이션 급등으로 인해 경제가 위기를 맞고 있습니다. 암호화폐는 기존의 안전 자산으로 주목받았지만, 이번 경제 위기에서는 대규모 매도세에 직면하며 급락하고 있습니다. 주요 암호화폐가 50% 이상 하락하는 가운데, 투자자들은 불안에 휩싸여 있습니다. 전문가들은 이러한 상황이 한동안 계속될 수 있다고 우려하고 있습니다."
        };
        private readonly List<string> companyNames = new List<string>
        {
            "호롤코퍼레이션", "버섯테크", "비둘기자산운용", "햄붕캐피탈", "링링 인베스트먼트"
        };

        private readonly List<string> bankNames = new List<string>
        {
            "호롤은행", "요정은행", "뭉뭉금융", "햄스터뱅크", "지나캐피탈"
        };
        public readonly List<string> reporterNames = new List<string>
        {
            "김띵또", "김햄붕", "김만쥬", "박기준", "인이모", "안건호", "김된찌", "김떠비", "한백수", "어루비", "더스트킴"
        };
    }
}
