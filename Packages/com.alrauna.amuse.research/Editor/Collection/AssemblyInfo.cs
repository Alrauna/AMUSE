using System.Runtime.CompilerServices;

// Issued by the research package to its own test assembly. The collector's
// entire surface is one public method, so without this the tests could not
// reach the vocabulary mappings, the attestation trial, or the renderer-level
// seam overload at all.
[assembly: InternalsVisibleTo("Alrauna.Amuse.Research.Tests.Editor")]
