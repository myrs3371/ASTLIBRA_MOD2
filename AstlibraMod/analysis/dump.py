"""Disassemble a RVA range of ASTLIBRA.exe with capstone, annotate rip-relative targets."""
import sys
import capstone
from peutil import Target

MD = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
MD.detail = True


def dump(t, start, size, base=None):
    base = base or t.base
    code = t.read(start, size)
    print(f"; ==== RVA {hex(start)} .. {hex(start+size)} ====")
    for insn in MD.disasm(code, base + start):
        rva = insn.address - base
        note = ""
        for op in insn.operands:
            if op.type == capstone.x86.X86_OP_MEM and op.mem.base == capstone.x86.X86_REG_RIP:
                tgt = insn.address + insn.size + op.mem.disp
                trva = tgt - base
                note = f"   ; ->RVA {hex(trva)}"
                sec = t.section_of(trva)
                if sec and sec != ".text":
                    note += f" ({sec})"
        print(f"{rva:08x}  {insn.bytes.hex():<20s} {insn.mnemonic} {insn.op_str}{note}")


if __name__ == "__main__":
    t = Target()
    start = int(sys.argv[1], 16)
    size = int(sys.argv[2], 16) if len(sys.argv) > 2 else 0x100
    dump(t, start, size)
