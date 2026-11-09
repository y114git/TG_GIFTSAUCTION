namespace G3MStudio.Core.Commands;

/// <summary>The Explorer organizing commands of the shell (folders, collections, bulk operations).</summary>
public interface IOrganizeShell
{
    /// <summary>Makes a folder (under the selected one) and puts the selected entries in it.</summary>
    void NewFolder();

    /// <summary>Asks for a folder and moves the selected entries into it.</summary>
    void MoveToFolder();

    /// <summary>Asks for a collection and adds the selected entries to it.</summary>
    void AddToCollection();

    /// <summary>Takes the selected entries out of the folder, collection or tag they are listed under.</summary>
    void RemoveFromGroup();

    /// <summary>Gives the selected folder, collection or tag a colour.</summary>
    void ColourLabel();

    /// <summary>Renames the selected entries by a pattern, with a preview.</summary>
    void BulkRename();

    /// <summary>Switches the Explorer between the type view and the folder view.</summary>
    void ToggleFolderView();

    /// <summary>Selects every entry of the type of the selected entry.</summary>
    void SelectByType();

    /// <summary>Selects every entry of the selected folder, collection, tag or type.</summary>
    void SelectContents();

    /// <summary>Moves the selected sprites, fonts and tile sets to a texture group.</summary>
    void MoveToTextureGroup();

    /// <summary>Moves the selected sounds to an audio group.</summary>
    void MoveToAudioGroup();

    /// <summary>Narrows the Explorer to the selected folder or collection.</summary>
    void OpenFolder();

    /// <summary>Packs the selected folder, collection, tag or entries into one ZIP.</summary>
    void ExportPackage();

    /// <summary>Goes one level up out of a folder.</summary>
    void FolderUp();
}
