# Crawler Pão de Açúcar — Refrigerantes

> Extrai todos os refrigerantes do Pão de Açúcar com filtro de packs, tabela nutricional completa e exportação em CSV e JSON.

📄 **Artigo completo no Medium:** [Como construir este crawler com .NET 8 e Playwright](#)

---

## O que este crawler faz

- Navega pela listagem paginada de refrigerantes em `paodeacucar.com`
- Filtra automaticamente packs, fardos, kits e combos
- Entra em cada produto individualmente e extrai:
  - Nome, marca e volume
  - Preço de venda e preço clube
  - Tabela nutricional completa (calorias, carboidratos, açúcares, sódio, proteínas...)
- Salva HTML bruto de cada produto para debug
- Exporta tudo em CSV (Excel-ready) e JSON

---

## Tecnologias

- **.NET 8** (C#)
- **Playwright** — controla um browser Chromium real (o site é SPA em React)
- **System.Text.Json** — serialização do JSON de saída

---

## Estrutura do projeto

```
PaodeAcucarCrawler/
├── Program.cs                   ← entry point e configuração
├── PaodeAcucarCrawler.csproj
├── Models/
│   └── Produto.cs               ← modelo de dados com NutricaoCompleta
├── Scraper/
│   ├── PaodeAcucarScraper.cs    ← lógica principal
│   ├── ScraperConfig.cs         ← parâmetros ajustáveis
│   └── FiltroUtils.cs           ← EhPack(), NormalizarPreco(), ExtrairVolume()
└── Export/
    └── Exportador.cs            ← CSV, JSON e saída no console
```

---

## Como rodar

**Pré-requisito:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
git clone https://github.com/cezarhvelazquez/crawlerPaoDeAcucarRefrigerante.git
cd crawlerPaoDeAcucarRefrigerante
dotnet restore
dotnet run
```

O Chromium é instalado automaticamente na primeira execução.

---

## Configuração

Edite as propriedades em `Program.cs`:

```csharp
var config = new ScraperConfig
{
    MaxPaginas        = 10,     // limite de páginas da listagem
    Headless          = true,   // false = abre janela (útil para debug)
    BloquearMidia     = true,   // bloqueia imagens/fontes para acelerar
    PastaHtmlProdutos = "produtos_html",
    OutputCsv         = "refrigerantes_paodeacucar.csv",
    OutputJson        = "refrigerantes_paodeacucar.json",
};
```

---

## Saídas geradas

| Arquivo | Descrição |
|---------|-----------|
| `refrigerantes_paodeacucar.csv` | CSV com separador `;`, UTF-8 BOM, abre direto no Excel |
| `refrigerantes_paodeacucar.json` | JSON completo com `NutricaoCompleta` |
| `produtos_html/p01_01_*.html` | HTML bruto de cada produto |
| `debug_*.html` | Páginas onde ocorreu erro (só aparece em caso de falha) |

---

## Filtro de packs

O filtro roda em dois momentos: na listagem (pelo nome do card) e na página do produto (pelo `h1` real). Palavras filtradas:

`pack` · `fardo` · `kit` · `caixa com` · `combo` · `leve ` · `fardinho` · `multipack`

---

## Debug de seletores

Os sites React mudam nomes de classe a cada deploy. Quando os seletores pararem de funcionar:

1. Mude `Headless = false` e rode novamente
2. Abra `debug_listagem_p1.html` no browser
3. Pressione F12 e inspecione os cards
4. Atualize os seletores em `PaodeAcucarScraper.cs`

---

## Licença

MIT
