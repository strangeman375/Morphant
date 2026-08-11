; Unshipped analyzer release

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
MORPH0001 | Morphant.Compatibility | Error | Unsupported C# language version
MORPH0002 | Morphant.Compatibility | Error | Morphant runtime contract not found
MORPH0003 | Morphant.Compatibility | Error | Ambiguous Morphant runtime contract
MORPH0004 | Morphant.Compatibility | Error | Incompatible Morphant runtime contract
MORPH0005 | Morphant.Declaration | Error | Mapper must derive from TypeMapper
MORPH0006 | Morphant.Declaration | Error | Mapper must be partial
MORPH0007 | Morphant.Declaration | Error | Containing type must be partial
MORPH0008 | Morphant.Declaration | Error | File-local mapper declaration is not supported
MORPH0009 | Morphant.Declaration | Error | Mapping contract is already declared
MORPH0010 | Morphant.Declaration | Error | Mapping contract conflicts with a declared interface
MORPH0011 | Morphant.Registration | Error | Mapping type is unavailable to generated code
MORPH0012 | Morphant.Registration | Error | Unsupported mapping root type
MORPH0013 | Morphant.Registration | Error | Duplicate mapping registration
MORPH0014 | Morphant.Registration | Error | Mapping contracts can unify
MORPH0034 | Morphant.Declaration | Error | Mapper member conflicts with generated Supports
