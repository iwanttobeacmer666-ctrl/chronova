using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShiSha;

public static class UiText
{
    public sealed record LanguageChoice(string Id, string Label);
    public static readonly LanguageChoice[] Languages =
    {
        new("zh-CN", "简体中文"), new("en-US", "English"), new("zh-TW", "繁體中文"),
        new("ja-JP", "日本語"), new("ko-KR", "한국어"), new("es-ES", "Español"),
        new("fr-FR", "Français"), new("de-DE", "Deutsch"), new("pt-BR", "Português"), new("ru-RU", "Русский")
    };
    private static readonly string[] CatalogCodes = { "zh-TW", "ja-JP", "ko-KR", "es-ES", "fr-FR", "de-DE", "pt-BR", "ru-RU" };
    private static readonly Dictionary<string, string[]> Catalog = Load();
    private static Dictionary<string, string[]> Load()
    {
        using var stream = typeof(UiText).Assembly.GetManifestResourceStream("ShiSha.Localization.json")
            ?? throw new InvalidDataException("Translation catalog is missing.");
        return JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream)
            ?? throw new InvalidDataException("Translation catalog is empty.");
    }
    public static bool Supported(string code) => Languages.Any(l => l.Id == code);
    public static string Get(string code, string chinese, string english)
    {
        if (code == "zh-CN") return chinese;
        if (code == "en-US") return english;
        int index = Array.IndexOf(CatalogCodes, code);
        return index >= 0 && Catalog.TryGetValue(english, out var translations) ? translations[index] : english;
    }
}
