using System.Reflection;
using System.Reflection.Emit;

if (args.Length != 3)
    throw new ArgumentException("usage: <guest.dll> <core.dll> <report.txt>");

var guest = Assembly.LoadFrom(Path.GetFullPath(args[0]));
var core = Assembly.LoadFrom(Path.GetFullPath(args[1]));
var owned = new HashSet<string> { guest.GetName().Name!, core.GetName().Name! };
var ownedTypes = guest.GetTypes().Concat(core.GetTypes()).ToArray();
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
    .ToDictionary(op => unchecked((ushort)op.Value));
var pending = new Queue<MethodBase>();
var methods = new Dictionary<string, MethodBase>();
var external = new SortedSet<string>(StringComparer.Ordinal);
var referencedTypes = new SortedSet<string>(StringComparer.Ordinal);
var ilFeatureCounts = new Dictionary<string, int>(StringComparer.Ordinal);

var entry = guest.GetType("DoomSharp.HybridCpu.Guest.EntryPoint", true)!
    .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;
Enqueue(entry);

while (pending.Count != 0)
{
    var method = pending.Dequeue();
    EnqueueTypeInitializer(method.DeclaringType);
    var body = method.GetMethodBody();
    if (body is null) continue;
    foreach (var clause in body.ExceptionHandlingClauses)
        if (clause.CatchType is not null) AddType(clause.CatchType);

    var il = body.GetILAsByteArray()!;
    var position = 0;
    while (position < il.Length)
    {
        ushort value = il[position++];
        if (value == 0xfe) value = (ushort)(0xfe00 | il[position++]);
        var op = opcodes[value];
        if (op == OpCodes.Callvirt || op == OpCodes.Box || op == OpCodes.Unbox || op == OpCodes.Unbox_Any ||
            op == OpCodes.Newarr || op == OpCodes.Throw || op == OpCodes.Rethrow || op == OpCodes.Isinst ||
            op == OpCodes.Castclass || op == OpCodes.Newobj)
            ilFeatureCounts[op.Name!] = ilFeatureCounts.GetValueOrDefault(op.Name!) + 1;
        var token = 0;
        switch (op.OperandType)
        {
            case OperandType.InlineMethod:
            case OperandType.InlineField:
            case OperandType.InlineType:
            case OperandType.InlineTok:
            case OperandType.InlineString:
            case OperandType.InlineSig:
                token = BitConverter.ToInt32(il, position); position += 4; break;
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
            case OperandType.ShortInlineBrTarget:
                position += 1; break;
            case OperandType.InlineVar:
                position += 2; break;
            case OperandType.InlineI:
            case OperandType.InlineBrTarget:
            case OperandType.ShortInlineR:
                position += 4; break;
            case OperandType.InlineI8:
            case OperandType.InlineR:
                position += 8; break;
            case OperandType.InlineSwitch:
                var count = BitConverter.ToInt32(il, position); position += 4 + count * 4; break;
        }

        try
        {
            var typeArgs = method.DeclaringType?.GetGenericArguments();
            var methodArgs = method is MethodInfo mi ? mi.GetGenericArguments() : null;
            if (op.OperandType == OperandType.InlineMethod)
            {
                var target = method.Module.ResolveMethod(token, typeArgs, methodArgs);
                Enqueue(target);
                if (target is not null && (op == OpCodes.Callvirt || op == OpCodes.Ldvirtftn)) ExpandVirtual(target);
            }
            else if (op.OperandType == OperandType.InlineField)
            {
                var field = method.Module.ResolveField(token, typeArgs, methodArgs);
                if (field is null) continue;
                AddType(field.FieldType);
                if (op == OpCodes.Ldsfld || op == OpCodes.Ldsflda || op == OpCodes.Stsfld)
                    EnqueueTypeInitializer(field.DeclaringType);
            }
            else if (op.OperandType is OperandType.InlineType or OperandType.InlineTok)
            {
                var member = method.Module.ResolveMember(token, typeArgs, methodArgs);
                if (member is Type type) AddType(type);
                else if (member is MethodBase memberMethod) Enqueue(memberMethod);
                else if (member is FieldInfo memberField) AddType(memberField.FieldType);
            }
        }
        catch (Exception error)
        {
            external.Add("UNRESOLVED " + MethodName(method) + " IL_" + (position - 1).ToString("x4") + ": " + error.Message);
        }
    }
}

var internalMethods = methods.Values.Where(IsOwned).Select(MethodName).OrderBy(x => x, StringComparer.Ordinal).ToArray();
var lines = new List<string>
{
    "HybridCPU DoomSharp conservative reachable CIL closure",
    "Entry: DoomSharp.HybridCpu.Guest.EntryPoint::Run()",
    "Algorithm: recursive IL call/newobj/ldftn scan; cctors; EH catch types; all owned virtual/interface implementations.",
    "Owned reachable methods: " + internalMethods.Length,
    "External/CoreLib members: " + external.Count,
    "Referenced types: " + referencedTypes.Count,
    "Cctors: " + internalMethods.Count(x => x.Contains("::.cctor(", StringComparison.Ordinal)),
    "EH methods: " + methods.Values.Count(m => m.GetMethodBody()?.ExceptionHandlingClauses.Count > 0),
    "IL feature counts: " + string.Join(", ", ilFeatureCounts.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value)),
    "",
    "[EXTERNAL_CORELIB_MEMBERS]"
};
lines.AddRange(external);
lines.Add(""); lines.Add("[OWNED_REACHABLE_METHODS]"); lines.AddRange(internalMethods);
lines.Add(""); lines.Add("[REFERENCED_TYPES]"); lines.AddRange(referencedTypes);
File.WriteAllLines(Path.GetFullPath(args[2]), lines);
Console.WriteLine(string.Join(Environment.NewLine, lines.Take(6)));

void Enqueue(MethodBase? method)
{
    if (method is null) return;
    if (!IsOwned(method))
    {
        external.Add(MethodName(method));
        return;
    }
    var key = method.Module.ModuleVersionId + ":" + method.MetadataToken + ":" + MethodName(method);
    if (methods.TryAdd(key, method)) pending.Enqueue(method);
}

bool IsOwned(MethodBase method) => owned.Contains(method.Module.Assembly.GetName().Name!);

void EnqueueTypeInitializer(Type? type)
{
    if (type is not null && owned.Contains(type.Assembly.GetName().Name!)) Enqueue(type.TypeInitializer);
}

void AddType(Type type)
{
    referencedTypes.Add(TypeName(type));
    if (type.IsArray || type.IsByRef || type.IsPointer) AddType(type.GetElementType()!);
    if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) AddType(argument);
}

void ExpandVirtual(MethodBase target)
{
    if (target is not MethodInfo targetMethod || target.DeclaringType is null) return;
    foreach (var type in ownedTypes)
    {
        if (type.IsInterface || type.IsAbstract || !target.DeclaringType.IsAssignableFrom(type)) continue;
        if (target.DeclaringType.IsInterface)
        {
            try
            {
                var map = type.GetInterfaceMap(target.DeclaringType);
                for (var i = 0; i < map.InterfaceMethods.Length; i++)
                    if (SameDefinition(map.InterfaceMethods[i], targetMethod)) Enqueue(map.TargetMethods[i]);
            }
            catch (ArgumentException) { }
        }
        else
        {
            var candidate = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => SameDefinition(m.GetBaseDefinition(), targetMethod.GetBaseDefinition()));
            Enqueue(candidate);
        }
    }
}

static bool SameDefinition(MethodInfo left, MethodInfo right) =>
    left.Name == right.Name && left.GetParameters().Length == right.GetParameters().Length;

static string MethodName(MethodBase method)
{
    var parameters = string.Join(",", method.GetParameters().Select(p => TypeName(p.ParameterType)));
    var result = method is MethodInfo info ? ":" + TypeName(info.ReturnType) : string.Empty;
    return TypeName(method.DeclaringType!) + "::" + method.Name + "(" + parameters + ")" + result;
}

static string TypeName(Type type)
{
    if (type.IsArray) return TypeName(type.GetElementType()!) + "[]";
    if (!type.IsGenericType) return type.FullName ?? type.Name;
    var name = (type.GetGenericTypeDefinition().FullName ?? type.Name).Split('`')[0];
    return name + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">";
}
