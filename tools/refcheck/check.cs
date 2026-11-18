using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
var game = Load(args[0]); var mod = Load(args[1]);
var gmd = game.md; var mmd = mod.md;
// index game types: fullname -> (fields, methods)
var types = new Dictionary<string,(HashSet<string> f, Dictionary<string,List<int>> m)>();
foreach (var th in gmd.TypeDefinitions) { var t = gmd.GetTypeDefinition(th); var n = FullName(gmd, t);
  var f = new HashSet<string>(t.GetFields().Select(x => gmd.GetString(gmd.GetFieldDefinition(x).Name)));
  var m = new Dictionary<string,List<int>>();
  foreach (var mh in t.GetMethods()) { var md2 = gmd.GetMethodDefinition(mh); var nm = gmd.GetString(md2.Name); if(!m.ContainsKey(nm)) m[nm]=new(); m[nm].Add(md2.GetParameters().Count(p=>gmd.GetParameter(p).SequenceNumber>0)); }
  types[n] = (f, m); }
string TRName(MetadataReader r, TypeReferenceHandle h){ var tr=r.GetTypeReference(h); var ns=r.GetString(tr.Namespace); var n=r.GetString(tr.Name); if (tr.ResolutionScope.Kind==HandleKind.TypeReference) return TRName(r,(TypeReferenceHandle)tr.ResolutionScope)+"/"+n; return (ns.Length>0?ns+".":"")+n; }
string Scope(MetadataReader r, TypeReferenceHandle h){ var tr=r.GetTypeReference(h); if (tr.ResolutionScope.Kind==HandleKind.TypeReference) return Scope(r,(TypeReferenceHandle)tr.ResolutionScope); if (tr.ResolutionScope.Kind==HandleKind.AssemblyReference) return r.GetString(r.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope).Name); return "?"; }
int missing=0, total=0;
foreach (var trh in mmd.TypeReferences) { if (Scope(mmd,trh)!="Assembly-CSharp") continue; var n=TRName(mmd,trh); total++; if(!types.ContainsKey(n)){ Console.WriteLine("MISSING TYPE "+n); missing++; } }
var refs = new SortedSet<string>();
foreach (var mrh in mmd.MemberReferences) { var mr = mmd.GetMemberReference(mrh); TypeReferenceHandle trh;
  if (mr.Parent.Kind==HandleKind.TypeReference) trh=(TypeReferenceHandle)mr.Parent;
  else if (mr.Parent.Kind==HandleKind.TypeSpecification) continue; else continue;
  if (Scope(mmd,trh)!="Assembly-CSharp") continue; var tn=TRName(mmd,trh); var name=mmd.GetString(mr.Name); total++;
  refs.Add(tn+"::"+name);
  if(!types.TryGetValue(tn, out var tt)) continue;
  if (mr.GetKind()==MemberReferenceKind.Field) { if(!tt.f.Contains(name)){Console.WriteLine($"MISSING FIELD {tn}.{name}"); missing++;} }
  else { var sig = mmd.GetBlobReader(mr.Signature); var hdr=sig.ReadSignatureHeader(); if(hdr.IsGeneric) sig.ReadCompressedInteger(); var pc=sig.ReadCompressedInteger();
    if(!tt.m.TryGetValue(name,out var lst)){Console.WriteLine($"MISSING METHOD {tn}.{name}"); missing++;} else if(!lst.Contains(pc)){Console.WriteLine($"ARITY MISMATCH {tn}.{name}({pc}) game has {string.Join("/",lst)}"); missing++;} } }
Console.WriteLine($"checked {total} refs, {missing} problems; {refs.Count} distinct members");
// reflection-by-string usage hint
static (PEReader pe, MetadataReader md) Load(string p){ var pe=new PEReader(File.OpenRead(p)); return (pe, pe.GetMetadataReader()); }
static string FullName(MetadataReader r, TypeDefinition t){ var n=r.GetString(t.Name); if (t.IsNested) return FullName(r, r.GetTypeDefinition(t.GetDeclaringType()))+"/"+n; var ns=r.GetString(t.Namespace); return (ns.Length>0?ns+".":"")+n; }
