namespace NewsBot.Logic.Localization;

public static class L10n
{
    public static readonly IReadOnlyList<(string Code, string Label)> SupportedLanguages =
    [
        ("en", "English"),
        ("ru", "Русский"),
        ("de", "Deutsch"),
        ("fr", "Français"),
        ("es", "Español")
    ];

    public static readonly IReadOnlyList<(string Code, string LabelEn)> NewsCategories =
    [
        ("general", "General"),
        ("world", "World"),
        ("politics", "Politics"),
        ("business", "Business"),
        ("technology", "Technology"),
        ("science", "Science"),
        ("health", "Health"),
        ("sports", "Sports"),
        ("entertainment", "Entertainment")
    ];

    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["welcome_new"] = new()
        {
            ["en"] = "Welcome to NewsBot! Please choose your language:",
            ["ru"] = "Добро пожаловать в NewsBot! Выберите язык:",
            ["de"] = "Willkommen bei NewsBot! Bitte wählen Sie Ihre Sprache:",
            ["fr"] = "Bienvenue sur NewsBot ! Choisissez votre langue :",
            ["es"] = "¡Bienvenido a NewsBot! Elige tu idioma:"
        },
        ["welcome_back"] = new()
        {
            ["en"] = "Welcome back! /news — news, /ask — AI Q&A, /forecast — probability forecasts, /category, /keywords, /channel, /format, /lang.",
            ["ru"] = "С возвращением! /news — новости, /ask — вопросы к ИИ, /forecast — прогнозы вероятностей, /category, /keywords, /channel, /format, /lang.",
            ["de"] = "Willkommen zurück! /news, /ask, /forecast, /category, /keywords, /channel, /format, /lang.",
            ["fr"] = "Bon retour ! /news, /ask, /forecast, /category, /keywords, /channel, /format, /lang.",
            ["es"] = "¡Bienvenido! /news, /ask, /forecast, /category, /keywords, /channel, /format, /lang."
        },
        ["lang_set"] = new()
        {
            ["en"] = "Language set to: {0}",
            ["ru"] = "Язык установлен: {0}",
            ["de"] = "Sprache eingestellt: {0}",
            ["fr"] = "Langue définie : {0}",
            ["es"] = "Idioma establecido: {0}"
        },
        ["lang_choose"] = new()
        {
            ["en"] = "Choose language:",
            ["ru"] = "Выберите язык:",
            ["de"] = "Sprache wählen:",
            ["fr"] = "Choisir la langue :",
            ["es"] = "Elige idioma:"
        },
        ["category_help"] = new()
        {
            ["en"] = "Categories: {0}\n\nUsage:\n/category list\n/category add technology\n/category remove sports\n/category clear\n\nOr tap a button below:",
            ["ru"] = "Категории: {0}\n\nИспользование:\n/category list\n/category add technology\n/category remove sports\n/category clear\n\nИли нажмите кнопку:",
            ["de"] = "Kategorien: {0}\n\n/category list | add | remove | clear",
            ["fr"] = "Catégories : {0}\n\n/category list | add | remove | clear",
            ["es"] = "Categorías: {0}\n\n/category list | add | remove | clear"
        },
        ["category_added"] = new()
        {
            ["en"] = "Category added: {0}",
            ["ru"] = "Категория добавлена: {0}",
            ["de"] = "Kategorie hinzugefügt: {0}",
            ["fr"] = "Catégorie ajoutée : {0}",
            ["es"] = "Categoría añadida: {0}"
        },
        ["category_removed"] = new()
        {
            ["en"] = "Category removed: {0}",
            ["ru"] = "Категория удалена: {0}",
            ["de"] = "Kategorie entfernt: {0}",
            ["fr"] = "Catégorie supprimée : {0}",
            ["es"] = "Categoría eliminada: {0}"
        },
        ["category_cleared"] = new()
        {
            ["en"] = "All categories cleared.",
            ["ru"] = "Все категории очищены.",
            ["de"] = "Alle Kategorien gelöscht.",
            ["fr"] = "Toutes les catégories supprimées.",
            ["es"] = "Todas las categorías eliminadas."
        },
        ["keywords_help"] = new()
        {
            ["en"] = "Keywords: {0}\n\nUsage:\n/keywords list\n/keywords add bitcoin\n/keywords remove bitcoin\n/keywords clear",
            ["ru"] = "Ключевые слова: {0}\n\n/keywords list\n/keywords add bitcoin\n/keywords remove bitcoin\n/keywords clear",
            ["de"] = "Schlüsselwörter: {0}\n\n/keywords list | add | remove | clear",
            ["fr"] = "Mots-clés : {0}\n\n/keywords list | add | remove | clear",
            ["es"] = "Palabras clave: {0}\n\n/keywords list | add | remove | clear"
        },
        ["keyword_added"] = new()
        {
            ["en"] = "Keyword added: {0}",
            ["ru"] = "Ключевое слово добавлено: {0}",
            ["de"] = "Schlüsselwort hinzugefügt: {0}",
            ["fr"] = "Mot-clé ajouté : {0}",
            ["es"] = "Palabra clave añadida: {0}"
        },
        ["keyword_removed"] = new()
        {
            ["en"] = "Keyword removed: {0}",
            ["ru"] = "Ключевое слово удалено: {0}",
            ["de"] = "Schlüsselwort entfernt: {0}",
            ["fr"] = "Mot-clé supprimé : {0}",
            ["es"] = "Palabra clave eliminada: {0}"
        },
        ["keywords_cleared"] = new()
        {
            ["en"] = "All keywords cleared.",
            ["ru"] = "Все ключевые слова очищены.",
            ["de"] = "Alle Schlüsselwörter gelöscht.",
            ["fr"] = "Tous les mots-clés supprimés.",
            ["es"] = "Todas las palabras clave eliminadas."
        },
        ["format_help"] = new()
        {
            ["en"] = "Current format: {0}\n\nChoose output length:\n• full1000 — up to 1000 chars\n• medium500_1000 — 500-1000 chars\n• short50_100 — 50-100 chars\n• tiny50 — ~50 chars\n\n/format medium500_1000",
            ["ru"] = "Текущий формат: {0}\n\nВыберите длину:\n• full1000 — до 1000 символов\n• medium500_1000 — 500-1000\n• short50_100 — 50-100\n• tiny50 — ~50\n\n/format medium500_1000",
            ["de"] = "Aktuelles Format: {0}\n\n/format full1000 | medium500_1000 | short50_100 | tiny50",
            ["fr"] = "Format actuel : {0}\n\n/format full1000 | medium500_1000 | short50_100 | tiny50",
            ["es"] = "Formato actual: {0}\n\n/format full1000 | medium500_1000 | short50_100 | tiny50"
        },
        ["format_set"] = new()
        {
            ["en"] = "Format set to: {0}",
            ["ru"] = "Формат установлен: {0}",
            ["de"] = "Format eingestellt: {0}",
            ["fr"] = "Format défini : {0}",
            ["es"] = "Formato establecido: {0}"
        },
        ["news_fetching"] = new()
        {
            ["en"] = "Fetching news...",
            ["ru"] = "Загружаю новости...",
            ["de"] = "Nachrichten werden geladen...",
            ["fr"] = "Récupération des actualités...",
            ["es"] = "Obteniendo noticias..."
        },
        ["news_empty"] = new()
        {
            ["en"] = "No news found. Try adding categories (/category), Telegram channels (/channel) or keywords (/keywords).",
            ["ru"] = "Новости не найдены. Добавьте категории (/category), Telegram-каналы (/channel) или ключевые слова (/keywords).",
            ["de"] = "Keine Nachrichten gefunden. /category, /channel oder /keywords hinzufügen.",
            ["fr"] = "Aucune actualité. Ajoutez /category, /channel ou /keywords.",
            ["es"] = "No se encontraron noticias. Añade /category, /channel o /keywords."
        },
        ["channel_help"] = new()
        {
            ["en"] = "Telegram channels:\n{0}\n\nUsage:\n/channel list\n/channel add http://t.me/Monarch_Kredit\n/channel add @channel_name\n/channel remove Monarch_Kredit\n/channel clear\n\nPublic channels only (via t.me/s/ preview).",
            ["ru"] = "Telegram-каналы:\n{0}\n\nИспользование:\n/channel list\n/channel add http://t.me/Monarch_Kredit\n/channel add @channel_name\n/channel remove Monarch_Kredit\n/channel clear\n\nТолько публичные каналы (через t.me/s/).",
            ["de"] = "Telegram-Kanäle:\n{0}\n\n/channel list | add | remove | clear",
            ["fr"] = "Canaux Telegram :\n{0}\n\n/channel list | add | remove | clear",
            ["es"] = "Canales de Telegram:\n{0}\n\n/channel list | add | remove | clear"
        },
        ["channel_added"] = new()
        {
            ["en"] = "Channel added: {0}",
            ["ru"] = "Канал добавлен: {0}",
            ["de"] = "Kanal hinzugefügt: {0}",
            ["fr"] = "Canal ajouté : {0}",
            ["es"] = "Canal añadido: {0}"
        },
        ["channel_removed"] = new()
        {
            ["en"] = "Channel removed: {0}",
            ["ru"] = "Канал удалён: {0}",
            ["de"] = "Kanal entfernt: {0}",
            ["fr"] = "Canal supprimé : {0}",
            ["es"] = "Canal eliminado: {0}"
        },
        ["channel_cleared"] = new()
        {
            ["en"] = "All Telegram channels cleared.",
            ["ru"] = "Все Telegram-каналы очищены.",
            ["de"] = "Alle Telegram-Kanäle gelöscht.",
            ["fr"] = "Tous les canaux Telegram supprimés.",
            ["es"] = "Todos los canales de Telegram eliminados."
        },
        ["channel_invalid"] = new()
        {
            ["en"] = "Invalid channel. Use: http://t.me/name, @name or channel name.",
            ["ru"] = "Неверный канал. Используйте: http://t.me/name, @name или имя канала.",
            ["de"] = "Ungültiger Kanal.",
            ["fr"] = "Canal invalide.",
            ["es"] = "Canal no válido."
        },
        ["ask_help"] = new()
        {
            ["en"] = "Ask AI about your last /news results.\n\nUsage:\n/ask What is the main topic today?\n/ask Summarize news about technology\n\nRequires NewsSummarizerAi.Enabled = true and /news loaded first.",
            ["ru"] = "Задайте вопрос ИИ по последним новостям из /news.\n\nПример:\n/ask Какая главная тема сегодня?\n/ask Кратко про новости о технологиях\n\nНужен NewsSummarizerAi.Enabled и предварительный /news.",
            ["de"] = "/ask Ihre Frage — KI antwortet basierend auf /news.",
            ["fr"] = "/ask votre question — l'IA répond d'après /news.",
            ["es"] = "/ask su pregunta — la IA responde según /news."
        },
        ["ask_ai_disabled"] = new()
        {
            ["en"] = "Summarizer AI is disabled. Enable NewsSummarizerAi.Enabled in appsettings to use /ask.",
            ["ru"] = "ИИ-сократитель отключён. Включите NewsSummarizerAi.Enabled в appsettings для /ask.",
            ["de"] = "KI ist deaktiviert.",
            ["fr"] = "IA désactivée.",
            ["es"] = "IA desactivada."
        },
        ["ask_no_context"] = new()
        {
            ["en"] = "No recent news loaded. Run /news first, then use /ask.",
            ["ru"] = "Нет загруженных новостей. Сначала выполните /news, затем /ask.",
            ["de"] = "Keine News geladen. Zuerst /news ausführen.",
            ["fr"] = "Aucune actualité chargée. Lancez /news d'abord.",
            ["es"] = "Sin noticias cargadas. Ejecute /news primero."
        },
        ["ask_thinking"] = new()
        {
            ["en"] = "Thinking...",
            ["ru"] = "Думаю...",
            ["de"] = "Denke nach...",
            ["fr"] = "Réflexion...",
            ["es"] = "Pensando..."
        },
        ["ask_failed"] = new()
        {
            ["en"] = "Failed to get AI answer. Check API key and try again.",
            ["ru"] = "Не удалось получить ответ ИИ. Проверьте API-ключ и повторите.",
            ["de"] = "KI-Antwort fehlgeschlagen.",
            ["fr"] = "Échec de la réponse IA.",
            ["es"] = "Error al obtener respuesta de IA."
        },
        ["forecast_help"] = new()
        {
            ["en"] = "Probability forecast based on archived news (short: week, medium: 1-6 months, long: 6 months - 5 years).\n\nUsage:\n/forecast What is the probability of inflation rising this year?\n\nThe answer is sent as a .docx file. Processing may take from 1 minute to several hours depending on queue load.\nRequires NewsPredictorAi.Enabled = true.",
            ["ru"] = "Прогноз вероятности по архиву новостей (краткий: неделя, средний: 1–6 мес., долгий: 6 мес. – 5 лет).\n\nПример:\n/forecast Какова вероятность дождя завтра?\n\nОтвет придёт файлом .docx. Обработка может занять от 1 минуты до нескольких часов.\nНужен NewsPredictorAi.Enabled.",
            ["de"] = "/forecast Ihre Frage. Antwort als .docx-Datei.",
            ["fr"] = "/forecast votre question. Réponse en fichier .docx.",
            ["es"] = "/forecast su pregunta. Respuesta en archivo .docx."
        },
        ["forecast_ai_disabled"] = new()
        {
            ["en"] = "Predictor AI is disabled. Enable NewsPredictorAi.Enabled in appsettings to use /forecast.",
            ["ru"] = "ИИ-предсказатель отключён. Включите NewsPredictorAi.Enabled в appsettings для /forecast.",
            ["de"] = "KI ist deaktiviert.",
            ["fr"] = "IA désactivée.",
            ["es"] = "IA desactivada."
        },
        ["forecast_analyzing"] = new()
        {
            ["en"] = "Analyzing news archive and building forecast...",
            ["ru"] = "Анализирую архив новостей и формирую прогноз...",
            ["de"] = "Analysiere Nachrichtenarchiv...",
            ["fr"] = "Analyse de l'archive...",
            ["es"] = "Analizando archivo de noticias..."
        },
        ["forecast_queued"] = new()
        {
            ["en"] = "Your forecast request has been queued.\n\nProcessing may take from 1 minute to several hours depending on queue load. The result will be sent as a .docx file: {0}",
            ["ru"] = "Запрос на прогноз поставлен в очередь.\n\nОбработка может занять от 1 минуты до нескольких часов. Результат будет отправлен файлом .docx: {0}",
            ["de"] = "Prognoseanfrage in Warteschlange. Ergebnis als .docx: {0}",
            ["fr"] = "Demande en file d'attente. Résultat en .docx : {0}",
            ["es"] = "Solicitud en cola. Resultado en .docx: {0}"
        },
        ["forecast_ready"] = new()
        {
            ["en"] = "Your forecast is ready.",
            ["ru"] = "Ваш прогноз готов.",
            ["de"] = "Ihre Prognose ist fertig.",
            ["fr"] = "Votre prévision est prête.",
            ["es"] = "Su pronóstico está listo."
        },
        ["forecast_failed"] = new()
        {
            ["en"] = "Failed to build forecast. Please try again later.",
            ["ru"] = "Не удалось сформировать прогноз. Попробуйте позже.",
            ["de"] = "Prognose fehlgeschlagen.",
            ["fr"] = "Échec de la prévision.",
            ["es"] = "Error al generar el pronóstico."
        },
        ["forecast_no_data"] = new()
        {
            ["en"] = "Not enough archived news. Run /news several times over days/weeks to build history, then try /forecast again.",
            ["ru"] = "Недостаточно новостей в архиве. Выполняйте /news регулярно (дни/недели), затем повторите /forecast.",
            ["de"] = "Nicht genug archivierte Nachrichten.",
            ["fr"] = "Pas assez d'actualités archivées.",
            ["es"] = "No hay suficientes noticias archivadas."
        },
        ["news_lang_required"] = new()
        {
            ["en"] = "Please set language first with /lang",
            ["ru"] = "Сначала выберите язык: /lang",
            ["de"] = "Bitte zuerst Sprache wählen: /lang",
            ["fr"] = "Choisissez d'abord la langue : /lang",
            ["es"] = "Primero elige idioma: /lang"
        },
        ["unknown_command"] = new()
        {
            ["en"] = "Unknown option. Type the command without arguments for help.",
            ["ru"] = "Неизвестная опция. Введите команду без аргументов для справки.",
            ["de"] = "Unbekannte Option.",
            ["fr"] = "Option inconnue.",
            ["es"] = "Opción desconocida."
        }
    };

    public static string T(string key, string lang, params object[] args)
    {
        var language = NormalizeLang(lang);
        if (!Texts.TryGetValue(key, out var translations))
            return key;

        var text = translations.TryGetValue(language, out var value)
            ? value
            : translations.GetValueOrDefault("en", key);

        return args.Length > 0 ? string.Format(text, args) : text;
    }

    public static string NormalizeLang(string? lang) =>
        string.IsNullOrWhiteSpace(lang) ? "en" : lang.ToLowerInvariant();

    public static string FormatLabel(NewsBot.Logic.Models.NewsFormat format) => format switch
    {
        NewsBot.Logic.Models.NewsFormat.Full1000 => "full1000",
        NewsBot.Logic.Models.NewsFormat.Medium500_1000 => "medium500_1000",
        NewsBot.Logic.Models.NewsFormat.Short50_100 => "short50_100",
        NewsBot.Logic.Models.NewsFormat.Tiny50 => "tiny50",
        _ => format.ToString()
    };
}
