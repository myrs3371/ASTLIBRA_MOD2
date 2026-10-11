# ASTLIBRA modding plan

- Install: `E:\game\ASTLIBRA\ASTLIBRA`, version `V1.5.0`, x64 native executable.
- Engine: unknown native engine; no managed runtime or known engine signature detected.
- Anti-cheat / online: no anti-cheat detected; offline single-player target.
- Saves: `C:\Users\lenovo\AppData\Local\ASTLIBRA\SAVE`; backup: `C:\Users\lenovo\.universal-modder\backups\ASTLIBRA-saves-before-quicksave\20261007-163555.zip`.
- Community route: no installed loader or ASTLIBRA field note found; `universal-modder` recommends native proxy-DLL/function hooks with x64dbg/Cheat Engine.
- Chosen route: independent native DLL with version-checked hook/condition patch, because menu simulation cannot expose saving away from save points.
- Lab status: `AstlibraQuickSave` is isolated from `AstlibraMod`; save API tracing is working and has confirmed writes to `SAVE_0002.DAT`.
- Unknowns to resolve: the save-point interaction predicate and the safe high-level save entry point.
