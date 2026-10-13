# Sources and provenance

This implementation is independent. The supplied MASTER_CONTEXT.md defines
architecture and scope; no simulator source was used to implement Phase 1.

Public ecosystem references, retained for future research only:

- OpenLR2: https://github.com/GOMazk/OpenLR2 (persistent chart index concept;
  implementation must not be copied).
- beatoraja: https://github.com/exch-bms2/beatoraja (optional future read-only
  import; external databases are never filesystem truth).
- Modern Neeted BeMusicSeeker fork: https://github.com/Neeted/bemusicseeker-unofficial-fork
  (modern `dev` source reviewed at `541db4fde12d1091ce0b7c939e11dd07d435b81c`;
  MD5 duplicate groups, reverse resource indexes, install estimation, parser
  compatibility and mutation sessions inform *research*, not permission to
  copy code or proof of resource equality/safe deletion; see `ROADMAP.md`).
- BMSON specification: https://github.com/bemusic/bmson-spec (future phases).

Platform references:

- .NET 10 SDK: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
- Microsoft.Data.Sqlite: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/
- Microsoft.Data.Sqlite package: https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.12

No private third-party source may be copied, translated, reconstructed,
vendored, depended upon or used as an implementation reference.
