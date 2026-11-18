using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>What a placement on the texture pages will do, in words, for the Resize dialogs: one line per distinct outcome.</summary>
public static class FitText
{
    public static string Describe(IReadOnlyList<FitStep> steps)
    {
        static string Size((int Width, int Height) s) => $"{s.Width} x {s.Height}";
        // a new page, a grown page and moved pictures come first, so the cut after four lines never hides them
        var lines = steps.OrderByDescending(s => s.Kind).Select(s => s.Kind switch
        {
            FitKind.InPlace => Loc.Current.Format("pages.fitInPlace", ("page", s.Page)),
            FitKind.FreeSpace => Loc.Current.Format("pages.fitFree", ("page", s.Page)),
            FitKind.Repack => Loc.Current.Format("pages.fitRepack", ("page", s.Page), ("count", s.Moved)),
            FitKind.Grow => Loc.Current.Format("pages.fitGrow", ("page", s.Page), ("from", Size(s.PageBefore)), ("to", Size(s.PageAfter))),
            _ => Loc.Current.Format("pages.fitNewPage", ("size", Size(s.PageAfter))),
        }).Distinct().ToList();
        // many frames: the first lines and a mark that there are more
        return lines.Count == 0 ? Loc.Current["pages.fitNothing"] : string.Join(Environment.NewLine, lines.Take(4).Concat(lines.Count > 4 ? ["…"] : []));
    }
}
