using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using StockDBApp.Data;
using StockDBApp.Models;
using Microsoft.EntityFrameworkCore;

namespace StockDBApp
{
    class StockApp
    {
        static readonly object _lock = new object();

        static async Task Main()
        {
            // Инициализация базы данных
            using (var context = new StockContext())
            {
                context.Database.Migrate();
            }

            List<string> tickers = new List<string>();

            using (StreamReader reader = new StreamReader("ticker.txt"))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    tickers.Add(line);
                }
            }

            List<Task> tasks = new List<Task>();
            foreach (string ticker in tickers)
            {
                tasks.Add(GetDataForTicker(ticker));
                System.Threading.Thread.Sleep(600);
            }
            await Task.WhenAll(tasks);

            // Анализ цен и заполнение таблицы TodaysCondition
            await AnalyzePrices();

            // Взаимодействие с пользователем
            Console.WriteLine("Введите тикер акции:");
            string userTicker = Console.ReadLine().ToUpper();

            using (var context = new StockContext())
            {
                var condition = await context.TodaysConditions
                    .FirstOrDefaultAsync(c => c.Ticker == userTicker);

                if (condition != null)
                {
                    Console.WriteLine($"Цена акции {userTicker} {condition.Condition} по сравнению с предыдущим днем.");
                }
                else
                {
                    Console.WriteLine("Данные по указанному тикеру не найдены.");
                }
            }
        }

        static async Task GetDataForTicker(string ticker)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string today = DateTime.Now.ToString("yyyy-MM-dd");
                    string oneYearAgo = DateTime.Now.AddMonths(-10).ToString("yyyy-MM-dd");

                    string url = $"https://api.marketdata.app/v1/stocks/candles/D/{ticker}/?from={oneYearAgo}&to={today}&token=RFU4aDItQlRLRjFuNDd5OVlMWGs4UGh6eXY5bldMWDRvS0xxOHctcmNLOD0";

                    HttpResponseMessage response = await client.GetAsync(url);

                    string responseContent = await response.Content.ReadAsStringAsync();
                    dynamic responceObject = Newtonsoft.Json.JsonConvert.DeserializeObject(responseContent);
                    if (responceObject != null && responceObject.t != null && responceObject.h != null && responceObject.l != null)
                    {
                        List<long> timestamps = responceObject?.t?.ToObject<List<long>>() ?? new List<long>();
                        List<double> highs = responceObject?.h?.ToObject<List<double>>() ?? new List<double>();
                        List<double> lows = responceObject?.l?.ToObject<List<double>>() ?? new List<double>();

                        using (var context = new StockContext())
                        {
                            var stock = await context.Stocks.FindAsync(ticker);
                            if (stock == null)
                            {
                                stock = new Stock { Ticker = ticker };
                                context.Stocks.Add(stock);
                            }

                            for (int i = 0; i < timestamps.Count; i++)
                            {
                                DateTime date = DateTimeOffset.FromUnixTimeMilliseconds(timestamps[i]).DateTime;
                                double averagePrice = (highs[i] + lows[i]) / 2;

                                var price = new Price
                                {
                                    Ticker = ticker,
                                    Date = date,
                                    AveragePrice = averagePrice
                                };

                                context.Prices.Add(price);
                            }

                            await context.SaveChangesAsync();
                            Console.WriteLine($"Данные по {ticker} успешно сохранены в базу данных.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Ошибка при обработке {ticker}: Отсутствуют данные.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при обработке {ticker}: {ex.Message}");
                }
            }
        }

        static async Task AnalyzePrices()
        {
            using (var context = new StockContext())
            {
                var stocks = await context.Stocks.Include(s => s.Prices).ToListAsync();

                foreach (var stock in stocks)
                {
                    var latestPrices = await context.Prices
                        .Where(p => p.Ticker == stock.Ticker)
                        .OrderByDescending(p => p.Date)
                        .Take(2)
                        .ToListAsync();

                    if (latestPrices.Count >= 2)
                    {
                        var todayPrice = latestPrices[0];
                        var yesterdayPrice = latestPrices[1];

                        string condition = todayPrice.AveragePrice > yesterdayPrice.AveragePrice ? "Выросла" : "Упала";

                        var todaysCondition = await context.TodaysConditions
                            .FirstOrDefaultAsync(c => c.Ticker == stock.Ticker);

                        if (todaysCondition == null)
                        {
                            todaysCondition = new TodaysCondition
                            {
                                Ticker = stock.Ticker,
                                Condition = condition,
                                Date = todayPrice.Date
                            };
                            context.TodaysConditions.Add(todaysCondition);
                        }
                        else
                        {
                            todaysCondition.Condition = condition;
                            todaysCondition.Date = todayPrice.Date;
                            context.TodaysConditions.Update(todaysCondition);
                        }
                    }
                }

                await context.SaveChangesAsync();
                Console.WriteLine("Анализ цен завершен.");
            }
        }
    }
}
