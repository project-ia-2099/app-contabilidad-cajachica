using Cash.Domain.Entities;
using Cash.Application.Services;
using Xunit;
namespace Cash.Tests;
public sealed class ArchitectureTests
{
    [Fact] public void DomainHasNoProjectOrPersistenceDependencies() => Assert.All(typeof(Fund).Assembly.GetReferencedAssemblies(), r => Assert.StartsWith("System", r.Name));
    [Fact] public void ApplicationOnlyReferencesDomainAndFramework() => Assert.All(typeof(CashService).Assembly.GetReferencedAssemblies(), r => Assert.True(r.Name == "Cash.Domain" || r.Name!.StartsWith("System", StringComparison.Ordinal), r.Name));
}