using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>The template of a new shader: a pass-through vertex and fragment pair in GLSL ES and GLSL that draws the texture times the draw color, as GameMaker's own new shader does.</summary>
public static class ShaderTemplate
{
    private const string Vertex = """
        attribute vec3 in_Position;
        attribute vec4 in_Colour;
        attribute vec2 in_TextureCoord;

        varying vec2 v_vTexcoord;
        varying vec4 v_vColour;

        void main()
        {
            vec4 object_space_pos = vec4(in_Position.x, in_Position.y, in_Position.z, 1.0);
            gl_Position = gm_Matrices[MATRIX_WORLD_VIEW_PROJECTION] * object_space_pos;

            v_vColour = in_Colour;
            v_vTexcoord = in_TextureCoord;
        }

        """;

    private const string Fragment = """
        varying vec2 v_vTexcoord;
        varying vec4 v_vColour;

        void main()
        {
            gl_FragColor = v_vColour * texture2D(gm_BaseTexture, v_vTexcoord);
        }

        """;

    public static GameMakerShader Create(GameMakerData data, GameMakerString name) => new()
    {
        Name = name,
        Type = GameMakerShader.ShaderType.GLSL_ES,
        GLSL_ES_Vertex = data.Strings.MakeString(Vertex),
        GLSL_ES_Fragment = data.Strings.MakeString(Fragment),
        GLSL_Vertex = data.Strings.MakeString(Vertex),
        GLSL_Fragment = data.Strings.MakeString(Fragment),
        HLSL9_Vertex = data.Strings.MakeString(string.Empty),
        HLSL9_Fragment = data.Strings.MakeString(string.Empty),
    };
}
