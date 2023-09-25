using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace HareHundlerBot
{
    class Program
    {
        private static string token { get; set; } = "5300197547:AAE_jOnQmJ0foXGGZmbfVEbdlvApi5zDiPU";

        private static Regex rg = new Regex(@"^[АВЕКМНОРСТУХ]\d{3}(?<!000)[АВЕКМНОРСТУХ]{2}\d{2,3}$",
            RegexOptions.IgnoreCase);

        private static TelegramBotClient _botClient = new TelegramBotClient(token);

        public static ConcurrentDictionary<string, Stopwatch> HareTimes = new();
        public static ConcurrentDictionary<string, string> SignBlackList = new();
        public static ConcurrentDictionary<long, string> LastAction = new();

        private const string firstButton = "Добавить во временный список";
        private const string secondButton = "Вывести временный список";
        private const string thirdButton = "Добавить в чёрный список";
        private const string fourthButton = "Вывести чёрный список";
        private const string fivethButton = "Удалить из временного списка";

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
            CancellationToken cancellationToken)
        {
            // Некоторые действия
            Console.WriteLine(JsonSerializer.Serialize(update));
            if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
            {
                var person = update.Message.From.Id;
                var message = update.Message;

                if (message.Text == firstButton)
                {
                    await botClient.SendTextMessageAsync(message.Chat, "Добавьте номер машины во временный список (доставка или друзья/родственники) (пример,в123oт45):", replyMarkup: GetButtons());
                    LastAction.AddOrUpdate(person, firstButton, (_, _) => firstButton);

                    return;
                }

                if (message.Text == secondButton)
                {
                    await botClient.SendTextMessageAsync(message.Chat, "Список машин внутри двора на данный момент:", replyMarkup: GetButtons());
                    
                    StringBuilder s = new();
                    foreach (var haretime in HareTimes)
                    {
                        s.Append($"{haretime.Key} {haretime.Value.Elapsed} \n");
                    }

                    if(s.Length > 0)
                        await botClient.SendTextMessageAsync(message.Chat, s.ToString());
                    else
                        await botClient.SendTextMessageAsync(message.Chat, "Список пуст.");

                    LastAction.AddOrUpdate(person, secondButton, (_, _) => secondButton);

                    return;
                }

                if (message.Text == thirdButton)
                {
                    await botClient.SendTextMessageAsync(message.Chat, "Добавьте номер машины в чёрный список (пример,в123oт45):", replyMarkup: GetButtons());
                    LastAction.AddOrUpdate(person, thirdButton, (_, _) => thirdButton);

                    return;
                }

                if (message.Text == fourthButton)
                {
                    await botClient.SendTextMessageAsync(message.Chat, "Список машин из чёного списка:", replyMarkup: GetButtons());

                    StringBuilder s = new();
                    foreach (var black in SignBlackList)
                    {
                        s.Append($"{black.Key} {black.Value} \n");
                    }

                    if (s.Length > 0)
                        await botClient.SendTextMessageAsync(message.Chat, s.ToString());

                    LastAction.AddOrUpdate(person, fourthButton, (_, _) => fourthButton);

                    return;
                }

                if (message.Text == fivethButton)
                {
                    await botClient.SendTextMessageAsync(message.Chat, "Введите номер который надо удалить из временного списка:", replyMarkup: GetButtons());

                    StringBuilder s = new();
                    foreach (var black in SignBlackList)
                    {
                        s.Append($"{black.Key} {black.Value} \n");
                    }

                    if (s.Length > 0)
                        await botClient.SendTextMessageAsync(message.Chat, s.ToString());
                    else
                        await botClient.SendTextMessageAsync(message.Chat, "Список пуст.");

                    LastAction.AddOrUpdate(person, fivethButton, (_, _) => fivethButton);

                    return;
                }

                var action = LastAction.Keys.Contains(person) ? LastAction[person] : LastAction.AddOrUpdate(person, string.Empty, (_, _) => string.Empty);
                if (action == thirdButton)
                {
                    var m = rg.Match(message.Text.ToLower());
                    if (rg.Match(message.Text.ToLower()).Success)
                    {
                        _ = SignBlackList.AddOrUpdate(message.Text.ToUpper(), DateTime.Now + " " + person, (k, v) => DateTime.Now + " " + person);

                        await botClient.SendTextMessageAsync(message.Chat, "Номер машины добавлен в чёрный список!", replyMarkup: GetButtons());
                        return;
                    }
                }

                if (action == firstButton)
                {
                    var m = rg.Match(message.Text.ToLower());
                    if (rg.Match(message.Text.ToLower()).Success)
                    {
                        var newHare = HareTimes.AddOrUpdate(message.Text.ToUpper(), new Stopwatch(), (k, v) => new Stopwatch());
                        newHare.Start();

                        await botClient.SendTextMessageAsync(message.Chat, "Номер машины добавлен во временный список!", replyMarkup: GetButtons());
                        return;
                    }
                }

                if (action == fivethButton)
                {
                    var m = rg.Match(message.Text.ToLower());
                    if (rg.Match(message.Text.ToLower()).Success)
                    {
                        if (HareTimes.TryRemove(message.Text.ToUpper(), out _))
                        {
                            await botClient.SendTextMessageAsync(message.Chat, "Номер машины удалён из временного списка!", replyMarkup: GetButtons());
                        }
                        else
                        {
                            await botClient.SendTextMessageAsync(message.Chat, "Номер машины отсутствует во временном списке!", replyMarkup: GetButtons());
                        }

                        
                        return;
                    }
                }

                await botClient.SendTextMessageAsync(message.Chat, "Не знаю такой команды или номер машины не распознан!", replyMarkup: GetButtons());
            }
        }

        public static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception,
            CancellationToken cancellationToken)
        {
            // Некоторые действия
            Console.WriteLine(JsonSerializer.Serialize(exception));
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Bot is started " + _botClient.GetMeAsync().Result.FirstName);

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { }, // receive all update types
                
            };
            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken
            );
            Console.ReadLine();
        }

        private static IReplyMarkup GetButtons()
        {
            var k = new List<List<KeyboardButton>>
            {
                new List<KeyboardButton> {new KeyboardButton(firstButton) },
                new List<KeyboardButton> {new KeyboardButton(secondButton) },
                new List<KeyboardButton> {new KeyboardButton(thirdButton) },
                new List<KeyboardButton> {new KeyboardButton(fourthButton) },
                new List<KeyboardButton> {new KeyboardButton(fivethButton) }
            };
            return new ReplyKeyboardMarkup(k);
        }
    }
}