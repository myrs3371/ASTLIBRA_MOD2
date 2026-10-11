"""Parse .pdata RUNTIME_FUNCTION table -> function boundaries; look up functions by RVA."""
import sys
import struct
from peutil import Target


def load_functions(t=None):
    t = t or Target()
    pd = next(s for s in t.sections if s[0] == ".pdata")
    _n, va, vend, praw, _sraw = pd
    raw = t.read(va, vend - va)
    funcs = []
    for i in range(0, len(raw) - 11, 12):
        begin, end, unwind = struct.unpack_from("<III", raw, i)
        if begin == 0 and end == 0:
            continue
        funcs.append((begin, end, unwind))
    funcs.sort()
    return funcs


def containing(funcs, rva):
    for b, e, u in funcs:
        if b <= rva < e:
            return (b, e, u)
    return None


def nearest(funcs, rva):
    """function whose begin is closest below rva (even if rva is outside by a bit)."""
    best = None
    for b, e, u in funcs:
        if b <= rva:
            best = (b, e, u)
        else:
            break
    return best


if __name__ == "__main__":
    t = Target()
    funcs = load_functions(t)
    print(f"{len(funcs)} functions in .pdata")
    for arg in sys.argv[1:]:
        rva = int(arg, 16)
        c = containing(funcs, rva)
        print(f"{hex(rva)}: containing={tuple(hex(x) for x in c) if c else None}")
