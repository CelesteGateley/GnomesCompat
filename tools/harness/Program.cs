using Mono.Cecil;
var managed = args[0];
BepInEx.Logging.Logger.Listeners.Add(new L());
var r = new DefaultAssemblyResolver(); r.AddSearchDirectory(managed);
var asm = AssemblyDefinition.ReadAssembly(System.IO.Path.Combine(managed, "Assembly-CSharp.dll"), new ReaderParameters { AssemblyResolver = r });
GnomesCompatPatcher.Patch(asm);
asm.Write(args[1]);
System.Console.WriteLine("written " + args[1]);
class L : BepInEx.Logging.ILogListener { public void LogEvent(object s, BepInEx.Logging.LogEventArgs e) => System.Console.WriteLine($"[{e.Level}:{e.Source.SourceName}] {e.Data}"); public void Dispose(){} }
