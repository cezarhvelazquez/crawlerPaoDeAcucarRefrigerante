namespace PaodeAcucarCrawler.Scraper;

public static class FiltroUtils
{
    // Palavras que indicam que o produto e um pack/fardo/kit (deve ser ignorado)
    private static readonly string[] PalavrasPack =
    [
        "pack", "fardo", "kit", "caixa com", "combo",
        "leve ", "c/ ", "c/6", "c/8", "c/12", "6 un", "12 un",
        "fardinho", "multipack", "multi pack",
    ];

    /// <summary>Retorna true se o texto indicar que o produto e um pack/fardo.</summary>
    public static bool EhPack(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return false;
        var lower = texto.ToLower();
        return PalavrasPack.Any(p => lower.Contains(p));
    }

    /// <summary>Normaliza string de preco para comparacao/ordenacao.</summary>
    public static decimal NormalizarPreco(string preco)
    {
        if (string.IsNullOrWhiteSpace(preco)) return 0;

        // Remove tudo que nao seja digito, virgula ou ponto
        var limpo = new string(preco.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray());

        // Converte formato BR (1.299,99) para decimal
        limpo = limpo.Replace(".", "").Replace(",", ".");

        return decimal.TryParse(limpo, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var valor) ? valor : 0;
    }

    /// <summary>Extrai o volume em ml/L do nome do produto.</summary>
    public static string ExtrairVolume(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "";

        // Padroes: 350ml, 2L, 1,5L, 600 ml
        var match = System.Text.RegularExpressions.Regex.Match(
            nome,
            @"(\d+[,.]?\d*)\s*(ml|l|litro|litros)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success ? match.Value.Trim() : "";
    }
}
