# 0007 What Git tracks

Status: proposed. Revised by ADR-0010: the project is an optional layer (Project mode); Direct mode needs no project.

A compiled data file is one binary blob, so Git can only say "changed". G3MStudio offers two modes:

1. Direct mode (default): edit `data.win` directly. Changes are shown with G3MLib's diff against the original or last save. The journal and rotating backups protect the user. No Git.
2. Project mode (opt-in, Enable project): export the data file to G3MLib's text-based project format (one file per resource, stable order). Git tracks this folder. Building writes a data file from the folder plus the original game file.

Opening a data file inside a Git repository offers to enable the project. Depends on G3MLib work item 6 (one stable full-game format).
