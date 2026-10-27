using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using G3MLib.Analyzer;
using G3MLib.DataFile.Models;
using G3MLib.Modding.Resources;

namespace G3MLib.DataFile.References;

/// <summary>
/// One place that uses a target.
/// </summary>
/// <param name="Target">The used object: a resource, string, variable, function or code entry.</param>
/// <param name="Owner">The top-level item that contains the use: a resource from one of the data file lists, or a data file section such as the general info.</param>
/// <param name="Holder">The object that holds the reference. For code this is the instruction.</param>
/// <param name="Member">Name of the property on <paramref name="Holder"/> that holds the reference. For code: <c>Variable</c>, <c>Function</c>, <c>String</c> or <c>AssetReference</c>.</param>
/// <param name="InstructionOffset">Byte offset of the instruction in the owner's code, or -1 when the use is not in code.</param>
public readonly record struct Reference(object Target, object Owner, object Holder, string Member, int InstructionOffset = -1)
{
    /// <summary>Name of the owner when it has one, otherwise the type name.</summary>
    public string OwnerName => ReferenceIndex.NameOf(Owner) ?? Owner.GetType().Name;

    public override string ToString()
    {
        string where = InstructionOffset >= 0 ? $"{Member} at {InstructionOffset}" : Member;
        return $"{Owner.GetType().Name} {OwnerName}: {where}";
    }
}

/// <summary>
/// Reverse reference index: for every resource, string, variable, function and code entry, where it is used.
/// </summary>
/// <remarks>
/// Uses are found by walking the data model, so every property that holds another resource counts, and new
/// model properties are covered without changes here. Code is scanned instruction by instruction for variables,
/// functions, string literals and typed asset references. References that exist only as numbers (an object index
/// in a constant, a name inside a string) are not found. The index reflects the model at the time of
/// <see cref="Build(GameMakerData, CancellationToken)"/> or the last <see cref="Update"/>.
/// </remarks>
public sealed class ReferenceIndex
{
    private static readonly IReadOnlyList<Reference> NoReferences = [];

    private readonly Dictionary<object, List<Reference>> _usages = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, object[]> _ownerTargets = new(ReferenceEqualityComparer.Instance);
    private HashSet<object> _registry = new(ReferenceEqualityComparer.Instance);

    // Per data file list: the list version and the owners seen at that version, so Update only looks at lists that changed.
    private readonly Dictionary<IList, (int Version, IReadOnlyList<object> Owners)> _lists = new(ReferenceEqualityComparer.Instance);

    // The properties of the data file that hold a list of model objects or a single section; found once per process.
    private static readonly Lazy<PropertyInfo[]> DataProperties = new(() => typeof(GameMakerData).GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.GetIndexParameters().Length == 0).ToArray());

    /// <summary>The data file this index describes.</summary>
    public GameMakerData Data { get; }

    /// <summary>Total number of recorded uses.</summary>
    public int ReferenceCount { get; private set; }

    private ReferenceIndex(GameMakerData data)
    {
        Data = data;
    }

    /// <summary>
    /// Builds the index for a data file.
    /// </summary>
    public static ReferenceIndex Build(GameMakerData data, CancellationToken cancellationToken = default) => Build(data, null, cancellationToken);

    /// <summary>
    /// Builds the index and reports how many owners were scanned.
    /// </summary>
    public static ReferenceIndex Build(GameMakerData data, IProgress<BatchProgress>? progress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ReferenceIndex index = new(data);
        List<object> owners = index.CollectOwners();
        index.ScanAndCommit(owners, progress, cancellationToken);
        return index;
    }

    /// <summary>
    /// Rescans the given owners after edits. An owner is a resource from a data file list (or a section such as
    /// the general info). Owners that are no longer in the data file lose all their uses; new ones are added.
    /// </summary>
    public void Update(IEnumerable<object> changedOwners, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changedOwners);
        SyncRegistry();
        List<object> toScan = [];
        foreach (object owner in changedOwners.Distinct(ReferenceEqualityComparer.Instance))
        {
            RemoveOwner(owner);
            if (_registry.Contains(owner))
            {
                toScan.Add(owner);
            }
        }
        ScanAndCommit(toScan, null, cancellationToken);
    }

    /// <summary>
    /// Returns the places that use the target, only those whose owner is of <paramref name="ownerType"/> and, when given, that hold the reference in <paramref name="member"/>.
    /// Nothing is copied: the uses are produced one by one.
    /// </summary>
    public IEnumerable<Reference> FindUsages(object target, Type? ownerType, string? member = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        foreach (Reference use in EnumerateUsages(target))
        {
            if ((ownerType is null || ownerType.IsInstanceOfType(use.Owner)) && (member is null || use.Member == member))
            {
                yield return use;
            }
        }
    }

    /// <summary>
    /// Returns the places that use the target. A script also reports the calls of its function entry
    /// (named like the script or <c>gml_Script_name</c>) because code calls scripts through that entry; uses from the script's own code are left out.
    /// </summary>
    public IReadOnlyList<Reference> FindUsages(object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        List<Reference>? direct = _usages.GetValueOrDefault(target);
        if (target is not GameMakerScript)
        {
            return direct is null ? NoReferences : direct;
        }
        return [.. EnumerateUsages(target)];
    }

    private IEnumerable<Reference> EnumerateUsages(object target)
    {
        if (_usages.TryGetValue(target, out List<Reference>? direct))
        {
            foreach (Reference use in direct)
            {
                yield return use;
            }
        }
        if (target is not GameMakerScript script || script.Name?.Content is not string name)
        {
            yield break;
        }

        string prefixedName = "gml_Script_" + name;
        HashSet<object> own = new(ReferenceEqualityComparer.Instance);
        if (script.Code is { } code)
        {
            own.Add(code);
            foreach (GameMakerCode child in code.ChildEntries)
            {
                own.Add(child);
            }
        }
        foreach (GameMakerFunction function in Data.Functions ?? [])
        {
            if ((function?.Name?.Content == name || function?.Name?.Content == prefixedName) && _usages.TryGetValue(function, out List<Reference>? calls))
            {
                foreach (Reference call in calls)
                {
                    if (!own.Contains(call.Owner))
                    {
                        yield return call;
                    }
                }
            }
        }
    }

    /// <summary>Returns the uses of every variable entry with this name, whatever its instance type.</summary>
    public IReadOnlyList<Reference> FindVariableUsages(string name)
    {
        return (Data.Variables ?? []).Where(v => v?.Name?.Content == name).SelectMany(v => FindUsages(v)).ToList();
    }

    /// <summary>Returns the uses of every function entry with this name.</summary>
    public IReadOnlyList<Reference> FindFunctionUsages(string name)
    {
        return (Data.Functions ?? []).Where(f => f?.Name?.Content == name).SelectMany(f => FindUsages(f)).ToList();
    }

    /// <summary>
    /// Returns assets that nothing uses. A use by the asset itself does not count. Without arguments it checks
    /// objects, sprites, sounds, rooms, backgrounds, paths, scripts, fonts, timelines, shaders, sequences,
    /// animation curves and particle systems. Assets used only by name at run time, or by a plain number, are
    /// reported as unreferenced.
    /// </summary>
    public IReadOnlyList<GameMakerNamedResource> FindUnreferenced(params Type[] assetTypes)
    {
        IEnumerable<GameMakerNamedResource> candidates = AssetLists().SelectMany(l => l.OfType<GameMakerNamedResource>());
        if (assetTypes.Length > 0)
        {
            candidates = candidates.Where(c => assetTypes.Any(t => t.IsInstanceOfType(c)));
        }
        return candidates.Where(c => !FindUsages(c).Any(r => !ReferenceEquals(r.Owner, c))).ToList();
    }

    /// <summary>
    /// Returns the objects of the chosen kinds that nothing uses. A use by the object itself does not count.
    /// With <paramref name="transitive"/> an object that is used only by objects found unused is found too, and so on.
    /// </summary>
    /// <remarks>
    /// Texture pages count as empty when no texture page item sits on them (the texture group still lists them).
    /// Embedded audio is checked by its reference from a sound; audio addressed by group and id is checked by <see cref="AudioIndex"/>.
    /// Transitive search recounts the remaining uses of a target once per removed owner (shortcut: quadratic for a target with very many uses).
    /// </remarks>
    public IReadOnlyList<object> FindUnreferenced(UnreferencedKind kinds, bool transitive = false)
    {
        List<object> candidates = [];
        if (kinds.HasFlag(UnreferencedKind.Assets))
        {
            candidates.AddRange(AssetLists().SelectMany(l => l.Cast<object>()).Where(c => c is not null));
        }
        AddIf(candidates, kinds, UnreferencedKind.Strings, Data.Strings);
        AddIf(candidates, kinds, UnreferencedKind.Variables, Data.Variables);
        AddIf(candidates, kinds, UnreferencedKind.Functions, Data.Functions);
        AddIf(candidates, kinds, UnreferencedKind.EmbeddedAudio, Data.EmbeddedAudio);
        AddIf(candidates, kinds, UnreferencedKind.TexturePageItems, Data.TexturePageItems);
        AddIf(candidates, kinds, UnreferencedKind.EmptyPages, Data.EmbeddedTextures);

        if (!transitive)
        {
            return candidates.Where(c => !EnumerateLive(c, null).Any()).ToList();
        }

        HashSet<object> removed = new(ReferenceEqualityComparer.Instance);
        bool IsUnused(object candidate) => !EnumerateLive(candidate, removed).Any();
        List<object> result = [];
        Queue<object> work = new();
        foreach (object candidate in candidates)
        {
            if (IsUnused(candidate) && removed.Add(candidate))
            {
                result.Add(candidate);
                work.Enqueue(candidate);
            }
        }

        HashSet<object> candidateSet = new(candidates, ReferenceEqualityComparer.Instance);
        while (work.Count > 0)
        {
            object owner = work.Dequeue();
            if (!_ownerTargets.TryGetValue(owner, out object[]? targets))
            {
                continue;
            }
            foreach (object target in targets)
            {
                if (candidateSet.Contains(target) && !removed.Contains(target) && IsUnused(target))
                {
                    removed.Add(target);
                    result.Add(target);
                    work.Enqueue(target);
                }
            }
        }
        return result;
    }

    private static void AddIf(List<object> candidates, UnreferencedKind kinds, UnreferencedKind kind, IEnumerable? list)
    {
        if (kinds.HasFlag(kind) && list is not null)
        {
            candidates.AddRange(list.Cast<object>().Where(c => c is not null));
        }
    }

    // Uses of the candidate that count: not by itself, not by removed owners, and for an empty page only from texture page items.
    private IEnumerable<Reference> EnumerateLive(object candidate, HashSet<object>? removed)
    {
        foreach (Reference use in EnumerateUsages(candidate))
        {
            if (ReferenceEquals(use.Owner, candidate) || (removed is not null && removed.Contains(use.Owner)))
            {
                continue;
            }
            if (candidate is GameMakerEmbeddedTexture && use.Owner is not GameMakerTexturePageItem)
            {
                continue;
            }
            yield return use;
        }
    }

    /// <summary>Name of a named resource or string, otherwise <see langword="null"/>.</summary>
    public static string? NameOf(object item) => item switch
    {
        GameMakerString s => s.Content,
        GameMakerNamedResource n => n.Name?.Content,
        _ => null
    };

    private IEnumerable<IList> AssetLists()
    {
        yield return (IList?)Data.GameObjects ?? Array.Empty<object>();
        yield return (IList?)Data.Sprites ?? Array.Empty<object>();
        yield return (IList?)Data.Sounds ?? Array.Empty<object>();
        yield return (IList?)Data.Rooms ?? Array.Empty<object>();
        yield return (IList?)Data.Backgrounds ?? Array.Empty<object>();
        yield return (IList?)Data.Paths ?? Array.Empty<object>();
        yield return (IList?)Data.Scripts ?? Array.Empty<object>();
        yield return (IList?)Data.Fonts ?? Array.Empty<object>();
        yield return (IList?)Data.Timelines ?? Array.Empty<object>();
        yield return (IList?)Data.Shaders ?? Array.Empty<object>();
        yield return (IList?)Data.Sequences ?? Array.Empty<object>();
        yield return (IList?)Data.AnimationCurves ?? Array.Empty<object>();
        yield return (IList?)Data.ParticleSystems ?? Array.Empty<object>();
    }

    // Owners are all items of the data file lists plus the single sections of the file.
    private List<object> CollectOwners()
    {
        List<object> owners = [];
        HashSet<object> registry = new(ReferenceEqualityComparer.Instance);
        _lists.Clear();
        foreach (PropertyInfo property in DataProperties.Value)
        {
            object? value = ReadProperty(property);
            if (value is IList list && IsModelList(property.PropertyType))
            {
                int start = owners.Count;
                foreach (object? item in list)
                {
                    if (item is not null && registry.Add(item))
                    {
                        owners.Add(item);
                    }
                }
                _lists[list] = (list is IVersionedList versioned ? versioned.Version : -1, owners.GetRange(start, owners.Count - start));
            }
            else if (IsSection(property, value) && registry.Add(value!))
            {
                owners.Add(value!);
            }
        }
        _registry = registry;
        return owners;
    }

    private object? ReadProperty(PropertyInfo property)
    {
        try
        {
            return property.GetValue(Data);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsSection(PropertyInfo property, object? value) =>
        value is GameMakerObject section && section is not GameMakerResourceRef && property.PropertyType.Namespace == typeof(GameMakerString).Namespace;

    // Brings the registry up to date after edits: only lists whose version changed are read again.
    private void SyncRegistry()
    {
        foreach (PropertyInfo property in DataProperties.Value)
        {
            object? value = ReadProperty(property);
            if (value is IList list && IsModelList(property.PropertyType))
            {
                int version = list is IVersionedList versioned ? versioned.Version : -1;
                if (_lists.TryGetValue(list, out (int Version, IReadOnlyList<object> Owners) known) && version != -1 && known.Version == version)
                {
                    continue;
                }
                HashSet<object> now = new(ReferenceEqualityComparer.Instance);
                foreach (object? item in list)
                {
                    if (item is not null)
                    {
                        now.Add(item);
                    }
                }
                if (known.Owners is not null)
                {
                    foreach (object old in known.Owners)
                    {
                        if (!now.Contains(old))
                        {
                            _registry.Remove(old);
                        }
                    }
                }
                List<object> owners = new(now.Count);
                foreach (object item in now)
                {
                    _registry.Add(item);
                    owners.Add(item);
                }
                _lists[list] = (version, owners);
            }
            else if (IsSection(property, value))
            {
                _registry.Add(value!);
            }
        }
    }

    private static bool IsModelList(Type type)
    {
        Type? element = ModelWalker.ElementTypeOf(type);
        return element is not null && typeof(GameMakerObject).IsAssignableFrom(element);
    }

    private void ScanAndCommit(List<object> owners, IProgress<BatchProgress>? progress, CancellationToken cancellationToken)
    {
        if (owners.Count == 0)
        {
            return;
        }
        int scanned = 0;
        // Workers scan into private buffers; the buffers are merged afterwards on this thread
        ConcurrentBag<(List<(object Target, Reference Reference)> Edges, List<(object Owner, object[] Targets)> Targets)> buffers = [];
        Parallel.ForEach(
            Partitioner.Create(0, owners.Count, Math.Max(256, owners.Count / (Environment.ProcessorCount * 8) + 1)),
            new ParallelOptions { CancellationToken = cancellationToken },
            () => new ModelWalker(Data, _registry),
            (range, _, walker) =>
            {
                List<(object, Reference)> edges = new(1024);
                List<(object, object[])> targets = new(256);
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    int start = edges.Count;
                    walker.Scan(owners[i], edges);
                    if (edges.Count > start)
                    {
                        targets.Add((owners[i], edges.Skip(start).Select(e => e.Item1).Distinct(ReferenceEqualityComparer.Instance).ToArray()));
                    }
                }
                buffers.Add((edges, targets));
                progress?.Report(new BatchProgress(Interlocked.Add(ref scanned, range.Item2 - range.Item1), owners.Count, string.Empty));
                return walker;
            },
            _ => { });

        foreach ((List<(object Target, Reference Reference)> edges, List<(object Owner, object[] Targets)> targets) in buffers)
        {
            foreach ((object target, Reference reference) in edges)
            {
                if (!_usages.TryGetValue(target, out List<Reference>? list))
                {
                    _usages[target] = list = new List<Reference>(2);
                }
                list.Add(reference);
            }
            ReferenceCount += edges.Count;

            // Remember each owner's targets so an owner can be removed without scanning every list
            foreach ((object owner, object[] ownerTargets) in targets)
            {
                _ownerTargets[owner] = ownerTargets;
            }
        }
    }

    private void RemoveOwner(object owner)
    {
        if (!_ownerTargets.Remove(owner, out object[]? targets))
        {
            return;
        }
        foreach (object target in targets)
        {
            if (_usages.TryGetValue(target, out List<Reference>? list))
            {
                ReferenceCount -= list.RemoveAll(r => ReferenceEquals(r.Owner, owner));
                if (list.Count == 0)
                {
                    _usages.Remove(target);
                }
            }
        }
    }
}

// Walks the model graph of one owner at a time and reports every reference to another registered object.
internal sealed class ModelWalker(GameMakerData data, HashSet<object> registry)
{
    private sealed record Member(string Name, Func<object, object?> Get);

    private static readonly ConcurrentDictionary<Type, Member[]> MemberCache = new();
    private static readonly Type ModelBase = typeof(GameMakerObject);

    private readonly HashSet<object> _visited = new(ReferenceEqualityComparer.Instance);
    private readonly Stack<object> _pending = new();

    public static Type? ElementTypeOf(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }
        foreach (Type i in type.IsInterface ? type.GetInterfaces().Prepend(type) : type.GetInterfaces())
        {
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>))
            {
                return i.GetGenericArguments()[0];
            }
        }
        return null;
    }

    public void Scan(object owner, List<(object Target, Reference Reference)> output)
    {
        if (owner is not GameMakerCode && MembersOf(owner.GetType()).Length == 0)
        {
            return;
        }
        _visited.Clear();
        _pending.Clear();
        _visited.Add(owner);
        _pending.Push(owner);
        while (_pending.Count > 0)
        {
            object holder = _pending.Pop();
            if (holder is GameMakerCode code)
            {
                ScanCode(owner, code, output);
            }
            foreach (Member member in MembersOf(holder.GetType()))
            {
                object? value;
                try
                {
                    value = member.Get(holder);
                }
                catch (Exception)
                {
                    continue;
                }
                if (value is null)
                {
                    continue;
                }
                if (value is IEnumerable and not string and not GameMakerResourceRef)
                {
                    foreach (object? element in (IEnumerable)value)
                    {
                        Visit(owner, holder, member.Name, element, output);
                    }
                }
                else
                {
                    Visit(owner, holder, member.Name, value, output);
                }
            }
        }
    }

    private void Visit(object owner, object holder, string member, object? value, List<(object Target, Reference Reference)> output)
    {
        if (value is GameMakerResourceRef reference)
        {
            if (reference.Resource is { } resolved && !ReferenceEquals(resolved, owner))
            {
                output.Add((resolved, new Reference(resolved, owner, holder, member)));
            }
        }
        else if (value is GameMakerObject child and not GameMakerInstruction)
        {
            if (registry.Contains(child))
            {
                if (!ReferenceEquals(child, owner))
                {
                    output.Add((child, new Reference(child, owner, holder, member)));
                }
            }
            else if (_visited.Add(child))
            {
                _pending.Push(child);
            }
        }
    }

    internal void ScanCode(object owner, GameMakerCode code, List<(object Target, Reference Reference)> output)
    {
        uint address = 0;
        foreach (GameMakerInstruction instr in code.Instructions)
        {
            int offset = (int)address * 4;
            address += instr.CalculateInstructionSize();
            if (instr.ValueVariable is { } variable)
            {
                output.Add((variable, new Reference(variable, owner, instr, "Variable", offset)));
            }
            else if (instr.ValueFunction is { } function)
            {
                output.Add((function, new Reference(function, owner, instr, "Function", offset)));
            }
            else if (instr.ValueString?.Resource is { } text)
            {
                output.Add((text, new Reference(text, owner, instr, "String", offset)));
            }
            if (instr.Kind == GameMakerInstruction.Opcode.Break && instr.ExtendedKind == (short)IGMInstruction.ExtendedOpcode.PushReference)
            {
                object? asset = ResolveAsset(instr.IntArgument);
                if (asset is not null)
                {
                    output.Add((asset, new Reference(asset, owner, instr, "AssetReference", offset)));
                }
            }
        }
    }

    // A typed asset reference holds the asset type in its top byte and the index in the rest
    private object? ResolveAsset(int encoded)
    {
        int index = encoded & 0xFFFFFF;
        IList? list;
        try
        {
            list = GameMakerInstruction.AdaptAssetType(data, encoded >> 24) switch
            {
                AssetType.Object => (IList?)data.GameObjects,
                AssetType.Sprite => (IList?)data.Sprites,
                AssetType.Sound => (IList?)data.Sounds,
                AssetType.Room => (IList?)data.Rooms,
                AssetType.Background => (IList?)data.Backgrounds,
                AssetType.Path => (IList?)data.Paths,
                AssetType.Script => (IList?)data.Scripts,
                AssetType.Font => (IList?)data.Fonts,
                AssetType.Timeline => (IList?)data.Timelines,
                AssetType.Shader => (IList?)data.Shaders,
                AssetType.Sequence => (IList?)data.Sequences,
                AssetType.AnimCurve => (IList?)data.AnimationCurves,
                AssetType.ParticleSystem => (IList?)data.ParticleSystems,
                _ => null
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
        return list is not null && index < list.Count ? list[index] : null;
    }

    private static Member[] MembersOf(Type type) => MemberCache.GetOrAdd(type, Discover);

    // Only members that can hold other model objects are read: no numbers, strings or byte buffers
    private static Member[] Discover(Type type)
    {
        List<Member> members = [];
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (type.IsValueType || property.GetIndexParameters().Length > 0 || property.GetMethod is null || type == typeof(GameMakerCode) && property.Name == "Instructions")
            {
                continue;
            }
            Type declared = property.PropertyType;
            bool isCollection = ElementTypeOf(declared) is not null;
            if (!isCollection && property.SetMethod is null)
            {
                continue;
            }
            if (!CanHoldModelObjects(declared))
            {
                continue;
            }
            MethodInfo create = typeof(ModelWalker).GetMethod(nameof(CreateGetter), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(property.DeclaringType!, property.PropertyType);
            members.Add(new Member(property.Name, (Func<object, object?>)create.Invoke(null, [property.GetMethod])!));
        }
        return [.. members];
    }

    private static bool CanHoldModelObjects(Type declared)
    {
        Type candidate = ElementTypeOf(declared) ?? declared;
        if (candidate == typeof(string) || candidate.IsPrimitive || candidate.IsEnum || candidate.IsValueType)
        {
            return false;
        }
        return candidate == typeof(object) || candidate.IsInterface || ModelBase.IsAssignableFrom(candidate);
    }

    private static Func<object, object?> CreateGetter<TOwner, TValue>(MethodInfo getter) where TOwner : class
    {
        Func<TOwner, TValue> typed = (Func<TOwner, TValue>)Delegate.CreateDelegate(typeof(Func<TOwner, TValue>), getter);
        return owner => typed((TOwner)owner);
    }
}

/// <summary>Kinds of objects <see cref="ReferenceIndex.FindUnreferenced(UnreferencedKind, bool)"/> can look for.</summary>
[Flags]
public enum UnreferencedKind
{
    Assets = 1,
    Strings = 2,
    Variables = 4,
    Functions = 8,
    EmbeddedAudio = 16,
    TexturePageItems = 32,
    EmptyPages = 64
}

/// <summary>What one code entry uses.</summary>
public sealed record CodeScanResult(IReadOnlyList<GameMakerVariable> Variables, IReadOnlyList<GameMakerFunction> Functions, IReadOnlyList<GameMakerString> Strings, IReadOnlyList<object> Assets);

/// <summary>Reads the variables, functions, string literals and typed asset references of one code entry.</summary>
public static class CodeScan
{
    public static CodeScanResult Scan(GameMakerData data, GameMakerCode code)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(code);
        List<(object Target, Reference Reference)> edges = [];
        new ModelWalker(data, new HashSet<object>(ReferenceEqualityComparer.Instance)).ScanCode(code, code, edges);
        IEnumerable<T> Of<T>(string member) => edges.Where(e => e.Reference.Member == member).Select(e => e.Target).OfType<T>().Distinct(ReferenceEqualityComparer.Instance as IEqualityComparer<T>);
        return new CodeScanResult(
            [.. Of<GameMakerVariable>("Variable")],
            [.. Of<GameMakerFunction>("Function")],
            [.. Of<GameMakerString>("String")],
            [.. Of<object>("AssetReference")]);
    }
}

/// <summary>Finds audio entries that no sound points at, by group id and audio id.</summary>
public static class AudioIndex
{
    /// <summary>Ids in 0 to <paramref name="entryCount"/> - 1 of the group's audio table that no sound uses.</summary>
    public static IReadOnlyList<int> Unreferenced(GameMakerData data, int groupId, int entryCount)
    {
        bool[] used = new bool[entryCount];
        foreach (GameMakerSound sound in data.Sounds ?? [])
        {
            if (sound != null && sound.GroupID == groupId && sound.AudioID >= 0 && sound.AudioID < entryCount)
            {
                used[sound.AudioID] = true;
            }
        }
        return [.. Enumerable.Range(0, entryCount).Where(id => !used[id])];
    }
}
