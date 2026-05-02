namespace PaodeAcucarCrawler.Models;

public class Produto
{
    public string Nome            { get; set; } = "";
    public string Marca           { get; set; } = "";
    public string Volume          { get; set; } = "";
    public string PrecoVenda      { get; set; } = "";
    public string PrecoClube      { get; set; } = "";

    // Nutricao principal
    public string Porcao          { get; set; } = "";
    public string Calorias        { get; set; } = "";
    public string Carboidratos    { get; set; } = "";
    public string Acucares        { get; set; } = "";
    public string Sodio           { get; set; } = "";
    public string Proteinas       { get; set; } = "";
    public string GordurasTotais  { get; set; } = "";

    /// <summary>Todos os campos da tabela nutricional (chave = nutriente, valor = quantidade)</summary>
    public Dictionary<string, string> NutricaoCompleta { get; set; } = new();

    public string Url             { get; set; } = "";
    public string HtmlPath        { get; set; } = "";
}
