"""Byte-level exhaustive RIP-relative reference finder (robust against disasm desync).

For every offset P in .text and every plausible instruction length L, a RIP-relative
memory operand has its ModRM byte at P+L-5 with (modrm & 0xC7) == 0x05, disp32 right
after it. target = insn_addr + L + disp32. This finds ALL such refs regardless of
opcode, at the cost of some false positives (rare in practice).
"""
import struct
from peutil import Target


def find_refs(code, va, base, targets):
    want = set(targets)
    hits = {t: [] for t in targets}
    n = len(code)
    for P in range(n):
        for L in range(5, 12):
            m = P + L - 5
            if m < 0 or m + 5 > n:
                continue
            if (code[m] & 0xC7) != 0x05:
                continue
            disp = struct.unpack_from("<i", code, m + 1)[0]
            tgt = (va + P) + L + disp
            if tgt in want:
                hits[tgt].append(va + P)
    return hits


def load_code(t=None):
    t = t or Target()
    name, va, vend = t.exec_sections()[0]
    return t.read(va, vend - va), va, t.base, t


if __name__ == "__main__":
    import sys
    from funcs import load_functions, containing
    code, va, base, t = load_code()
    tgts = [int(x, 16) for x in sys.argv[1:]]
    hits = find_refs(code, va, base, tgts)
    funcs = load_functions(t)
    for tg in tgts:
        print(f"== refs to {hex(tg)} ==")
        for a in hits[tg]:
            f = containing(funcs, a)
            print(f"   {hex(a):>8s}  func={hex(f[0]) if f else '?'}..{hex(f[1]) if f else '?'}")
