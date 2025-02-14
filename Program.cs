using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using WebDriverManager.DriverConfigs.Impl;
using WebDriverManager;

class Program
{
    private static readonly HttpClient client = new HttpClient();

    static async Task Main(string[] args)
    {
        string option;
        do
        {
            // Menu de Opções
            Console.WriteLine("==== Menu de Opções ====");
            Console.WriteLine("1 - Baixar capítulos em PDFs separados");
            Console.WriteLine("2 - Baixar capítulos e combiná-los em um único PDF");
            Console.WriteLine("3 - Sair");
            Console.Write("Escolha uma opção (1, 2 ou 3): ");
            option = Console.ReadLine();

            if (option == "1" || option == "2")
            {
                // Solicitar o nome do anime/mangá
                Console.WriteLine("Informe o nome do anime/mangá:");
                string animeName = Console.ReadLine();

                // Solicitar a URL base
                Console.WriteLine("Informe a URL base (exemplo: https://mangaonline.biz/capitulo/one-piece-capitulo-):");
                string baseUrl = Console.ReadLine();

                // Perguntar se a URL finaliza com "/" ou ".html"
                Console.WriteLine("A URL finaliza com '/' ou '.html'? Digite apenas '/' ou 'html':");
                string urlFormat = Console.ReadLine().Trim().ToLower();

                if (urlFormat != "/" && urlFormat != "html")
                {
                    Console.WriteLine("Formato inválido! O programa só aceita '/' ou 'html'.");
                    return;
                }

                // Solicitar o capítulo inicial e final
                Console.WriteLine("Informe o capítulo inicial (exemplo: 1):");
                string startChapter = Console.ReadLine();

                Console.WriteLine("Informe o capítulo final (exemplo: 10):");
                string endChapter = Console.ReadLine();

                // Criar pasta de saída
                string outputFolder = Path.Combine(Directory.GetCurrentDirectory(), animeName);
                Directory.CreateDirectory(outputFolder);

                Console.WriteLine($"Processando capítulos de {startChapter} a {endChapter}...");

                // Gerar a lista de capítulos
                var chapterList = GenerateChapterList(startChapter, endChapter);

                if (option == "1")
                {
                    foreach (string chapter in chapterList)
                    {
                        string chapterUrl = (urlFormat == "/") ? $"{baseUrl}{chapter}/" : $"{baseUrl}{chapter}.html";
                        string chapterPdfPath = Path.Combine(outputFolder, $"{animeName}-Capitulo-{chapter}.pdf");

                        Console.WriteLine($"Baixando capítulo: {chapterUrl}");

                        try
                        {
                            var imageUrls = (urlFormat == "html") ? GetImageUrlsWithSelenium(chapterUrl) : await GetImageUrlsFromChapter(chapterUrl);

                            if (imageUrls.Count == 0)
                            {
                                Console.WriteLine($"Nenhuma imagem encontrada no capítulo {chapter}. Pulando...");
                                continue;
                            }

                            using (PdfWriter writer = new PdfWriter(chapterPdfPath))
                            using (PdfDocument pdf = new PdfDocument(writer))
                            {
                                Document document = new Document(pdf);

                                foreach (var imageUrl in imageUrls)
                                {
                                    Console.WriteLine($"Baixando imagem: {imageUrl}");
                                    var imageData = await DownloadImage(imageUrl);

                                    if (imageData != null)
                                    {
                                        var image = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(imageData));
                                        image.SetAutoScale(true);
                                        image.SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER);
                                        document.Add(image);
                                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                                    }
                                }
                            }

                            Console.WriteLine($"PDF do capítulo {chapter} salvo em: {chapterPdfPath}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Erro ao processar o capítulo {chapter}: {ex.Message}");
                        }
                    }
                }
                else if (option == "2")
                {
                    string combinedPdfPath = Path.Combine(outputFolder, $"{animeName}-Capitulos-{startChapter}-a-{endChapter}.pdf");

                    using (PdfWriter writer = new PdfWriter(combinedPdfPath))
                    using (PdfDocument pdf = new PdfDocument(writer))
                    {
                        Document document = new Document(pdf);

                        foreach (string chapter in chapterList)
                        {
                            string chapterUrl = (urlFormat == "/") ? $"{baseUrl}{chapter}/" : $"{baseUrl}{chapter}.html";

                            Console.WriteLine($"Baixando capítulo: {chapterUrl}");

                            try
                            {
                                var imageUrls = (urlFormat == "html") ? GetImageUrlsWithSelenium(chapterUrl) : await GetImageUrlsFromChapter(chapterUrl);

                                if (imageUrls.Count == 0)
                                {
                                    Console.WriteLine($"Nenhuma imagem encontrada no capítulo {chapter}. Pulando...");
                                    continue;
                                }

                                foreach (var imageUrl in imageUrls)
                                {
                                    Console.WriteLine($"Baixando imagem: {imageUrl}");
                                    var imageData = await DownloadImage(imageUrl);

                                    if (imageData != null)
                                    {
                                        var image = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(imageData));
                                        image.SetAutoScale(true);
                                        image.SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER);
                                        document.Add(image);
                                        document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                                    }
                                }

                                document.Add(new AreaBreak(AreaBreakType.NEXT_PAGE));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao processar o capítulo {chapter}: {ex.Message}");
                            }
                        }
                    }

                    Console.WriteLine($"PDF combinado salvo em: {combinedPdfPath}");
                }

                Console.WriteLine("Operação concluída!");
            }
            else if (option == "3")
            {
                Console.WriteLine("Saindo... Obrigado por usar o programa!");
            }
            else
            {
                Console.WriteLine("Opção inválida. Tente novamente.");
            }

        } while (option != "3");
    }

    static List<string> GetImageUrlsWithSelenium(string url)
    {
        var imageUrls = new List<string>();

        new DriverManager().SetUpDriver(new ChromeConfig());

        var options = new ChromeOptions();
        options.AddArgument("--headless");

        using (var driver = new ChromeDriver(options))
        {
            driver.Navigate().GoToUrl(url);
            System.Threading.Thread.Sleep(3000);

            var imageElements = driver.FindElements(By.XPath("//div[@class='image-gallery']//img"));

            foreach (var img in imageElements)
            {
                string src = img.GetAttribute("src") ?? img.GetAttribute("data-src");
                if (!string.IsNullOrEmpty(src))
                {
                    imageUrls.Add(src);
                }
            }
        }

        return imageUrls;
    }

    static async Task<List<string>> GetImageUrlsFromChapter(string url)
    {
        var imageUrls = new List<string>();

        try
        {
            var response = await client.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);

            var nodes = htmlDoc.DocumentNode.SelectNodes("//p/img");

            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    var src = node.GetAttributeValue("src", null);
                    if (!string.IsNullOrEmpty(src))
                    {
                        imageUrls.Add(src);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao obter URLs de imagens: {ex.Message}");
        }

        return imageUrls;
    }

    static async Task<byte[]> DownloadImage(string imageUrl)
    {
        try
        {
            return await client.GetByteArrayAsync(imageUrl);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao baixar imagem {imageUrl}: {ex.Message}");
            return null;
        }
    }

    static List<string> GenerateChapterList(string start, string end)
    {
        var chapterList = new List<string>();

        try
        {
            int startMain = int.Parse(start.Split('-')[0]);
            int endMain = int.Parse(end.Split('-')[0]);

            for (int i = startMain; i <= endMain; i++)
            {
                if (i == endMain && end.Contains('-'))
                {
                    int subChapters = int.Parse(end.Split('-')[1]);
                    for (int j = 1; j <= subChapters; j++)
                    {
                        chapterList.Add($"{i}-{j}");
                    }
                }
                else
                {
                    chapterList.Add(i.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao gerar lista de capítulos: {ex.Message}");
        }

        return chapterList;
    }
}
