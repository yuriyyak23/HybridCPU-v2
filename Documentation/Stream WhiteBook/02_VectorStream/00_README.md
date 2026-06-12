# Vector Stream Plane

The vector stream plane is a composition of helpers:

```text
validated StreamExecutionRequest
    -> StreamEngine orchestration
    -> BurstIO / SRF operand movement
    -> VectorALU typed compute
    -> BurstIO result movement
    -> canonical vector retire/publication path
```

No component in this chain is a substitute for MicroOp legality, scheduler
placement, or retire authority.

Current code roots:

- `NonRTL/Core/Execution/StreamEngine/`
- `CloseToRTL/Core/Execution/StreamEngine/`
- `CloseToRTL/Core/Execution/Vector/`
- `CloseToRTL/Memory/Registers/StreamRegisterFile*.cs`
- `NonRTL/Core/Execution/BurstIO/`

Addressing includes bounded 1D, strided, 2D, and descriptor-backed indexed or
tri-operand contours where explicitly supported. Unsupported or
non-representable shapes fail closed.
