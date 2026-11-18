// GnomesCompat — BepInEx 5 preloader patcher.
//
// Gnomes 0.7.0 was compiled against a Stonewards build that had members the
// current game no longer has. This patcher adds those members back to
// Assembly-CSharp as thin shims over their replacements, so Gnomes.dll runs
// unmodified. Every shim is skipped if the member already exists, so the
// patcher is inert once the game or Gnomes changes again. Remove it when
// Gnomes gets an update.
//
//   ChestDataSO.level1DropPoolSO / level2DropPoolSO  (merged into dropPoolSO)
//   PlayerStats.ServerHealWithFX(float)              (now takes a healer NetworkIdentity)
//   DiggingPattern.Id                                (removed; patterns now use patternIndex)
//   DiggingManager.SDiggingPattern.id                (renamed to patternIndex, now int)

using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class GnomesCompatPatcher
{
    public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

    static readonly ManualLogSource Log = Logger.CreateLogSource("GnomesCompat");

    public static void Patch(AssemblyDefinition assembly)
    {
        var module = assembly.MainModule;
        var types = module.GetTypes().ToList(); // includes nested types

        Run("ChestDataSO drop pools", () => PatchChestData(module, types));
        Run("PlayerStats.ServerHealWithFX(float)", () => PatchHeal(module, types));
        Run("DiggingPattern.Id", () => PatchDiggingPatternId(module, types));
        Run("SDiggingPattern.id", () => PatchSDiggingPatternId(module, types));
    }

    static void Run(string name, System.Func<string> patch)
    {
        try { Log.LogInfo($"{name}: {patch()}"); }
        catch (System.Exception e) { Log.LogError($"{name}: failed — {e}"); }
    }

    static TypeDefinition Find(List<TypeDefinition> types, string name, string declaringType = null) =>
        types.FirstOrDefault(t => t.Name == name && (declaringType == null || t.DeclaringType?.Name == declaringType))
        ?? throw new System.Exception($"type {name} not found");

    // A field that Unity's serializer ignores, so asset layouts are unchanged.
    static FieldDefinition AddShimField(TypeDefinition type, string name, TypeReference fieldType)
    {
        var f = new FieldDefinition(name, FieldAttributes.Public | FieldAttributes.NotSerialized, fieldType);
        type.Fields.Add(f);
        return f;
    }

    // Gnomes reads level1/level2 pools to decide whether a chest only drops tomes.
    // The game now has a single pool: expose it as level 1, leave level 2 null
    // (Gnomes treats a null level-2 pool as "no second pool").
    static string PatchChestData(ModuleDefinition module, List<TypeDefinition> types)
    {
        var chest = Find(types, "ChestDataSO");
        if (chest.Fields.Any(f => f.Name == "level1DropPoolSO")) return "already present, skipped";

        var pool = chest.Fields.FirstOrDefault(f => f.Name == "dropPoolSO")
                   ?? throw new System.Exception("ChestDataSO.dropPoolSO not found");
        var level1 = AddShimField(chest, "level1DropPoolSO", pool.FieldType);
        AddShimField(chest, "level2DropPoolSO", pool.FieldType);

        // Copy the pool into level1 once Unity has deserialized the asset.
        var onEnable = chest.Methods.FirstOrDefault(m => m.Name == "OnEnable" && !m.HasParameters);
        bool created = onEnable == null;
        if (created)
        {
            onEnable = new MethodDefinition("OnEnable", MethodAttributes.Private | MethodAttributes.HideBySig,
                module.TypeSystem.Void);
            chest.Methods.Add(onEnable);
            onEnable.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
        }
        var il = onEnable.Body.GetILProcessor();
        var first = onEnable.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Ldfld, pool));
        il.InsertBefore(first, il.Create(OpCodes.Stfld, level1));
        return $"added level1/level2 fields ({(created ? "new" : "existing")} OnEnable fills level1)";
    }

    // The fairy's heal throw calls the old one-argument overload; forward it with no healer.
    static string PatchHeal(ModuleDefinition module, List<TypeDefinition> types)
    {
        var stats = Find(types, "PlayerStats");
        var methods = stats.Methods.Where(m => m.Name == "ServerHealWithFX").ToList();
        if (methods.Any(m => m.Parameters.Count == 1)) return "already present, skipped";

        var target = methods.FirstOrDefault(m => m.Parameters.Count == 2
                         && m.Parameters[0].ParameterType.MetadataType == MetadataType.Single)
                     ?? throw new System.Exception("ServerHealWithFX(float, NetworkIdentity) not found");

        var shim = new MethodDefinition("ServerHealWithFX", MethodAttributes.Public | MethodAttributes.HideBySig,
            module.TypeSystem.Void);
        shim.Parameters.Add(new ParameterDefinition("amount", ParameterAttributes.None, module.TypeSystem.Single));
        var il = shim.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldnull); // healer: the game null-checks it and just skips healer feedback
        il.Emit(OpCodes.Call, target);
        il.Emit(OpCodes.Ret);
        stats.Methods.Add(shim);
        return "added forwarding overload";
    }

    // Gnomes pairs DiggingPattern objects with SDiggingPattern entries by
    // (Id == id) AND position within 0.1 m. Both shims return null, so the ID
    // check always passes and position does the matching. Room keys become
    // "_0", "_1", … (still unique per entry).
    static string PatchDiggingPatternId(ModuleDefinition module, List<TypeDefinition> types)
    {
        var pattern = Find(types, "DiggingPattern");
        if (pattern.Methods.Any(m => m.Name == "get_Id")) return "already present, skipped";

        var getter = new MethodDefinition("get_Id",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            module.TypeSystem.String);
        var il = getter.Body.GetILProcessor();
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ret);
        pattern.Methods.Add(getter);
        pattern.Properties.Add(new PropertyDefinition("Id", PropertyAttributes.None, module.TypeSystem.String)
            { GetMethod = getter });
        return "added Id getter (returns null; matching falls back to position)";
    }

    static string PatchSDiggingPatternId(ModuleDefinition module, List<TypeDefinition> types)
    {
        var sdp = Find(types, "SDiggingPattern", "DiggingManager");
        if (sdp.Fields.Any(f => f.Name == "id")) return "already present, skipped";
        AddShimField(sdp, "id", module.TypeSystem.String); // stays null; not sent over the network
        return "added id field";
    }
}
