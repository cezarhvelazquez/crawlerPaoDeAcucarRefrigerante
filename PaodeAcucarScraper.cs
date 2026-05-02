using Microsoft.Playwright;
using PaodeAcucarCrawler.Models;

namespace PaodeAcucarCrawler.Scraper;

public class PaodeAcucarScraper
{
    private readonly ScraperConfig _config;

    public PaodeAcucarScraper(ScraperConfig config)
    {
        _config = config;
    }

    public async Task<List<Produto>> ExecutarAsync()
    {
        var todos = new List<Produto>();

        using var playwright = await Playwright.CreateAsync();

        // Chromium com args anti-deteccao
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _config.Headless,
            SlowMo   = _config.SlowMo,
            Args =
            [
                "--disable-blink-features=AutomationControlled",
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-infobars",
                "--window-size=1366,768",
                "--disable-extensions",
                "--ignore-certificate-errors",
            ],
        });

        var context = await CriarContextoAsync(browser);

        Directory.CreateDirectory(_config.PastaHtmlProdutos);

        var pageListagem = await context.NewPageAsync();
        await ConfigurarPaginaAsync(pageListagem);

        var pagina = 1;

        while (true)
        {
            // Pao de Acucar usa ?s=relevance&p=N
            var url = $"{_config.UrlBase}{pagina}";
            Console.WriteLine($"\n[Pagina {pagina}] {url}");

            // ── Navega com retry ──────────────────────────────────────────────
            if (!await Navegar(pageListagem, url))
            {
                Console.WriteLine("  Falha ao carregar. Encerrando.");
                await SalvarDebugAsync(pageListagem, $"listagem_p{pagina}");
                break;
            }

            // ── Aguarda React renderizar ──────────────────────────────────────
            // O site e SPA em React: aguarda produto aparecer no DOM
            var renderizou = await AguardarProdutosAsync(pageListagem);
            if (!renderizou)
            {
                Console.WriteLine("  Produtos nao renderizaram. Salvando debug...");
                await SalvarDebugAsync(pageListagem, $"listagem_p{pagina}");
                break;
            }

            // ── Diagnostico anti-bloqueio ─────────────────────────────────────
            var diag = await DiagnosticarAsync(pageListagem, $"listagem_p{pagina}");
            if (diag == TipoPagina.Bloqueio)     { Console.WriteLine("  Bloqueio detectado. Encerrando."); break; }
            if (diag == TipoPagina.SemResultados){ Console.WriteLine("  Sem resultados. Encerrando."); break; }

            // ── Scroll suave para forcar lazy-load dos cards ──────────────────
            await ScrollSuaveAsync(pageListagem);

            // ── Coleta URLs dos produtos desta pagina ─────────────────────────
            var urls = await ColetarUrlsProdutosAsync(pageListagem);

            if (urls.Count == 0)
            {
                Console.WriteLine("  Nenhum produto encontrado. Salvando debug...");
                await SalvarDebugAsync(pageListagem, $"listagem_p{pagina}");
                break;
            }

            Console.WriteLine($"  {urls.Count} produtos validos (sem packs). Extraindo dados...");

            // ── Entra em cada produto em aba separada ─────────────────────────
            var produtos = await ProcessarProdutosAsync(context, urls, pagina);
            Console.WriteLine($"  {produtos.Count} produtos extraidos com sucesso.");
            todos.AddRange(produtos);

            // ── Proxima pagina ────────────────────────────────────────────────
            if (pagina >= _config.MaxPaginas)
            {
                Console.WriteLine($"  Limite de {_config.MaxPaginas} paginas atingido.");
                break;
            }

            var temProxima = await TemProximaPaginaAsync(pageListagem, pagina);
            if (!temProxima) { Console.WriteLine("  Ultima pagina."); break; }

            pagina++;
            await Task.Delay(Random.Shared.Next(3000, 6000));
        }

        await browser.CloseAsync();
        return todos;
    }

    // =========================================================================
    // AGUARDAR REACT RENDERIZAR
    // =========================================================================
    private static async Task<bool> AguardarProdutosAsync(IPage page)
    {
        // Estrategia 1: aguardar seletor de produto aparecer
        var seletores = new[]
        {
            "a[href*='/produto/']",
            "[data-testid*='product']",
            "[class*='ProductCard']",
            "[class*='product-card']",
            "[class*='ProductList'] a",
        };

        foreach (var sel in seletores)
        {
            try
            {
                await page.WaitForSelectorAsync(sel, new PageWaitForSelectorOptions
                {
                    Timeout = 15_000,
                    State   = WaitForSelectorState.Visible,
                });
                Console.WriteLine($"  React renderizou. Seletor: '{sel}'");
                return true;
            }
            catch { /* tenta proximo */ }
        }

        // Estrategia 2: aguarda rede estabilizar e checa HTML
        await Task.Delay(5000);
        var html = await page.ContentAsync();
        var temProduto = html.Contains("/produto/") || html.Contains("ProductCard") || html.Contains("product-card");
        if (temProduto) Console.WriteLine("  Produtos encontrados via HTML (fallback).");
        return temProduto;
    }

    // =========================================================================
    // COLETA URLs DA LISTAGEM (FILTRO DE PACKS AQUI)
    // =========================================================================
    private static async Task<List<string>> ColetarUrlsProdutosAsync(IPage page)
    {
        // Tenta coletar links de produto diretamente
        var links = await page.QuerySelectorAllAsync("a[href*='/produto/']");

        if (links.Count == 0)
        {
            Console.WriteLine("  Nenhum link /produto/ encontrado na pagina.");
            return [];
        }

        Console.WriteLine($"  {links.Count} links /produto/ encontrados. Aplicando filtro de packs...");

        var urls  = new List<string>();
        var packs = new List<string>();

        foreach (var link in links)
        {
            var href = await link.GetAttributeAsync("href") ?? "";
            if (string.IsNullOrEmpty(href)) continue;

            // Monta URL absoluta
            if (href.StartsWith("/")) href = "https://www.paodeacucar.com" + href;

            // Evita duplicatas
            if (urls.Contains(href) || packs.Contains(href)) continue;

            // Tenta obter o nome do produto a partir do texto do link ou do card pai
            var nomeEl = await link.QuerySelectorAsync("[class*='name'], [class*='title'], h3, h2, span");
            var nome   = nomeEl is not null ? await nomeEl.InnerTextAsync() : await link.InnerTextAsync();

            // Filtro 1: verifica nome do produto
            if (FiltroUtils.EhPack(nome))
            {
                packs.Add(href);
                Console.WriteLine($"    [PACK IGNORADO] {nome.Trim()}");
                continue;
            }

            // Filtro 2: verifica URL (alguns slugs contem 'pack')
            if (FiltroUtils.EhPack(href))
            {
                packs.Add(href);
                Console.WriteLine($"    [PACK NA URL IGNORADO] {href}");
                continue;
            }

            urls.Add(href);
        }

        Console.WriteLine($"  Resultado: {urls.Count} validos | {packs.Count} packs ignorados");
        return urls;
    }

    // =========================================================================
    // PROCESSA CADA PRODUTO EM ABA SEPARADA
    // =========================================================================
    private async Task<List<Produto>> ProcessarProdutosAsync(
        IBrowserContext context, List<string> urls, int paginaListagem)
    {
        var lista = new List<Produto>();

        for (var i = 0; i < urls.Count; i++)
        {
            var url = urls[i];
            Console.WriteLine($"  [{i + 1:D2}/{urls.Count:D2}] {url}");

            IPage? page = null;
            try
            {
                page = await context.NewPageAsync();
                await ConfigurarPaginaAsync(page);

                if (!await Navegar(page, url, timeout: 45_000))
                {
                    Console.WriteLine("         Falha ao navegar. Pulando produto.");
                    continue;
                }

                // Aguarda produto renderizar
                await AguardarProdutoIndividualAsync(page);

                // Salva HTML bruto
                var html        = await page.ContentAsync();
                var nomeArquivo = GerarNomeArquivo(url, paginaListagem, i + 1);
                var caminho     = Path.Combine(_config.PastaHtmlProdutos, nomeArquivo);
                await File.WriteAllTextAsync(caminho, html, System.Text.Encoding.UTF8);
                Console.WriteLine($"         HTML: {nomeArquivo} ({html.Length:N0} chars)");

                // Extrai dados estruturados
                var produto = await ExtrairDadosProdutoAsync(page, url, caminho);

                // Confirmacao final do filtro de pack (pelo nome real da pagina)
                if (FiltroUtils.EhPack(produto.Nome))
                {
                    Console.WriteLine($"         [PACK CONFIRMADO NA PAGINA — IGNORANDO] {produto.Nome}");
                    continue;
                }

                Console.WriteLine($"         Nome: {produto.Nome} | Preco: {produto.PrecoVenda}");
                lista.Add(produto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"         Erro: {ex.Message}");
            }
            finally
            {
                if (page is not null) await page.CloseAsync();
            }

            await Task.Delay(Random.Shared.Next(1500, 3000));
        }

        return lista;
    }

    // =========================================================================
    // AGUARDA PRODUTO INDIVIDUAL RENDERIZAR
    // =========================================================================
    private static async Task AguardarProdutoIndividualAsync(IPage page)
    {
        var seletores = new[]
        {
            "h1",
            "[class*='price'], [class*='Price']",
            "[data-testid*='product-name']",
            "[itemprop='name']",
        };

        foreach (var sel in seletores)
        {
            try
            {
                await page.WaitForSelectorAsync(sel, new PageWaitForSelectorOptions { Timeout = 10_000 });
                return;
            }
            catch { }
        }

        // Fallback: aguarda fixo
        await Task.Delay(3000);
    }

    // =========================================================================
    // EXTRAI DADOS DO PRODUTO
    // =========================================================================
    private static async Task<Produto> ExtrairDadosProdutoAsync(IPage page, string url, string htmlPath)
    {
        var produto = new Produto { Url = url, HtmlPath = htmlPath };

        // ── Nome ─────────────────────────────────────────────────────────────
        produto.Nome = await TextoAsync(page,
            "h1[class*='title'], h1[class*='name'], h1[itemprop='name'], h1");

        // ── Marca ─────────────────────────────────────────────────────────────
        produto.Marca = await TextoAsync(page,
            "[class*='brand'], [itemprop='brand'], [data-testid*='brand']");

        // ── Volume (extraido do nome se nao tiver campo proprio) ──────────────
        produto.Volume = await TextoAsync(page, "[class*='volume'], [data-testid*='weight']");
        if (string.IsNullOrEmpty(produto.Volume))
            produto.Volume = FiltroUtils.ExtrairVolume(produto.Nome);

        // ── Preco de venda ────────────────────────────────────────────────────
        produto.PrecoVenda = await TextoAsync(page,
            "[class*='price__value'], [class*='PriceValue'], [class*='salePriceValue'], " +
            "[itemprop='price'], [data-testid*='price'], [class*='price-value'], " +
            "span[class*='Price']:not([class*='club']):not([class*='Club'])");

        // ── Preco clube ───────────────────────────────────────────────────────
        produto.PrecoClube = await TextoAsync(page,
            "[class*='club-price'], [class*='clubPrice'], [class*='ClubPrice'], " +
            "[class*='preco-cliente'], [data-testid*='club']");

        // ── Tabela nutricional ────────────────────────────────────────────────
        await ExtrairNutricaoAsync(page, produto);

        return produto;
    }

    // =========================================================================
    // EXTRAI TABELA NUTRICIONAL
    // =========================================================================
    private static async Task ExtrairNutricaoAsync(IPage page, Produto produto)
    {
        // Tenta expandir a secao nutricional se estiver colapsada
        var botaoNutricao = await page.QuerySelectorAsync(
            "button[class*='nutrition'], button[aria-label*='nutri'], " +
            "[data-testid*='nutrition'] button, [class*='NutritionTable'] button, " +
            "button:has-text('Nutri'), button:has-text('Informa')");

        if (botaoNutricao is not null)
        {
            try
            {
                await botaoNutricao.ClickAsync();
                await Task.Delay(1000);
                Console.WriteLine("         Secao nutricional expandida.");
            }
            catch { }
        }

        // Seletores da tabela nutricional
        var seletoresTabela = new[]
        {
            "table[class*='nutrition'] tr",
            "table[class*='Nutrition'] tr",
            "[class*='NutritionTable'] tr",
            "[class*='nutrition-table'] tr",
            "[class*='nutritional'] tr",
            "[data-testid*='nutrition'] tr",
        };

        IReadOnlyList<IElementHandle>? linhas = null;
        foreach (var sel in seletoresTabela)
        {
            var encontradas = await page.QuerySelectorAllAsync(sel);
            if (encontradas.Count > 0)
            {
                Console.WriteLine($"         Tabela nutricional: '{sel}' ({encontradas.Count} linhas)");
                linhas = encontradas;
                break;
            }
        }

        if (linhas is null || linhas.Count == 0)
        {
            Console.WriteLine("         Tabela nutricional nao encontrada.");
            return;
        }

        // Parseia cada linha da tabela
        foreach (var linha in linhas)
        {
            try
            {
                var colunas = await linha.QuerySelectorAllAsync("td, th");
                if (colunas.Count < 2) continue;

                var chave = (await colunas[0].InnerTextAsync()).Trim();
                var valor = (await colunas[1].InnerTextAsync()).Trim();

                if (string.IsNullOrWhiteSpace(chave)) continue;

                // Armazena no dicionario completo
                produto.NutricaoCompleta[chave] = valor;

                // Mapeia para campos tipados (case-insensitive)
                var chLower = chave.ToLower();
                if (produto.Porcao        == "" && (chLower.Contains("por") && chLower.Contains("o")))
                    produto.Porcao = valor;
                else if (produto.Calorias     == "" && (chLower.Contains("energia") || chLower.Contains("caloria") || chLower.Contains("kcal")))
                    produto.Calorias = valor;
                else if (produto.Carboidratos == "" && chLower.Contains("carboidrato"))
                    produto.Carboidratos = valor;
                else if (produto.Acucares     == "" && chLower.Contains("a") && chLower.Contains("car"))
                    produto.Acucares = valor;
                else if (produto.Sodio        == "" && chLower.Contains("s") && chLower.Contains("dio"))
                    produto.Sodio = valor;
                else if (produto.Proteinas    == "" && chLower.Contains("prote"))
                    produto.Proteinas = valor;
                else if (produto.GordurasTotais == "" && chLower.Contains("gordura") && chLower.Contains("total"))
                    produto.GordurasTotais = valor;
            }
            catch { }
        }
    }

    // =========================================================================
    // PROXIMA PAGINA
    // =========================================================================
    private static async Task<bool> TemProximaPaginaAsync(IPage page, int paginaAtual)
    {
        // Tenta seletores do botao de proxima pagina
        var seletores = new[]
        {
            $"a[aria-label*='proxima']:not([disabled])",
            $"a[aria-label*='Proxima']:not([disabled])",
            $"button[aria-label*='next']:not([disabled])",
            $"[data-testid*='next-page']:not([disabled])",
            $"a[class*='next']:not([disabled])",
            $"li.next a",
        };

        foreach (var sel in seletores)
        {
            if (await page.QuerySelectorAsync(sel) is not null) return true;
        }

        // Fallback: verifica se pagina N+1 teria conteudo
        // (sera detectado pelo AguardarProdutosAsync na proxima iteracao)
        return true; // deixa o loop tentar; break vira de produtos vazios
    }

    // =========================================================================
    // CONTEXTO ANTI-DETECCAO (reaproveitado do VivaReal)
    // =========================================================================
    private static async Task<IBrowserContext> CriarContextoAsync(IBrowser browser)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent    = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1366, Height = 768 },
            Locale       = "pt-BR",
            TimezoneId   = "America/Sao_Paulo",
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Accept-Language"] = "pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7",
                ["Accept"]          = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
                ["DNT"]             = "1",
            },
        });

        await context.AddInitScriptAsync(@"
            delete Object.getPrototypeOf(navigator).webdriver;
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
            Object.defineProperty(navigator, 'languages', { get: () => ['pt-BR', 'pt', 'en-US'] });
            Object.defineProperty(navigator, 'plugins', {
                get: () => ({ length: 3,
                    0: { name: 'PDF Viewer' },
                    1: { name: 'Chrome PDF Viewer' },
                    2: { name: 'Chromium PDF Viewer' } })
            });
            window.chrome = { runtime: {}, loadTimes: function(){}, csi: function(){} };
            const origQuery = window.navigator.permissions?.query;
            if (origQuery) {
                window.navigator.permissions.query = (p) =>
                    p.name === 'notifications'
                        ? Promise.resolve({ state: Notification.permission })
                        : origQuery.call(navigator.permissions, p);
            }
        ");

        return context;
    }

    private async Task ConfigurarPaginaAsync(IPage page)
    {
        if (_config.BloquearMidia)
        {
            await page.RouteAsync(
                "**/*.{png,jpg,jpeg,gif,svg,woff,woff2,ttf,otf,mp4,webm}",
                async route => await route.AbortAsync());
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    /// <summary>Navega para a URL com ate 3 tentativas e delay progressivo.</summary>
    private static async Task<bool> Navegar(IPage page, string url, int timeout = 60_000)
    {
        for (var tentativa = 1; tentativa <= 3; tentativa++)
        {
            try
            {
                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.Load,
                    Timeout   = timeout,
                });
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Tentativa {tentativa}/3: {ex.Message}");
                if (tentativa < 3) await Task.Delay(3000 * tentativa);
            }
        }
        return false;
    }

    /// <summary>Tenta varios seletores CSS e retorna o InnerText do primeiro que encontrar.</summary>
    private static async Task<string> TextoAsync(IPage page, string seletores)
    {
        foreach (var sel in seletores.Split(',').Select(s => s.Trim()))
        {
            try
            {
                var el = await page.QuerySelectorAsync(sel);
                if (el is null) continue;
                var txt = await el.InnerTextAsync();
                if (!string.IsNullOrWhiteSpace(txt)) return txt.Trim();
            }
            catch { }
        }
        return "";
    }

    /// <summary>Scroll suave para forcar carregamento lazy-load dos cards.</summary>
    private static async Task ScrollSuaveAsync(IPage page)
    {
        var altura = await page.EvaluateAsync<int>("document.body.scrollHeight");
        var atual  = 0;
        while (atual < altura)
        {
            atual += Random.Shared.Next(200, 400);
            await page.EvaluateAsync($"window.scrollTo(0, {atual})");
            await Task.Delay(Random.Shared.Next(60, 140));
        }
        await page.EvaluateAsync("window.scrollTo(0, 0)");
        await Task.Delay(500);
    }

    private static async Task<TipoPagina> DiagnosticarAsync(IPage page, string label)
    {
        var title = await page.TitleAsync();
        var html  = await page.ContentAsync();

        Console.WriteLine($"  Titulo: {title}  |  HTML: {html.Length:N0} chars");

        if (html.Length < 5000)
        {
            await SalvarDebugAsync(page, label);
            return TipoPagina.Bloqueio;
        }

        var titleLower = title.ToLower();
        if (titleLower.Contains("access denied") || titleLower.Contains("blocked")
            || titleLower.Contains("captcha")    || titleLower.Contains("cloudflare")
            || titleLower.Contains("just a moment"))
        {
            await SalvarDebugAsync(page, label);
            return TipoPagina.Bloqueio;
        }

        var bodyText = await page.EvaluateAsync<string>(
            "document.body?.innerText?.toLowerCase() ?? ''");

        if ((bodyText.Contains("captcha") && bodyText.Contains("verify"))
            || bodyText.Contains("access denied")
            || bodyText.Contains("enable javascript and cookies"))
        {
            await SalvarDebugAsync(page, label);
            return TipoPagina.Bloqueio;
        }

        if (bodyText.Contains("nenhum resultado") || bodyText.Contains("nenhum produto"))
            return TipoPagina.SemResultados;

        return TipoPagina.ResultadosPresentes;
    }

    private static string GerarNomeArquivo(string url, int pagina, int indice)
    {
        var uri      = new Uri(url);
        var segmentos = uri.AbsolutePath.Trim('/').Split('/');
        var slug     = segmentos.LastOrDefault(s => s.Length > 0) ?? $"produto_{indice}";
        var nome     = string.Concat(slug.Take(60)).Replace(" ", "_").Replace("/", "_");
        return $"p{pagina:D2}_{indice:D2}_{nome}.html";
    }

    private static async Task SalvarDebugAsync(IPage page, string label)
    {
        var arquivo = $"debug_{label}.html";
        await File.WriteAllTextAsync(arquivo, await page.ContentAsync());
        Console.WriteLine($"  Debug salvo: {Path.GetFullPath(arquivo)}");
    }

    private enum TipoPagina { ResultadosPresentes, SemResultados, Bloqueio }
}
