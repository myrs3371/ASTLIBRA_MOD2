"""RIP-relative xref scanner over ASTLIBRA.exe .text using capstone."""
import capstone
from peutil import Target

MD = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
MD.detail = True


def scan_text(t=None):
    """Linear-sweep .text, return list of (insn_rva, insn_len, text, [target_rvas])."""
    t = t or Target()
    name, va, vend = t.exec_sections()[0]
    code = t.read(va, vend - va)
    out = []
    off = 0
    n = len(code)
    while off < n:
        got = False
        for insn in MD.disasm(code[off:], va + off):
            tgts = []
            for op in insn.operands:
                if op.type == capstone.x86.X86_OP_MEM and op.mem.base == capstone.x86.X86_REG_RIP:
                    tgts.append(insn.address + insn.size + op.mem.disp)
            out.append((insn.address, insn.size, f"{insn.mnemonic} {insn.op_str}", tgts))
            off = insn.address - va + insn.size
            got = True
        if not got:
            off += 1  # skip un-decodable byte (data / padding) and resync
    return out


def xrefs_to(scan, target_rvas):
    want = set(target_rvas)
    hits = {}
    for addr, size, text, tgts in scan:
        for tg in tgts:
            if tg in want:
                hits.setdefault(tg, []).append((addr, text))
    return hits


if __name__ == "__main__":
    import sys
    t = Target()
    scan = scan_text(t)
    print(f"disassembled {len(scan)} insns from .text")
    targets = [int(x, 16) for x in sys.argv[1:]]
    hits = xrefs_to(scan, targets)
    for tgt in targets:
        print(f"\n== xrefs to {hex(tgt)} ==")
        for addr, text in hits.get(tgt, []):
            print(f"  {hex(addr):>9s}  {text}")
