#!/usr/bin/env bash
# Runs the test project as several processes at once. The headless UI session of one process is single threaded,
# so the speed-up comes from splitting the test classes over processes (each has its own session and static state).
# Usage: tools/test-parallel.sh [shards] [previous.trx]   (shards can also come from TEST_SHARDS, the output folder from TEST_OUT; default shards = min(cpu/3, 6): more processes only fight over the cores; the trx, by default the last run, only balances the split)
set -uo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root"
cpus="$(nproc 2>/dev/null || echo 4)"
shards="${1:-${TEST_SHARDS:-$(( cpus / 3 > 6 ? 6 : (cpus / 3 < 2 ? 2 : cpus / 3) ))}}"
trx="${2:-}"
out="${TEST_OUT:-$root/.test-shards}"
weights="${TMPDIR:-/tmp}/shard-weights.trx"
# The folder is wiped next, and the file may live in it. Without a file, the last run's own results balance this split.
if [ -n "$trx" ] && [ -f "$trx" ]; then cp "$trx" "$weights"; elif ls "$out"/shard*.trx >/dev/null 2>&1; then cat "$out"/shard*.trx > "$weights"; else weights=""; fi
trx="$weights"
rm -rf "$out"; mkdir -p "$out"

dotnet build G3MStudio.Tests --nologo -v q -warnaserror 2>&1 | tail -3 || exit 1

dotnet test G3MStudio.Tests --no-build --nologo --list-tests 2>/dev/null | tr -d '\r' \
  | sed -n 's/^ *\(G3MStudio\.Tests\.[A-Za-z0-9_]*\)\.[A-Za-z0-9_]*\((.*)\)\?$/\1/p' | sort | uniq -c | sort -rn > "$out/classes.txt"
[ -s "$out/classes.txt" ] || { echo "no tests found"; exit 1; }

python - "$out" "$shards" "$trx" <<'PY'
import sys, re, collections
out, n, trx = sys.argv[1], int(sys.argv[2]), sys.argv[3]
weight = collections.defaultdict(float)
if trx:
    for name, dur in re.findall(r'<UnitTestResult [^>]*?testName="([^"]*)"[^>]*?duration="([^"]*)"', open(trx, encoding='utf-8').read()):
        h, m, s = dur.split(':')
        weight[name.split('(')[0].rsplit('.', 1)[0]] += int(h) * 3600 + int(m) * 60 + float(s)
classes = []
for line in open(out + '/classes.txt'):
    count, name = line.split()
    classes.append((weight.get(name, int(count) * 0.2), name))
classes.sort(reverse=True)
loads, groups = [0.0] * n, [[] for _ in range(n)]
for w, name in classes:
    i = loads.index(min(loads))
    loads[i] += w; groups[i].append(name)
for i, g in enumerate(groups):
    open(f'{out}/shard{i}.filter', 'w').write('|'.join('FullyQualifiedName~' + c + '.' for c in g))
PY
[ $? = 0 ] && ls "$out"/shard*.filter >/dev/null 2>&1 || { echo "could not split the tests"; exit 1; }

start=$(date +%s)
pids=()
for f in "$out"/shard*.filter; do
  i="$(basename "$f" .filter)"
  ( dotnet test G3MStudio.Tests --no-build --nologo --filter "$(cat "$f")" --blame-hang-timeout 12m \
      --logger "trx;LogFileName=$out/$i.trx" > "$out/$i.txt" 2>&1; echo $? > "$out/$i.code" ) &
  pids+=($!)
done
wait "${pids[@]}"

bad=0
for f in "$out"/shard*.txt; do
  grep -E "^(Passed|Failed)!" "$f" | sed "s|^|$(basename "$f" .txt): |"
  grep -E "^  Failed " "$f"
  [ "$(cat "${f%.txt}.code")" = 0 ] || bad=1
done
echo "elapsed $(( $(date +%s) - start )) s, shards $shards"
exit $bad
