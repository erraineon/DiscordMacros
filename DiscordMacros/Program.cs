﻿using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

// config
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();

// setup
var client = new DiscordSocketClient();
client.Log += async message => Console.WriteLine(message.ToString());

// auth
var token = configuration["Token"] ?? throw new Exception("You must specify your token to run this program.");
await client.LoginAsync(TokenType.User, token);

// connect
var readyTcs = new TaskCompletionSource();
client.Ready += async () => readyTcs.SetResult();
await client.StartAsync();
await readyTcs.Task;

// process macros
var inputViaArguments = args.FirstOrDefault();
var waitForUserInput = string.IsNullOrWhiteSpace(inputViaArguments);
do
{
    var input = waitForUserInput ? Console.ReadLine() : inputViaArguments;
    configuration.Reload();
    var macros = configuration.GetRequiredSection("Macros").Get<List<Macro>>();
    var macro = macros?.FirstOrDefault(x => string.Equals(x.Input, input, StringComparison.OrdinalIgnoreCase));

    if (macro != null)
    {
        var validCommands = (macro.Commands ?? Enumerable.Empty<Command>())
            .Where(x => x is { Server: not null, Channel: not null });

        foreach (var command in validCommands)
        {
            var server = client.Guilds
                .FirstOrDefault(x => string.Equals(x.Name, command.Server, StringComparison.OrdinalIgnoreCase));
            var channel = server?.TextChannels
                .FirstOrDefault(x => string.Equals(x.Name, command.Channel, StringComparison.OrdinalIgnoreCase));

            if (channel != null)
            {
                var text = command.Text ??
                    Random.Shared
                    .GetItems(command.Choices!, 1, x => x.Weight)
                    .Select(x => x.Text)
                    .SingleOrDefault(x => !string.IsNullOrEmpty(x));

                if (!string.IsNullOrEmpty(text))
                {
                    await channel.SendMessageAsync(text);
                    await Task.Delay(TimeSpan.FromSeconds(macro.SecondsBetweenCommands));
                }
            }
        }
    }
} while (waitForUserInput);

public class Macro
{
    public string? Input { get; set; }
    public List<Command>? Commands { get; set; }
    public double SecondsBetweenCommands { get; set; } = 2f;
}

public class Command
{
    public string? Server { get; set; }
    public string? Channel { get; set; }
    public string? Text { get; set; }
    public List<CommandText>? Choices { get; set; }
}

public class CommandText
{
    public string? Text { get; set; }
    public float Weight { get; set; } = 1;
}

public static class RandomExtensions
{
    public static IEnumerable<T> GetItems<T>(this Random random, IEnumerable<T> choices, int length, Func<T, float> weightFunction)
    {
        var itemsAndWeight = choices
            .Select(item => (item, weight: weightFunction(item)))
            .Where(t => t.weight > 0)
            .ToList();

        for (var i = 0; i < length; i++)
            if (itemsAndWeight.Any())
            {
                var totalWeight = itemsAndWeight.Sum(t => t.weight);
                var minimumWeightToPick = random.NextDouble() * totalWeight;
                var weightAccumulator = 0f;
                var (selectedItem, _, selectedIndex) = itemsAndWeight
                    .Select((t, index) => (t.item, t.weight, index))
                    .SkipWhile(t => (weightAccumulator += t.weight) < minimumWeightToPick)
                    .First();
                yield return selectedItem;
                itemsAndWeight.RemoveAt(selectedIndex);
            }
    }
}