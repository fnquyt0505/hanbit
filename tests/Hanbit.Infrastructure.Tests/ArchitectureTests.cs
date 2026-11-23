using System.Reflection;

using Shouldly;

namespace Hanbit.Infrastructure.Tests;

public class ArchitectureTests
{
    [Fact]
    public void GetReferencedAssemblies_InfrastructureAssembly_DoesNotDependOnWeb()
    {
        var referenced = Assembly.Load("Hanbit.Infrastructure")
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty);

        referenced.ShouldNotContain(name =>
            name == "Hanbit.Web"
            || name.StartsWith("Microsoft.AspNetCore.Mvc", StringComparison.Ordinal));
    }
}
