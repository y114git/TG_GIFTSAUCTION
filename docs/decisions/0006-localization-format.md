# 0006 Localization format

Status: proposed.

UI strings live in one JSON file per language in the same format G3M uses, so G3M translators work the same way and shared terms stay identical. A markup extension resolves keys in XAML; code uses a typed accessor generated from the English file. English is the fallback.

Tests: every key in English exists in every language or is reported as missing; no key is unused; placeholders match between languages.

Translations are written in-house (owner decision, 2026-10-25) from a glossary per language. Glossary sources, in order: G3M's own language files, VS Code's official language packs (MIT), GameMaker's IDE packs (reference only, known errors excluded), UTMT. A test checks that glossary terms are used consistently.

Rejected: .resx (no G3M tooling, poor diff for translators), bulk machine translation (requirement F-L10N-4).
