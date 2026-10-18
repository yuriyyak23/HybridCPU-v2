# VectorALU

## Role

`VectorALU` is a typed compute library over byte spans supplied by the vector
execution path. It performs datatype-aware element operations and updates
vector exception/dirty state through the owning core context.

Families include:

- binary, immediate, and unary arithmetic;
- comparisons and mask operations;
- FMA;
- reductions and scans;
- saturating arithmetic;
- bit operations;
- dot and widening dot products;
- compress/expand, permutation, gather, and slides.

Predication, tail, and mask policy are supplied by validated vector ingress.
Inactive elements do not write or trap unless the selected policy explicitly
allows overwrite.

## Boundaries

`VectorALU` has no standalone scheduler slot class and no direct memory,
descriptor, tile-register, retire, replay, DSC, or L7 authority.

Matrix operations must not be lowered to VectorALU as fallback. MTILE MACC and
transpose use the MatrixTile compute contour even when an algorithm resembles a
vector helper.
