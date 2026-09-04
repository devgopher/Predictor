using NewsBot.Logic.Settings;

namespace NewsBot.Logic.Models;

public enum NewsHorizon
{
    Short,
    Medium,
    Long
}

public static class NewsHorizonExtensions
{
    public static (DateTime From, DateTime To) GetDateRange(this NewsHorizon horizon, ForecastSettings settings, DateTime now)
    {
        return horizon switch
        {
            NewsHorizon.Short => (now.AddDays(-settings.ShortTermDays), now),
            NewsHorizon.Medium => (now.AddMonths(-settings.MediumTermMaxMonths), now.AddDays(-settings.ShortTermDays)),
            NewsHorizon.Long => (now.AddYears(-settings.LongTermMaxYears), now.AddMonths(-settings.LongTermMinMonths)),
            _ => (now.AddDays(-7), now)
        };
    }

    public static string GetLabel(this NewsHorizon horizon, string lang) => horizon switch
    {
        NewsHorizon.Short when lang == "ru" => "Краткий срок (неделя)",
        NewsHorizon.Medium when lang == "ru" => "Средний срок (1–6 мес.)",
        NewsHorizon.Long when lang == "ru" => "Долгий срок (6 мес. – 5 лет)",
        NewsHorizon.Short => "Short term (week)",
        NewsHorizon.Medium => "Medium term (1–6 months)",
        NewsHorizon.Long => "Long term (6 months – 5 years)",
        _ => horizon.ToString()
    };
}
