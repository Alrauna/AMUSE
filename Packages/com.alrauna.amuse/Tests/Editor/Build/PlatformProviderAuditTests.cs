using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The platform provider audit of both test assemblies. The audit
    /// pins the two rules of the platform split. First, no test
    /// platform may claim a qualified name owned by another provider
    /// object, because NDMF selects passes by qualified name and those
    /// passes then fail their provider identity checks. Second, every
    /// tier A test needs a tier B twin, so a host without the VRChat
    /// SDK loses only vendor coexistence evidence and never an AMUSE
    /// behavior assertion.
    /// </summary>
    public sealed class PlatformProviderAuditTests
    {
        private const string VendorVrChatSuffix = "VendorVRChat";

        private static readonly string[] AuditedAssemblyNames =
        {
            "Alrauna.Amuse.Tests.Editor",
            "Alrauna.Amuse.Research.Tests.Editor",
        };

        /// <summary>
        /// Every platform provider type the two test assemblies
        /// declare, with one live instance per type. A provider without
        /// an Instance singleton is built through its non-public
        /// constructor.
        /// </summary>
        private static List<INDMFPlatformProvider> TestPlatformProviders()
        {
            var providers = new List<INDMFPlatformProvider>();
            foreach (var type in TypeCache.GetTypesDerivedFrom
                         <INDMFPlatformProvider>())
            {
                if (!AuditedAssemblyNames.Contains(
                        type.Assembly.GetName().Name))
                {
                    continue;
                }

                providers.Add(CreateProvider(type));
            }

            return providers;
        }

        private static INDMFPlatformProvider CreateProvider(Type type)
        {
            var instanceProperty = type.GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public
                    | BindingFlags.NonPublic);
            if (instanceProperty != null)
            {
                return (INDMFPlatformProvider)instanceProperty
                    .GetValue(null);
            }

            return (INDMFPlatformProvider)Activator.CreateInstance(
                type, nonPublic: true);
        }

        // --- Falsifier 3: a test platform whose QualifiedName equals a name owned by another provider object selects that provider's passes and then fails their identity checks. ---
        [Test]
        public void NoTestPlatformClaimsABuiltInPlatformName()
        {
            var providers = TestPlatformProviders();
            Assert.That(providers, Is.Not.Empty,
                "the audit found no provider in the test assemblies,"
                + " so it proved nothing");

            var builtInNames = new[]
            {
                WellKnownPlatforms.Generic,
                WellKnownPlatforms.VRChatAvatar30,
                WellKnownPlatforms.Resonite,
            };

            var claimers = new List<string>();
            foreach (var provider in providers)
            {
                var name = provider.QualifiedName;
                if (builtInNames.Contains(name))
                {
                    claimers.Add(
                        provider.GetType().Name + " (" + name + ")");
                }
            }

            var sharedNames = new List<string>();
            foreach (var group in providers.GroupBy(
                         provider => provider.QualifiedName,
                         StringComparer.Ordinal))
            {
                if (group.Count() > 1)
                {
                    sharedNames.Add(group.Key + " is returned by "
                        + string.Join(", ", group.Select(
                            provider => provider.GetType().Name)));
                }
            }

            var violations = new List<string>();
            if (claimers.Count > 0)
            {
                violations.Add("these providers claim a built-in name: "
                    + string.Join(", ", claimers));
            }

            if (sharedNames.Count > 0)
            {
                violations.Add("these providers share one name: "
                    + string.Join(", ", sharedNames));
            }

            Assert.That(violations, Is.Empty,
                "a test platform must not claim a qualified name owned"
                + " by another provider object, because NDMF selects"
                + " that provider's passes by name and the passes then"
                + " fail their identity checks. Every test platform"
                + " also needs one qualified name that no other"
                + " provider returns. The violations are: "
                + string.Join(" Also: ", violations));
        }

        // --- Falsifier 4: a tier A test whose AMUSE behavior assertion has no tier B twin turns a product regression into a named skip on an SDK-less environment. ---
        [Test]
        public void EveryTierAAssertionHasATierBTwin()
        {
            var testMethodNames = new HashSet<string>(
                StringComparer.Ordinal);
            var tierAMethods = new List<MethodInfo>();
            foreach (var type in typeof(PlatformProviderAuditTests)
                         .Assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(
                             BindingFlags.Static | BindingFlags.Instance
                                 | BindingFlags.Public
                                 | BindingFlags.NonPublic))
                {
                    if (method.GetCustomAttribute<TestAttribute>()
                        == null)
                    {
                        continue;
                    }

                    testMethodNames.Add(method.Name);

                    var category = method
                        .GetCustomAttribute<CategoryAttribute>();
                    if (category != null && category.Name
                        == VendorVrChatTierGate.TierACategory)
                    {
                        tierAMethods.Add(method);
                    }
                }
            }

            Assert.That(tierAMethods, Is.Not.Empty,
                "no test method carries the category "
                + VendorVrChatTierGate.TierACategory
                + ", so tier A has no coverage to audit");

            var orphans = new List<string>();
            foreach (var method in tierAMethods)
            {
                var baseName = method.Name.EndsWith(
                    VendorVrChatSuffix, StringComparison.Ordinal)
                    ? method.Name.Substring(
                        0, method.Name.Length - VendorVrChatSuffix.Length)
                    : method.Name;
                if (!testMethodNames.Contains(baseName))
                {
                    orphans.Add(method.Name + " has no tier B twin named "
                        + baseName);
                }
            }

            Assert.That(orphans, Is.Empty,
                "every tier A test pairs with a tier B twin through the "
                + VendorVrChatSuffix + " suffix rule. The unpaired tests"
                + " are: " + string.Join(", ", orphans));
        }

        [Test]
        public void TierATwinsIgnoreWithTheMissingPackageNameAndLockedVersion()
        {
            var previousSource = VendorVrChatTierGate.InstalledPackageIds;
            VendorVrChatTierGate.InstalledPackageIds =
                () => new[]
                {
                    "nadena.dev.ndmf",
                    "jp.lilxyzw.liltoon",
                };
            try
            {
                var thrown = Assert.Throws<IgnoreException>(
                    () => VendorVrChatTierGate
                        .RequireInstalledVrcSdkPackages());

                Assert.That(
                    thrown.Message,
                    Does.Contain(VendorVrChatTierGate.VrcAvatarsPackage),
                    "the skip must name the absent SDK package");
                Assert.That(
                    thrown.Message,
                    Does.Contain(VendorVrChatTierGate.LockedSdkVersion),
                    "the skip must name the locked SDK version");
            }
            finally
            {
                VendorVrChatTierGate.InstalledPackageIds =
                    previousSource;
            }
        }

        [Test]
        public void SuiteStartRequiresTheTierACategoryWhenTheSdkIsInstalled()
        {
            var previousSource = VendorVrChatTierGate.InstalledPackageIds;
            VendorVrChatTierGate.InstalledPackageIds =
                () => new[]
                {
                    VendorVrChatTierGate.VrcAvatarsPackage,
                    VendorVrChatTierGate.VrcBasePackage,
                };
            try
            {
                Assert.DoesNotThrow(
                    VendorSuiteStartCheck.AssertTierACategoryPresent,
                    "the SDK is installed, so the suite start must find"
                    + " the tier A category on at least one test"
                    + " method");
            }
            finally
            {
                VendorVrChatTierGate.InstalledPackageIds =
                    previousSource;
            }
        }
    }
}
