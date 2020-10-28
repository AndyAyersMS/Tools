using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// read pgo dump with class profiles and summarize

public class Method
{
    public uint token;
    public uint hash;
    public uint ilSize;
    public List<CallSite> callSites;
}

public class HistogramEntry
{
    public ulong methodTable;
    public string className;
    public uint count;
}

public class Histogram
{
    public List<HistogramEntry> entries;

}

public class CallSite
{
    public Method method;
    public uint ilOffset;
    public uint samples;
    public uint entries;
    public uint total;
    public Histogram data;
    public bool isVirtual;

    public HistogramEntry DominantEntry(double likelihood = 0.5) => data.entries.Where(x => (double) x.count >= (double) total * likelihood).FirstOrDefault();
    public double DominantLikelihood() => data.entries.Max(x => x.count) / (double) total;
    public double DominantCalls() => DominantLikelihood() * samples;
}

class Program
{
    static void Main(string[] args)
    {
        string[] readText = File.ReadAllLines(args[0]);

        List<Method> methods = new List<Method>();
        List<CallSite> callSites = new List<CallSite>();
        Method m = null;
        CallSite cs = null;
        Histogram h = null;
        foreach (string s in readText)
        {
            try
            {
            if (s.StartsWith("@@@"))
            {
                // @@@ token 0x06004EBA hash 0xBC4945F9 ilSize 0x00000019 records 0x00000005 index 126964
                //
                m = new Method();
                cs = null;
                h = null;
                
                string[] data = s.Split(" ");
                // m.token = Convert.ToUInt32(data[2]);
                // m.hash = Convert.ToUInt32(data[4]);
                // m.ilSize = Convert.ToUInt32(data[6]);

                methods.Add(m);
            }
            else if (s.StartsWith("classProfile"))
            {
                // classProfile iloffs 7 samples 1 entries 1 totalCount 1 virtual
                //
                cs = new CallSite();
                h = null;

                string[] data = s.Split(" ");

                cs.method = m;
                cs.ilOffset = Convert.ToUInt32(data[2]);
                cs.samples = Convert.ToUInt32(data[4]);
                cs.entries = Convert.ToUInt32(data[6]);
                cs.total = Convert.ToUInt32(data[8]);
                cs.isVirtual = data[9].Equals("virtual");

                h = new Histogram();
                h.entries = new List<HistogramEntry>();

                cs.data = h;

                if (m.callSites == null)
                {
                    m.callSites = new List<CallSite>();
                }
                m.callSites.Add(cs);
                callSites.Add(cs);
            }
            else if (s.StartsWith("class"))
            {
                // class 00007FF8BD8BEC10 (Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax) count 23
                //
                HistogramEntry e = new HistogramEntry();

                string[] data = s.Split(" ");

                e.methodTable = 0;
                e.className = data[2]; // not quite; class name may have embedded spaces
                e.count = Convert.ToUInt32(data[data.Length-1]);

                h.entries.Add(e);
            }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Can't hack {s}: {e}");
                break;
            }
        }

        Console.WriteLine($"{methods.Count,6:D} methods");

        Analyze(callSites, "all sites");
        Analyze(callSites.Where(x => x.isVirtual), "virtual sites");
        Analyze(callSites.Where(x => !x.isVirtual), "interface sites");
    }

    static void Analyze(IEnumerable<CallSite> callSites, string kind)
    {
        // Profiled sites
        
        var profiledSites = callSites.Where(x => x.samples > 0);
        var missedSites = callSites.Where(x => x.samples == 0);
        var monomorphicSites = callSites.Where(x => x.entries == 1);
        var bimorphicSites = callSites.Where(x => x.entries == 2);
        var polymorphicSites = callSites.Where(x => x.entries > 2);
        var polymorphicPredictableSites = polymorphicSites.Where(x => x.DominantEntry() != null);
        var polymorphicUnpredictableSites = polymorphicSites.Where(x => x.DominantEntry() == null);
        var polymorphicMarginalSites = polymorphicUnpredictableSites.Where(x => x.DominantEntry(0.3) != null);
        var polymorphicRemainderSites = polymorphicSites.Where(x => x.DominantEntry(0.3) == null);
        
        Console.WriteLine($"\n--- static data for {kind}--- \n");
        
        Console.WriteLine($"{callSites.Count(),10:D}           total call sites");
        Console.WriteLine($"{missedSites.Count(),10:D} [{((double) missedSites.Count() / callSites.Count()),7:P}] sites were not hit at runtime");
        Console.WriteLine($"{profiledSites.Count(),10:D} [{((double) profiledSites.Count() / callSites.Count()),7:P}] sites were hit at runtime");
        Console.WriteLine($"{monomorphicSites.Count(),10:D} [{((double) monomorphicSites.Count() / callSites.Count()),7:P}] sites were monomorphic");
        Console.WriteLine($"{bimorphicSites.Count(),10:D} [{((double) bimorphicSites.Count() / callSites.Count()),7:P}] sites were bimorphic");
        Console.WriteLine($"{polymorphicSites.Count(),10:D} [{((double) polymorphicSites.Count() / callSites.Count()),7:P}] sites were polymorphic");
        Console.WriteLine($"{polymorphicPredictableSites.Count(),10:D} [{((double) polymorphicPredictableSites.Count() / callSites.Count()),7:P}] sites were polymorphic predictable (0.5+)");
        Console.WriteLine($"{polymorphicMarginalSites.Count(),10:D} [{((double) polymorphicMarginalSites.Count() / callSites.Count()),7:P}] sites were polymorphic marginally predictable (0.3-0.5)");
        Console.WriteLine($"{polymorphicRemainderSites.Count(),10:D} [{((double) polymorphicRemainderSites.Count() / callSites.Count()),7:P}] sites were truly polymorphic");
        
        var gdvSites = monomorphicSites.Count() + bimorphicSites.Count() + polymorphicPredictableSites.Count() + polymorphicMarginalSites.Count();
        
        Console.WriteLine($"\n   GDV would make predictions at {gdvSites} out of {callSites.Count()} sites ==> {((double)gdvSites / callSites.Count()):P}");
        
        Console.WriteLine($"\n--- dynamic data for {kind} --- \n");
        
        var profiledCalls = profiledSites.Sum(x => x.samples);
        var monomorphicCalls = monomorphicSites.Sum(x => x.samples);
        var bimorphicCalls = bimorphicSites.Sum(x => x.samples);
        var polymorphicCalls = polymorphicSites.Sum(x => x.samples);
        var polymorphicPredictableCalls = polymorphicPredictableSites.Sum(x => x.samples);
        var polymorphicMarginalCalls = polymorphicMarginalSites.Sum(x => x.samples);
        var polymorphicRemainderCalls = polymorphicRemainderSites.Sum(x => x.samples);
        
        Console.WriteLine($"{profiledCalls,10:D}           total calls");
        Console.WriteLine($"{monomorphicCalls,10:D} [{((double) monomorphicCalls / profiledCalls),7:P}] calls were monomorphic");
        Console.WriteLine($"{bimorphicCalls,10:D} [{((double) bimorphicCalls / profiledCalls),7:P}] calls were bimorphic");
        Console.WriteLine($"{polymorphicCalls,10:D} [{((double) polymorphicCalls / profiledCalls),7:P}] calls were polymorphic");
        Console.WriteLine($"{polymorphicPredictableCalls,10:D} [{((double) polymorphicPredictableCalls / profiledCalls),7:P}] calls were polymorphic and predictable");
        Console.WriteLine($"{polymorphicMarginalCalls,10:D} [{((double) polymorphicMarginalCalls / profiledCalls),7:P}] calls were polymorphic and marginally predictable");
        Console.WriteLine($"{polymorphicRemainderCalls,10:D} [{((double) polymorphicRemainderCalls / profiledCalls),7:P}] calls were truly polymorphic");
        
        // can't really take full credit for these, let's adjust.
        
        var bimorphicWins = (long) bimorphicSites.Sum(x => x.DominantCalls());
        var polymorphicPredictableWins = (long) polymorphicPredictableSites.Sum(x => x.DominantCalls());
        var polymorphicMarginalWins = (long) polymorphicMarginalSites.Sum(x => x.DominantCalls());
        
        var gdvImpact = monomorphicCalls + bimorphicWins + polymorphicPredictableWins + polymorphicMarginalWins;
        
        Console.WriteLine($"\n  GDV would correctly predict {gdvImpact} out of {profiledCalls} calls ==> {((double)gdvImpact / profiledCalls):P}\n");
        Console.WriteLine($"{monomorphicCalls,10:D} monomorphic");
        Console.WriteLine($"{bimorphicWins,10:D} [{((double) bimorphicWins / bimorphicCalls),7:P}] bimorphic");       
        Console.WriteLine($"{polymorphicPredictableWins,10:D} [{((double) polymorphicPredictableWins / polymorphicPredictableCalls),7:P}] polymorphic predictable");       
        Console.WriteLine($"{polymorphicMarginalWins,10:D} [{((double) polymorphicMarginalWins / polymorphicMarginalCalls),7:P}] polymorphic marginal");       
        
        var polymorphicRemainderWins = (long) polymorphicRemainderSites.Sum(x => x.DominantCalls());
        
        Console.WriteLine($"\n  GDV would likely not try and predict these polymorphic calls, though it might pay off if virtual\n");
        
        Console.WriteLine($"{polymorphicRemainderWins,10:D} [{((double) polymorphicRemainderWins / polymorphicRemainderCalls),7:P}] polymorphic remainder");       
    }
}

