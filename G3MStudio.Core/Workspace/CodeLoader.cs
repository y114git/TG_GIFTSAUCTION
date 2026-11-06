namespace G3MStudio.Core.Workspace;

/// <summary>What the code tab shows for an entry.</summary>
public enum CodeMode
{
    /// <summary>The decompiled GML. Editable.</summary>
    Decompiled,

    /// <summary>The decompiler failed; the VM assembly is shown. Editable, Apply assembles.</summary>
    Assembly,

    /// <summary>The decompiled text of the parent script of a child function entry. Read only.</summary>
    Parent,
}

/// <param name="Text">What the editor shows.</param>
/// <param name="Mode">Which form of the code it is.</param>
/// <param name="Reason">Why the decompiler failed, null in <see cref="CodeMode.Decompiled"/>.</param>
/// <param name="ParentName">The parent entry of a child function entry (GameMaker 2.3), or null.</param>
public sealed record CodeLoad(string Text, CodeMode Mode, string? Reason = null, string? ParentName = null);

/// <summary>Never leaves the editor empty: when the decompiler fails the assembly is shown, and a child function entry can show its parent's text.</summary>
public static class CodeLoader
{
    /// <summary>Decompiles, falls back to the assembly, then to the parent text. Throws only when nothing could be produced.</summary>
    public static CodeLoad Load(Func<string> decompile, Func<string> disassemble, string? parentName = null, Func<string>? decompileParent = null)
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
                if (decompileParent is null) throw new InvalidOperationException(reason, second);
                return LoadParent(reason, parentName, decompileParent);
            }
        }
    }

    /// <summary>The parent entry's text, for the "show parent" button of an entry that was shown as assembly.</summary>
    public static CodeLoad LoadParent(string? reason, string? parentName, Func<string> decompileParent)
    {
        try
        {
            return new CodeLoad(decompileParent(), CodeMode.Parent, reason, parentName);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            throw new InvalidOperationException(reason ?? Reason(e), e);
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
