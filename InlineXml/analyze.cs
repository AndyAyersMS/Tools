// Summarize an inline replay log

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Serialization;
using System.Text;
using System.Xml.Linq;

public class InlineForest
{
    public string Policy;
    public string DataSchema;
    public Method[] Methods;
}

public class Method
{
    public Method()
    {
    }
    
    public static int HasMoreInlines(Method x, Method y)
    {
        return (int) y.InlineCount - (int) x.InlineCount;
    }
    
    public void Dump()
    {
        Console.WriteLine("Inlines into {0} {1:X8}", Name, Token);
        foreach (Inline x in Inlines)
        {
            x.Dump(2);
        }
    }

    public int CountInlines(Dictionary<string, int> reasons)
    {
        int count = 0;

        foreach (var x in Inlines)
        {
            count += x.CountInlines(reasons);
        }

        return count;
    }

    public int CountForceInlineExtras(Dictionary<string, int> reasons)
    {
        int count = 0;

        foreach (var x in Inlines)
        {
            if (! (x is FailedInline))
            {
                if (x.Reason == "aggressive inline attribute")
                {
                    count += x.CountInlines(reasons);
                }
                else
                {
                    count += x.CountForceInlineExtras(reasons);
                }
            }
        }

        return count;
    }

    public int CountFailedInlines(Dictionary<string, int> reasons)
    {
        int count = 0;

        foreach (var x in Inlines)
        {
            count += x.CountFailedInlines(reasons);
        }

        return count;
    }

    [XmlIgnore]    
    public uint Token;

    [XmlIgnore]
    public uint Hash;

    [XmlElement(ElementName="Token")]
    public string HexToken
    {
        get
        {
            // convert int to hex representation
            return Token.ToString("x");
        }
        set
        {
            // convert hex representation back to int
            Token = uint.Parse(value, 
                System.Globalization.NumberStyles.HexNumber);
        }
    }

    [XmlElement(ElementName="Hash")]
    public string HexHash
    {
        get
        {
            // convert int to hex representation
            return Hash.ToString("x");
        }
        set
        {
            // convert hex representation back to int
            Hash = uint.Parse(value, 
                System.Globalization.NumberStyles.HexNumber);
        }
    }

    public string Name;
    public uint InlineCount;
    public uint HotSize;
    public uint ColdSize;
    public uint JitTime;
    public uint SizeEstimate;
    public uint TimeEstimate;

    [XmlArrayItem("Inline", typeof(Inline))]
    [XmlArrayItem("FailedInline", typeof(FailedInline))]
    public Inline[] Inlines { get; set; }
}

public class Inline
{
    [XmlIgnore]
    public uint Token;

    [XmlIgnore]
    public uint Hash;

    [XmlElement(ElementName = "Token")]
    public string HexToken
    {
        get
        {
            // convert int to hex representation
            return Token.ToString("x");
        }
        set
        {
            // convert hex representation back to int
            Token = uint.Parse(value,
                System.Globalization.NumberStyles.HexNumber);
        }
    }

    [XmlElement(ElementName = "Hash")]
    public string HexHash
    {
        get
        {
            // convert int to hex representation
            return Hash.ToString("x");
        }
        set
        {
            // convert hex representation back to int
            Hash = uint.Parse(value,
                System.Globalization.NumberStyles.HexNumber);
        }
    }

    public uint Offset;
    public uint CollectData;
    public string Reason;
    public string Data;

    [XmlArrayItem("Inline", typeof(Inline))]
    [XmlArrayItem("FailedInline", typeof(FailedInline))]
    public Inline[] Inlines { get; set; }

    public int CountInlines(Dictionary<string, int> reasons)
    {
        if (this is FailedInline)
        {
            return 0;
        }

        int count = 1;

        if (!reasons.ContainsKey(Reason))
        {
            reasons[Reason] = 1;
        }
        else
        {
            reasons[Reason]++;
        }

        foreach (var x in Inlines)
        {
            count += x.CountInlines(reasons);
        }
        return count;
    }

    public int CountForceInlineExtras(Dictionary<string, int> reasons)
    {
        int count = 0;

        foreach (var x in Inlines)
        {
            if (! (x is FailedInline))
            {
                if (x.Reason == "aggressive inline attribute")
                {
                    count += x.CountInlines(reasons);
                }
                else
                {
                    count += x.CountForceInlineExtras(reasons);
                }
            }
        }

        return count;
    }

    public int CountFailedInlines(Dictionary<string, int> reasons)
    {
        if (this is FailedInline)
        {
            if (!reasons.ContainsKey(Reason))
            {
                reasons[Reason] = 1;
            }
            else
            {
                reasons[Reason]++;
            }
            return 1;
        }

        int count = 0;
        foreach (var x in Inlines)
        {
            if (x is Inline)
            {
                count += x.CountFailedInlines(reasons);
            }
        }
        return count;
    }

    public virtual void Dump(int indent)
    {
        for (int i = 0; i < indent; i++) Console.Write(" ");
        Console.WriteLine("{0:X8} {1}", Token, Reason);
        foreach (Inline x in Inlines)
        {
            x.Dump(indent + 2);
        }
    }
}

public class FailedInline : Inline
{
    public override void Dump(int indent)
    {
        for (int i = 0; i < indent; i++) Console.Write(" ");
        Console.WriteLine("FAILED: {0:X8} {1}", Token, Reason);
    }
}

public class P
{
    public static void Main(string[] args)
    {
        InlineForest forest = null;
        try
        {
            Stream xmlFile = new FileStream(args[0], FileMode.Open);
            XmlSerializer xml = new XmlSerializer(typeof(InlineForest));
            forest = (InlineForest)xml.Deserialize(xmlFile);
        }
        catch (System.Exception ex)
        {
            Console.WriteLine("Xml deserialization failed: " + ex.Message);
            if (ex.InnerException != null)
            {
                Console.WriteLine("... " + ex.InnerException.Message);
            }
            return;
        }
        
        var successReasons = new Dictionary<string, int>();
        var failReasons = new Dictionary<string, int>();
        var extraReasons = new Dictionary<string, int>();
        long inlineCount = forest.Methods.Sum(m => m.CountInlines(successReasons));
        long failedInlineCount = forest.Methods.Sum(m => m.CountFailedInlines(failReasons));
        long forceInlineExtraCount = forest.Methods.Sum(m => m.CountForceInlineExtras(extraReasons));
        Console.WriteLine("Overall Stats");
        Console.WriteLine("{0} methods with inlines, {1} inlines, {2} failed, {3} extra", 
            forest.Methods.Length, inlineCount, failedInlineCount, forceInlineExtraCount);

        Console.WriteLine("");
        Console.WriteLine("Success Breakdown");
        var successes = successReasons.OrderByDescending(p => p.Value);
        foreach (var s in successes)
        {
            Console.WriteLine("{0,6} {1,7:P2} {2}", s.Value, (double) s.Value/inlineCount, s.Key);
        }

        Console.WriteLine("");
        Console.WriteLine("Failure Breakdown");
        var failures = failReasons.OrderByDescending(p => p.Value);
        foreach (var f in failures)
        {
            Console.WriteLine("{0,6} {1,7:P2} {2}", f.Value, (double) f.Value/failedInlineCount, f.Key);
        }

        Console.WriteLine("");
        Console.WriteLine("Extra Breakdown");
        var extras = extraReasons.OrderByDescending(p => p.Value);
        foreach (var e in extras)
        {
            Console.WriteLine("{0,6} {1,7:P2} {2}", e.Value, (double) e.Value/forceInlineExtraCount, e.Key);
        }
    }
}
