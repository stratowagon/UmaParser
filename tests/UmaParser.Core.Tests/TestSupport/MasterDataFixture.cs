using System.Globalization;
using System.Runtime.CompilerServices;
using UmaParser.DataModel.ResponseData;
using UmaParser.MasterData;

namespace UmaParser.Core.Tests.TestSupport;

/// <summary>
/// Loads <see cref="EmbeddedMasterFallback"/> into <see cref="GameMasterService.Current"/>
/// before any test reads a name. Does not touch AppData, a live master.mdb, or the cache.
/// </summary>
public sealed class MasterDataFixture
{
    public MasterDataFixture()
    {
        EmbeddedMaster.Ensure();
    }
}

internal static class EmbeddedMaster
{
    [ModuleInitializer]
    internal static void InitializeModule()
    {
        Ensure();
    }

    internal static void Ensure()
    {
        // Ace-delta labels inside analysis reports use the current culture.
        // Golden files are written with the invariant format so they match on every machine.
        CultureInfo invariant = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = invariant;
        CultureInfo.DefaultThreadCurrentUICulture = invariant;
        CultureInfo.CurrentCulture = invariant;
        CultureInfo.CurrentUICulture = invariant;

        UmaApiResponse.RegisterAllKnownTypes();
        GameMasterService.Current.InitializeEmbeddedOnly();
    }
}
