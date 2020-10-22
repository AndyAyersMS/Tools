# Tools
Various useful programs for working with .NET or the .NET Jit.

# Self-disassembly

# InlineXml

## To produce Inline Xml

Set

`COMPlus_JitInlineDumpXml=N`

where N is
* 0 -- no xml
* 1 -- full xml for all methods, including failures (in DEBUG/CHK)
* 2 -- xml only for methods with inlines, including failures (in DEBUG/CHK)
* 3 -- xml only for methods with inlines, no failures

The inline xml is current written to stderr.

## To analyze Inline Xml

```
cd analyze
dotnet run <inline-xml-file>
```

## To use Inline Xml to script (replay) inlining

You can feed inline Xml into the jit to force it perform particular inlines.
The inline xml can come from the jit as above, or you can hand craft the xml
or modify the jit's xml.

To enable replay, set

`COMPlus_JitInlinePolicyReplay=1`
`COMPlus_JitInlineReplayFile=inlines.xml`

Methods not mentioned in the inline XML will not have any inlines performed.

For an example of how all this can be used to programmatically explore
the impact of inlines on a program, see the
[PerformanceExplorer](https://github.com/AndyAyersMS/PerformanceExplorer)
repo.

# Sparse solvers

Useful for investigating how to do profile reconstruction.
