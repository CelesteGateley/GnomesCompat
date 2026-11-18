using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
var path = args[0]; var want = args[1].Split(',');
using var fs = File.OpenRead(path); using var pe = new PEReader(fs); var md = pe.GetMetadataReader();
foreach (var th in md.TypeDefinitions) {
  var t = md.GetTypeDefinition(th); var name = md.GetString(t.Name);
  if (!want.Contains(name)) continue;
  string bt = "";
  if (!t.BaseType.IsNil) { bt = t.BaseType.Kind == HandleKind.TypeReference ? md.GetString(md.GetTypeReference((TypeReferenceHandle)t.BaseType).Name) : t.BaseType.Kind==HandleKind.TypeDefinition? md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)t.BaseType).Name):"?"; }
  Console.WriteLine($"== {md.GetString(t.Namespace)}.{name} : {bt}");
  foreach (var f in t.GetFields()) { var fd = md.GetFieldDefinition(f); var sig = fd.DecodeSignature(new P(md), null); Console.WriteLine($"  F {fd.Attributes & System.Reflection.FieldAttributes.FieldAccessMask} {sig} {md.GetString(fd.Name)}"); }
  foreach (var p in t.GetProperties()) { var pd = md.GetPropertyDefinition(p); var sig = pd.DecodeSignature(new P(md), null); Console.WriteLine($"  P {sig.ReturnType} {md.GetString(pd.Name)}"); }
  foreach (var m in t.GetMethods()) { var mm = md.GetMethodDefinition(m); var sig = mm.DecodeSignature(new P(md), null); Console.WriteLine($"  M {sig.ReturnType} {md.GetString(mm.Name)}({string.Join(", ", sig.ParameterTypes)})"); }
}
class P(MetadataReader md) : ISignatureTypeProvider<string, object?> {
  public string GetArrayType(string e, ArrayShape s) => e+"[,]"; public string GetByReferenceType(string e) => e+"&";
  public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr"; public string GetGenericInstantiation(string g, System.Collections.Immutable.ImmutableArray<string> a) => g+"<"+string.Join(",",a)+">";
  public string GetGenericMethodParameter(object? c, int i) => "!!"+i; public string GetGenericTypeParameter(object? c, int i) => "!"+i;
  public string GetModifiedType(string m, string u, bool r) => u; public string GetPinnedType(string e) => e; public string GetPointerType(string e) => e+"*";
  public string GetPrimitiveType(PrimitiveTypeCode t) => t.ToString(); public string GetSZArrayType(string e) => e+"[]";
  public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => r.GetString(r.GetTypeDefinition(h).Name);
  public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => r.GetString(r.GetTypeReference(h).Name);
  public string GetTypeFromSpecification(MetadataReader r, object? c, TypeSpecificationHandle h, byte k) => r.GetTypeSpecification(h).DecodeSignature(this, c);
}
