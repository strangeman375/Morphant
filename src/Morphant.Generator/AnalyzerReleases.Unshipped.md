; Unshipped analyzer release

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
MORPH0063 | Morphant.Compatibility | Error | Incompatible Morphant library
MORPH0064 | Morphant.Registration | Error | Ambiguous open mapping extensions
MORPH0065 | Morphant.Registration | Error | Mapping extensions visible from a friend assembly

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
MORPH0058 | Morphant.Declaration | Error | TypeMapper has no self type
MORPH0059 | Morphant.Declaration | Error | Pair extensions do not reference the mapper
MORPH0060 | Morphant.Registration | Error | Only parameters present in the pair are needed
