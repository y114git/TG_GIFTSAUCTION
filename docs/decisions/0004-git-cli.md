# 0004 Git through the system git CLI

Status: proposed, see questions.md Q6.

G3MStudio runs the installed `git` with `--porcelain`/`-z` output, like VS Code. It uses the user's credential manager, SSH keys, hooks, LFS and config without extra work. If git is missing or older than 2.30, Git features show an install hint and the rest of the app works.

Rejected for now: LibGit2Sharp 0.32.0. Native binaries cover all six RIDs, but libgit2 does not use credential helpers or the user's SSH setup, so push and pull fail in common setups. Could be added later for read-only status if spawning processes proves slow.
