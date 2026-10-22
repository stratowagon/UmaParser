using UmaParser.Core.Tests.TestSupport;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

// One catalog for the whole run. GameMasterService.Current is a mutable singleton.
[assembly: AssemblyFixture(typeof(MasterDataFixture))]
[assembly: Parallelization(Mode = ParallelMode.None)]
