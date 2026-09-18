# Phase 00 compiler baseline — clean subject

- Subject: `f1de3445dd897bc525d57dfa882fd736799f203d`
- Branch: `refactor/compiler-core-authority-boundaries`
- Disposition: `Clean`
- Exact source files: `1284`
- Source-manifest fingerprint:
  `d99abb7554314b4784aa5e0d09e485f63ec24355bfdaf2d97610ebfc51a42599`
- Deterministic artifact SHA-256:
  `cad4465ed108269aef19ab0091db6bd413437e2ebabb049f2cceb0c4dbe6ea63`
- Invocation artifact SHA-256:
  `5c31bf2ea0d052051056334c327c076e73e0b1fda91211258d25a21dcdc38c18`
- Resource telemetry SHA-256:
  `c08f4b76adb40fceaa6730fb72c0dcc569c1e8271eb85959258ff058f67d0a85`

Two captures produced byte-identical deterministic artifacts. Resource telemetry is deliberately
separate and never participates in compiler code selection.

| Profile | Instructions | Schedule cycles | Bundles | Average width |
| --- | ---: | ---: | ---: | ---: |
| `alu` | 185 | 75 | 60 | 3.0833 |
| `novt` | 186 | 75 | 61 | 3.0492 |
| `vt` | 164 | 48 | 36 | 4.5556 |
| `max` | 165 | 49 | 37 | 4.4595 |
| `lk` | 164 | 48 | 36 | 4.5556 |
| `bnmcz` | 164 | 48 | 36 | 4.5556 |

Phase 00 tests passed `25/25`; the exact-subject compiler-focused suite passed `720/720`.
No runtime admission, execution, publication, commit or retire authority follows from this report.
