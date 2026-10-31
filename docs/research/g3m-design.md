# G3M design baseline for G3MStudio

> Decision note (questions.md Q23, owner, 2026-10-25): G3MStudio has one default theme, 90% G3M design and style and 10% GameMaker, VS Code and Zed. There is no light theme and no dark/light split, and the whole look is customizable. This decision supersedes the proposal in this file where they differ (for example the OS light/dark follow of the Fluent base and the "solid surfaces by default" advice). The measured facts about G3M stay as research.

Sources: G3M 3.4.3 (PyQt6), G3MTool GUI 1.0.0 (Avalonia 12.1.4), G3M-MCE 1.0.0 (React 19). Paths are relative to `G3MSpace/`.

## 1. G3M design tokens

### Default palette

Defined once in `G3M/src/config/styles.py` (`DEFAULT_THEME`, re-exported by `G3M/src/config/config.py`). The wiki page `G3MWiki/G3M/customization/colors.md` documents the same seven user channels.

| Key | Hex | Role |
| --- | --- | --- |
| `background` | `#282828` | Window backdrop. Panels draw it translucent: `rgba(40, 40, 40, 150)` over the background image |
| `elements` | `#222222` | Button, field, list, tab, menu and checkbox fill |
| `border` | `#039D5B` | Outline of every control and panel; also the "ready" launch status color |
| `hover` | `#616B78` | Hover fill, checked button fill, selected tab and selected list item fill |
| `select` | `#ECEDEF` | 2 px outline of a selected list item or checked button, checked checkbox fill |
| `main_text` | `#E8E9EB` | All text, arrow glyphs, scrollbar handle default |
| `secondary_text` | `#6DE985` | Version labels, metadata, progress bar chunk |
| `disabled_bg` | `#333333` | Disabled button, combo box, tab, checkbox fill |
| `disabled_text` | `#888888` | Disabled text |
| `disabled_border` | `#555555` | Disabled outline |

Derived and fixed values:

| Value | Where | Use |
| --- | --- | --- |
| `rgba(40, 40, 40, 150)` | `styles.py` `FALLBACK_FRAME_BG` | Frame and panel background over the image |
| `rgba(40, 40, 40, 230)` | `FALLBACK_TOOLTIP_BG` | Tooltip background |
| `rgba(0, 0, 0, 200)` | `FALLBACK_WINDOW_BG` | Window fallback |
| `rgba(40, 40, 40, 40)` | `FALLBACK_SCROLL_GROOVE` | Scrollbar groove |
| `red`, `orange`, `green`, `gray`, `lightgreen`, `blue` | `UI_COLORS` | Status bar message colors (Qt named colors) |
| `#4CAF50` / hover `#5CB85C` | `MOD_WIDGET_STYLE_TEMPLATE` | Card Download button |
| `#F44336` / hover `#D32F2F` | same | Card Uninstall button |
| `#FF4444`, `#5599FF`, `#44FF44`, `#FFDD44`, `#FFFFFF` | `RICH_HTML_CSS_CLASS_MAP` | Red, blue, green, yellow, white classes in mod descriptions |
| `#D9534F` | `ui/dialogs/mod_editor/dialog.py:1615` | Invalid field outline in Mod Editor |
| `#8F8F8F`, `#6F6F6F` | `ui/common/dialog_theme.py` | Disabled text and border in themed dialogs (differs from the main `#888888`/`#555555`) |

There are no separate error, success or warning tokens. Errors use Qt `red` in the status bar and ad hoc hexes elsewhere.

### Custom colors and alternative themes

- `G3M/src/config/settings_schema.py`: `THEME_COLOR_SETTING_KEYS` maps the seven channels to settings keys (`custom_background_color` ... `custom_secondary_text_color`). `get_theme_color_key(name)` resolves aliases (`text` to `main_text`, `version_text` to `secondary_text`) and falls back to `custom_<name>_color`.
- `G3M/src/ui/common/styling.py:400` `get_theme_color(config, key)`: stored override if non-empty, else `DEFAULT_COLORS[key]`. Cached per config identity and value.
- `G3M/src/controllers/theme_controller.py` `ThemeController.apply_theme()`: reads overrides, converts the custom background color to `rgba(r,g,b,150)` for frames and `rgba(r,g,b,230)` for tooltips (an 8-digit `#AARRGGBB` value keeps its own alpha), scales fonts by `ui_scale`, calls `build_stylesheet()` and sets it on the `QApplication`. Updates are debounced by 150 ms. Disabled colors can be overridden only through hidden keys `custom_disabled_bg`, `custom_disabled_text`, `custom_disabled_border`.
- `G3M/src/config/style_loader.py` `build_stylesheet()`: loads six QSS templates from `G3M/src/config/qss/` (`main`, `titlebar`, `buttons`, `tooltips`, `settings`, `scrollbar`), replaces `%token%` placeholders, computes a clamped radius per control, and multiplies every `Npx` by the zoom factor. Results are cached by the full parameter tuple.
- Themes are ZIP packages with `theme_config.json` (version `1.0.0`) plus optional `background.*`, `background_music.*`, `startup_sound.*`, `custom_logo.*`, `custom_font.ttf|otf`. They hold the seven colors, `custom_border_radius`, `background_disabled`, `disable_animations`, `disable_startup_sound`. Stored in the user themes dir (`get_user_themes_dir()`).

Catalog themes in `G3M/catalog/themes/*/theme_config.json` (empty string means default):

| Theme | background | elements | border | hover | select | main text | secondary | radius |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLEANG3M | default | default | default | default | default | default | default | 7 |
| DELTAG3M | `#000000` | `#000000` | `#FFFFFF` | `#3C393E` | `#CBCBCB` | `#FFFFFF` | `#939393` | 0 |
| FRICKG3M | `#550000` | default | `#FF0000` | default | default | default | `#FF8282` | 0 |
| PIZZAG3M | `#AA533D52` | `#684868` | `#000000` | `#7A3A00` | `#F6E17D` | default | `#F7BD00` | 5 |
| SUGARYG3M | `#150028` | `#65009F` | `#93049D` | `#3E005C` | `#D5D6D8` | `#FFFAB9` | `#FFB5FF` | 5 |

Themes change color, radius, media and font only. Sizes, spacing and border widths are fixed.

### Fonts and sizes

- Family: per-language font from the lang file metadata, loaded from `G3M/src/assets/lang/`: `NotoSans-Bold.ttf` (en, es, ru, zh_tw), `NotoSansJP-Bold.ttf`, `NotoSansKR-Bold.ttf`, `NotoSansSC-Bold.ttf`. A theme `custom_font` overrides it. Final fallback is `"Arial"` in `build_stylesheet()`. Only Bold files ship, so the whole UI renders bold.
- Sizes (`DEFAULT_THEME`): `font_size_main` 16 and `font_size_small` 12, both emitted as `pt` in QSS (about 21 px and 16 px at 96 DPI). Dialogs, message boxes, tooltips and title bar menus use the small size. Scaled by `ui_scale`.
- Fixed px sizes in QSS and code: 10 px (arrow labels, progress bar text), 11 px (close glyph), 12 px (sort button, downloads button, title bar buttons), 16 px (search icon buttons, loading label, bold section labels), 18 px (top refresh button). Card text 14 px, card buttons 15 px (`generate_widget_style`).

### Spacing and sizing

| Element | Value | Source |
| --- | --- | --- |
| Push button | padding 6 px 16 px, min-height 30 px | `main.qss` |
| Combo box, line edit, spin box | padding 5 px 10 px, min-height 30 px | `main.qss` |
| Dialog buttons | padding 8 px 15 px, bold | `dialog_theme.py` |
| Tab | padding 4 px 12 px, min-height 23 px, margin-right 4 px; tab bar centered, min-width 92 or 110 px | `main.qss`, `styles.py` |
| Menu | padding 4 px, item 6 px 18 px | `main.qss` |
| Icon buttons | 35 x 30 px; top refresh 40 x 40 px | `buttons.qss` |
| Checkbox indicator | 18 x 18 px | `main.qss` |
| Scrollbar | 16 px thick, handle min 25 px, handle margin 1 px | `scrollbar.qss` |
| Title bar | 38 px high, margins 8,4,8,4, spacing 6 and 8, window buttons 26 x 26 px with 12 px icons | `custom_title_bar.py` |
| Tooltip | content margins 12,10,12,10, max width 400 px | `custom_controls.py` |
| Layout spacing, by frequency | 8, 10, 12, 6, 4, 0, 15, 2 | `setSpacing` calls across `src` |
| Layout margins, by frequency | 0, 12, 8, 18, 16, 15 | `setContentsMargins` calls |

There is no spacing scale; 10 and 15 sit beside a 4 px rhythm.

### Corner radii

One user setting, `custom_border_radius`, default 7 px (`styling.py:473 get_border_radius`). `clamp_border_radius()` limits it to half of the smallest control dimension (`min(radius, min(width, height) // 2)`, with border and margin taken into account), so a 30 px button and an 18 px checkbox receive different effective radii. Exceptions: progress bar 4 px with 3 px chunk (`dialog_theme.py build_progress_bar_stylesheet`), card frames and icons from the same clamp. Themes set 0, 5 or 7.

### Border widths

The baseline is 2 px solid on buttons, fields, combo boxes, lists, list items, tabs, checkboxes, menus, tooltips, the title bar and cards (`generate_widget_style` hardcodes `"2px"`). Places that use another width:

| Location | Width | What |
| --- | --- | --- |
| `G3M/src/config/qss/main.qss:19` | 3 px | `QDialog`, `QMessageBox` outline |
| `G3M/src/config/qss/main.qss:222` | 0 | `QTabWidget::pane` |
| `main.qss` `QMenu::separator` | 1 px line | Menu separator height |
| `G3M/src/ui/common/styling.py:753,758` | 1 px | Optional scrollbar handle and track borders |
| `G3M/src/ui/dialogs/mod_editor/dialog.py:1615` | 1 px | Invalid field outline `#d9534f` |
| `G3M/src/ui/dialogs/mod_editor/dialog.py:2066` | 1 px | Pane title bottom border |
| `G3M/src/ui/widgets/mod/mod_summary_panel.py:947` | 1 px | Row bottom border |
| `G3M/src/ui/dialogs/mod_diagnostics_dialog.py:1871` | 1 px | Diagnostics frame |
| `G3M/src/ui/dialogs/mod/priority_steps_dialog.py:358` | 1 px | `QGroupBox` in `#d8d8d8` |
| `G3M/src/ui/dialogs/mod/readme_dialog.py:411` | 3 px | Left quote bar |
| `G3M/src/ui/dialogs/g3mtool_diff_viewer.py:155` | 3 px | Blockquote bar |
| `G3M/src/ui/common/rich_html.py:444,484` | 1 px, 4 px | Rich description code block and table bar |
| `G3M/src/services/diagnostics/preflight_service.py:113` | 1 px | Exported HTML report table |

### Component styles

- Buttons: `elements` fill, 2 px `border`, `main_text`. Hover: `hover` fill. Checked: `hover` fill plus 2 px `select` outline. Disabled: disabled triplet. No pressed state for ordinary buttons. Dialog buttons are bold and use hover for pressed.
- Inputs: `QLineEdit` and `QSpinBox` center their text (`qproperty-alignment: AlignCenter`). Focus has no style in the main sheet; themed dialogs set `QLineEdit:focus` to a 2 px `hover` border. The color field (`settings.qss`) switches its border to `main_text` on focus and hover.
- Combo box: 30 px wide drop-down area, arrow SVG 16 x 10 px tinted with `main_text` (written to `%TEMP%/g3m_arrows`). Popup items use the list item rules. `NoScrollComboBox` ignores the mouse wheel.
- Lists: item hover `hover` fill with transparent 2 px border; selected item `hover` fill plus 2 px `select` outline, radius clamped to a 30 px row.
- Checkbox: 18 px indicator, `elements` fill, checked fill `select` (solid light square, no check mark).
- Tabs: same look as buttons, selected tab = `hover` fill.
- Scrollbar: borderless, groove `rgba(40,40,40,40)`, handle color = custom `elements` color, else `main_text` `#E8E9EB` (a light handle by default), no arrow buttons.
- Tooltip: `#animated_tooltip` and `QToolTip` share 2 px `border`, `rgba(40,40,40,230)`, small font, `custom_border_radius`.
- Title bar (`G3M/src/ui/widgets/shared/custom_title_bar.py`, `G3M/src/config/qss/titlebar.qss`): frameless window (`main.py:204`), a rounded bar with frame background and 2 px border. Left: text menu buttons (bold, small font, transparent with transparent 2 px border; hover/pressed adds `hover` fill and `border` outline) opening `titleBarPopupMenu` 6 px below. Right: minimize, maximize/restore, close as 26 px square buttons with tinted 12 px SVG icons; pressed outline becomes `main_text`. A window corner mask follows the radius.
- `SectionToggle` (`G3M/src/ui/widgets/shared/custom_controls.py:14`): checkable `QToolButton`, text beside a native down/right arrow, keyboard operable, no own QSS (inherits transparent `QWidget` rule).
- Global rule `QWidget { outline: none; }` in `main.qss` removes the focus rectangle everywhere. Only a few dialogs add a `:focus` border (`game/manager_dialog.py:290`, `manual_install/styling.py:30`, `profile_manager_dialog.py:451`, `g3mtool_diff_viewer.py:448`), using `select` color.

### Animation

`UIAnimator` in `G3M/src/ui/utils/ui_utils.py`; all helpers return early when `disable_animations` is set.

| Effect | Duration | Easing |
| --- | --- | --- |
| `fade_in` / `fade_out` opacity (default) | 200 ms | InOutSine |
| `AnimatedToolTip` show and hide (`app/window/main.py` `_show_custom_tooltip`) | 150 ms | InOutSine |
| Overlay fade | 300 ms | InOutSine |
| Image fade | 250 ms | InOutSine |
| `collapse_expand` on `maximumHeight` (default) | 250 ms | InOutQuad |
| Settings section expand (`settings_view_builder.py:327`) | 200 ms | InOutQuad |
| `progress_to` progress bar value | 220 ms | OutCubic |

`AnimatedToolTip` is a frameless, input-transparent, translucent `QLabel` that paints its stylesheet background with antialiasing, word-wraps and caps width at 400 px.

### Icons

- 35 single-color SVGs in `G3M/src/assets/icons/` (add, arrow_down/up, block, checkmark, cross, delete, download, duplicate, edit, export, external, extract_type, filerestore, folder, g3m, import, info_type, like, maximize, minimize, note, overwrite_type, patch_type, refresh, reset, restore, save, search, settings, time, tool, update, wip, plus brand logos boosty, discord, telegram) and `gbicon.png` (GameBanana).
- Tinting: `colored_icon(name, color)` (`G3M/src/utils/path_utils.py:677`) replaces a hardcoded fill or stroke (`#0F0F0F`, `#000000`, `#444`, `#0C0310`, `#0D0D0D`, `#010002`, `#e8e9eb`) listed in `_ICON_DEFS` (`path_utils.py:586`) and renders a 128 px pixmap. Icons follow `main_text`.
- No icon source or license is recorded in `THIRD_PARTY_NOTICES.md`. Confirm origin before copying icons into G3MStudio.
- G3M-MCE reuses 13 of these files (`G3M-MCE/src/assets/icons/`) through a CSS mask with `currentColor`.

## 2. G3MTool GUI and G3M-MCE compared with G3M

### G3MTool GUI (`G3MTool/G3MToolGUI/Styles.axaml`, `App.axaml`)

Matches G3M: `G3MBackground #282828`, `G3MElements #222222`, `G3MBorder #039D5B`, `G3MAccent #6DE985` (= `secondary_text`), `G3MText #E8E9EB`; `BorderThickness 2` on `Button`, `TextBox`, `ComboBox`, `CheckBox`, `Border.card`.

Differs from G3M:

| Aspect | G3M | G3MTool GUI |
| --- | --- | --- |
| Base theme | Fully custom QSS | `FluentTheme` with overrides; no `RequestedThemeVariant`, so it follows the OS light/dark setting |
| Font | Noto Sans Bold per language | `Segoe UI, Arial`, regular weight, sizes 11, 12, 30 |
| Window chrome | Frameless with `CustomTitleBar` | Native title bar |
| Background | Full image under `rgba(40,40,40,150)` frames | `G3MTool_bg.png` at opacity 0.18, panels `#E6222222` |
| Button | `#222222`, opaque green border, hover `#616B78` | `#2B3431`, border `#7A039D5B`, hover `#3A4A43`, 120 ms brush transitions |
| Primary button | None (only green card Download `#4CAF50`) | `Button.primary`: `#039D5B` fill, `#6DE985` border, white SemiBold text |
| Field | `#222222`, green border, no focus style | `#CC171717`, border `#70616B78`, focus border green, 120 ms transition |
| Radii | One value (7), clamped | card 10, button 6, text box 5, combo box Fluent default |
| Muted text | None | `G3MMuted #9CA3AF` |
| Selection color | `#ECEDEF` | Not used |
| Progress bar | 2 px border, `secondary_text` chunk, radius 4 | 7 px high, `#3A3A3A` track, accent fill, 180 ms value transition |

Reusable for G3MStudio: the `Styles.Resources` color-key pattern, `Border.card`, the `Transitions` blocks on `Button`, `TextBox` and `ProgressBar`, the `:focus` border swap, the `avares://` asset layout, and the CI macOS `.app` bundling and per-RID publish settings in `G3MToolGUI.csproj`. Replace values with the proposed tokens below and set `RequestedThemeVariant="Dark"`. Fluent defaults outside the overridden controls (popups, tooltips, menus, scrollbars, list items) keep Fluent border thicknesses and need explicit styles to meet the 2 px rule.

### G3M-MCE (`G3M-MCE/src/styles/theme.css`, component CSS)

Matches G3M: the seven channel colors as `--g3m-background`, `--g3m-elements`, `--g3m-hover`, `--g3m-select`, `--g3m-border`, `--g3m-text`, `--g3m-secondary`; `--g3m-radius: 7px`; `--g3m-border-width: 2px` applied to every element; Noto Sans Bold 700 (`public/fonts/NotoSans-Bold.ttf` with `OFL.txt`); hover fill = `hover`; checked checkbox fill = `select`; field focus border = `hover` (same as G3M dialogs).

Differs from G3M: body 14 px/1.5, h1 22 px, h2 16 px, small 12 px; controls min-height 40 px (44 px on coarse pointers), padding 8 px 12 px; checkbox 22 px; focus ring `outline: 2px solid var(--g3m-select)` with 2 px offset; disabled shown by `opacity: 0.5`; extra tokens `--g3m-muted #b1b9b5`, `--g3m-danger #ff9f9f`; operation colors `#6aeb7e` (soft) and `#6a8eeb` (hard); dialog backdrop `#000b`; `prefers-reduced-motion` and `forced-colors` support. Border exceptions: scrollbar thumb `3px solid transparent` as inset padding (`ModEditor.css:63`), `border: 0` on inline error buttons and a few resets.

## 3. Image assets that show the look

No screenshots exist in the workspace (`G3MWiki` has none). Existing images:

| Path | Content |
| --- | --- |
| `G3M/src/assets/images/logo.png` | Wordmark "G3M": light green bevelled letters with dark green outline, diamond G glyph |
| `G3M/src/assets/images/background.png` | 1920 x 1080 blurred dark teal and green field with soft olive light spots |
| `G3M/src/assets/icons/icon.ico`, `G3M/builds/assets/icons/icon.icns`, `G3M/src/assets/icons/g3m_icon.svg` | App icon and monochrome G glyph |
| `G3M/builds/assets/WizardImage.bmp`, `SmallIcon.bmp` | Windows installer images |
| `G3M/catalog/themes/{DELTAG3M,FRICKG3M,PIZZAG3M,SUGARYG3M}/background.*`, `custom_logo.png` | Alternative theme looks |
| `G3MTool/G3MToolGUI/Assets/images/G3MTool_logo.png`, `G3MTool_bg.png` | G3MTool wordmark and background |
| `G3MTool/G3MToolGUI/Assets/icons/G3MTool_icon.ico`, `.icns`, `boosty_logo.png`, `telegram_logo.png`, `discord_logo.png` | G3MTool icons |
| `G3M-MCE/src/assets/g3m-logo.png`, `icon.ico` | Copies of the G3M logo and icon |

## 4. Changelog and UI string style

`G3M/CHANGELOG.md`: one section per version, heading "Version X.Y.Z", an em dash, then a `DD.MM.YY` date; the unreleased section sits in a `<details>` block. Flat bullets, each one to three full sentences, past tense ("Fixed", "Added") or a stated result ("Progress bars across G3M now fill smoothly"). Control names in bold, file names in backticks. Large features get a bold lead bullet with nested sub-bullets. Tone is plain and user-facing.

UI strings (`G3M/src/assets/lang/lang_en.json`): Title Case for buttons and dialog titles, with exceptions in sentence case; tooltips, errors and statuses are full sentences ending in a period; progress statuses end with three ASCII periods; placeholders use `{name}`; quoted names use single quotes; errors follow "Failed to ...: {error}"; some labels are all caps (`INFO`, `BOOSTY`, `DATA`).

1. `buttons.change_background`: "Change Background"
2. `buttons.open_g3m_folder`: "Open G3M folder"
3. `tooltips.manual_install_save_loading`: "GameBanana details are loading. Wait for loading to finish."
4. `status.recovering_previous_session`: "Recovering previous session..."
5. `dialogs.gamebanana_picker_title`: "Select file for '{mod_name}'"

## Proposed G3MStudio token set

Keep the G3M identity (dark neutral surfaces, green outline, light text, green accent text, light selection) and make it denser. Every value below is for Avalonia resources in `Styles.axaml`, the single default theme (no light variant).

### Color

| Token | Hex | Origin | Use | Contrast |
| --- | --- | --- | --- | --- |
| `Bg.Window` | `#282828` | G3M `background` | Window, docking area | text 12.1:1 |
| `Bg.Surface` | `#222222` | G3M `elements` | Controls, panels | text 13.1:1 |
| `Bg.Sunken` | `#1B1B1B` | new | Code editor, hex view, dense lists | text 14.2:1 |
| `Bg.Raised` | `#2B3431` | G3MTool button | Pane headers, toolbars, tab strip | text 10.6:1 |
| `Bg.Overlay` | `#E6282828` | G3M tooltip bg | Tooltips, popups, flyouts | |
| `Border` | `#039D5B` | G3M `border` | Interactive control outline | 4.5:1 on `Bg.Surface` |
| `Border.Subtle` | `#3A4A43` | G3MTool hover | Pane splitters, grid lines, structural dividers | |
| `Hover` | `#5A6370` | G3M `#616B78`, darkened | Hover fill | text 5.0:1 (G3M value gives 4.45:1) |
| `Pressed` | `#4E5763` | new | Pressed fill | text 6.0:1 |
| `Select` | `#ECEDEF` | G3M `select` | Selection outline, checked fill, focus ring | 13.6:1 |
| `Selection.Bg` | `#14523A` | new | Selected row fill in dense lists and trees | text 7.5:1 |
| `Text` | `#E8E9EB` | G3M `main_text` | Body text, icons | |
| `Text.Accent` | `#6DE985` | G3M `secondary_text` | Versions, metadata, links | 10.3:1 |
| `Text.Muted` | `#9CA3AF` | G3MTool muted | Hints, captions, placeholders | 6.3:1 |
| `Accent.Fill` | `#039D5B` | G3MTool primary | Primary button fill with `#0F0F0F` text | 5.5:1 (white text gives 3.5:1) |
| `Danger` | `#FF9F9F` | MCE danger | Error text and error outline | 8.1:1 |
| `Danger.Fill` | `#F44336` | G3M uninstall | Destructive button fill | |
| `Warning` | `#FFB74D` | new (G3M uses Qt `orange`) | Warning text and outline | 9.2:1 |
| `Success` | `#6DE985` | G3M `secondary_text` | Success text | 10.3:1 |
| `Info` | `#5599FF` | G3M rich text blue | Info text | 5.7:1 |
| `Disabled.Bg` / `.Text` / `.Border` | `#333333` / `#888888` / `#555555` | G3M | Disabled controls | |

Map G3M theme channels onto these tokens so G3M theme ZIPs can be imported: `background` to `Bg.Window`, `elements` to `Bg.Surface`, `border` to `Border`, `hover` to `Hover`, `select` to `Select`, `main_text` to `Text`, `secondary_text` to `Text.Accent`, `custom_border_radius` to the radius base.

### Type

Font: Noto Sans (ship Regular and Bold, OFL), CJK fallbacks Noto Sans JP, KR, SC, TC chosen by UI language. Code: Noto Sans Mono. G3M's all-bold look is kept for headings and buttons only.

| Token | Size | Weight | Use |
| --- | --- | --- | --- |
| `Caption` | 11 px | Regular | Status bar, grid headers |
| `Small` | 12 px | Regular | Secondary labels, tooltips |
| `Body` | 13 px | Regular | Default text, inputs, lists |
| `Code` | 13 px | Regular mono | Editors, hex, disassembly |
| `Strong` | 13 px | Bold | Buttons, tab labels, pane titles |
| `Title` | 16 px | Bold | Dialog titles |
| `Display` | 20 px | Bold | Start page only |

### Spacing, size, radius

- Spacing scale: 2, 4, 6, 8, 12, 16, 24 px. Drop G3M's 10 and 15.
- Control height: 24 px in panes and toolbars, 28 px in dialogs (G3M uses 30 px plus borders). Padding 4 px 8 px (dense) and 6 px 12 px (dialog).
- Tab 26 px, title bar 32 px with 24 px window buttons, checkbox 16 px, scrollbar 10 px (14 px on hover), tree row 22 px, list row 24 px.
- Radii: `Radius.Control` 4 px, `Radius.Panel` 6 px, `Radius.Window` 7 px (G3M default). Keep G3M's clamp: radius never exceeds half the smaller side. A theme radius of 0 sets all three to 0.

### Border rule

Every border is exactly 2 px: controls, panels, popups, tooltips, menus, dialogs, splitters, focus ring and invalid fields. No 1 px, 3 px or 0 px exceptions (G3M's 3 px dialog outline and 1 px Mod Editor lines are not copied). Use `Border.Subtle` instead of a thinner line where an outline should recede. Menu separators are 2 px lines in `Border.Subtle`. Override every Fluent template border in use.

### Focus ring and states

| State | Fill | Border (2 px) | Text |
| --- | --- | --- | --- |
| Default | `Bg.Surface` | `Border` | `Text` |
| Hover | `Hover` | `Border` | `Text` |
| Pressed | `Pressed` | `Text` | `Text` (G3M title bar pattern) |
| Checked / toggled | `Hover` | `Select` | `Text` |
| Selected row | `Selection.Bg` | none, or `Select` when the list has focus | `Text` |
| Focus-visible | unchanged | 2 px `Select` outline, 2 px offset (MCE pattern) | unchanged |
| Invalid | unchanged | `Danger` | message in `Danger` |
| Disabled | `Disabled.Bg` | `Disabled.Border` | `Disabled.Text` |

Do not copy G3M's global `outline: none`; every focusable control shows the ring on keyboard focus (`:focus-visible`).

Motion: hover and border color 120 ms (G3MTool), tooltip fade 150 ms, expand and collapse 200 ms ease-in-out, progress 180 to 220 ms. One "Disable animations" setting turns all of them off, as in G3M.

### Changes for a dense dev-tool UI

- Solid surfaces by default. The G3M background image and translucent frames stay optional (theme import), because they reduce text contrast behind code and tables.
- Green `Border` only on interactive controls and the focused pane; structural lines use `Border.Subtle`, otherwise a multi-pane layout becomes a grid of bright green boxes.
- Row selection uses a fill (`Selection.Bg`) instead of G3M's per-item 2 px outline, which costs 4 px per row and shifts text.
- Regular weight for body text; Bold everywhere (G3M) lowers density and legibility in code.
- Left-aligned inputs; G3M centers line edit text, which breaks for paths and code.
- Scrollbar handle in `Hover`, not G3M's default light `#E8E9EB`.
- 13 px body instead of 16 pt; G3M's 16 pt with 30 px controls fits about half the rows per pane.
- Native drop-down arrow glyphs and icons as vector `PathIcon` geometry bound to `Text`, replacing G3M's temp-file SVG recolor.
