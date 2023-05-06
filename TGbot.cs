using Microsoft.Playwright;
using PuppeteerSharp;
using Quartz;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TG_Stock_Bot
{
    [DisallowConcurrentExecution]

    public class TGbot : IJob
    {
        #region 基本參數
        //Time
        int year;
        int month;
        int day;
        int hour;
        int minute;
        int second;

        //Messages and user info
        long chatId = 0;
        string messageText;
        int messageId;
        string firstName;
        string lastName;
        long id;
        Message sentMessage;
        int StockNumber;

        //股價資訊
        Dictionary<int, string> InfoDic = new Dictionary<int, string>()
            {
               { 0, "開盤價"},{ 1, "最高價"},{ 2, "成交量"},
               { 3, "昨日收盤價"},{ 4, "最低價"},{ 5, "成交額"},
               { 6, "均價"},{ 7, "本益比"},{ 8, "市值"},
               { 9, "振幅"},{ 10, "周轉率"},{ 11, "發行股"},
               { 12, "漲停"},{ 13, "52W高"},{ 14, "內盤量"},
               { 15, "跌停"},{ 16, "52W低"},{ 17, "外盤量"},
               { 18, "近四季EPS"},{ 19, "當季EPS"},{ 20, "毛利率"},
               { 21, "每股淨值"},{ 22, "本淨比"},{ 23, "營利率"},
               { 24, "年股利"},{ 25, "殖利率"},{ 26, "淨利率"},
            };

        #endregion

        public async Task Execute(IJobExecutionContext context1)
        {
            //Read time and save variables
            year = int.Parse(DateTime.UtcNow.Year.ToString());
            month = int.Parse(DateTime.UtcNow.Month.ToString());
            day = int.Parse(DateTime.UtcNow.Day.ToString());
            hour = int.Parse(DateTime.UtcNow.Hour.ToString());
            minute = int.Parse(DateTime.UtcNow.Minute.ToString());
            second = int.Parse(DateTime.UtcNow.Second.ToString());

            Console.WriteLine("Data: " + year + "/" + month + "/" + day);
            Console.WriteLine("Time: " + hour + ":" + minute + ":" + second);

            #region 設定TG_BOT
            //Bot
            var botClient = new TelegramBotClient("1546506272:AAFeRnuJOsjfxYIo_LcsSOMRgIfl24v5fzY");

            var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { } // receive all update types
            };
            botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken: cts.Token);

            var me = await botClient.GetMeAsync();

            Console.WriteLine($"\nHello! I'm {me.Username} and i'm your Bot!");

            #endregion

            async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
            {
                try
                {
                    // Only process Message updates: https://core.telegram.org/bots/api#message
                    if (update.Type != UpdateType.Message)
                        return;
                    // Only process text messages
                    if (update.Message!.Type != MessageType.Text)
                        return;

                    #region 初始化參數
                    chatId = update.Message.Chat.Id;
                    messageText = update.Message.Text;
                    messageId = update.Message.MessageId;
                    firstName = update.Message.From.FirstName;
                    lastName = update.Message.From.LastName;
                    id = update.Message.From.Id;
                    year = update.Message.Date.Year;
                    month = update.Message.Date.Month;
                    day = update.Message.Date.Day;
                    hour = update.Message.Date.Hour;
                    minute = update.Message.Date.Minute;
                    second = update.Message.Date.Second;

                    Console.WriteLine(" message --> " + year + "/" + month + "/" + day + " - " + hour + ":" + minute + ":" + second);
                    Console.WriteLine($"Received a '{messageText}' message in chat {chatId} from user:\n" + firstName + " - " + lastName + " - " + " 5873853");

                    messageText = messageText.ToLower();
                    #endregion

                    if (messageText == "/start" || messageText == "hello")
                    {
                        // Echo received message text
                        sentMessage = await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Hello " + firstName + " " + lastName + "",
                        cancellationToken: cancellationToken);
                    }
                    else if (messageText.Split().ToList().Count >= 2)
                    {
                        var text = messageText.Split().ToList();
                        int.TryParse(text[1], out StockNumber);
                        //if (!int.TryParse(text[1], out StockNumber)) return;
                        
                        #region 建立瀏覽器

                        await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
                        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
                        {
                            //ExecutablePath = "/root/.cache/ms-playwright/chromium-1055/chrome-linux/chrome",
                            Args = new[] {
                                "--disable-dev-shm-usage",
                                "--disable-setuid-sandbox",
                                "--no-sandbox",
                                "--disable-gpu"
                            },
                            Headless = true,
                        });
                        using var page = await browser.NewPageAsync();
                        await page.SetViewportAsync(new ViewPortOptions
                        {
                            Width = 1920,
                            Height = 1080
                        });

                        Console.WriteLine($"Browser is Setting");
                        #endregion

                        #region 測試
                        if (messageText.Contains("/url"))
                        {
                            if (text.Count == 2)
                            {
                                Console.WriteLine($"讀取網站中...");
                                WaitUntilNavigation[] waitUntil = new[] { WaitUntilNavigation.Networkidle0, WaitUntilNavigation.Networkidle2, WaitUntilNavigation.DOMContentLoaded, WaitUntilNavigation.Load };

                                await page.GoToAsync($"{text[1]}", 6000 ,waitUntil);
                                Console.WriteLine($"存取圖片中...");
                                Stream stream = new MemoryStream(await page.ScreenshotDataAsync());
                                sentMessage = await botClient.SendPhotoAsync(
                                chatId: chatId,
                                photo: stream,
                                parseMode: ParseMode.Html,
                                cancellationToken: cancellationToken);
                            }
                        }
                        #endregion
                    }
                }
                catch (ApiRequestException ex)
                {
                    Console.WriteLine(ex.Message);
                    sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: $"錯誤：{ex.Message}",
                            cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.Message);
                    sentMessage = await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: $"錯誤：{ex.Message}",
                            cancellationToken: cancellationToken);
                }
            }

            Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
            {
                var ErrorMessage = exception switch
                {
                    ApiRequestException apiRequestException
                        => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                    _ => exception.ToString()
                };
                Console.WriteLine(ErrorMessage);
                cts.Cancel();
                botClient.CloseAsync(cancellationToken);
                return Task.CompletedTask;
            }

        }

    }
}
