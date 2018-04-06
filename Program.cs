using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Accord.Math;
using Accord.Math.Decompositions;
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
        private static SortedDictionary<string, List<string>> termsDictionary = CreateInvertedIndex("porter");

        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // MainAsync(args).GetAwaiter().GetResult();

            // CreateInvertedIndex("mystem");
            // CreateInvertedIndex("porter");

            // string phrase = TypePhrase();
            // Intersection(phrase);

            Task5();

            // Task6();
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

            using (var reader = new StreamReader(p.StandardOutput.BaseStream, Encoding.UTF8))
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
        private static SortedDictionary<string, List<string>> CreateInvertedIndex(string type)
        {
            XElement inputXML = XElement.Load("document.xml");
            var elements = inputXML.Elements("article");
            var termsDictionary = new SortedDictionary<string, List<string>>();
            var pattern = Regex.Escape(@"$\{125,96,1;1,48,125\}$") + "|" +
                Regex.Escape(@"m(w)=(2\pi)^{-1}\ln\omega(w)$") + "|" +
                Regex.Escape(@"$k_0(w,\overline\omega)$и$l_0(w,\omega)$") + "| 3|" +
                Regex.Escape(@"$t$") + "|" +
                Regex.Escape(@"$\mathrm") + "|" +
                Regex.Escape(@"–") + "|" +
                Regex.Escape(@"$gq(4,6)$") + "|" +
                Regex.Escape(@"$t$,$2&lt;t\leq3$") + "|" +
                Regex.Escape(@"$t=1,2,\dots$в") + "|" +
                Regex.Escape(@",$\{176,150,1;1,25,176\}$и$\{256,204,1;1,51,256\}$.") + "|" +
                Regex.Escape(@"$\omega=\omega(w)$") + "|" +
                Regex.Escape(@"$\omega(w)$") + "|" +
                Regex.Escape(@"$t$") + "|" +
                Regex.Escape(@"$2&lt;t\leq3$") + "|" +
                @"\d" + "|" +
                Regex.Escape(@"$") + "|" +
                Regex.Escape(@"t\leq") + "|" +
                Regex.Escape(@"<");

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
                    terms = Regex.Replace(elements.ElementAt(i).Element(type).Value, pattern, " ").Trim(new Char[] { '(', ')', '.', ',', '“' }).Split();
                }

                foreach (var term in terms)
                {
                    var tempTerm = term.Trim(new Char[] { '(', ')', '.', ',', '“', ' ', '”', ':' });

                    if (tempTerm.Equals(""))
                    {
                        continue;
                    }

                    if (termsDictionary.TryGetValue(tempTerm, out List<string> docList))
                    {
                        if (!docList.Contains(doc))
                        {
                            docList.Add(doc);
                        }
                    }
                    else
                    {
                        var tempList = new List<string>() { doc };
                        termsDictionary[tempTerm] = tempList;
                    }

                }

            }

            XElement xml = new XElement(new XElement("terms",
                new XElement(type, "")
            ));

            foreach (KeyValuePair<string, List<string>> term in termsDictionary)
            {
                XElement newNode = new XElement("term", new XAttribute("name", term.Key),
                    new XElement("docs", new XAttribute("count", term.Value.Count), ""));
                foreach (var value in term.Value)
                {
                    newNode.Descendants("docs").LastOrDefault().Add(new XElement("doc", value));
                }
                xml.Descendants(type).LastOrDefault().Add(newNode);
            }

            xml.Save(directory + "\\" + type + "_index.xml");

            return termsDictionary;

        }

        private static string TypePhrase()
        {
            Console.WriteLine("Type terms and press Enter: ");
            string phrase = Console.ReadLine();

            return phrase;
        }

        //Task4
        private static IEnumerable<string> Intersection(string phrase, out string[] words)
        {
            words = phrase.Trim().Split(" ");
            bool exceptSign = false;

            IEnumerable<string> result = null;

            foreach (var word in words)
            {
                var porterWord = word;
                if (porterWord.ElementAt(0).Equals('-'))
                {
                    exceptSign = true;
                    porterWord = porterWord.Remove(0, 1);
                }

                porterWord = PorterForString(porterWord).Trim();

                if (termsDictionary.ContainsKey(porterWord))
                {
                    if (!exceptSign)
                    {
                        result = result == null ? termsDictionary[porterWord] : result.Intersect(termsDictionary[porterWord]);
                    }
                    else
                    {
                        result = result.Except(termsDictionary[porterWord]);
                    }
                }
                else
                {
                    Console.WriteLine(porterWord + " isn't found in the dictionary");
                    result = null;
                    break;
                }

                exceptSign = false;
            }

            if (result != null)
            {
                Console.WriteLine("Intersection: " + String.Join(", ", result));
            }
            else
            {
                Console.WriteLine("There are no intersections");
            }

            return result;

        }

        private static IEnumerable<string> Union(string phrase, out string[] words)
        {
            words = phrase.Trim().Split(" ");

            IEnumerable<string> result = null;

            foreach (var word in words)
            {
                var porterWord = word;
                porterWord = PorterForString(porterWord).Trim();

                if (termsDictionary.ContainsKey(porterWord))
                {
                    result = result == null ? termsDictionary[porterWord] : result.Union(termsDictionary[porterWord]);

                }
                else
                {
                    Console.WriteLine(porterWord + " isn't found in the dictionary");
                    result = null;
                    break;
                }

            }

            if (result != null)
            {
                Console.WriteLine("Union: " + String.Join(", ", result));
            }
            else
            {
                Console.WriteLine("There are no unions");
            }

            return result;
        }

        //TF-IDF
        private static void Task5()
        {
            string phrase = TypePhrase();
            string[] words;
            var docs = Union(phrase, out words);
            float TFIDF = 0;
            float zoneCoef = 1;

            Dictionary<string, float> scoresDictionary = new Dictionary<string, float>();

            foreach (var word in words)
            {
                var porterWord = PorterForString(word).Trim();
                Console.WriteLine("word: " + word);

                foreach (var doc in docs)
                {
                    TFIDF = TF(porterWord, doc) * IDF(porterWord);

                    zoneCoef = GetZoneCoef(word, doc);

                    if (scoresDictionary.ContainsKey(doc))
                    {
                        scoresDictionary[doc] += TFIDF * zoneCoef;
                    }
                    else
                    {
                        scoresDictionary[doc] = TFIDF * zoneCoef;
                    }

                    Console.WriteLine("article link: " + doc);
                    Console.WriteLine("tf-idf = " + TFIDF);
                    Console.WriteLine(" ");
                }
                Console.WriteLine("-----------------------------");
            }

            var sortedScores = from entry in scoresDictionary orderby entry.Value descending select entry;

            foreach (KeyValuePair<string, float> score in sortedScores)
            {
                Console.WriteLine("Score for " + score.Key + " = " + score.Value);
            }


        }

        private static float GetZoneCoef(string word, string doc)
        {
            XElement inputXML = XElement.Load("document.xml");
            var articleText = inputXML.Descendants().Where(e => (string)e.Attribute("link") == doc).FirstOrDefault();

            if (articleText.Element("title").Value.Contains(word))
            {
                // Console.WriteLine("doc " + doc + ", word " + word);
                return 0.6f;
            }
            if (articleText.Element("annotation").Value.Contains(word))
            {
                return 0.4f;
            }

            return 1;
        }

        private static float TF(string word, string doc)
        {
            XElement inputXML = XElement.Load("document.xml");
            var parsedText = inputXML.Descendants().Where(e => (string)e.Attribute("link") == doc).FirstOrDefault().Element("porter").Value;

            var pattern = Regex.Escape(@"$\{125,96,1;1,48,125\}$") + "|" +
                Regex.Escape(@"m(w)=(2\pi)^{-1}\ln\omega(w)$") + "|" +
                Regex.Escape(@"$k_0(w,\overline\omega)$и$l_0(w,\omega)$") + "| 3|" +
                Regex.Escape(@"$t$") + "|" +
                Regex.Escape(@"$\mathrm") + "|" +
                Regex.Escape(@"–") + "|" +
                Regex.Escape(@"$gq(4,6)$") + "|" +
                Regex.Escape(@"$t$,$2&lt;t\leq3$") + "|" +
                Regex.Escape(@"$t=1,2,\dots$в") + "|" +
                Regex.Escape(@",$\{176,150,1;1,25,176\}$и$\{256,204,1;1,51,256\}$.") + "|" +
                Regex.Escape(@"$\omega=\omega(w)$") + "|" +
                Regex.Escape(@"$\omega(w)$") + "|" +
                Regex.Escape(@"$t$") + "|" +
                Regex.Escape(@"$2&lt;t\leq3$") + "|" +
                @"\d" + "|" +
                Regex.Escape(@"$") + "|" +
                Regex.Escape(@"t\leq") + "|" +
                Regex.Escape(@"<");

            var terms = Regex.Replace(parsedText, pattern, " ").Trim(new Char[] { '(', ')', '.', ',', '“' }).Split();

            float count = 0;

            foreach (var term in terms)
            {
                var tempTerm = term.Trim(new Char[] { '(', ')', '.', ',', '“', ' ', '”', ':' });

                if (string.Equals(word, tempTerm))
                {
                    count++;
                }
            }

            return count / terms.Length;
        }

        private static float IDF(string word)
        {
            XElement inputXML = XElement.Load("document.xml");
            var elements = inputXML.Elements("article");

            return MathF.Log((float)elements.Count() / (float)termsDictionary[word].Count);
        }

        //SVD and more
        private static void Task6()
        {
            var phrase = TypePhrase();
            var termsWeightsMap = new SortedDictionary<string, List<int>>();
            var docsRefs = new List<string>();

            // Step 1
            var A = CreateMatrixA(out termsWeightsMap, out docsRefs);
            var queryVec = CreateQueryVector(phrase, termsWeightsMap);

            // Step 2
            SingularValueDecompositionF svd = new SingularValueDecompositionF(A);
            var U = svd.LeftSingularVectors;
            var S = svd.DiagonalMatrix;
            var V = svd.RightSingularVectors;
            var Vt = V.Transpose();

            // Step 3
            var newRank = 7;
            var U_rank = U.Get(0, U.GetLength(0), 0, newRank);
            var S_rank = S.Get(0, newRank, 0, newRank);
            var V_rank = V.Get(0, V.GetLength(0), 0, newRank);
            var Vt_rank = V_rank.Transpose();

            // Step 4
            float[][] docsVectors = GetNewDocVecCoordinates(V_rank);

            // Step 5
            var newQueryVec = GetNewQueryVector(queryVec, U_rank, S_rank);

            // Step 6
            var scores = CalculateSimForDocs(newQueryVec, docsVectors, docsRefs);
            foreach (var score in scores)
            {
                Console.WriteLine("Score for " + score.Key + " = " + score.Value);
            }

        }

        private static IOrderedEnumerable<KeyValuePair<string, float>> CalculateSimForDocs(float[] queryVec, float[][] docsVectors, List<string> docsRefs)
        {
            var scores = new Dictionary<string, float>();


            for (int i = 0; i < docsVectors.GetLength(0); i++)
            {
                scores[docsRefs.ElementAt(i)] = Sim(queryVec, docsVectors[i]);
            }

            var orderedScores = from score in scores orderby score.Value descending select score;

            return orderedScores;
        }

        private static float Sim(float[] queryVec, float[] docVec)
        {
            float num = 0;
            float denom = 0;
            float lengthQuery = 0;
            float lengthDoc = 0;

            for (int i = 0; i < queryVec.Length; i++)
            {
                num += queryVec[i] * docVec[i];
                lengthQuery += queryVec[i] * queryVec[i];
                lengthDoc += docVec[i] * docVec[i];
                // Console.WriteLine(lengthQuery);
                // Console.WriteLine(lengthDoc);
            }
            denom = MathF.Sqrt(lengthQuery) * MathF.Sqrt(lengthDoc);
            

            return num / denom;
        }

        private static float[] GetNewQueryVector(float[] queryVec, float[,] U_rank, float[,] S_rank)
        {
            var S_rank_inv = S_rank.Inverse();

            return queryVec.Dot(U_rank).Dot(S_rank_inv);

        }

        private static float[][] GetNewDocVecCoordinates(float[,] V_rank)
        {
            float[][] docsVectors = new float[V_rank.GetLength(0)][];

            for (int i = 0; i < V_rank.GetLength(0); i++)
            {
                docsVectors[i] = new float[V_rank.GetLength(1)];
                for (int j = 0; j < V_rank.GetLength(1); j++)
                {
                    docsVectors[i][j] = V_rank[i, j];
                }
            }

            return docsVectors;
        }

        private static float[] CreateQueryVector(string phrase, SortedDictionary<string, List<int>> termsWeightsMap)
        {
            var termsPhrase = PorterForString(phrase);
            var terms = termsPhrase.Split();
            float[] queryVec = Enumerable.Repeat(0f, termsWeightsMap.Keys.Count).ToArray();

            for (int i = 0; i < terms.Length; i++)
            {

                for (int j = 0; j < queryVec.Length; j++)
                {
                    if (termsWeightsMap.ElementAt(j).Key.Equals(terms[i]))
                    {
                        queryVec[j]++;
                    }
                    
                }
            }

            return queryVec;
        }

        private static float[,] CreateMatrixA(out SortedDictionary<string, List<int>> dictionary, out List<string> docsRefs)
        {
            XElement inputXML = XElement.Load("document.xml");
            var elements = inputXML.Elements("article");
            var termsDictionary = new SortedDictionary<string, List<int>>();
            var pattern = Regex.Escape(@"$\{125,96,1;1,48,125\}$") + "|" +
                Regex.Escape(@"m(w)=(2\pi)^{-1}\ln\omega(w)$") + "|" +
                Regex.Escape(@"$k_0(w,\overline\omega)$и$l_0(w,\omega)$") + "| 3|" +
                Regex.Escape(@"$t$") + "|" +
                Regex.Escape(@"$\mathrm") + "|" +
                Regex.Escape(@"–") + "|" +
                Regex.Escape(@"$gq(4,6)$") + "|" +
                Regex.Escape(@"$t$,$2&lt;t\leq3$") + "|" +
                Regex.Escape(@"$t=1,2,\dots$в") + "|" +
                Regex.Escape(@",$\{176,150,1;1,25,176\}$и$\{256,204,1;1,51,256\}$.") + "|" +
                Regex.Escape(@"$\omega=\omega(w)$") + "|" +
                Regex.Escape(@"$\omega(w)$") + "|" +
                Regex.Escape(@"$t$") + "|" +
                Regex.Escape(@"$2&lt;t\leq3$") + "|" +
                @"\d" + "|" +
                Regex.Escape(@"$") + "|" +
                Regex.Escape(@"t\leq") + "|" +
                Regex.Escape(@"<");

            docsRefs = new List<string>();
            

            for (int i = 0; i < elements.Count(); i++)
            {
                docsRefs.Add(elements.ElementAt(i).FirstAttribute.Value);
                var terms = Regex.Replace(elements.ElementAt(i).Element("porter").Value, pattern, " ").Trim(new Char[] { '(', ')', '.', ',', '“' }).Split();

                foreach (var term in terms)
                {
                    var tempTerm = term.Trim(new Char[] { '(', ')', '.', ',', '“', ' ', '”', ':' });

                    if (tempTerm.Equals(""))
                    {
                        continue;
                    }

                    if (termsDictionary.TryGetValue(tempTerm, out List<int> weightsList))
                    {
                        weightsList[i]++;
                    }
                    else
                    {
                        var tempList = Enumerable.Repeat(0, elements.Count()).ToList();
                        tempList[i]++;
                        termsDictionary[tempTerm] = tempList;
                    }

                }
            }

            float[,] A = new float[termsDictionary.Keys.Count, 10];
            int n = 0;
            int j = 0;

            for (n = 0; n < termsDictionary.Keys.Count; n++)
            {
                for (j = 0; j < 10; j++)
                {
                    A[n, j] = termsDictionary.ElementAt(n).Value[j];

                }

            }

            dictionary = termsDictionary;
            return A;

        }
    }

}