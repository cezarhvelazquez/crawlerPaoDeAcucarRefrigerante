namespace PaodeAcucarCrawler.Scraper;

public class ScraperConfig
{
    /// <summary>URL base da listagem de refrigerantes (sem &p=N)</summary>
    public string UrlBase { get; set; } =
        "https://www.paodeacucar.com/categoria/bebidas/nao-alcoolicas/refrigerantes?s=relevance&p=";

    /// <summary>Maximo de paginas de listagem a percorrer (0 = ilimitado)</summary>
    public int MaxPaginas { get; set; } = 10;

    /// <summary>true = sem janela | false = abre janela visivel (debug)</summary>
    public bool Headless { get; set; } = true;

    /// <summary>Delay em ms entre acoes (0 = sem delay extra)</summary>
    public int SlowMo { get; set; } = 0;

    /// <summary>Bloqueia imagens/fontes/videos para acelerar</summary>
    public bool BloquearMidia { get; set; } = true;

    /// <summary>Pasta onde os HTMLs de cada produto serao salvos</summary>
    public string PastaHtmlProdutos { get; set; } = "produtos_html";

    /// <summary>Caminho do CSV gerado</summary>
    public string OutputCsv  { get; set; } = "refrigerantes_paodeacucar.csv";

    /// <summary>Caminho do JSON gerado</summary>
    public string OutputJson { get; set; } = "refrigerantes_paodeacucar.json";
}
