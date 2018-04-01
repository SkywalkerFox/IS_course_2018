﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Parser.Html;

namespace IS
{
    class Program
    {
        private const string mainUrl = "http://www.mathnet.ru";
        private const string url = "http://www.mathnet.ru/php/archive.phtml?jrnid=uzku&wshow=issue&bshow=contents&series=0&year=2017&volume=159&issue=1&option_lang=rus&bookID=1681";

        private static string directory = new DirectoryInfo(Environment.CurrentDirectory).FullName;

        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            MainAsync(args).GetAwaiter().GetResult();

            CreateInvertedIndex("mystem");
            CreateInvertedIndex("porter");
        }

        private static async Task MainAsync(string[] args)
        {
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(url);

            await Task1(document);
        }

        private static async Task Task1(IDocument document)
        {
            List<string> articleLinks = new List<string>();

            var parser = new HtmlParser();

            XElement xml = new XElement(new XElement("articles", new XAttribute("link", url), new XAttribute("year", "2017")));

            foreach (IElement element in document.QuerySelectorAll("td[width='90%'] a.SLink"))
            {
                articleLinks.Add(element.GetAttribute("href"));

                var link = mainUrl + element.GetAttribute("href");
                var articleDoc = await document.Context.OpenAsync(mainUrl + element.GetAttribute("href"));
                var title = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(element.TextContent));

                string annotation = "";
                var part = articleDoc.QuerySelectorAll("b").Where(e => e.TextContent.Contains("Аннотация:")).ElementAt(0).NextSibling;
                while (!part.NextSibling.TextContent.Contains("Ключ"))
                {
                    annotation += part.TextContent.Trim();
                    part = part.NextSibling;
                }
                annotation.Trim();

                var mystem = Mystem(title + " " + annotation);

                var porter = PorterForString(title + " " + annotation);

                var keywords = articleDoc.QuerySelectorAll("b ~ i").Where(e => e != null).ElementAt(0).TextContent.Trim();

                xml.Add(new XElement("article", new XAttribute("link", link),
                    new XElement("title", title),
                    new XElement("annotation", annotation),
                    new XElement("mystem", mystem),
                    new XElement("porter", porter),
                    new XElement("keywords", keywords)
                ));
            }
            Console.WriteLine(xml.ToString());
            xml.Save("C:\\Projects\\IS\\document.xml");
        }

        private static string Mystem(string input)
        {
            // File.WriteAllText("C:\\Projects\\IS\\input.txt", input);
            File.WriteAllText(directory + "\\input.txt", input);

            Process p = new Process();
            string output = "";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = "mystem.exe";
            p.StartInfo.Arguments = "-l -d input.txt";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();

            using(var reader = new StreamReader(p.StandardOutput.BaseStream, Encoding.UTF8))
            {
                output = reader.ReadToEnd();
            }
            p.WaitForExit();

            return output;
        }

        private static string PorterForString(string input)
        {
            string[] words = input.Split(' ');
            string output = "";

            foreach (var item in words)
            {

                output += Porter.TransformingWord(item.Trim(new Char[] { '(', ')', '.', ',' })) + " ";
            }

            return output;
        }

        //Task3
        private static void CreateInvertedIndex(string type)
        {
            XElement inputXML = XElement.Load("document.xml");
            var elements = inputXML.Elements("article");
            var termsDictionary = new SortedDictionary<string, List<string>>();

            for (int i = 0; i < elements.Count(); i++)
            {
                string doc = elements.ElementAt(i).Attribute("link").Value;
                string[] terms;
                if (type.Equals("mystem"))
                {
                    terms = elements.ElementAt(i).Element(type).Value.Trim(new Char[] { '{', '}' }).Split("}{");
                }
                else
                {
                    terms = elements.ElementAt(i).Element(type).Value.Trim(new Char[] { '(', ')', '.', ',' }).Split(" ");
                }

                foreach (var term in terms)
                {
                    if (termsDictionary.TryGetValue(term, out List<string> docList))
                    {
                        if (!docList.Contains(doc))
                        {
                            docList.Add(doc);
                        }
                    }
                    else
                    {
                        var tempList = new List<string>() { doc };
                        termsDictionary[term] = tempList;
                    }
                }

                // terms.AddRange(value.Split("}{").ToList());
                // Console.WriteLine(item.Value.Contains("{"));                
                // Console.WriteLine(String.Join(", ", item.Element("mystem").Value.Split("}{").ToList()));
            }

            XElement xml = new XElement(new XElement("terms",
                new XElement(type)
            ));

            foreach (KeyValuePair<string, List<string>> term in termsDictionary)
            {
                xml.Add(new XElement("term", new XAttribute("name", term.Key),
                    new XElement("docs", String.Join(", ", term.Value.ToArray()))
                ));
            }

            Console.WriteLine(xml);
            Console.WriteLine(directory);
            xml.Save(directory + "\\" + type + "_index.xml");
            // Console.WriteLine(String.Join(", ", words.ToArray()));

        }

    }

}