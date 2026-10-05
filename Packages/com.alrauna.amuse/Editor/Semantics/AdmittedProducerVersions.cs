using System;
using System.Collections.Generic;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// The producer-version admission seam one attestation owns: the pinned,
    /// characterized admitted set, the install-read override the tests steer,
    /// and the once-per-session cache of the installed version. The admitted
    /// set is pinned, characterized data, never grown at runtime. A version
    /// that is null or empty is never admitted, and an unreadable install
    /// refuses rather than guessing.
    /// </summary>
    internal sealed class AdmittedProducerVersions
    {
        private readonly string _producerPackageName;
        private readonly string[] _productionAdmittedVersions;
        private readonly List<string> _admitted;

        private string _cachedVersion;
        private bool _versionReadCompleted;

        internal AdmittedProducerVersions(
            string producerPackageName,
            string[] productionAdmittedVersions)
        {
            if (producerPackageName == null)
            {
                throw new ArgumentNullException(nameof(producerPackageName));
            }
            if (productionAdmittedVersions == null)
            {
                throw new ArgumentNullException(
                    nameof(productionAdmittedVersions));
            }

            _producerPackageName = producerPackageName;
            _productionAdmittedVersions = productionAdmittedVersions;
            _admitted = new List<string>(productionAdmittedVersions);
        }

        internal Func<string, string> ReadInstalledPackageVersionOrNull
        {
            get;
            set;
        } = PackageVersionReader.ReadInstalledOrNull;

        internal IReadOnlyList<string> AdmittedVersions => _admitted;

        internal void SetAdmittedVersionsForTests(
            params string[] versions)
        {
            _admitted.Clear();
            if (versions != null)
            {
                _admitted.AddRange(versions);
            }
        }

        internal void ResetForTests()
        {
            _admitted.Clear();
            _admitted.AddRange(_productionAdmittedVersions);
            ReadInstalledPackageVersionOrNull =
                PackageVersionReader.ReadInstalledOrNull;
            ClearVersionCacheForSession();
        }

        internal bool TryReadInstalledProducerVersion(
            out string version)
        {
            if (!_versionReadCompleted)
            {
                _cachedVersion = ReadInstalledPackageVersionOrNull?.Invoke(
                    _producerPackageName);
                _versionReadCompleted = true;
            }
            version = _cachedVersion;
            return !string.IsNullOrEmpty(version);
        }

        internal void ClearVersionCacheForSession()
        {
            _versionReadCompleted = false;
            _cachedVersion = null;
        }

        internal bool IsVersionAdmitted(string version)
        {
            return !string.IsNullOrEmpty(version) &&
                _admitted.Contains(version);
        }
    }
}
