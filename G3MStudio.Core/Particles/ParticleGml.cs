using System.Globalization;
using System.Text;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Particles;

/// <summary>GML that rebuilds a particle system with the part_* functions: one particle type and one emitter per emitter of the system.</summary>
public static class ParticleGml
{
    private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Colour(uint argb) => string.Create(CultureInfo.InvariantCulture, $"${argb & 0xFF:X2}{(argb >> 8) & 0xFF:X2}{(argb >> 16) & 0xFF:X2}");

    public static string Generate(GameMakerParticleSystem system)
    {
        var text = new StringBuilder();
        text.AppendLine("var _system = part_system_create();");
        text.AppendLine(CultureInfo.InvariantCulture, $"part_system_position(_system, {system.OriginX}, {system.OriginY});");
        foreach (var e in (system.Emitters ?? []).Select(r => r.Resource).OfType<GameMakerParticleSystemEmitter>().Where(e => e.Enabled)) Emitter(text, e);
        return text.ToString();
    }

    private static void Emitter(StringBuilder text, GameMakerParticleSystemEmitter e)
    {
        var name = e.Name?.Content ?? "emitter";
        text.AppendLine(CultureInfo.InvariantCulture, $"var _type_{name} = part_type_create();");
        if (e.Sprite?.Name?.Content is { } sprite) text.AppendLine(CultureInfo.InvariantCulture, $"part_type_sprite(_type_{name}, {sprite}, {Bool(e.Animate)}, {Bool(e.Stretch)}, {Bool(e.IsRandom)});");
        else if (e.Texture != GameMakerParticleSystemEmitter.TextureEnum.None) text.AppendLine(CultureInfo.InvariantCulture, $"part_type_shape(_type_{name}, pt_shape_{e.Texture.ToString().ToLowerInvariant()});");
        text.AppendLine(CultureInfo.InvariantCulture, $"part_type_size(_type_{name}, {F(e.SizeMinX)}, {F(e.SizeMaxX)}, {F(e.SizeIncreaseX)}, {F(e.SizeWiggleX)});");
        text.AppendLine(CultureInfo.InvariantCulture, $"part_type_scale(_type_{name}, {F(e.ScaleX)}, {F(e.ScaleY)});");
        text.AppendLine(CultureInfo.InvariantCulture, $"part_type_speed(_type_{name}, {F(e.SpeedMin)}, {F(e.SpeedMax)}, {F(e.SpeedIncrease)}, {F(e.SpeedWiggle)});");
        text.AppendLine(CultureInfo.InvariantCulture, $"part_type_direction(_type_{name}, {F(e.DirectionMin)}, {F(e.DirectionMax)}, {F(e.DirectionIncrease)}, {F(e.DirectionWiggle)});");
        text.AppendLine(CultureInfo.InvariantCulture, $"part_type_gravity(_type_{name}, {F(e.GravityForce)}, {F(e.GravityDirection)});");
        text.AppendLine(CultureInfo.InvariantCulture, $"part_type_orientation(_type_{name}, {F(e.OrientationMin)}, {F(e.OrientationMax)}, {F(e.OrientationIncrease)}, {F(e.OrientationWiggle)}, {Bool(e.OrientationRelative)});");
        text.AppendLine(CultureInfo.InvariantCulture, $"part_type_colour3(_type_{name}, {Colour(e.StartColor)}, {Colour(e.MidColor)}, {Colour(e.EndColor)});");
        text.AppendLine(CultureInfo.InvariantCulture, $"part_type_blend(_type_{name}, {Bool(e.AdditiveBlend)});");
        text.AppendLine(CultureInfo.InvariantCulture, $"part_type_life(_type_{name}, {F(e.LifetimeMin)}, {F(e.LifetimeMax)});");
        text.AppendLine(CultureInfo.InvariantCulture, $"var _emitter_{name} = part_emitter_create(_system);");
        text.AppendLine(CultureInfo.InvariantCulture, $"part_emitter_region(_system, _emitter_{name}, {F(e.RegionX - (e.RegionWidth / 2))}, {F(e.RegionX + (e.RegionWidth / 2))}, {F(e.RegionY - (e.RegionHeight / 2))}, {F(e.RegionY + (e.RegionHeight / 2))}, ps_shape_{e.Shape.ToString().ToLowerInvariant()}, ps_distr_{Distribution(e.Distribution)});");
        var call = e.Mode == GameMakerParticleSystemEmitter.EmitMode.Burst ? "part_emitter_burst" : "part_emitter_stream";
        text.AppendLine(CultureInfo.InvariantCulture, $"{call}(_system, _emitter_{name}, _type_{name}, {e.EmitCount});");
    }

    private static string Distribution(GameMakerParticleSystemEmitter.DistributionEnum d) => d == GameMakerParticleSystemEmitter.DistributionEnum.InverseGaussian ? "invgaussian" : d.ToString().ToLowerInvariant();

    private static string Bool(bool v) => v ? "true" : "false";
}
