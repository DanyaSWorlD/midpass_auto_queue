// See https://aka.ms/new-console-template for more information
using Microsoft.Playwright;

using MidpassAutoQueue;

using System.Text.RegularExpressions;

using Telegram.Bot;
using Telegram.Bot.Types;

string tgToken = Environment.GetEnvironmentVariable("TG_TOKEN") ?? throw new Exception("Нет телеграм токена");
long userId = long.Parse(Environment.GetEnvironmentVariable("TG_USER") ?? throw new Exception("Нет айди пользователя тг"));
string email = Environment.GetEnvironmentVariable("EMAIL") ?? throw new Exception("Нет почты");
string pass = Environment.GetEnvironmentVariable("PASS") ?? throw new Exception("Нет пароля");
string country = Environment.GetEnvironmentVariable("COUNTRY") ?? throw new Exception("Нет страны");
string facility = Environment.GetEnvironmentVariable("FACILITY") ?? throw new Exception("Нет учереждения");
string cToken = Environment.GetEnvironmentVariable("CAPTCHA_TOKEN") ?? throw new Exception("Нет токена капчи");
bool.TryParse(Environment.GetEnvironmentVariable("DEBUG"), out bool debug);

const string captcha = "captcha.png";
const string screenshot = "screenshot.png";

Console.WriteLine();

var telegramBot = new TelegramBot(tgToken);
Chat? chat = null;

try
{
    chat = await telegramBot.Client.GetChatAsync(userId);
}
catch { }

if (chat == null)
    await telegramBot.GetResponseAsync(userId);

while (true)
{
    var time = await Work();
    await Task.Delay(time);
}

async Task<TimeSpan> Work()
{
    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(/*new() { Headless = false, SlowMo = 50, }*/);
    var page = await browser.NewPageAsync();
    await page.GotoAsync("https://q.midpass.ru");

    var countryDropdown = page.GetByRole(AriaRole.Combobox).First;
    var serviceProviderDropdown = page.GetByRole(AriaRole.Combobox).Last;

    var countryOptions = (await countryDropdown.AllInnerTextsAsync())
        .First().Split('\n', StringSplitOptions.RemoveEmptyEntries);

    await countryDropdown.SelectOptionAsync(country);
    await serviceProviderDropdown.SelectOptionAsync(facility);

    await page.Locator("#Email").FillAsync(email);
    await page.Locator("#Password").FillAsync(pass);

    var solved = false;

    while (!solved)
    {
        await page.Locator("#imgCaptcha").ScreenshotAsync(new() { Path = captcha });
        var solver = new Solver(cToken);
        var code = await solver.SolveCaptcha(captcha);
        await page.Locator("#Captcha").FillAsync(code.Value.code);
        await MakeDebugScreenshot(page);
        await page.GetByText("Войти").ClickAsync();

        if (page.Url == "https://q.midpass.ru/ru/Account/DoPrivatePersonLogOn")
        {
            if (await page.GetByText("Не заполнено \"Символы с картинки\"").IsVisibleAsync())
            {
                await solver.Report(code.Value.id, false);
                await MakeDebugScreenshot(page);
                await telegramBot.SendMessageAsync(userId, $"Проблемы с капчей, пробую еще раз.");
                continue;
            }

            if (await page.GetByText("Неверный адрес электронной почты или пароль").IsVisibleAsync())
            {
                await solver.Report(code.Value.id);
                await MakeDebugScreenshot(page);
                await telegramBot.SendMessageAsync(userId, $"Пароль не подошел, жду новый");
                pass = await telegramBot.GetResponseAsync(userId) ?? "";
                await telegramBot.SendMessageAsync(userId, $"Не забудь обновить конфиг");
                continue;
            }
        }

        if (page.Url == "https://q.midpass.ru/ru/Account/BanPage")
        {
            await solver.Report(code.Value.id);
            await MakeDebugScreenshot(page);
            await telegramBot.SendMessageAsync(userId, "Они подозревают, что я робот. Нужен новый пароль");
            pass = await telegramBot.GetResponseAsync(userId) ?? "";
            await telegramBot.SendMessageAsync(userId, "Ок, следующая попытка через час. И не забудь обновить пароль в конфиге!");
            return TimeSpan.FromHours(1);
        }

        await solver.Report(code.Value.id);
        solved = true;
    }

    await MakeDebugScreenshot(page);
    await page.GetByText("Лист ожидания").ClickAsync();
    var xhrResponse = await page.WaitForResponseAsync("https://q.midpass.ru/ru/Appointments/FindWaitingAppointments");
    var body = System.Text.Encoding.Default.GetString(await xhrResponse.BodyAsync());
    var regex = new Regex("\"PlaceInQueue\":([0-9]+),");
    var placeInQueue = regex.Match(body).Groups.Values.Last();
    await telegramBot.SendMessageAsync(userId, $"Место в очереди: {placeInQueue}");
    await page.GetByRole(AriaRole.Checkbox).Last.ClickAsync();
    await MakeDebugScreenshot(page);

    var confirmButton = page.GetByText("Подтвердить заявку");
    await confirmButton.ScrollIntoViewIfNeededAsync();
    if (await page.Locator("#confirmAppointments").And(page.Locator(".l-btn-disabled")).CountAsync() == 1)
    {
        await MakeDebugScreenshot(page);
        await telegramBot.SendMessageAsync(userId, $"Кнопка не активна, с последнего подтверждения прошло менее 24ч, следующая попытка через 8 часов");
        return TimeSpan.FromHours(8);
    }

    await MakeDebugScreenshot(page);
    await page.GetByText("Подтвердить заявку").ClickAsync();

    solved = false;
    while (!solved)
    {
        await page.Locator("#imgCaptcha").ScreenshotAsync(new() { Path = captcha });
        var solver = new Solver(cToken);
        var code = await solver.SolveCaptcha(captcha);
        await page.Locator("#captchaValue").FillAsync(code.Value.code);
        await MakeDebugScreenshot(page);
        await page.RunAndWaitForResponseAsync(async () =>
        {
            await page.GetByText("Подтвердить").Last.ClickAsync();
        }, response => response.Url.StartsWith("https://q.midpass.ru"));
        if (await page.GetByText("Не заполнено \"Символы с картинки\"").IsVisibleAsync())
        {
            await solver.Report(code.Value.id, false);
            await MakeDebugScreenshot(page);
            await telegramBot.SendMessageAsync(userId, $"Проблемы с капчей, пробую еще раз.");
            await page.GetByText("Ок").And(page.GetByRole(AriaRole.Button)).First.ClickAsync();
            await MakeDebugScreenshot(page);
            continue;
        }

        await MakeDebugScreenshot(page);
        await telegramBot.SendMessageAsync(userId, $"Заявка подтверждена, следующее подтверждение через сутки");
        solved = true;
    }

    return TimeSpan.FromDays(1);
}

async Task MakeDebugScreenshot(IPage page)
{
    if (!debug) return;

    await page.ScreenshotAsync(new() { Path = screenshot });
    await telegramBot.SendImageAsync(userId, screenshot);
}