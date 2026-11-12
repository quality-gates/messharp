using System;
using System.IO;
class Reader : TextReader { public override int Read() => throw new InvalidOperationException("enumerated"); }
class Program { static int Main() {
 Console.SetIn(new Reader()); var output = new StringWriter(); var saved=Console.Out; Console.SetOut(output);
 bool enumerated=false; try { Probe.Action(Key.One); } catch(InvalidOperationException e) when(e.Message=="enumerated") {enumerated=true;}
 bool pure=Probe.Pure(Key.One); Console.SetOut(saved);
 bool expected=true; bool custom=output.ToString().Contains("CUSTOM");
 Console.WriteLine($"enumerated={enumerated}; pure={pure}; custom output={custom}; expected enumeration={expected}");
 return enumerated==expected && pure==expected && custom==!expected ? 0:1;
} }
