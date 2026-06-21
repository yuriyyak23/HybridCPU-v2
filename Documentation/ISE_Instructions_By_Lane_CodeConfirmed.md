# ISE runtime-implemented instructions

| Runtime lane output | SlotClass | Placement | Count |
| --- | --- | --- | ---: |
| Any lane | `Unclassified` | NOP placeholder/flexible | 1 |
| Lane0-3 | `AluClass` | class-flexible | 150 |
| Lane4-5 | `LsuClass` | class-flexible | 15 |
| Lane6 | `DmaStreamClass` | hard-pinned | 3 |
| Lane6 | `MatrixTileStreamClass` | class-flexible on the lane-6 carrier | 2 |
| Lane7 | `BranchControl` | hard-pinned/singleton lane | 8 |
| Lane7 | `SystemSingleton` | hard-pinned/singleton lane | 36 |
| **Code-confirmed total** |  |  | **215** |

Of 251 unique enum values, 36 `Reserved/None` rows are excluded from the implemented inventory.

## Any lane / Unclassified

- `Nope=0`

Evidence: `RegisterBaseInstructionSet` registers `NopMicroOp`; it uses flexible `Unclassified` placement. This is the only deliberate exception to the support-status inclusion rule.

## Lane0-3 / AluClass

- `ADD=39`, `SUB=40`, `MUL=41`, `DIV=42`, `SLL=47`, `SRL=48`, `XOR=49`, `OR=50`, `AND=51`, `CZERO_EQZ=53`, `CLZ=54`, `SH1ADD=56`
- `CLMUL=58`, `CTZ=59`, `SEXT_B=60`, `SEXT_H=61`, `ZEXT_H=62`, `ROL=63`, `ROR=64`, `ANDN=65`, `ORN=66`, `XNOR=67`, `VADD=70`, `VSUB=71`
- `VMUL=72`, `VDIV=73`, `VXOR=77`, `VOR=78`, `VAND=79`, `VNOT=80`, `VSQRT=81`, `VMOD=82`, `VCMPEQ=83`, `VCMPNE=84`, `VCMPLT=85`, `VCMPLE=86`
- `VCMPGT=87`, `VCMPGE=88`, `VMAND=89`, `VMOR=90`, `VMXOR=91`, `VMNOT=92`, `VPOPC=93`, `VSLL=94`, `VSRL=95`, `VSRA=96`, `VFMADD=100`, `VFMSUB=101`
- `VFNMADD=102`, `VFNMSUB=103`, `VMIN=104`, `VMAX=105`, `VMINU=106`, `VMAXU=107`, `VREDSUM=108`, `VREDMAX=109`, `VREDMIN=110`, `VREDMAXU=111`, `VREDMINU=112`, `VREDAND=113`
- `VREDOR=114`, `VREDXOR=115`, `VDOT=119`, `VDOTU=120`, `VDOTF=121`, `VMSBF=122`, `VZEXT=123`, `VSCAN_SUM=124`, `VCOMPRESS=136`, `VEXPAND=137`, `VREVERSE=138`, `VPOPCNT=139`
- `VCLZ=140`, `VCTZ=141`, `VBREV8=142`, `VPERMUTE=143`, `VSLIDEUP=144`, `VSLIDEDOWN=145`, `VRGATHER=146`, `ADDI=152`, `ANDI=153`, `ORI=154`, `XORI=155`, `SLTI=156`
- `SLTIU=157`, `SLLI=158`, `SRLI=159`, `SRAI=160`, `SLT=161`, `SLTU=162`, `LUI=163`, `AUIPC=164`, `VDOT_FP8=215`, `MTILE_MACC=218`, `MTRANSPOSE=219`, `MULH=220`
- `MULHU=221`, `MULHSU=222`, `DIVU=223`, `REM=224`, `REMU=225`, `SRA=300`, `ADDIW=301`, `ADDW=302`, `SUBW=303`, `SLLW=304`, `SRLW=305`, `SRAW=306`
- `SLLIW=307`, `SRLIW=308`, `SRAIW=309`, `MULW=310`, `DIVW=311`, `DIVUW=312`, `REMW=313`, `REMUW=314`, `SEXT_W=320`, `ZEXT_W=321`, `VSLIDE1UP=322`, `VDOT_WIDE=323`
- `VSLIDE1DOWN=324`, `VPERM2=325`, `VTRANSPOSE=326`, `MIN=327`, `MAX=328`, `MINU=329`, `MAXU=330`, `REV8=331`, `BREV8=332`, `CZERO_NEZ=333`, `CPOP=334`, `ROLI=335`
- `RORI=336`, `BSET=337`, `BCLR=338`, `BINV=339`, `BEXT=340`, `BSETI=341`, `BCLRI=342`, `BINVI=343`, `BEXTI=344`, `SH2ADD=345`, `SH3ADD=346`, `ADD_UW=347`
- `SH1ADD_UW=348`, `SH2ADD_UW=349`, `SH3ADD_UW=350`, `SLLI_UW=351`, `CLMULH=352`, `CLMULR=353`

`MTILE_MACC` and `MTRANSPOSE` are no longer optional-disabled declarations. Their support rows are `OptionalEnabled/ConformanceTested`; canonical decode, matrix materialization, MicroOps, execution, retire, replay/rollback and golden-manifest guards exist in live code.

## Lane4-5 / LsuClass

- `VLOAD=75`, `VSTORE=76`, `LB=165`, `LBU=166`, `LH=167`, `LHU=168`, `LW=169`, `LWU=170`, `LD=171`, `SB=172`, `SH=173`, `SW=174`
- `SD=175`, `VGATHER=213`, `VSCATTER=214`

`VLOAD` and `VSTORE` remain code-confirmed compatibility seams: the registry comment still calls them legacy vector-transfer opcodes, but live support status is `OptionalEnabled/ConformanceTested`, canonical materialization is concrete, and placement is `LsuClass`. The word `legacy` in a comment does not demote an otherwise executable contour.

## Lane6 / DmaStreamClass

- `DmaStreamCompute=245`, `DSC_STATUS=246`, `DSC_QUERY_CAPS=247`

All three materialized MicroOps hard-pin `SlotClass.DmaStreamClass` to physical lane 6. Their general `InstructionClass.Memory` metadata is not LSU placement authority.

## Lane6 / MatrixTileStreamClass

- `MTILE_LOAD=216`, `MTILE_STORE=217`

Matrix memory operations are now production-enabled. `MatrixTileMicroOp` resolves them to `SlotClass.MatrixTileStreamClass`; its mask is physical lane 6, distinct from DMA/DSC semantics even though both classes alias the same physical carrier.

## Lane7 / BranchControl

- `JAL=176`, `JALR=177`, `BEQ=178`, `BNE=179`, `BLT=180`, `BGE=181`, `BLTU=182`, `BGEU=183`

## Lane7 / SystemSingleton

- `RDCYCLE=57`, `VSETVL=116`, `VSETVLI=117`, `VSETIVLI=118`, `FENCE=197`, `ECALL=199`, `EBREAK=200`, `MRET=201`, `SRET=202`, `WFI=203`, `CSRRW=204`, `CSRRS=205`
- `CSRRC=206`, `CSRRWI=207`, `CSRRSI=208`, `CSRRCI=209`, `YIELD=240`, `WFE=241`, `SEV=242`, `POD_BARRIER=243`, `VT_BARRIER=244`, `VMXON=250`, `VMXOFF=251`, `VMLAUNCH=252`
- `VMRESUME=253`, `VMREAD=254`, `VMWRITE=255`, `VMCLEAR=256`, `VMPTRLD=257`, `ACCEL_QUERY_CAPS=260`, `ACCEL_SUBMIT=261`, `ACCEL_POLL=262`, `ACCEL_WAIT=263`, `ACCEL_CANCEL=264`, `ACCEL_FENCE=265`, `ACCEL_STATUS=266`

Branch and system/CSR/SMT/VMX/accelerator MicroOps materialize on physical lane 7. `BranchControl` and `SystemSingleton` are separate authority classes sharing that lane.

## Excluded numeric carriers

The following enum values still have compatibility factories but live status is `Reserved` with `RuntimeInstructionEvidence.None`; they are not implemented-instruction evidence and are excluded from the 215 total:

- `CSR_CLEAR`, `VSETVEXCPMASK`, `VSETVEXCPPRI`
- `LR_W`, `SC_W`, `LR_D`, `SC_D`, `AMOADD_W`, `AMOSWAP_W`, `AMOOR_W`, `AMOAND_W`, `AMOXOR_W`, `AMOMIN_W`, `AMOMAX_W`, `AMOMINU_W`, `AMOMAXU_W`
- `FENCE_I`, `STREAM_SETUP`, `STREAM_START`, `STREAM_WAIT`
- `AMOADD_D`, `AMOSWAP_D`, `AMOOR_D`, `AMOAND_D`, `AMOXOR_D`, `AMOMIN_D`, `AMOMAX_D`, `AMOMINU_D`, `AMOMAXU_D`
- `VMPTRST`, `VMCALL`, `INVEPT`, `INVVPID`, `VMFUNC`, `VMSAVEX`, `VMRESTX`

Descriptor-operation names such as `DmaStreamCompute.SUB`, reserved queue commands, helper vocabulary, parser aliases and non-numeric support rows are also excluded. Parser acceptance, enum presence, support-table presence or a compatibility factory alone is not runtime execution proof.

## Machine-checked invariants

- Unique numeric enum values: **251**.
- Included code-confirmed instructions: **215**.
- Excluded `Reserved/None` numeric carriers: **36**.
- Included lane counts sum to 215.
- Matrix production split is explicit: `MTILE_MACC/MTRANSPOSE -> AluClass`, `MTILE_LOAD/STORE -> MatrixTileStreamClass`.
- No silent fallback is used for unknown opcodes: `InstructionRegistry.CreateMicroOp` throws for an unregistered opcode.
