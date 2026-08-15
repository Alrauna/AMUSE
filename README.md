# alpha-material-optimizer-ndmf
NDMF-based Unity add-on for automatically separating opaque geometry from transparent materials on VRChat avatars.

## Development setup

After restoring the VPM dependencies, run the temporary NDMF standalone bootstrap before opening Unity:

```powershell
pwsh -NoProfile -File ./Tools/Bootstrap-NdmfStandalone.ps1
```

NDMF 1.14.4 packages standalone dependency assemblies under `Dependencies~`, which Unity intentionally ignores. The bootstrap verifies the resolved package version and payload, then copies those files into an ignored, Unity-importable `Dependencies` directory. It is safe to run repeatedly and requires no junction or symlink privileges. Remove this workaround once an upstream NDMF release exposes its standalone dependencies directly.
