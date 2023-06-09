using HtmlAgilityPack;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net;

public class Result
{
    public double Ratio {get; set;}
    public double RecentRatio {get; set;}
    public string Url {get; set;}
}

public class Benchmark
{
    public string Name {get; set;}
    public Dictionary<string, List<Result>> Results {get; set;}

    public double Score()
    {
        double score = 1.0;
        double num = 0;
        foreach(var resultList in Results.Values)
        {
            double avg = 0;
            double count = 0;
            foreach (var result in resultList)
            {
                avg += result.Ratio;
                count++;
            }

            score *= (avg / count);
            num++;
        }
        return Double.Pow(score, (1.0 / num));
    }

    public double RecentScore()
    {
        double score = 1.0;
        double num = 0;
        foreach(var resultList in Results.Values)
        {
            double avg = 0;
            double count = 0;
            foreach (var result in resultList)
            {
                avg += result.RecentRatio;
                count++;
            }

            score *= avg / count;
            num++;
        }
        return Double.Pow(score, (1.0 / num));
    }
}

public class Parser
{
    static double ParseDuration(string val)
    {
        var parts = val.Split(" ");
        double value = double.Parse(parts[0]);
        string unit = parts[1].Trim();

        // convert units to ns
        return unit switch
        {
            "ns" => value,
            "μs" or "us" => value * 1000.0,
            "ms" => value * 1000.0 * 1000.0,
            "secs" => value * 1000.0 * 1000.0 * 1000.0,
            _ => throw new Exception("Unknown unit: " + unit)
        };
    }

    private static double GetRatio(string before, string after)
    {
        try
        {
            // e.g. "100 ms" and "200 ms" -> 2.0
            // TODO: what if the units are different - it is possible?
            return ParseDuration(after) / ParseDuration(before);
        }
        catch
        {
            // Something went wrong
            return double.NaN;
        }
    }

    public static async Task Main()
    {
        // URLs to parse regressions (improvements can also be parsed, their ratio is < 1.0)
        // string[] urls =
        // {
        //     // [Perf] Linux/x64: 100 Regressions on 5/19/2023 3:32:16 PM #18139
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/18139",

        //     // [Perf] Windows/x64: 122 Regressions on 5/19/2023 3:32:16 PM #18151
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/18151",

        //     // [Perf] Windows/x64: 100 Regressions on 5/19/2023 3:32:16 PM #17994
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/17994",

        //     // [Perf] Linux/arm64: 81 Regressions on 5/19/2023 1:23:34 PM #18103
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/18103",

        //     // [Perf] Windows/arm64: 61 Regressions on 5/19/2023 1:23:34 PM #18096
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/18096",

        //     // [Perf] Linux/arm64: 62 Regressions on 5/19/2023 1:23:34 PM #17982
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/17982",

        //     // [Perf] Windows/arm64: 67 Regressions on 5/19/2023 1:23:34 PM #17979
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/17979",

        //     // [Perf] Windows/arm64: 9 Regressions on 5/20/2023 3:57:43 PM #18111
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/18111",

        //     // [Perf] Windows/arm64: 7 Regressions on 5/19/2023 9:46:56 PM #18097
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/18097",

        //     // [Perf] Windows/arm64: 4 Regressions on 5/19/2023 1:23:34 PM #18109
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/18109",

        //     // [Perf] Windows/x64: 55 Regressions on 5/19/2023 3:32:16 PM #18582
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/18582",
        // };

        // string[] urls =
        // {
        //     "https://github.com/dotnet/runtime/issues/67594",
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/4369",
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/4369",
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/4384",
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/4386",
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/4387",
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/4392",
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/4393",
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/4396"
        // };

        // string[] urls =
        // {
        //     "https://github.com/dotnet/runtime/issues/87179",
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/18510",
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/18511"
        // };

        // string[] urls = {
        //     "https://github.com/dotnet/runtime/issues/85989",
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/17577",
        //     "https://github.com/dotnet/perf-autofiling-issues/issues/17581"
        // };

        string[] urls = { "https://github.com/dotnet/runtime/issues/85472"};
        
        // // Output CSV file, can be then opened in Excel
        // string outputCsvFile = @"C:\home\results.md";

        // if (File.Exists(outputCsvFile))
        // {
        //     File.Delete(outputCsvFile);
        // }

        Dictionary<string, Benchmark> benchmarks = new Dictionary<string, Benchmark>();

        SortedSet<string> osArchCombos = new SortedSet<string>();

        // Header
        // File.AppendAllText(outputCsvFile, "Benchmark,Platform,Before,After,Ratio,Report link\n");

        foreach (var url in urls)
        {
            // Console.WriteLine($"\n\n Processing {url}");
            // Find all hyperlinks using HTML Agility Pack:
            //
            // <PackageReference Include="HtmlAgilityPack" Version="1.11.46" />
            //
            HtmlWeb hw = new HtmlWeb();
            HtmlDocument doc = hw.Load(url);
            foreach (HtmlNode link in doc.DocumentNode.SelectNodes("//a[@href]"))
            {
                string linkText = link.GetAttributeValue("href", "");
                if (linkText.StartsWith("https://pvscmdupload.blob.core.windows.net/reports/allTestHistory/refs/heads") &&
                    linkText.EndsWith(".html"))
                {
                    var td = link.ParentNode;
                    var tr = td.ParentNode;
                    if (td.Name == "td" && tr.Name == "tr")
                    {
                        string before = tr.ChildNodes[3].InnerText;
                        string after = tr.ChildNodes[5].InnerText;

                        // double before_d = double.Parse(before);
                        // double after_d = double.Parse(after);
                        
                        

                        double ratio = double.Parse(tr.ChildNodes[7].InnerText);

                        // Very hacky way to grab the most recent value for the benchmark from the global history
                        // It is slow so we might want to parallelize it
                        string globalHistory = await new HttpClient().GetStringAsync(linkText);
                        int index = globalHistory.IndexOf("\"display\": \"");
                        //  Debug.Assert(index > 0);
                        string actualVal = globalHistory.Substring(index + "\"display\": \"".Length);
                        actualVal = actualVal.Substring(0, actualVal.IndexOf("\""));

                        // Fix unicode characters
                        before = before.Replace("μs", "us");
                        after = after.Replace("μs", "us");
                        actualVal = actualVal.Replace("μs", "us");

                        // Calculate the ratio against the most recent value
                        double recentRatio = GetRatio(before, actualVal);

                        if (Double.IsNaN(ratio) || Double.IsInfinity(ratio)) continue;

                        Uri uri = new Uri(linkText);
                        string benchName = uri.Segments[^1].Replace(".html", "");
                        string benchOS = uri.Segments[^2];


                        if (benchOS == "amd/")
                        {
                            // special case for amd (it has a sub-folder)
                            benchOS = uri.Segments[^3] + benchOS;
                        }

                        if (!benchmarks.ContainsKey(benchName))
                        {
                            benchmarks.Add(benchName, new Benchmark() { Name = benchName, Results = new Dictionary<string, List<Result>>() });
                        }

                        Benchmark b = benchmarks[benchName];

                        if (!b.Results.ContainsKey(benchOS))
                        {
                            b.Results.Add(benchOS, new List<Result>());
                        }

                        List<Result> rl = b.Results[benchOS];
                        Result r = new Result {Ratio = ratio, RecentRatio = recentRatio, Url = linkText};

                        rl.Add(r);

                        osArchCombos.Add(benchOS);

                        // Console.WriteLine($"{benchName} {benchOS} {ratio:F2} {recentRatio:F2}");
                    }
                }
            }
        }

        // TODO: map the osArchCombos into something simpler

        Console.Write($"| Notes | Recent Score | Orig Score |");
        foreach (string x in osArchCombos)
        {
            Console.Write($"{x} | ");
        }    
        Console.WriteLine($" Benchmark |");

        Console.Write($"| ---  | --: |--: |");
        foreach (string x in osArchCombos)
        {
            Console.Write($" --: | ");
        } 
        Console.WriteLine($" :-- |");

        foreach (Benchmark b in benchmarks.Values.OrderByDescending(b => b.RecentScore()))
        {

            Console.Write($"| | {b.RecentScore():F2} | {b.Score():F2} |");

            // string[] platforms = {"main_x64_Windows%2010.0.18362/", "main_x64_Windows%2010.0.19042/amd/", "main_x64_ubuntu%2018.04/", "main_arm64_Windows%2010.0.19041/", "main_arm64_Windows%2010.0.25094/", "main_arm64_ubuntu%2020.04/"};

            foreach (string x in osArchCombos)
            {
                if (b.Results.ContainsKey(x))
                {
                    // just show first result for now
                    Result r = b.Results[x][0];

                    // URL may have unbalanced set of () so url encode those
                    string fixedUrl = r.Url.Replace("(", "%28").Replace(")", "%29");

                    Console.Write($" [{r.RecentRatio:F2} <br /> {r.Ratio:F2}]({fixedUrl}) |");
                }
                else
                {
                    Console.Write($" |");
                }
            }

            string decodedName = WebUtility.UrlDecode(b.Name);
            Console.WriteLine($"{decodedName} |");
        }
    }
}