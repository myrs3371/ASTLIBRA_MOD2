"""Shared PE helpers for ASTLIBRA static analysis (pefile + capstone)."""
import pefile

EXE = r"E:\game\ASTLIBRA\ASTLIBRA\ASTLIBRA.exe"


class Target:
    def __init__(self, path=EXE):
        self.pe = pefile.PE(path, fast_load=True)
        self.path = path
        self.base = self.pe.OPTIONAL_HEADER.ImageBase
        self.sections = [
            (s.Name.decode(errors="replace").rstrip("\x00"),
             s.VirtualAddress, s.VirtualAddress + max(s.Misc_VirtualSize, s.SizeOfRawData),
             s.PointerToRawData, s.SizeOfRawData)
            for s in self.pe.sections
        ]

    def rva_to_raw(self, rva):
        for _name, va, vend, praw, sraw in self.sections:
            if va <= rva < vend and praw:
                off = rva - va
                if off < sraw:
                    return praw + off
        return None

    def read(self, rva, size):
        raw = self.rva_to_raw(rva)
        if raw is None:
            return b""
        with open(self.path, "rb") as f:
            f.seek(raw)
            return f.read(size)

    def section_of(self, rva):
        for name, va, vend, _p, _s in self.sections:
            if va <= rva < vend:
                return name
        return None

    def exec_sections(self):
        """(name, va, vend) for sections with IMAGE_SCN_MEM_EXECUTE."""
        out = []
        for s in self.pe.sections:
            if s.Characteristics & 0x20000000:
                out.append((s.Name.decode(errors="replace").rstrip("\x00"),
                            s.VirtualAddress,
                            s.VirtualAddress + max(s.Misc_VirtualSize, s.SizeOfRawData)))
        return out


def find_ascii(data, minlen=4):
    """Yield (offset, text) for printable ASCII runs."""
    out = []
    cur = bytearray()
    start = 0
    for i, b in enumerate(data):
        if 0x20 <= b < 0x7f or b in (9,):
            if not cur:
                start = i
            cur.append(b)
        else:
            if len(cur) >= minlen:
                out.append((start, cur.decode("ascii")))
            cur = bytearray()
    if len(cur) >= minlen:
        out.append((start, cur.decode("ascii")))
    return out
