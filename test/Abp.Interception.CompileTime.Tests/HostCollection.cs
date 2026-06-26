using Abp.Interception.CompileTime.Host;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Abp.Interception.CompileTime.Tests;

[CollectionDefinition("CompileTimeHost")]
public sealed class HostCollection : ICollectionFixture<WebApplicationFactory<Startup>>
{
}
