using System.Reflection;

using Shouldly;

namespace Hanbit.Domain.Tests;

public class ArchitectureTests
{
    [Fact]
    public void GetReferencedAssemblies_DomainAssembly_HasNoProjectOrFrameworkDependencies()
    {
        var referenced = Assembly.Load("Hanbit.Domain")
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty);

        referenced.ShouldNotContain(name =>
            name.StartsWith("Hanbit.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }
}
