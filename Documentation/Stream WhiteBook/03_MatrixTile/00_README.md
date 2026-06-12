# MatrixTile Execution Plane

Status: closed/runtime-isa

MatrixTile is a first-class execution plane for:

- `MTILE_LOAD`
- `MTILE_STORE`
- `MTILE_MACC`
- `MTRANSPOSE`

In this WhiteBook, **MatrixRegisterFile** means the runtime type
`MatrixTileArchitecturalTileRegisterFile`; it is distinct from SRF.

It separates:

- memory-semantic ISA identity;
- `MatrixTileMemory` or `MatrixTileCompute` resource ownership;
- lane placement;
- typed StreamEngine/SRF transport;
- architectural tile state;
- execute capture;
- retire publication;
- replay/rollback.

The complete phase record lives in
`Documentation/InstructionsList/MTILE_RefPlan/`.

MatrixTile is not ordinary LSU, generic StreamEngine execution,
`DmaStreamCompute`, VectorALU fallback, or an external accelerator backend.
