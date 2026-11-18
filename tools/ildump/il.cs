using System.Reflection.Metadata; using System.Reflection.Metadata.Ecma335; using System.Reflection.PortableExecutable; using System.Reflection.Emit;
var pe = new PEReader(File.OpenRead(args[0])); var md = pe.GetMetadataReader(); var targets = args[1].Split(',');
var ops1 = new Dictionary<short,OpCode>(); foreach (var f in typeof(OpCodes).GetFields()) { var o=(OpCode)f.GetValue(null)!; ops1[o.Value]=o; }
string Tok(int tok){ var h = MetadataTokens.EntityHandle(tok);
  try { switch(h.Kind){
   case HandleKind.MemberReference: { var r=md.GetMemberReference((MemberReferenceHandle)h); return ParentName(r.Parent)+"::"+md.GetString(r.Name); }
   case HandleKind.MethodDefinition: { var d=md.GetMethodDefinition((MethodDefinitionHandle)h); return md.GetString(md.GetTypeDefinition(d.GetDeclaringType()).Name)+"::"+md.GetString(d.Name); }
   case HandleKind.FieldDefinition: { var d=md.GetFieldDefinition((FieldDefinitionHandle)h); return md.GetString(md.GetTypeDefinition(d.GetDeclaringType()).Name)+"::"+md.GetString(d.Name); }
   case HandleKind.TypeReference: return md.GetString(md.GetTypeReference((TypeReferenceHandle)h).Name);
   case HandleKind.TypeDefinition: return md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)h).Name);
   case HandleKind.MethodSpecification: { var s=md.GetMethodSpecification((MethodSpecificationHandle)h); return Tok(MetadataTokens.GetToken(s.Method))+"<>"; }
   default: return h.Kind.ToString(); } } catch { return "?"; } }
string ParentName(EntityHandle p) => p.Kind==HandleKind.TypeReference ? md.GetString(md.GetTypeReference((TypeReferenceHandle)p).Name) : p.Kind==HandleKind.TypeDefinition? md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)p).Name) : "spec";
foreach (var th in md.TypeDefinitions) { var t=md.GetTypeDefinition(th);
 foreach (var mh in t.GetMethods()) { var m=md.GetMethodDefinition(mh); if (m.RelativeVirtualAddress==0) continue;
  var body = pe.GetMethodBody(m.RelativeVirtualAddress); var il = body.GetILBytes()!; var lines=new List<string>(); bool hit=false; int i=0;
  while(i<il.Length){ int start=i; short v=il[i++]; if(v==0xFE) v=(short)(0xFE00|il[i++]); if(!ops1.TryGetValue(v,out var op)){lines.Add($"IL_{start:X4} ??");continue;}
   string arg=""; switch(op.OperandType){ case OperandType.InlineNone: break; case OperandType.ShortInlineBrTarget: arg=$"IL_{i+1+(sbyte)il[i]:X4}"; i+=1; break; case OperandType.ShortInlineI: case OperandType.ShortInlineVar: arg=il[i].ToString(); i+=1; break;
    case OperandType.InlineVar: arg=BitConverter.ToInt16(il,i).ToString(); i+=2; break; case OperandType.InlineI8: case OperandType.InlineR: arg=BitConverter.ToDouble(il,i).ToString(); i+=8; break; case OperandType.ShortInlineR: arg=BitConverter.ToSingle(il,i).ToString(); i+=4; break;
    case OperandType.InlineSwitch: { int n=BitConverter.ToInt32(il,i); i+=4+4*n; arg="switch"; break; } case OperandType.InlineBrTarget: arg=$"IL_{i+4+BitConverter.ToInt32(il,i):X4}"; i+=4; break; case OperandType.InlineI: arg=BitConverter.ToInt32(il,i).ToString(); i+=4; break;
    case OperandType.InlineString: arg="\""+md.GetUserString(MetadataTokens.UserStringHandle(BitConverter.ToInt32(il,i)&0xFFFFFF))+"\""; i+=4; break;
    default: arg=Tok(BitConverter.ToInt32(il,i)); i+=4; if (targets.Any(x=>arg.EndsWith(x))) {hit=true; arg+="   <<<<<";} break; }
   lines.Add($"IL_{start:X4} {op.Name} {arg}"); }
  if(hit){ Console.WriteLine($"### {md.GetString(t.Namespace)}.{md.GetString(t.Name)}::{md.GetString(m.Name)} ({il.Length} bytes)"); if (args.Length>2) foreach(var l in lines) Console.WriteLine("  "+l); else { for(int k=0;k<lines.Count;k++) if(lines[k].Contains("<<<<<")) for(int j=Math.Max(0,k-8);j<=Math.Min(lines.Count-1,k+6);j++) Console.WriteLine((j==k?"> ":"  ")+lines[j]); } }
 } }
