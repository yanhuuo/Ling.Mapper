; Assembly listing for method Ling.Mapper.TypeConverterRegistry:get_Version():long (FullOpts)
; Emitting BLENDED_CODE for generic X64 + VEX + EVEX on Unix
; FullOpts code
; optimized code
; rsp based frame
; partially interruptible
; No PGO data

G_M000_IG01:                ;; offset=0x0000
       push     rax
 
G_M000_IG02:                ;; offset=0x0001
       test     byte  ptr [(reloc 0x7f6e1cc22660)], 1
       je       SHORT G_M000_IG05
 
G_M000_IG03:                ;; offset=0x000A
       mov      rax, qword ptr [(reloc 0x7f6e1ba9b0b0)]
 
G_M000_IG04:                ;; offset=0x0011
       add      rsp, 8
       ret      
 
G_M000_IG05:                ;; offset=0x0016
       mov      rdi, 0x7F6E1CC225F0
       call     CORINFO_HELP_GET_NONGCSTATIC_BASE
       jmp      SHORT G_M000_IG03
 
; Total bytes of code 39

; Assembly listing for method Ling.Mapper.Mapper.Mapper:CurrentGeneration():Ling.Mapper.Mapper.Mapper+PlanGeneration:this (FullOpts)
; Emitting BLENDED_CODE for generic X64 + VEX + EVEX on Unix
; FullOpts code
; optimized code
; rbp based frame
; partially interruptible
; No PGO data
; 0 inlinees with PGO data; 2 single block inlinees; 1 inlinees without PGO data

G_M000_IG01:                ;; offset=0x0000
       push     rbp
       push     r15
       push     r14
       push     r13
       push     rbx
       lea      rbp, [rsp+0x20]
       mov      rbx, rdi
 
G_M000_IG02:                ;; offset=0x0010
       mov      r15, gword ptr [rbx+0x38]
       mov      r14, qword ptr [(reloc 0x7f6e1ba9b0b0)]
       cmp      qword ptr [r15+0x28], r14
       je       SHORT G_M000_IG04
 
G_M000_IG03:                ;; offset=0x0021
       mov      rdi, 0x7F6E1D106280
       call     CORINFO_HELP_NEWSFAST
       mov      r13, rax
       mov      rdi, r13
       mov      rsi, r14
       call     [Ling.Mapper.Mapper.Mapper+PlanGeneration:.ctor(long):this]
       lea      rdi, bword ptr [rbx+0x38]
       test     rdi, rdi
       je       SHORT G_M000_IG06
       mov      rsi, r13
       mov      rdx, r15
       call     System.Threading.Interlocked:CompareExchangeObject(byref,System.Object,System.Object):System.Object
       cmp      rax, r15
       cmovne   r13, rax
       mov      r15, r13
 
G_M000_IG04:                ;; offset=0x005D
       mov      rax, r15
 
G_M000_IG05:                ;; offset=0x0060
       pop      rbx
       pop      r13
       pop      r14
       pop      r15
       pop      rbp
       ret      
 
G_M000_IG06:                ;; offset=0x0069
       call     [System.ThrowHelper:ThrowNullReferenceException()]
       int3     
 
; Total bytes of code 112

