// See https://aka.ms/new-console-template for more information
using CsvHelper;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Text;
using System;
using Newtonsoft.Json.Linq;
using System.Reflection.PortableExecutable;
using Newtonsoft.Json;


namespace Application
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Start!");
            //ScrapeSubito();
        }


        void ScrapeSubito()
        {
            var whereToScrape = new ConcurrentBag<string> {
        "milano",
        "varese",
        "como",
        "novara",
        "monza"
    };
            string what = "supporto+tastiera"; // "cassettiera";

            var subitoProducts = new ConcurrentBag<SubitoProduct>();

            // creating the HAP object 
            var web = new HtmlWeb();

            // setting a global User-Agent header in HAP 
            web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/109.0.0.0 Safari/537.36";

            // the maximum number of pages to scrape before stopping 
            int limit = 300;

            Parallel.ForEach(
                whereToScrape,
                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                currentWhere =>
                {

                    // until there are no pages to scrape or limit is hit 
                    for (int page = 1; page <= limit; page++)
                    {
                        // loading the target web page 
                        var urlSubito = GetUrlSubito(currentWhere, what, page);
                        var document = web.Load(urlSubito);

                        var productHTMLElements = document.DocumentNode.QuerySelectorAll("a.SmallCard-module_link__hOkzY");

                        if (productHTMLElements.Count == 0)
                            break;

                        foreach (var productHTMLElement in productHTMLElements)
                        {
                            if (productHTMLElement.QuerySelector("span.item-sold-badge") != null)
                                continue;

                            // scraping the interesting data from the current HTML element 
                            var url = HtmlEntity.DeEntitize(productHTMLElement.QuerySelector("a.link").Attributes["href"].Value);
                            var title = HtmlEntity.DeEntitize(productHTMLElement.QuerySelector("h2.ItemTitle-module_item-title__VuKDo").InnerText);
                            var image = HtmlEntity.DeEntitize(productHTMLElement.QuerySelector("img,CardImage-module_photo__WMsiO").Attributes["src"].Value);
                            var city = HtmlEntity.DeEntitize(productHTMLElement.QuerySelector("span.index-module_town__2H3jy").InnerText);
                            var prov = HtmlEntity.DeEntitize(productHTMLElement.QuerySelector("span.city").InnerText);
                            //var publicaton = HtmlEntity.DeEntitize(productHTMLElement.QuerySelector("span.index-module_date__Fmf-4").InnerText);
                            var price = HtmlEntity.DeEntitize(productHTMLElement.QuerySelector("p.price").InnerText);
                            var distance = GetDistance("legnano", city);
                            var spedizioneDisponibile = (productHTMLElement.QuerySelector("span.shipping-badge") != null ? "yes" : "");

                            var subitoProduct = new SubitoProduct() { Url = url, Image = image, Title = title, 
                                City = city, Prov = prov, Price = GetPrice(price), Distance = distance };
                            // adding the object containing the scraped data to the list 
                            subitoProducts.Add(subitoProduct);
                        }
                    }
                }
                );

            // initializing the CSV output file 
            using (var writer = new StreamWriter("subito-products.csv"))
            // initializing the CSV writer 
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                // populating the CSV file 
                csv.WriteRecords(subitoProducts);
            }
        }

        string GetUrlSubito(string where, string what, int page)
        {
            return $"https://www.subito.it/annunci-lombardia/vendita/usato/{where}/?q={what}{(page > 1 ? "&o=" + page : "")}";
        }

        string GetPrice(string price)
        {
            return price.Replace("€", "").Replace(".", "").Replace("Spedizione disponibile", "");
            //return decimal.Parse(price, NumberStyles.Currency, CultureInfo.GetCultureInfo("it-IT").NumberFormat);
        }

        const string OpenRouteToken = "5b3ce3597851110001cf624853fac863027a40f2a411232d18bad937";
        static Dictionary<string, decimal> distanze = new Dictionary<string, decimal>();

        static decimal GetDistance(string from, string to)
        {

            // https://openrouteservice.org/dev/#/api-docs/introduction
            if (distanze.ContainsKey(to))
                return distanze[to];

            string url = $"https://api.openrouteservice.org/geocode/search?api_key={OpenRouteToken}&text={from}&boundary.country=IT";
            var ris = WebGetResult(url);
            dynamic results0 = JsonConvert.DeserializeObject<dynamic>(ris);
            var coordinates0 = results0.features[0].geometry.coordinates;

            url = $"https://api.openrouteservice.org/geocode/search?api_key={OpenRouteToken}&text={to}&boundary.country=IT";
            ris = WebGetResult(url);
            dynamic results1 = JsonConvert.DeserializeObject<dynamic>(ris);
            var coordinates1 = results1.features[0].geometry.coordinates;

            url = $"https://api.openrouteservice.org/v2/directions/driving-car?api_key={OpenRouteToken}&start={coordinates0[0] + "," + coordinates0[1]}&end={coordinates1[0] + "," + coordinates1[1]}";
            ris = WebGetResult(url);
            dynamic results = JsonConvert.DeserializeObject<dynamic>(ris);
            decimal distance = results.features[0].properties.summary.distance; // metri
            decimal duration = results.features[0].properties.summary.duration; // secondi

            distanze.Add(to, distance);
            return distance;
        }

        static string WebGetResult(string url)
        {
            WebRequest request = WebRequest.Create(url);
            using (WebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }


        public class SubitoProduct
        {
            public string? Title { get; set; }
            public string? City { get; set; }
            public string? Prov { get; set; }
            public decimal? Distance { get; set; }
            public string? Publication { get; set; }
            public string? Price { get; set; }
            public string? SpedizioneDisponibile { get; set; }
            public string? Url { get; set; }
            public string? Image { get; set; }
        }
    }
}