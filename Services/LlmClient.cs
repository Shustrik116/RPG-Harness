using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RPG_Harness.Services;

/// <summary>Ответ GET /models: соединение, список моделей и — если сервер их публикует — размеры контекста.</summary>
public sealed record ProbeResult(bool Ok, string Message, List<string> Models)
{
    /// <summary>
    /// Размер контекстного окна, озвученный сервером (id модели → токены). Поле вне стандарта OpenAI,
    /// поэтому заполнено только для части серверов; пусто — контекст придётся задать руками.
    /// </summary>
    public Dictionary<string, int> Contexts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Результат одного раунда генерации: текст, накопленные вызовы инструментов и рассуждения модели.</summary>
public sealed record AssistantTurn(string Content, IReadOnlyList<ToolCall> ToolCalls, string Reasoning = "");

/// <summary>
/// Прогресс «невидимой» части ответа: размышления модели (reasoning) и стриминг аргументов инструментов.
/// Без него игрок минутами смотрит на три точки, пока мастер создаёт мир.
/// </summary>
public sealed record StreamProgress(int ReasoningChars, int ToolCalls, string? LastTool, int ToolChars);

/// <summary>
/// Заполнение контекстного окна: сколько токенов уйдёт в запрос, каков лимит и при каком проценте
/// приложение начнёт сокращать старую историю (см. <see cref="HarnessSettings.AutoCompressPercent"/>).
/// </summary>
public sealed record ContextUsage(long UsedTokens, long LimitTokens, int Percent, int CompressPercent)
{
    /// <summary>Контекст дошёл до порога автосокращения — история вот-вот начнёт «забываться».</summary>
    public bool OverCompress => Percent >= CompressPercent;
}

/// <summary>Клиент OpenAI-совместимого chat completions API с потоковой передачей и tool calls.</summary>
public sealed class LlmClient
{
    public const string HttpClientName = "llm";

    /// <summary>Дописывается к системному промпту, когда включены инструменты.</summary>
    /// <summary>Префикс сообщения игрока в режиме «Игрок» (вне игры).</summary>
    public static string OocPrefix => Lang.T("[ВНЕ ИГРЫ — пишет игрок, а не персонаж] ", "[OUT OF GAME — written by the player, not the character] ");

    public static string ToolsSystemNote => Lang.T(
        "Инструменты: пути файлов — относительные, внутри папки кампании (например, World.md, Characters/Лира.md). " +
        "Несколько независимых изменений делай одним пакетом вызовов. Если инструмент вернул ошибку — исправь аргументы или учти её в повествовании, " +
        "не притворяйся, что действие удалось.",
        "Tools: file paths are relative, inside the campaign folder (for example, World.md, Characters/Lyra.md). " +
        "Make several independent changes in one batch of calls. If a tool returned an error, fix the arguments or account for it in the narration; " +
        "do not pretend the action succeeded.");

    public static string InstanceSessionsNote => Lang.T(
        "Раздельные игровые сессии включены. Для продолжительных замкнутых сцен (данж, многораундовый бой, погоня, проникновение, испытание) " +
        "основной Мастер может вызвать start_instance и передать полный самодостаточный бриф. Не используй инстанс для короткой проверки, обычного разговора, торговли или одного простого столкновения. " +
        "В активном инстансе ты являешься суб-мастером: веди только переданную сцену, читай нужные .md через инструменты, фиксируй состояние сразу и заверши её finish_instance со структурированной передачей. " +
        "Инстансы нельзя вкладывать друг в друга. Игрок всё время видит один непрерывный чат — не обсуждай внутреннюю маршрутизацию контекста без вопроса игрока.",
        "Separate game sessions are enabled. For long self-contained scenes (a dungeon, a multi-round battle, a chase, an infiltration, a trial) " +
        "the main Master may call start_instance and hand over a complete self-sufficient brief. Do not use an instance for a short check, an ordinary conversation, trading or a single simple encounter. " +
        "In an active instance you are the sub-master: run only the handed-over scene, read the needed .md files through tools, record the state immediately and finish it with finish_instance and a structured handover. " +
        "Instances cannot be nested. The player always sees one continuous chat — do not discuss the internal context routing unless the player asks.");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FileTools _fileTools;

    public LlmClient(IHttpClientFactory httpClientFactory, FileTools fileTools)
    {
        _httpClientFactory = httpClientFactory;
        _fileTools = fileTools;
    }

    /// <summary>
    /// Последние limit сообщений истории (0 — вся история). Обрезка начинается с сообщения игрока,
    /// чтобы контекст не открывался ответом мастера или результатами инструментов.
    /// </summary>
    public static IReadOnlyList<ChatMessage> TrimHistory(IReadOnlyList<ChatMessage> history, int limit, out bool truncated)
    {
        truncated = false;
        if (limit <= 0 || history.Count <= limit)
        {
            return history;
        }

        var start = history.Count - limit;
        while (start < history.Count && history[start].Role != "user")
        {
            start++;
        }

        if (start >= history.Count)
        {
            return history;
        }

        truncated = true;
        return history.Skip(start).ToList();
    }

    /// <summary>
    /// Сколько символов в запросе уходит помимо истории: системный промпт, сводка книги героя,
    /// описания инструментов и запас на служебную обвязку. Одна формула на отправку и на счётчик контекста.
    /// </summary>
    public static int ReservedChars(HarnessSettings settings, string? stateNote, int toolsChars) =>
        (settings.PromptFor(Genre.Current)?.Length ?? 0) + (stateNote?.Length ?? 0) + toolsChars + 8000;

    /// <summary>
    /// Приблизительный «вес» сообщения в символах: текст, рассуждения, картинки и результаты инструментов.
    /// Одна и та же мера используется и для укладки истории в контекст, и для счётчика заполнения окна.
    /// </summary>
    public static int MessageSize(ChatMessage message) =>
        (message.Content?.Length ?? 0) + (message.Reasoning?.Length ?? 0) +
        (message.Images?.Sum(x => x.Length) ?? 0) +
        (message.ToolCalls?.Sum(c => c.Arguments.Length + (c.Result?.Length ?? 0) + c.Name.Length + 80) ?? 0) + 40;

    /// <summary>
    /// Оценка заполнения контекстного окна. <paramref name="reservedChars"/> — то, что уходит помимо истории
    /// (системный промпт, сводка книги героя, описания инструментов, запас); ровно эту же величину вычитает
    /// <see cref="FitHistoryToContext"/>, поэтому порог сокращения истории совпадает с <see cref="ContextUsage.CompressPercent"/>.
    /// Токены считаются как символы / 4 — так же, как в остальном харнесе.
    /// </summary>
    public static ContextUsage MeasureContext(HarnessSettings settings, int reservedChars, IReadOnlyList<ChatMessage> history)
    {
        var limitTokens = Math.Max(1, settings.MaxContextTokens);
        var usedChars = Math.Max(0, reservedChars) + history.Sum(MessageSize);
        var usedTokens = usedChars / 4;
        var percent = (int)Math.Round(usedTokens * 100.0 / limitTokens);
        var compressPercent = settings.AutoCompressPercent <= 0
            ? 100
            : Math.Clamp(settings.AutoCompressPercent, 10, 100);
        return new ContextUsage(usedTokens, limitTokens, Math.Clamp(percent, 0, 999), compressPercent);
    }

    /// <summary>
    /// Приблизительно укладывает историю в заданную долю контекстного окна. Старые полные ходы
    /// исключаются только с границы сообщения игрока; долговременная память остаётся в Journal.md и RPG-сводке.
    /// </summary>
    public static IReadOnlyList<ChatMessage> FitHistoryToContext(
        IReadOnlyList<ChatMessage> history, HarnessSettings settings, int reservedChars, out bool compressed)
    {
        compressed = false;
        if (settings.MaxContextTokens <= 0 || settings.AutoCompressPercent <= 0 || history.Count == 0)
        {
            return history;
        }

        var thresholdTokens = (long)settings.MaxContextTokens * Math.Clamp(settings.AutoCompressPercent, 10, 100) / 100;
        var budgetChars = Math.Max(4000L, thresholdTokens * 4L - Math.Max(0, reservedChars));

        var total = history.Sum(MessageSize);
        if (total <= budgetChars)
        {
            return history;
        }

        var start = 0;
        while (start < history.Count - 2 && total > budgetChars)
        {
            total -= MessageSize(history[start]);
            start++;
            while (start < history.Count - 2 && history[start].Role != "user")
            {
                total -= MessageSize(history[start]);
                start++;
            }
        }

        compressed = start > 0;
        return compressed ? history.Skip(start).ToList() : history;
    }

    /// <summary>
    /// Отправляет историю диалога и вызывает <paramref name="onDelta"/> на каждый фрагмент текста.
    /// Возвращает накопленные вызовы инструментов (пустой список, если модель ответила только текстом).
    /// </summary>
    public async Task<AssistantTurn> StreamChatAsync(
        HarnessSettings settings,
        IReadOnlyList<ChatMessage> history,
        Action<string>? onDelta,
        bool toolsEnabled,
        CancellationToken cancellationToken,
        string? rpgNote = null,
        Action<StreamProgress>? onProgress = null,
        RequestProfile? profile = null)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var url = JoinUrl(settings.ApiBaseUrl, "chat/completions");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        ApplyAuth(request, settings);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = settings.Model,
            ["messages"] = BuildApiMessages(settings, history, toolsEnabled, settings.VisionEnabled, rpgNote),
            ["stream"] = true,
        };
        if (toolsEnabled)
        {
            payload["tools"] = _fileTools.Definitions;
        }

        ApplyProfile(payload, settings, profile);

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                Lang.T("API вернул", "API returned") + $" {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body)}");
        }

        var content = new StringBuilder();
        var reasoning = new StringBuilder();
        var toolCalls = new List<ToolCall>();
        var reasoningChars = 0;
        var lastProgressKey = "";

        // Сообщаем о прогрессе крупными шагами (≈ каждые 400 символов), чтобы не дёргать UI на каждом токене.
        void ReportProgress()
        {
            if (onProgress is null)
            {
                return;
            }

            var toolChars = toolCalls.Sum(c => c.Arguments.Length);
            var last = toolCalls.LastOrDefault(c => c.Name.Length > 0)?.Name;
            var key = $"{reasoningChars / 400}|{toolCalls.Count}|{last}|{toolChars / 400}";
            if (key == lastProgressKey)
            {
                return;
            }

            lastProgressKey = key;
            onProgress(new StreamProgress(reasoningChars, toolCalls.Count, last, toolChars));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } rawLine)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isSse = line.StartsWith("data:", StringComparison.Ordinal);
            var data = isSse ? line["data:".Length..].TrimStart() : line;

            if (data == "[DONE]")
            {
                break;
            }

            if (data.Length == 0 || data[0] != '{')
            {
                continue;
            }

            JsonObject? choice = null;
            try
            {
                choice = ((JsonNode.Parse(data)?["choices"] as JsonArray)?.FirstOrDefault()) as JsonObject;
            }
            catch
            {
                continue;
            }

            if (choice is null)
            {
                continue;
            }

            // Обычный (нестриминговый) ответ: {choices:[{message:{content, tool_calls}}]}
            if (!isSse)
            {
                var message = choice["message"] as JsonObject;
                AppendText(content, message?["content"], onDelta);
                if (message?["tool_calls"] is JsonArray plainCalls)
                {
                    MergeToolCallFragments(toolCalls, plainCalls);
                }

                break;
            }

            // Стриминговый фрагмент: {choices:[{delta:{content, tool_calls}, finish_reason}]}
            var delta = choice["delta"] as JsonObject;
            AppendText(content, delta?["content"], onDelta);
            if ((TryGetString(delta?["reasoning_content"]) ?? TryGetString(delta?["reasoning"])) is { Length: > 0 } thinking)
            {
                reasoningChars += thinking.Length;
                reasoning.Append(thinking);
                ReportProgress();
            }

            if (delta?["tool_calls"] is JsonArray fragments)
            {
                MergeToolCallFragments(toolCalls, fragments);
                ReportProgress();
            }

            var finish = TryGetString(choice["finish_reason"]);
            if (!string.IsNullOrEmpty(finish))
            {
                break;
            }
        }

        return new AssistantTurn(content.ToString(), toolCalls, reasoning.ToString());
    }

    /// <summary>
    /// Температура и рассуждения для вида запроса.
    /// OpenAI: reasoning_effort (none, minimal, low, medium, high, xhigh).
    /// DeepSeek: thinking {type: enabled|disabled} + reasoning_effort (low | high | max; minimal → low, medium/xhigh → high);
    /// в режиме рассуждений DeepSeek игнорирует температуру.
    /// </summary>
    internal static void ApplyProfile(IDictionary<string, object?> payload, HarnessSettings settings, RequestProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        if (profile.Temperature is { } temperature)
        {
            payload["temperature"] = Math.Round(Math.Clamp(temperature, 0, 2), 2);
        }

        var effort = (profile.Effort ?? "").Trim().ToLowerInvariant();
        if (!settings.ReasoningEnabled || effort.Length == 0)
        {
            return;
        }

        if (IsDeepSeek(settings))
        {
            if (effort == "none")
            {
                payload["thinking"] = new { type = "disabled" };
                return;
            }

            payload["thinking"] = new { type = "enabled" };
            payload["reasoning_effort"] = effort switch
            {
                "minimal" or "low" => "low",
                "max" => "max",
                _ => "high",
            };
            return;
        }

        payload["reasoning_effort"] = effort;
    }

    private static bool IsDeepSeek(HarnessSettings settings) =>
        settings.ReasoningEnabled && string.Equals(settings.ReasoningFormat, "deepseek", StringComparison.OrdinalIgnoreCase);

    /// <summary>Проверка соединения: GET /models.</summary>
    public async Task<ProbeResult> ProbeAsync(HarnessSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
        {
            return new ProbeResult(false, Lang.T("✗ Не задан базовый URL API.", "✗ The API base URL is not set."), new List<string>());
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, JoinUrl(settings.ApiBaseUrl, "models"));
            ApplyAuth(request, settings);

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new ProbeResult(
                    false,
                    Lang.T("✗ Ошибка", "✗ Error") + $" {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body)}",
                    new List<string>());
            }

            var models = ExtractModels(body);
            var contexts = ExtractContexts(body);
            var withContext = models.Count(contexts.ContainsKey);
            var message = models.Count > 0
                ? Lang.T($"✓ Соединение установлено. Моделей: {models.Count} — выберите нужную в списке ниже.",
                         $"✓ Connected. Models: {models.Count} — pick one in the list below.")
                  + (withContext > 0 ? Lang.T($" Сервер сообщил контекст для {withContext} из них.", $" The server reported the context size for {withContext} of them.") : "")
                : Lang.T("✓ Соединение установлено.", "✓ Connected.");

            return new ProbeResult(true, message, models) { Contexts = contexts };
        }
        catch (Exception ex)
        {
            return new ProbeResult(false, $"✗ {ex.Message}", new List<string>());
        }
    }

    /// <summary>Собирает payload messages: system + история, включая tool_calls и их результаты.</summary>
    private static List<object> BuildApiMessages(HarnessSettings settings, IReadOnlyList<ChatMessage> history, bool toolsEnabled, bool toolsVision, string? rpgNote = null)
    {
        var messages = new List<object>();

        var systemPrompt = settings.PromptFor(Genre.Current) ?? "";
        if (toolsEnabled)
        {
            systemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                ? ToolsSystemNote
                : systemPrompt + "\n\n" + ToolsSystemNote;

            if (settings.SeparateInstanceSessions)
            {
                systemPrompt += "\n\n" + InstanceSessionsNote;
            }

            // Сводка книги героя: мастер видит состояние панели и что ещё не заполнено.
            if (!string.IsNullOrWhiteSpace(rpgNote))
            {
                systemPrompt += "\n\n" + rpgNote;
            }
        }

        // Язык мастера — последней строкой: даже правленый игроком промпт не уведёт ответы на другой язык.
        if (Lang.IsEn)
        {
            systemPrompt += "\n\n" + GmPrompt.LanguageNote;
        }

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new { role = "system", content = systemPrompt });
        }

        // DeepSeek в режиме рассуждений требует вернуть reasoning_content ходов с инструментами
        // внутри текущего хода игрока (после последнего сообщения user); более ранние не нужны.
        var passReasoning = IsDeepSeek(settings);
        var lastUser = -1;
        for (var i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].Role == "user")
            {
                lastUser = i;
                break;
            }
        }

        for (var index = 0; index < history.Count; index++)
        {
            var message = history[index];
            if (message.Role == "assistant" && message.ToolCalls is { Count: > 0 })
            {
                var calls = message.ToolCalls.Select(tc => new
                {
                    id = tc.Id,
                    type = "function",
                    function = new { name = tc.Name, arguments = tc.Arguments },
                }).ToArray();
                if (passReasoning && index > lastUser)
                {
                    messages.Add(new
                    {
                        role = "assistant",
                        content = message.Content ?? "",
                        reasoning_content = message.Reasoning ?? "",
                        tool_calls = calls,
                    });
                }
                else
                {
                    messages.Add(new { role = "assistant", content = message.Content ?? "", tool_calls = calls });
                }

                // Результаты каждого вызова — отдельными сообщениями role=tool.
                foreach (var call in message.ToolCalls)
                {
                    messages.Add(new
                    {
                        role = "tool",
                        tool_call_id = call.Id,
                        content = TravelEncounters.StripMeta(call.Result ?? ""),
                    });
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(message.Content) && !message.HasImages)
            {
                continue;
            }

            // Мультимодальное сообщение: content как массив частей text/image_url.
            var text = message.Role == "user" && message.Ooc ? OocPrefix + message.Content : message.Content;
            if (message.HasImages && toolsVision)
            {
                var parts = new List<object>();
                if (!string.IsNullOrWhiteSpace(message.Content))
                {
                    parts.Add(new { type = "text", text });
                }

                foreach (var dataUri in message.Images!)
                {
                    parts.Add(new { type = "image_url", image_url = new { url = dataUri } });
                }

                messages.Add(new { role = message.Role, content = parts });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(message.Content))
            {
                messages.Add(new { role = message.Role, content = text });
            }
        }

        return messages;
    }

    private static void AppendText(StringBuilder sb, JsonNode? node, Action<string>? onDelta)
    {
        var text = TryGetString(node);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        sb.Append(text);
        onDelta?.Invoke(text);
    }

    /// <summary>
    /// Склеивает фрагменты tool_calls по index: id/имя — из первого фрагмента,
    /// arguments — конкатенация строк.
    /// </summary>
    private static void MergeToolCallFragments(List<ToolCall> acc, JsonArray fragments)
    {
        foreach (var fragment in fragments.OfType<JsonObject>())
        {
            try
            {
                var index = fragment["index"] is { } idx ? idx.GetValue<int>() : acc.Count;
                while (acc.Count <= index)
                {
                    acc.Add(new ToolCall());
                }

                var call = acc[index];

                var id = TryGetString(fragment["id"]);
                if (!string.IsNullOrEmpty(id))
                {
                    call.Id = id!;
                }

                if (fragment["function"] is JsonObject fn)
                {
                    var name = TryGetString(fn["name"]);
                    if (!string.IsNullOrEmpty(name))
                    {
                        // Провайдеры либо присылают имя целиком (возможно, повторно), либо кусками.
                        if (call.Name.Length == 0 || name!.Length > call.Name.Length)
                        {
                            call.Name = name!;
                        }
                        else if (!call.Name.Contains(name, StringComparison.Ordinal))
                        {
                            call.Name += name;
                        }
                    }

                    var args = TryGetString(fn["arguments"]);
                    if (!string.IsNullOrEmpty(args))
                    {
                        call.Arguments += args;
                    }
                }
            }
            catch
            {
                // некорректный фрагмент — пропускаем
            }
        }
    }

    private static string? TryGetString(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    private static void ApplyAuth(HttpRequestMessage request, HarnessSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey.Trim());
        }
    }

    private static string JoinUrl(string baseUrl, string relative) =>
        $"{baseUrl.TrimEnd('/')}/{relative.TrimStart('/')}";

    private static List<string> ExtractModels(string json)
    {
        try
        {
            var data = JsonNode.Parse(json)?["data"] as JsonArray;
            if (data is null)
            {
                return new List<string>();
            }

            return data
                .OfType<JsonObject>()
                .Select(n => n["id"]?.GetValue<string>())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string Truncate(string text) =>
        text.Length <= 500 ? text : text[..500] + "…";

    /// <summary>
    /// Размер контекста из ответа GET /models. Поля не входят в стандарт OpenAI и встречаются лишь у части
    /// серверов: OpenRouter — context_length (и top_provider.context_length), LM Studio/llama.cpp-прокси —
    /// context_window, vLLM — max_model_len, Some clouds — max_context_length. Нет полей — нет данных:
    /// тогда список моделей отдаётся без контекста, а «Максимальный контекст» остаётся ручной настройкой.
    /// </summary>
    private static Dictionary<string, int> ExtractContexts(string json)
    {
        var contexts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var data = JsonNode.Parse(json)?["data"] as JsonArray;
            if (data is null)
            {
                return contexts;
            }

            foreach (var node in data.OfType<JsonObject>())
            {
                var id = Text(node, "id");
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                var tokens = Number(node, "context_length", "context_window", "max_context_length", "max_model_len")
                    ?? Number(node["top_provider"] as JsonObject, "context_length");

                if (tokens is > 0)
                {
                    contexts[id] = Math.Min(tokens.Value, 100_000_000);
                }
            }
        }
        catch
        {
            // Ответ не похож на /models — просто не сообщаем контекст.
        }

        return contexts;
    }

    /// <summary>Строковое поле объекта или null (значение может быть не строкой).</summary>
    private static string? Text(JsonObject? obj, string key)
    {
        if (obj is null || !obj.TryGetPropertyValue(key, out var node) || node is null)
        {
            return null;
        }

        return node.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : null;
    }

    /// <summary>Первое из перечисленных полей, приводимое к целому числу (числом или строкой), иначе null.</summary>
    private static int? Number(JsonObject? obj, params string[] keys)
    {
        if (obj is null)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (!obj.TryGetPropertyValue(key, out var node) || node is null)
            {
                continue;
            }

            switch (node.GetValueKind())
            {
                case JsonValueKind.Number when node is JsonValue number:
                    if (number.TryGetValue<long>(out var whole))
                    {
                        return (int)Math.Clamp(whole, 0, 100_000_000);
                    }

                    if (number.TryGetValue<decimal>(out var frac))
                    {
                        return (int)Math.Clamp(frac, 0m, 100_000_000m);
                    }

                    break;
                case JsonValueKind.String when node is JsonValue text:
                    if (text.TryGetValue<string>(out var raw)
                        && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return Math.Clamp(parsed, 0, 100_000_000);
                    }

                    break;
            }
        }

        return null;
    }
}
