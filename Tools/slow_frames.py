# Slow main-thread frames in a markers.tsv from CaptureReport, with their largest markers.
#   slow_frames.py markers.tsv [busy ms, default 4] [frame ms, default 20]
# Busy = PlayerLoop minus the waits for vsync, presentation and the render thread. Run with -I.
import sys, collections
path = sys.argv[1]; busy_lim = float(sys.argv[2]) if len(sys.argv) > 2 else 4; frame_lim = float(sys.argv[3]) if len(sys.argv) > 3 else 20
WAITS = {'WaitForTargetFPS', 'Semaphore.WaitForSignal', 'Gfx.WaitForPresentOnGfxThread', 'Gfx.WaitForRenderThread', 'Gfx.WaitForGfxCommandsFromMainThread'}
frames = collections.defaultdict(lambda: {'frameMs': 0, 'loop': 0, 'wait': 0, 'self': collections.Counter(), 'total': collections.Counter()})
for line in open(path):
    if line.startswith('#') or line.startswith('frame\t'): continue
    f, th, fms, name, self_ms, total_ms, calls = line.rstrip('\n').split('\t')
    if th != 'Main Thread': continue
    d = frames[int(f)]; d['frameMs'] = float(fms)
    if name == 'PlayerLoop': d['loop'] += float(total_ms)
    if name in WAITS: d['wait'] += float(self_ms)
    d['self'][name] += float(self_ms); d['total'][name] = max(d['total'][name], float(total_ms))
busy = {f: d['loop'] - d['wait'] for f, d in frames.items()}
vals = sorted(busy.values())
print(f"{len(frames)} main-thread frames {min(frames)}..{max(frames)}; busy p50 {vals[len(vals)//2]:.2f} p95 {vals[int(len(vals)*.95)]:.2f} max {vals[-1]:.2f} ms")
for f in sorted(frames):
    d = frames[f]
    if busy[f] > busy_lim or d['frameMs'] > frame_lim:
        top = [(n, v) for n, v in d['self'].most_common(40) if n not in WAITS and v >= 0.2][:8]
        print(f"{f}: frame {d['frameMs']:.1f} busy {busy[f]:.2f} wait {d['wait']:.1f} | " + ', '.join(f"{n} {v:.2f}" for n, v in top))
