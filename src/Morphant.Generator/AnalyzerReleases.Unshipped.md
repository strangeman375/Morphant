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
MORPH0015 | Morphant.Configuration | Error | Mapper must declare Configure
MORPH0016 | Morphant.Configuration | Error | Base mapper configuration is unavailable
MORPH0017 | Morphant.Configuration | Error | Unsupported mapper builder flow
MORPH0018 | Morphant.Configuration | Error | Unsupported mapping builder flow
MORPH0019 | Morphant.Composition | Error | Duplicate mapping plan slot
MORPH0020 | Morphant.Composition | Error | Convert cannot be combined with result policy or Members
MORPH0021 | Morphant.Settings | Error | Invalid mapping setting value
MORPH0022 | Morphant.Settings | Error | Invalid MSBuild mapping setting value
MORPH0023 | Morphant.Settings | Error | Mapping setting is not applicable
MORPH0024 | Morphant.Inheritance | Error | Duplicate base configuration call
MORPH0025 | Morphant.Inheritance | Error | Duplicate IncludeBase call
MORPH0026 | Morphant.Inheritance | Error | Included mapping pair not found
MORPH0027 | Morphant.Inheritance | Error | Included mapping type is incompatible
MORPH0028 | Morphant.Inheritance | Error | Inherited mapping callback is inaccessible
MORPH0029 | Morphant.Callbacks | Error | Structured callback must be a lambda
MORPH0030 | Morphant.Callbacks | Error | Callback cannot be transferred
MORPH0031 | Morphant.Callbacks | Error | Unsupported structured callback syntax
MORPH0032 | Morphant.Callbacks | Error | Structured destination input is read-only
MORPH0033 | Morphant.Callbacks | Error | Invalid compile-time marker use
MORPH0034 | Morphant.Declaration | Error | Mapper member conflicts with generated Supports
MORPH0035 | Morphant.Construction | Error | Destination construction is not configured
MORPH0036 | Morphant.Construction | Error | Convention construction is unavailable
MORPH0037 | Morphant.Construction | Error | Constructor parameter rule is invalid
MORPH0038 | Morphant.Construction | Error | Previous destination is unavailable
MORPH0039 | Morphant.Construction | Error | Structured construction plan is null
MORPH0040 | Morphant.Members | Error | Member rule is invalid
MORPH0041 | Morphant.Members | Error | Required destination member is not initialized
MORPH0042 | Morphant.Members | Error | Member rule cannot be applied
MORPH0043 | Morphant.Members | Error | Structured member plan is null
MORPH0044 | Morphant.NestedMapping | Error | Nested mapping pair cannot be determined
MORPH0045 | Morphant.NestedMapping | Error | Nested mapping result is incompatible
MORPH0046 | Morphant.NestedMapping | Error | Nested Update destination is invalid
