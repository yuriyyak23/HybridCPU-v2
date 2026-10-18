# Build-time projection lineage

`schemas/` contains required live build inputs, independent of historical
documentation. Do not remove these JSON files when cleaning legacy documents.
`VerifyProjectionLineage.ps1` regenerates expectations into the requested output
directory and rejects missing inputs or drift from the checked-in generated C#.
It never modifies C# sources and remains enabled by default.

The schema tables were recovered after documentation cleanup from the unchanged
checked-in generated artifacts: 23 VMCS fields, 11 aliases, 9 capability bits,
and 6 spec artifacts. The three embedded source/artifact identity pairs are
preserved. The spec table has no embedded identity; its recovery source and
artifact identities record the SHA-256 of the recovered-from C# file. These
identities are not JSON-file digests or new runtime authority.
