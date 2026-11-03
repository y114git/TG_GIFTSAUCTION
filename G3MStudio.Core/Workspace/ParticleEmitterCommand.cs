using System.Globalization;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// Adds a new emitter with default settings to a particle system, or takes one out of it. A new emitter is also added to the game's emitter table under a new name;
/// a removed emitter stays in that table (other systems may share it) and only leaves this system.
/// </summary>
#pragma warning disable CA1001 // the emitter belongs to the game data or to the system; the model disposes it
public sealed class ParticleEmitterCommand : EditCommand
{
    public const string Name = "particleEmitter";
    public const string Add = "add";
    public const string Remove = "remove";

    private readonly string _system;
    private readonly string _operation;
    private readonly int _index;
    private GameMakerParticleSystemEmitter? _created;
    private GameMakerResourceById<GameMakerParticleSystemEmitter, GameMakerChunkPSEM>? _reference;
    private int _at;

    public ParticleEmitterCommand(string system, string operation, int index = -1) => (_system, _operation, _index) = (system, operation, index);

    public override string Kind => Name;

    public override string Editor => "particleSystems";

    public override IReadOnlyDictionary<string, string> Args => new Dictionary<string, string>
    {
        ["system"] = _system,
        ["operation"] = _operation,
        ["index"] = _index.ToString(CultureInfo.InvariantCulture),
    };

    public override IReadOnlyList<ProjectTouch>? Touches => null;

    public static ParticleEmitterCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["system"], a["operation"], int.Parse(a["index"], CultureInfo.InvariantCulture));

    private GameMakerParticleSystem Find(GameMakerData data) => data.ParticleSystems?.FirstOrDefault(p => p.Name.Content == _system) ?? throw new InvalidOperationException($"Particle system '{_system}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var system = Find(data);
        if (_operation == Add)
        {
            var table = data.ParticleSystemEmitters ?? throw new InvalidOperationException("The data file has no emitter table.");
            var number = table.Count;
            string name;
            do name = "emitter_" + (number++).ToString(CultureInfo.InvariantCulture);
            while (table.Any(e => e.Name.Content == name));
            _created = new GameMakerParticleSystemEmitter { Name = data.Strings.MakeString(name, out _) };
            table.Add(_created);
            _reference = new GameMakerResourceById<GameMakerParticleSystemEmitter, GameMakerChunkPSEM>(_created);
            _at = system.Emitters.Count;
            system.Emitters.Add(_reference);
            return;
        }

        if (_operation != Remove) throw new InvalidDataException($"Unknown emitter operation '{_operation}'.");
        if (_index < 0 || _index >= system.Emitters.Count) throw new InvalidOperationException($"Particle system '{_system}' has no emitter {_index}.");
        (_reference, _at) = (system.Emitters[_index], _index);
        system.Emitters.RemoveAt(_index);
    }

    public override void Revert(GameMakerData data)
    {
        var system = Find(data);
        if (_reference is null) throw new InvalidOperationException("The edit was never applied.");
        if (_operation == Add)
        {
            system.Emitters.Remove(_reference);
            if (_created is not null) data.ParticleSystemEmitters.Remove(_created);
        }
        else
        {
            system.Emitters.Insert(_at, _reference);
        }
    }
}
#pragma warning restore CA1001
