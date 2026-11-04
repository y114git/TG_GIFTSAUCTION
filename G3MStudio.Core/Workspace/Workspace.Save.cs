using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

public sealed partial class Workspace
{
    /// <summary>A save ended: edits run again unless an export still holds them.</summary>
    private void EndSave()
    {
        if (_hold) return;
        History.Frozen = false;
        ApplyQueued();
    }

    /// <summary>Saves through the pipeline. Edits wait while it runs; on success the journal starts over from the saved file.</summary>
    public async Task<SaveResult> SaveAsync(SavePipeline pipeline, IProgress<GameMakerProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        History.Frozen = true;
        try
        {
            var result = await pipeline.SaveAsync(Data, Path, progress, cancellationToken);
            if (!result.Saved) return result;

            // No cancellation from here: the file is already replaced, so the journal must follow.
            Identity = await DataFileIdentity.ComputeAsync(Path, CancellationToken.None);
            _journal?.Reset(Identity, []);
            History.MarkSaved();
            JournalSuspended = false;
            return result;
        }
        finally
        {
            EndSave();
        }
    }

    /// <summary>Writes the model to another file (Save As, Save a Copy). Edits wait while it runs, as for a save, so the file never holds half an edit.</summary>
    public async Task<SaveResult> SaveCopyAsync(SavePipeline pipeline, string target, IProgress<GameMakerProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        History.Frozen = true;
        try
        {
            return await pipeline.SaveAsync(Data, target, progress, cancellationToken);
        }
        finally
        {
            EndSave();
        }
    }
}
