# Phase 00 runtime controls — clean subject

- Subject: `f1de3445dd897bc525d57dfa882fd736799f203d`
- Branch: `refactor/compiler-core-authority-boundaries`
- Disposition: `Clean`
- Exact source files: `1387`
- Source-manifest fingerprint:
  `bceeaec71f8f5ab30ffb9d42d70fcb993a293c9cd7f052b10ead0491d8b67286`
- Deterministic artifact SHA-256:
  `2456aeca559483568e01745979a1ebded2ee9c2eb40d8e7b23a3d2fcd332b2cd`
- Invocation artifact SHA-256:
  `f4cc63714895457d31270f276c84f29a134539654afe7faf3ec041541332b66e`
- Resource telemetry SHA-256:
  `21eb7ed2f7e3cc0cfa131e3ff7e343b5398feba2949124bf28961b316d225236`

All profiles passed three repetitions with deterministic normalized outcomes.

| Profile | Common normalized fingerprint |
| --- | --- |
| `replay` | `d34c2febab6f1f45c0ee915a9727af3fc70a050ecfe23d91d00380e84802c7f6` |
| `safety` | `98ebc95fb105b9a0c8f276b79fdc45370aaf2b1101ede817be869a7a5bbfc964` |
| `stream-vector` | `6d0c64e7f5d001fc0813cca44e06c5dc875201757d0588426736aa8dee90da61` |
| `matrix-tile` | `a2aff43167d984412a2b6b16ffcfbf0be42cbc8a72bf0b965e679226a0b0f2eb` |

Timing is separately reported telemetry. These controls observe existing runtime behavior; they do
not change or authorize runtime legality, execution, publication, commit or retire behavior.
