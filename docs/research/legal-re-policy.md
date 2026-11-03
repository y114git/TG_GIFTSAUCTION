# Reverse engineering policy

Checked on 2026-10-25. This is a working policy, not legal advice. The owner decides edge cases.

## Terms that apply

| Document | Date | Reverse engineering clause |
| --- | --- | --- |
| GameMaker licence bundled with the IDE (`License.txt` in the install folder) | 21 November 2023 | none |
| GameMaker Runtime Licence, Free (gamemaker.io/legal/gamemaker-runtime-licence-free) | 21 November 2023 | none |
| Opera General Terms (opera.com/legal/terms), GameMaker Feature section | 30 July 2026, GameMaker section 11 February 2026 | none for GameMaker; Norwegian law, Oslo City Court; "nothing ... is intended to be construed as limiting your rights under applicable law" |

Norway is in the EEA and follows the EU Software Directive (2009/24/EC):

- Article 5(3): a lawful user may observe, study and test how a program works while loading, running or displaying it. A contract cannot remove this right (Article 8).
- Article 6: decompiling is allowed when it is indispensable to get information needed for interoperability of an independently created program, the information is not already readily available, and it covers only the parts needed. The information may not be used for other goals, given to others except for interoperability, or used to build a substantially similar program.

## What we do

| Activity | Allowed | Conditions |
| --- | --- | --- |
| Use the IDE, create and build test projects, read the output with G3MLib | yes | normal use, Article 5(3) |
| Screenshots and notes of the IDE for research | yes | kept private; not shipped |
| Read GameMaker's own data files on disk (hotkeys JSON, language CSV, licences) | yes | reference only; not shipped |
| Install other GameMaker versions (LTS 2022, monthly) for black-box tests | yes | with an account the owner controls |
| Decompile GameMaker code that reads or writes data files (asset compiler, runner loader) | yes, limited | only for format facts G3MLib needs that UTMT, G3MLib and tests do not already answer; record which fact and why; never copy code into G3MLib or G3MStudio; never publish decompiled output |
| Decompile the IDE to learn or copy its UI or editor code | no | not interoperability; use normal use instead |
| Decompile anything to bypass licensing, accounts or DRM | no | |

Every decompilation session is logged in `research/gamemaker-format.md` with the question, the component, and the answer in our own words.
