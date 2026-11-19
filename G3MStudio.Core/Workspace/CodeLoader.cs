namespace G3MStudio.Core.Workspace;

/// <summary>What the code tab shows for an entry.</summary>
public enum CodeMode
{
    /// <summary>The decompiled GML. Editable.</summary>
    Decompiled,

    /// <summary>The decompiler failed; the VM assembly is shown. Editable, Apply assembles.</summary>
    Assembly,
}

/// <param name="Text">What the editor shows.</param>
/// <param name="Mode">Which form of the code it is.</param>
/// <param name="Reason">Why the decompiler failed, null in <see cref="CodeMode.Decompiled"/>.</param>
/// <param name="ParentName">The parent entry of a child function entry (GameMaker 2.3), or null.</param>
/// <param name="Child">What a child entry is, when this is one and is shown without a decompile attempt.</param>
public sealed record CodeLoad(string Text, CodeMode Mode, string? Reason = null, string? ParentName = null, CodeChild? Child = null);

/// <summary>
/// A function that lives inside another code entry (GameMaker 2.3 and later: a method, a constructor or an anonymous function). Only its parent decompiles to
/// GML, so the tab explains this instead of failing. <c>Offset</c> and <c>Length</c> are bytes of the parent's bytecode.
/// </summary>
public sealed record CodeChild(string Parent, bool Anonymous, int Arguments, uint Locals, uint Offset, uint Length)
{
    public static CodeChild Of(G3MLib.DataFile.Models.GameMakerCode code, G3MLib.DataFile.Models.GameMakerCode parent) => new(
        parent.Name?.Content ?? "",
        code.Name?.Content?.Contains("anon", StringComparison.OrdinalIgnoreCase) == true, // the compiler names anonymous functions gml_Script_anon...
        code.ArgumentsCount, code.LocalsCount, code.Offset, code.Length);
}

/// <summary>Never leaves the editor empty: when the decompiler fails the assembly is shown.</summary>
public static class CodeLoader
{
    /// <summary>Decompiles, falls back to the assembly. Throws only when nothing could be produced.</summary>
    public static CodeLoad Load(Func<string> decompile, Func<string> disassemble, string? parentName = null)
    {
        try
        {
            return new CodeLoad(decompile(), CodeMode.Decompiled, null, parentName);
        }
        catch (Exception first) when (first is not OutOfMemoryException)
        {
            var reason = Reason(first);
            try
            {
                return new CodeLoad(disassemble(), CodeMode.Assembly, reason, parentName);
            }
            catch (Exception second) when (second is not OutOfMemoryException)
            {
                throw new InvalidOperationException(reason, second);
            }
        }
    }

    /// <summary>The first line of the message, which is the part a person can use.</summary>
    public static string Reason(Exception e)
    {
        var message = e.InnerException is { } inner && e.Message.Length == 0 ? inner.Message : e.Message;
        var line = message.Split('\n', 2)[0].Trim();
        return line.Length > 0 ? line : e.GetType().Name;
    }
}
