# Design reference

Source of truth for colours, type, spacing and states: the proposed token set at the end of [research/g3m-design.md](research/g3m-design.md#proposed-g3mstudio-token-set). This page holds the rules that sit above the tokens. Change a token there, change a rule here.

## Rules

1. 90% G3M design and style, 10% GameMaker, VS Code and Zed (owner decision Q23). The default theme matches G3M's default theme almost exactly, including the same background image and translucent panels: dark neutral surfaces (`#282828`, `#222222`), green outline `#039D5B`, light text `#E8E9EB`, green accent text `#6DE985`, light selection `#ECEDEF`, Noto Sans, rounded corners with G3M's clamp (radius never above half the smaller side). Changes are limited to what a dense editor needs: smaller sizes, regular weight for body and code text, row selection by fill. Code, hex and table views get a more opaque surface behind text so contrast stays readable over the background.
2. Every border is exactly 2 px. This covers controls, panels, popups, tooltips, menus, dialogs, splitters, separators, focus rings and invalid fields. No 1 px or 3 px exceptions. A border that should recede uses `Border.Subtle`, never a thinner line. Every Fluent template in use is restyled; a test walks the visual tree and fails on any other `BorderThickness`.
3. Green outline marks interactive controls and the focused pane only. Structural lines use `Border.Subtle`.
4. Focus is always visible on keyboard focus: 2 px `Select` ring.
5. Customizable: every token can be changed in settings. One action imports G3M's current theme settings (one way, G3M to G3MStudio).
6. One default theme. No light theme and no dark/light split. Tokens are named by role, and the whole look can be customized.
7. Icons: single-colour vector geometry bound to the text colour. Source to be chosen with a known licence (G3M's icons have no recorded origin, see the token doc). Lucide (ISC), decided.
8. Motion is short (120 to 200 ms) and a single "Reduce motion" setting turns it off.
9. Text: Arial Bold at 13 px for the whole UI (owner, 2026-10-27; no bundled fonts, OS fallback, weight and family are theme tokens), code 13 px in a monospace stack.

## Interaction rules

- Every command lives in one registry with id, title (localized), icon, default shortcut and enablement context. Menus, toolbars, context menus and the palette render from it.
- Tooltips name the command and its shortcut.
- Destructive actions: prefer undo. Confirm only when undo is impossible, and say exactly what will change.
- Errors say what happened, why, and what to do next, with a "Copy details" action.
- Long operations: progress in the status bar, cancel button, result notification, details in Output.

## Open

- Canvas view versus tabs, splits and docks is decided by [ADR-0010](decisions/0010-layout-modes.md) (questions.md Q15). Only the details come from the M0 prototypes: docking library fit, canvas rendering cost and how onboarding presents the presets.
