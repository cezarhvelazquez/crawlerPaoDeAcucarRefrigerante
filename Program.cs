using PaodeAcucarCrawler.Export;
using PaodeAcucarCrawler.Scraper;


Console.WriteLine(" Caçando refrigerantes no Pão de Açucar| Playwright ");
Console.WriteLine();

// ── Instala Chromium ──────────────────────────────────────────────────────────
Console.Write("Instalando Chromium via Playwright... ");
var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
Console.WriteLine(exitCode == 0 ? "OK" : $"Aviso (codigo {exitCode})");
Console.WriteLine();

//Configuracao
var config = new ScraperConfig
{
    // URL base: o crawler substitui o N no final com o numero da pagina
    UrlBase          = "https://www.paodeacucar.com/categoria/bebidas/nao-alcoolicas/refrigerantes?s=relevance&p=",

    MaxPaginas       = 1,      // maximo de paginas da listagem
    Headless         = true,    // false = indica se vai rodar com o navegador de forma visivel.
    SlowMo           = 0,       // delay de microsegundos entre acoes
    BloquearMidia    = true,    // bloqueia imagens/fontes para ser mais rapido

    PastaHtmlProdutos = "produtos_html", // pasta onde serao salvos os HTMLs de cada produto (para debug)
    OutputCsv         = "refrigerantes_paodeacucar.csv", // nome do arquivo CSV de saida
    OutputJson        = "refrigerantes_paodeacucar.json", // nome do arquivo JSON de saida
};

// ── Execucao ──────────────────────────────────────────────────────────────────
try
{
    var scraper  = new PaodeAcucarScraper(config);
    var produtos = await scraper.ExecutarAsync();

    if (produtos.Count == 0)
    {
        Console.WriteLine();
        Console.WriteLine("Nenhum produto extraido.");
        Console.WriteLine();
        Console.WriteLine("Passos para diagnosticar:");
        Console.WriteLine("  1. Mude Headless = false e rode novamente");
        Console.WriteLine("  2. Abra debug_listagem_p1.html no browser");
        Console.WriteLine("  3. Inspecione os elementos com F12 e ajuste os seletores");
        Console.WriteLine("     em Scraper/PaodeAcucarScraper.cs");
    }
    else
    {
        Exportador.ImprimirResumo(produtos);

        await Exportador.SalvarCsvAsync(produtos, config.OutputCsv);
        Console.WriteLine($"CSV  salvo: {Path.GetFullPath(config.OutputCsv)}");

        await Exportador.SalvarJsonAsync(produtos, config.OutputJson);
        Console.WriteLine($"JSON salvo: {Path.GetFullPath(config.OutputJson)}");

        Console.WriteLine($"HTMLs em : {Path.GetFullPath(config.PastaHtmlProdutos)}/");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"\nErro fatal: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

Console.WriteLine("\nConcluido. Pressione qualquer tecla...");
Console.ReadKey();
