# Filter effects

Status: proposed. The editor for FEDS filter and effect definitions, and the catalogue of effect types used by room effect layers. Room layers are in [rooms.md](rooms.md). Shared rules: [../ux-standards.md](../ux-standards.md). Shell: [../shell.md](../shell.md).

## 1. Purpose and entry points

Filters and effects (blur, vignette, colour tint and so on) are shader-based effects that GameMaker applies to a room layer or to everything below an effect layer. The data file stores them in two places: the FEDS chunk (files from GMS 2.3.6) holds effect definitions, each with a name, a filter name and parameter values; and each room layer (files from GM 2022.1) stores the chosen effect type and its parameters. UTMT reads the chunk and the layer data and shows neither (UTMT-NOED-05 and UTMT-NOED-09). G3MStudio edits both, so a modder can change a vignette's strength without reading shader code.

The editor follows GameMaker's Filters and Effects panel (effect type list with parameter rows; [../../research/gamemaker/editors.md](../../research/gamemaker/editors.md) section 2.11).

Opens from Explorer > Data > Filter Effects, Go To `fx:`, the Effect row of a room layer ("Edit Effect"), and Find Usages on a filter name.

## 2. Layout

```
+-----------------------------------------------------------------------------------------+
| Filter effects   12 definitions                                   [search...] [New Effect]|
+---------------------------+-------------------------------------------------------------+
| Name              Filter  | Effect '_filter_vignette'                                    |
| _filter_blur      blur    |  Name           [_filter_vignette]  (string)                 |
| _filter_vignette > vignette|  Filter name   [vignette v]                                  |
| _filter_tint      tint    |  Parameters (from the type)                                  |
|                           |   Edges 1   [0.30]   Edges 2 [0.80]   Sharpness [0.10]       |
|                           |   Texture   [none  ...]                                      |
|                           |  Preview    [ image with the effect applied ]   [Play]       |
+---------------------------+-------------------------------------------------------------+
```

Left: list of definitions (virtualized, searchable). Right: a detail form whose parameter rows come from the selected effect type. The same detail form is used inside the room editor's Inspector for an effect layer.

## 3. Definitions (UTMT-NOED-05)

| Field | Control | Rules |
| --- | --- | --- |
| Name | String reference field | The definition name |
| Filter name | Combo of known effect types, plus free text for unknown | Selects the parameter set |
| Parameters | Property grid built from the type | See 4. Unknown types show a generic name and value list |
| Chunk version | Read-only | Version of the chunk header (1) |

Add (Insert) creates a definition for a chosen type with the type's default parameters. Delete is blocked while room layers refer to the definition by name: "{n} room layers use this effect." with Show Uses. Duplicate copies the parameters under a new name.

## 4. Effect types and parameters

Parameters are stored as name and value pairs. Known types give the pairs a label, range and control; the values G3MStudio shows by name are the parameter names the effect defines. Unknown pairs are shown as raw name and value rows and round-trip.

Types in GameMaker's filter and effect list, grouped as the manual groups them:

| Group | Types |
| --- | --- |
| Colour | Colour Balance, Colourise, Contrast, Greyscale, Hue, LUT Colour, Posterise, Tint |
| Blur | Gaussian Blur, Large Blur, Linear Blur, Recursive Blur, Twist Blur, Zoom Blur |
| Distort | Distort, Heat Haze, Panorama, Ripples, Screen Shake, Twirl Distort, Underwater |
| Pattern and noise | Blocks, Boxes, Clouds, Dots, Fractal Noise, Gradient, Pixelate, RGB Noise, Stripes, White Noise |
| Composite | Blend, Extended Blend, Mask, Parallax |
| Stylise | Edge Detect, Glow, Hard Drop Shadow, Old Film, Outline, Vignette, Windblown Particles |

Parameter definitions live in a metadata file shipped with the app (`effects.json`, one entry per type: label, parameter names, kind, range, default). New GameMaker types are added by editing that file; no code change is needed. Parameter kinds: Float (slider and number), Integer, Boolean, Colour (colour field), Vector (2 to 4 number fields), Sprite or texture (reference field), Enum (combo).

## 5. Preview

A preview renders a sample image (a room snapshot if opened from a layer, a built-in test card otherwise) with a simplified CPU implementation of the common types (colour, blur, vignette, tint, pixelate, posterise, greyscale, outline). Other types show the image unchanged with the note "Preview is not available for this effect." The preview never claims to match the game exactly and starts paused for animated effects (ux-standards section 5). Effects that strobe (old film, white noise, screen shake) show a still frame until "Allow Flashing Preview" is on.

## 6. Room layer effects (UTMT-NOED-09)

The room editor's Effect row on a layer (and the Effect layer type) uses this form. The layer stores: effect type name and parameter pairs, as in the definition. "Use Definition..." fills the layer's parameters from a FEDS definition; "Save As Definition..." creates one. Effect layers apply to everything below them; single-layer effects apply to that layer only. Depth on an effect layer shows as locked when the file marks it fixed. Details in [rooms.md](rooms.md) section 4.2.

## 7. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| New Effect Definition | `effects.new` | Insert | Insert |
| Duplicate Effect Definition | `effects.duplicate` | Ctrl+D | Cmd+D |
| Delete Effect Definition | `effects.delete` | Delete | Delete |
| Reset Parameters | `effects.reset` | None | None |
| Use Definition... | `effects.useDefinition` | None | None |
| Save As Definition... | `effects.saveDefinition` | None | None |
| Show Uses | `effects.showUses` | None | None |
| Export... | `effects.export` | None | None |
| Import... | `effects.import` | None | None |

Export and import use JSON with the type name and the parameter pairs.

## 8. Validation and messages

| Case | Message |
| --- | --- |
| Parameter out of range | "{parameter} must be between {min} and {max}." |
| Unknown filter name | Information "Filter '{name}' is not known to G3MStudio. Its parameters are shown as raw values." |
| Duplicate definition name | "'{name}' is already used by a {type}." |
| Delete a definition in use | See 3 |
| Chunk absent in file | Explorer row hidden |

## 9. Undo

Undoable: parameter commits, and definition add, delete, duplicate, reset and import. Grouping: a slider drag is one step; every other action is one step named after it.

## 10. States

Empty:
- No definitions in the chunk: "This data file has no filter effect definitions. Room layers can still store their own effects."
- Data file before GMS 2.3.6: the group is hidden and the room Effect row is unavailable.

Loading:
- Preview image decoding: "Loading preview..."

Error:
- Chunk unreadable: "The filter effect definitions could not be read: {error}. Room layers keep their own effects."
- Preview failed: "The preview could not be drawn: {error}. The parameters are still editable."

## 11. Parity coverage

| ID | Feature | Where |
| --- | --- | --- |
| UTMT-NOED-05 | Filter effects (FEDS) | 2, 3, 4 |
| UTMT-NOED-09 | Effect layer properties | 6, and rooms.md 4.2 |
| UTMT-CTRL-02, UTMT-CTRL-05 | String reference field, colour field | 3, 4 |
