using System.Text;
using System.Text.Json;
using PaodeAcucarCrawler.Models;

namespace PaodeAcucarCrawler.Export;

public static class Exportador
{
    // =========================================================================
    // CSV
    // =========================================================================
    public static async Task SalvarCsvAsync(List<Produto> produtos, string caminho)
    {
        var sb = new StringBuilder();

        // Cabecalho
        sb.AppendLine(
            "Nome;Marca;Volume;PrecoVenda;PrecoClube;" +
            "Porcao;Calorias;Carboidratos;Acucares;Sodio;Proteinas;GordurasTotais;" +
            "Url;HtmlPath");

        foreach (var p in produtos)
        {
            sb.AppendLine(string.Join(";",
                Escape(p.Nome),
                Escape(p.Marca),
                Escape(p.Volume),
                Escape(p.PrecoVenda),
                Escape(p.PrecoClube),
                Escape(p.Porcao),
                Escape(p.Calorias),
                Escape(p.Carboidratos),
                Escape(p.Acucares),
                Escape(p.Sodio),
                Escape(p.Proteinas),
                Escape(p.GordurasTotais),
                Escape(p.Url),
                Escape(p.HtmlPath)));
        }

        await File.WriteAllTextAsync(caminho, sb.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));  // BOM para Excel
    }

    // =========================================================================
    // JSON
    // =========================================================================
    public static async Task SalvarJsonAsync(List<Produto> produtos, string caminho)
    {
        var json = JsonSerializer.Serialize(produtos, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder       = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        await File.WriteAllTextAsync(caminho, json, Encoding.UTF8);
    }

    // =========================================================================
    // Console
    // =========================================================================
    public static void ImprimirResumo(List<Produto> produtos)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 70));
        Console.WriteLine($"  RESULTADO FINAL: {produtos.Count} refrigerantes extraidos");
        Console.WriteLine(new string('=', 70));

        foreach (var (p, i) in produtos.Select((x, i) => (x, i + 1)))
        {
            Console.WriteLine();
            Console.WriteLine($"  [{i:D3}] {p.Nome}");

            if (!string.IsNullOrEmpty(p.Marca))
                Console.WriteLine($"        Marca       : {p.Marca}");
            if (!string.IsNullOrEmpty(p.Volume))
                Console.WriteLine($"        Volume      : {p.Volume}");

            Console.WriteLine($"        Preco Venda : {p.PrecoVenda}");

            if (!string.IsNullOrEmpty(p.PrecoClube))
                Console.WriteLine($"        Preco Clube : {p.PrecoClube}");

            // Nutricao
            var temNutricao = !string.IsNullOrEmpty(p.Calorias) || !string.IsNullOrEmpty(p.Carboidratos);
            if (temNutricao)
            {
                Console.WriteLine($"        --- Nutricao (por {p.Porcao}) ---");
                if (!string.IsNullOrEmpty(p.Calorias))      Console.WriteLine($"        Calorias    : {p.Calorias}");
                if (!string.IsNullOrEmpty(p.Carboidratos))  Console.WriteLine($"        Carboidratos: {p.Carboidratos}");
                if (!string.IsNullOrEmpty(p.Acucares))      Console.WriteLine($"        Acucares    : {p.Acucares}");
                if (!string.IsNullOrEmpty(p.Sodio))         Console.WriteLine($"        Sodio       : {p.Sodio}");
                if (!string.IsNullOrEmpty(p.Proteinas))     Console.WriteLine($"        Proteinas   : {p.Proteinas}");
                if (!string.IsNullOrEmpty(p.GordurasTotais))Console.WriteLine($"        Gorduras    : {p.GordurasTotais}");
                if (p.NutricaoCompleta.Count > 7)
                    Console.WriteLine($"        (+{p.NutricaoCompleta.Count - 7} campos adicionais no JSON)");
            }
            else
            {
                Console.WriteLine($"        Nutricao    : nao disponivel");
            }

            Console.WriteLine($"        URL         : {p.Url}");
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 70));
    }

    private static string Escape(string v) =>
        $"\"{v.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "")}\"";
}
