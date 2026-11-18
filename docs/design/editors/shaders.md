# Shaders

Status: proposed. The editor for SHDR resources: source text for each language, vertex attributes and the shader type. Shared rules: [../ux-standards.md](../ux-standards.md). Text editing uses the same editor control as [code.md](code.md).

## 1. Purpose and entry points

Edit shader source stored in the data file. Opens from Explorer > Shaders, Go To `shd:`, and References results. Each shader has a vertex and a fragment stage per language that the file contains.

## 2. Layout

```
+---------------------------------------------------------------------------+
| sh_blur   Index 1   GLSL ES + HLSL9      [Vertex] [Fragment]   Language [GLSL ES v]|
+---------------------------------------------------------------------------+
| Warning: precompiled HLSL bytecode cannot be edited as text.               |
+---------------------------------------------------------------------------+
|  1 attribute vec3 in_Position;                                              |
|  2 varying vec2 v_vTexcoord;                                                |
|    ...                                                                      |
+---------------------------------------------------------------------------+
| Attributes:  in_Position  in_Colour  in_TextureCoord        [Add Attribute] |
+---------------------------------------------------------------------------+
```

A stage switch (Vertex, Fragment; F4 switches), a language combo, a text editor, and the vertex attribute list.

## 3. Fields (UTMT-AST-01)

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | |
| Type | Combo: GLSL ES, GLSL, HLSL9, HLSL11, PSSL, Cg PS3, Cg PSVita | Types the file supports; changing the type does not convert source |
| GLSL ES vertex, GLSL ES fragment | Text editors | Syntax highlighting for GLSL; no compile check inside G3MStudio (the runner compiles at load) |
| GLSL vertex, GLSL fragment | Text editors | |
| HLSL9 vertex, HLSL9 fragment | Text editors | Source text only |
| HLSL11 and PSSL blobs | Read-only hex summary: size, "Replace From File..." | Precompiled bytecode cannot be edited as text; replacing needs a blob compiled elsewhere |
| Vertex attributes | List grid of attribute names (text) | Add, delete, reorder |

An info bar above the text warns when editing text for a language whose compiled blob is also present: "This shader also contains compiled code. Text edits do not change the compiled code." (the UTMT warning against editing compiled data). The source language combo lists only languages present in the file.

New shader (Resource > New Resource > Shader): creates six empty sources (UTMT-TREE-04) with a pass-through template for GLSL ES.

## 4. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Switch Vertex and Fragment | `shaders.switchStage` | F4 | F4 |
| Show Source Language... | `shaders.language` | None | None |
| Add Vertex Attribute | `shaders.addAttribute` | None | None |

Find and replace, folding, multi-cursor and comment toggles use the code editor's commands.

## 5. Validation and messages

| Case | Message |
| --- | --- |
| Empty required stage | Warning in Problems: "'{shader}' has an empty {stage} stage for {language}." |
| Attribute name invalid | "Attribute names can use letters, digits and underscores and cannot start with a digit." |
| Replace blob with the wrong size or empty file | "The file is empty." |
| Delete shader in use | Reference scan dialog |

## 6. Undo

Text edits use the text history until the editor loses focus; the commit then adds one workspace step "Edit {shader} {stage}". Attribute and blob changes are single steps.

## 7. Empty and loading states

- No shaders: "This game has no shaders."
- Shader with only a blob: the text area shows "No source text for this language."

## 8. Parity coverage

- UTMT-AST-01: sections 2 and 3.
- UTMT-TREE-04 (shader defaults on add): section 3.
- UTMT-PROJ-07 (shader project support): shaders export as `.vsh` and `.fsh` in project folders.
